/*Copyright(c) <2017> <Benoit Constantin ( France )>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. 
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Match;

namespace BC_Solution.UnetNetwork
{
    /*
* wire protocol is a list of :   size   |  msgType     | payload
*                               (short)  (variable)   (buffer)
*/
    public class NetworkingConnection : IDisposable
    {

        #region Events
        /// <summary>
        /// Called on client when the local connection disconnect
        /// </summary>
        public static Action<NetworkingConnection> OnConnectionDisconnect;

        /// <summary>
        /// Called on client when the local connection connect
        /// </summary>
        public static Action<NetworkingConnection> OnConnectionConnect;


        /// <summary>
        /// Called on client when the local connection stop
        /// </summary>
        public static Action<NetworkingConnection> OnStopConnection;

        #endregion

        public enum ConnectState
        {
            None,
            Resolving,
            Resolved,
            Connecting,
            Connected,
            Disconnected,
            Failed
        }

        public ConnectState m_currentState;

        public bool IsConnected()
        {
            return this.m_currentState == ConnectState.Connected;
        }

        NetworkingChannelBuffer[] m_channels;

        NetworkingWriter m_writer = new NetworkingWriter();

        public Dictionary<NetworkingMessageType, Action<NetworkingMessage>> m_messageHandlers = new Dictionary<NetworkingMessageType, Action<NetworkingMessage>>();

        NetworkingMessage m_messageInfo = new NetworkingMessage();

        const int k_MaxMessageLogSize = 150;
        private NetworkError error;
        private HostTopology m_hostTopology;
        public HostTopology hostTopology { get { return m_hostTopology; } }

        public int m_hostId = -1;
        public int m_connectionId = -1;


        public float lastMessageTime;
        public bool logNetworkMessages = false;
        public bool isConnected { get { return m_hostId != -1; } }

        byte[] m_msgBuffer;
        const int k_MaxEventsPerFrame = 500;
        int m_statResetTime;

        /// <summary>
        /// The server which connection belong to
        /// </summary>
        public string m_serverAddress { get; private set; }
        public int m_serverPort { get; private set; }

#if SERVER
        /// <summary>
        /// Only available on server
        /// </summary>
        public NetworkingServer m_server;
#endif 
        public NetworkingConnection()
        {
            m_msgBuffer = new byte[NetworkingMessage.MaxMessageSize];
        }

        public NetworkingConnection(string serverAddress, int serverPort)
        {
            m_serverAddress = serverAddress;
            m_serverPort = serverPort;

            m_msgBuffer = new byte[NetworkingMessage.MaxMessageSize];
        }

        public class PacketStat
        {
            public PacketStat()
            {
                msgType = 0;
                count = 0;
                bytes = 0;
            }

            public PacketStat(PacketStat s)
            {
                msgType = s.msgType;
                count = s.count;
                bytes = s.bytes;
            }

            public ushort msgType;
            public int count;
            public int bytes;

            public override string ToString()
            {
                return msgType + ": count=" + count + " bytes=" + bytes;
            }
        }

        public NetworkError m_lastError { get { return error; } internal set { error = value; } }

        Dictionary<ushort, PacketStat> m_packetStats = new Dictionary<ushort, PacketStat>();
        internal Dictionary<ushort, PacketStat> packetStats { get { return m_packetStats; } }

#if UNITY_EDITOR
        static int s_MaxPacketStats = 255;//the same as maximum message types
#endif

        public void Configure(ConnectionConfig config, int maxConnections)
        {
            m_hostTopology = new HostTopology(config, maxConnections);
        }

        public virtual void Initialize(string networkAddress, int networkHostId, int networkConnectionId)
        {
            m_serverAddress = networkAddress;
            m_hostId = networkHostId;
            m_connectionId = networkConnectionId;


            int numChannels = hostTopology.DefaultConfig.ChannelCount;
            int packetSize = hostTopology.DefaultConfig.PacketSize;

            if ((hostTopology.DefaultConfig.UsePlatformSpecificProtocols) && (UnityEngine.Application.platform != RuntimePlatform.PS4) && (UnityEngine.Application.platform != RuntimePlatform.PSP2))
                throw new ArgumentOutOfRangeException("Platform specific protocols are not supported on this platform");

            m_channels = new NetworkingChannelBuffer[numChannels];
            for (int i = 0; i < numChannels; i++)
            {
                var qos = hostTopology.DefaultConfig.Channels[i];
                int actualPacketSize = packetSize;
                if (qos.QOS == QosType.ReliableFragmented || qos.QOS == QosType.UnreliableFragmented)
                {
                    actualPacketSize = hostTopology.DefaultConfig.FragmentSize * 128;
                }
                m_channels[i] = new NetworkingChannelBuffer(actualPacketSize, (byte)i, NetworkingChannelBuffer.IsReliableQoS(qos.QOS), NetworkingChannelBuffer.IsSequencedQoS(qos.QOS));
            }
        }

        ~NetworkingConnection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            // Take yourself off the Finalization queue
            // to prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            m_channels = null;
        }


        public bool SetChannelOption(int channelId, NetworkingChannel.ChannelOption option, int value)
        {
            if (m_channels == null)
                return false;

            if (channelId < 0 || channelId >= m_channels.Length)
                return false;

            return m_channels[channelId].SetOption(option, value);
        }


        internal virtual void Update()
        {
            switch (m_currentState)
            {
                case NetworkingConnection.ConnectState.None:
                case NetworkingConnection.ConnectState.Resolving:
                case NetworkingConnection.ConnectState.Disconnected:
                    return;

                case NetworkingConnection.ConnectState.Failed:
                    GenerateConnectError((int)NetworkError.DNSFailure);
                    m_currentState = NetworkingConnection.ConnectState.Disconnected;
                    return;

                case NetworkingConnection.ConnectState.Resolved:
                    m_currentState = NetworkingConnection.ConnectState.Connecting;
                    byte error;
                    m_connectionId = NetworkTransport.Connect(m_hostId, m_serverAddress, m_serverPort, 0, out error);
                    this.Initialize(this.m_serverAddress, this.m_hostId, this.m_connectionId);
                    return;

                case NetworkingConnection.ConnectState.Connecting:
                case NetworkingConnection.ConnectState.Connected:
                    {
                        break;
                    }
            }


            if ((int)Time.time != m_statResetTime)
            {
                ResetStats();
                m_statResetTime = (int)Time.time;
            }

            int numEvents = 0;
            NetworkEventType networkEvent;
            do
            {
                int connectionId;
                int channelId;
                int receivedSize;
                byte error;

                networkEvent = NetworkTransport.ReceiveFromHost(this.m_hostId, out connectionId, out channelId, m_msgBuffer, (ushort)m_msgBuffer.Length, out receivedSize, out error);
                m_lastError = (NetworkError)error;

                if (networkEvent != NetworkEventType.Nothing)
                {
                    if (LogFilter.logDev) { Debug.Log("Client event: host=" + this.m_connectionId + " event=" + networkEvent + " error=" + error); }
                }           

                switch (networkEvent)
                {
                    case NetworkEventType.ConnectEvent:

                        if (LogFilter.logDebug) { Debug.Log("Client connected"); }

                        if (error != 0)
                        {
                            GenerateConnectError(error);
                            return;
                        }

                        m_currentState = ConnectState.Connected;
                        InternalOnConnectionConnect();
                        break;

                    case NetworkEventType.DataEvent:
                        if (error != 0)
                        {
                            GenerateDataError(error);
                            return;
                        }

#if UNITY_EDITOR
                        /* UnityEditor.NetworkDetailStats.IncrementStat(
                         UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                         MsgType.LLAPIMsg, "msg", 1);*/
#endif

                        TransportReceive(m_msgBuffer, receivedSize, channelId);
                        break;

                    case NetworkEventType.DisconnectEvent:
                        if (LogFilter.logDebug) { Debug.Log("Client disconnected"); }

                        m_currentState = ConnectState.Disconnected;

                        if (error != 0)
                        {
                            if ((NetworkError)error != NetworkError.Timeout)
                            {
                                GenerateDisconnectError(error);
                            }
                        }
                        //ClientScene.HandleClientDisconnect(m_Connection);
                        InternalOnConnectionDisconnect();
                        return; //we are disconnected

                    case NetworkEventType.Nothing:
                        break;

                    default:
                        if (LogFilter.logError) { Debug.LogError("Unknown network message type received: " + networkEvent); }
                        break;
                }

                if (++numEvents >= k_MaxEventsPerFrame)
                {
                    if (LogFilter.logDebug) { Debug.Log("MaxEventsPerFrame hit (" + k_MaxEventsPerFrame + ")"); }
                    break;
                }
                if (this.m_connectionId == -1)
                {
                    break;
                }
            }
            while (networkEvent != NetworkEventType.Nothing);

            if (this.m_currentState == ConnectState.Connected)
                FlushChannels();
        }

        public void Connect(MatchInfo matchInfo)
        {
            m_serverAddress = matchInfo.address;
            m_serverPort = matchInfo.port;

            this.m_hostId = NetworkTransport.AddHost(m_hostTopology, 0);

            this.m_currentState = NetworkingConnection.ConnectState.Connecting;

            Update();

            byte error;
            this.m_connectionId = NetworkTransport.ConnectToNetworkPeer(
                    this.m_hostId,
                    matchInfo.address,
                    matchInfo.port,
                    0,
                    0,
                    matchInfo.networkId,
                    Utility.GetSourceID(),
                    matchInfo.nodeId,
                    out error);

            this.Initialize(matchInfo.address, this.m_hostId, this.m_connectionId);
        }

        public void Connect(string serverIp, int serverPort)
        {
            m_serverAddress = serverIp;
            m_serverPort = serverPort;

            this.m_hostId = NetworkTransport.AddHost(m_hostTopology, 0);
           
            if (LogFilter.logDebug) { Debug.Log("Client Connect: " + serverIp + ":" + serverPort); }


            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                this.m_currentState = ConnectState.Resolved;
            }
            else if (serverIp.Equals("127.0.0.1") || serverIp.Equals("localhost"))
            {
                m_serverAddress = "127.0.0.1";
                this.m_currentState = ConnectState.Resolved; 
            }
            else if (serverIp.IndexOf(":") != -1 && IsValidIpV6(serverIp))
            {
                this.m_currentState = ConnectState.Resolved;
            }
            else
            {
                if (LogFilter.logDebug) { Debug.Log("Async DNS START:" + serverIp); }

                this.m_currentState = ConnectState.Resolving;
                Dns.BeginGetHostAddresses(serverIp, GetHostAddressesCallback, this);
            }
        }

        // this called in another thread! Cannot call Update() here.
        internal static void GetHostAddressesCallback(IAsyncResult ar)
        {
            try
            {
                IPAddress[] ip = Dns.EndGetHostAddresses(ar);
                NetworkingConnection connection = (NetworkingConnection)ar.AsyncState;
                string previousAddress = connection.m_serverAddress;

                if (ip.Length == 0)
                {
                    if (LogFilter.logError) { Debug.LogError("DNS lookup failed for:" + connection.m_serverAddress); }
                    connection.m_currentState = ConnectState.Failed;
                    return;
                }

                connection.m_serverAddress = ip[0].ToString();
                connection.m_currentState = ConnectState.Resolved;

                if (LogFilter.logDebug) { Debug.Log("Async DNS Result:" + connection.m_serverAddress + " for " + previousAddress); }
            }
            catch (SocketException e)
            {
                NetworkingConnection connection = (NetworkingConnection)ar.AsyncState;
                if (LogFilter.logError) { Debug.LogError("DNS resolution failed: " + e.ErrorCode); }
                if (LogFilter.logDebug) { Debug.Log("Exception:" + e); }
                connection.m_currentState = ConnectState.Failed;
            }
        }

        //What the use case ? Disabled for the moment
       /* public void Connect(EndPoint secureTunnelEndPoint)
        {
            bool usePlatformSpecificProtocols = NetworkTransport.DoesEndPointUsePlatformProtocols(secureTunnelEndPoint);
            PrepareForConnect(usePlatformSpecificProtocols);

            if (LogFilter.logDebug) { Debug.Log("Client Connect to remoteSockAddr"); }

            if (secureTunnelEndPoint == null)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: null endpoint passed in"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            // Make sure it's either IPv4 or IPv6
            if (secureTunnelEndPoint.AddressFamily != AddressFamily.InterNetwork && secureTunnelEndPoint.AddressFamily != AddressFamily.InterNetworkV6)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: Endpoint AddressFamily must be either InterNetwork or InterNetworkV6"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            // Make sure it's an Endpoint we know what to do with
            string endPointType = secureTunnelEndPoint.GetType().FullName;
            if (endPointType == "System.Net.IPEndPoint")
            {
                IPEndPoint tmp = (IPEndPoint)secureTunnelEndPoint;
                Connect(tmp.Address.ToString(), tmp.Port);
                return;
            }
            if ((endPointType != "UnityEngine.XboxOne.XboxOneEndPoint") && (endPointType != "UnityEngine.PS4.SceEndPoint") && (endPointType != "UnityEngine.PSVita.SceEndPoint"))
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: invalid Endpoint (not IPEndPoint or XboxOneEndPoint or SceEndPoint)"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            byte error = 0;
            // regular non-relay connect
            m_RemoteEndPoint = secureTunnelEndPoint;
            m_AsyncConnect = ConnectState.Connecting;

            try
            {
                m_ClientConnectionId = NetworkTransport.ConnectEndPoint(m_ClientId, m_RemoteEndPoint, 0, out error);
            }
            catch (Exception ex)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: Exception when trying to connect to EndPoint: " + ex); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }
            if (m_ClientConnectionId == 0)
            {
                if (LogFilter.logError) { Debug.LogError("Connect failed: Unable to connect to EndPoint (" + error + ")"); }
                m_AsyncConnect = ConnectState.Failed;
                return;
            }

            m_Connection = (NetworkConnection)Activator.CreateInstance(m_NetworkConnectionClass);
            m_Connection.SetHandlers(m_MessageHandlers);
            m_Connection.Initialize(m_ServerIp, m_ClientId, m_ClientConnectionId, m_hostTopology);
        } */

       public void Stop()
        {
            if (OnStopConnection != null)
                OnStopConnection(this);

            this.Disconnect();
        }

        public void Disconnect()
        {
            m_currentState = ConnectState.Disconnected;
            byte error;
            NetworkTransport.Disconnect(m_hostId, m_connectionId, out error);
            this.Dispose();
        }


        static bool IsValidIpV6(string address)
        {
            // vis2k: the original implementation wasn't correct, use C# built-in method instead
            IPAddress temp;
            return IPAddress.TryParse(address, out temp) && temp.AddressFamily == AddressFamily.InterNetworkV6;
        }


        public bool InvokeHandler(NetworkingMessageType msgType, NetworkingReader reader, int channelId)
        {
            Action<NetworkingMessage> msgDelegate;
            m_messageHandlers.TryGetValue(msgType, out msgDelegate);

            if (msgDelegate != null)
            {
                m_messageInfo.m_connection = this;
                m_messageInfo.reader = reader;
                m_messageInfo.channelId = channelId;
                msgDelegate(m_messageInfo);

                return true;
            }
            else
            {
                //if (LogFilter.logWarn) { Debug.LogWarning("NetworkConnection InvokeHandler no handler for " + msgType); }
                //Debug.Log(reader.Length);

#if SERVER
                if (m_server != null) //By design, if you have no handler but you are server, just send back information to all client.
                {
                    NetworkingWriter writer = new NetworkingWriter();
                    writer.StartMessage();
                    writer.Write(msgType);
                    writer.Write(reader.ToArray());
                    writer.FinishMessage();
                    //Debug.Log(msgType);
                    m_server.SendToAll(writer, channelId);
                    return true;
                }
                else
                { 
                #endif
                    //NOTE: this throws away the rest of the buffer. Need moar error codes
                    if (LogFilter.logError) { Debug.LogError("Unknown message ID " + msgType + " connId:" + m_connectionId + " channelID : " + channelId); }

                    return false;
 #if SERVER 
                }
#endif
            }
        }

        internal void HandleFragment(NetworkingReader reader, int channelId)
        {
            if (channelId < 0 || channelId >= m_channels.Length)
            {
                return;
            }

            var channel = m_channels[channelId];
            if (channel.HandleFragment(reader))
            {
                NetworkingReader msgReader = new NetworkingReader(channel.fragmentBuffer.ToArray());
                msgReader.ReadInt16(); // size
                NetworkingMessageType msgType = msgReader.ReadEnum<NetworkingMessageType>();
                InvokeHandler(msgType, msgReader, channelId);
            }
        }

        public void RegisterHandler(NetworkingMessageType msgType, Action<NetworkingMessage> handler)
        {
            Action<NetworkingMessage> callbacks;
            m_messageHandlers.TryGetValue(msgType, out callbacks);

            if (callbacks != null)
            {
                callbacks += handler;
                m_messageHandlers[msgType] = callbacks;
            }
            else
            {
                callbacks = handler;
                m_messageHandlers.Add(msgType, callbacks);
            }
        }

        public void UnregisterHandler(NetworkingMessageType msgType, Action<NetworkingMessage> handler)
        {
            Action<NetworkingMessage> callbacks;
            m_messageHandlers.TryGetValue(msgType, out callbacks);

            if (callbacks != null)
            {
                callbacks -= handler;
                m_messageHandlers[msgType] = callbacks;
            }
        }


        public void FlushChannels()
        {
            if (m_channels == null)
            {
                return;
            }
            for (int channelId = 0; channelId < m_channels.Length; channelId++)
            {
                m_channels[channelId].CheckInternalBuffer(this);
            }
        }


        /// <summary>
        /// Send with the default reliable sequence channel (0)
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public bool Send(NetworkingMessageType msgType, NetworkingMessage msg, int channelId = NetworkingChannel.DefaultReliableSequenced)
        {
            return SendByChannel(msgType, msg, channelId);
        }


        bool SendByChannel(NetworkingMessageType msgType, NetworkingMessage msg, int channelId)
        {
            m_writer.StartMessage();
            m_writer.Write(msgType);
            msg.Serialize(m_writer);
            m_writer.FinishMessage();

            return Send(m_writer, channelId);
        }

        public virtual bool Send(byte[] bytes, int numBytes, int channelId)
        {
            if (logNetworkMessages)
            {
                LogSend(bytes);
            }
            return CheckChannel(channelId) && m_channels[channelId].SendBytes(this, bytes, numBytes);
        }

        public virtual bool Send(NetworkingWriter writer, int channelId)
        {
            if (logNetworkMessages)
            {
                LogSend(writer.ToArray());
            }
            return CheckChannel(channelId) && m_channels[channelId].SendWriter(this, writer);
        }

        void LogSend(byte[] bytes)
        {
            NetworkReader reader = new NetworkReader(bytes);
            var msgSize = reader.ReadUInt16();
            var msgId = reader.ReadUInt16();

            const int k_PayloadStartPosition = 4;

            StringBuilder msg = new StringBuilder();
            for (int i = k_PayloadStartPosition; i < k_PayloadStartPosition + msgSize; i++)
            {
                msg.AppendFormat("{0:X2}", bytes[i]);
                if (i > k_MaxMessageLogSize) break;
            }
            Debug.Log("ConnectionSend con:" + m_connectionId + " bytes:" + msgSize + " msgId:" + msgId + " " + msg);
        }

        bool CheckChannel(int channelId)
        {
            if (m_channels == null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Channels not initialized sending on id '" + channelId); }
                return false;
            }
            if (channelId < 0 || channelId >= m_channels.Length)
            {
                if (LogFilter.logError) { Debug.LogError("Invalid channel when sending buffered data, '" + channelId + "'. Current channel count is " + m_channels.Length); }
                return false;
            }
            return true;
        }

        public void ResetStats()
        {
#if UNITY_EDITOR
            for (ushort i = 0; i < s_MaxPacketStats; i++)
            {
                if (m_packetStats.ContainsKey(i))
                {
                    var value = m_packetStats[i];
                    value.count = 0;
                    value.bytes = 0;
                    NetworkTransport.SetPacketStat(0, i, 0, 0);
                    NetworkTransport.SetPacketStat(1, i, 0, 0);
                }
            }
#endif
        }

        protected void HandleBytes(byte[] buffer, int receivedSize, int channelId)
        {
            // build the stream form the buffer passed in
            NetworkingReader reader = new NetworkingReader(buffer);

            HandleReader(reader, receivedSize, channelId);
        }

        protected void HandleReader( NetworkingReader reader, int receivedSize, int channelId)
        {
            //Debug.Log("Receive size : " + receivedSize);

            // read until size is reached.
            // NOTE: stream.Capacity is 1300, NOT the size of the available data
            while (reader.Position < receivedSize)
            {
                // the reader passed to user code has a copy of bytes from the real stream. user code never touches the real stream.
                // this ensures it can never get out of sync if user code reads less or more than the real amount.
                ushort sz = reader.ReadUInt16();
                NetworkingMessageType msgType = reader.ReadEnum<NetworkingMessageType>(); //BC : Mode by byte

                //Debug.Log("Size : " + sz);
                //Debug.Log("Message type : " + msgType);

                // create a reader just for this message
                byte[] msgBuffer = reader.ReadBytes(sz);

                NetworkingReader msgReader = new NetworkingReader(msgBuffer);

                if (logNetworkMessages)
                {
                    StringBuilder msg = new StringBuilder();
                    for (int i = 0; i < sz; i++)
                    {
                        msg.AppendFormat("{0:X2}", msgBuffer[i]);
                        if (i > k_MaxMessageLogSize) break;
                    }
                    Debug.Log("ConnectionRecv con:" + m_connectionId + " bytes:" + sz + " msgId:" + msgType + " " + msg);
                }

                InvokeHandler(msgType, msgReader, channelId);

#if UNITY_EDITOR
                /* UnityEditor.NetworkDetailStats.IncrementStat(
                     UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                     MsgType.HLAPIMsg, "msg", 1);

                 if (msgType > MsgType.Highest)
                 {
                     UnityEditor.NetworkDetailStats.IncrementStat(
                         UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                         MsgType.UserMessage, msgType.ToString() + ":" + msgType.GetType().Name, 1);
                 } 


                 if (m_packetStats.ContainsKey(msgType))
                 {
                     PacketStat stat = m_packetStats[msgType];
                     stat.count += 1;
                     stat.bytes += sz;
                 }
                 else
                 {
                     PacketStat stat = new PacketStat();
                     stat.msgType = msgType;
                     stat.count += 1;
                     stat.bytes += sz;
                     m_packetStats[msgType] = stat;
                 }
             }
             else
             {
                 //NOTE: this throws away the rest of the buffer. Need moar error codes
                 if (LogFilter.logError) { Debug.LogError("Unknown message ID " + msgType + " connId:" + m_connectionId); }
                 break;
             } */
#endif
            }
        }

        public virtual void GetStatsOut(out int numMsgs, out int numBufferedMsgs, out int numBytes, out int lastBufferedPerSecond)
        {
            numMsgs = 0;
            numBufferedMsgs = 0;
            numBytes = 0;
            lastBufferedPerSecond = 0;

            for (int channelId = 0; channelId < m_channels.Length; channelId++)
            {
                var channel = m_channels[channelId];
                numMsgs += channel.numMsgsOut;
                numBufferedMsgs += channel.numBufferedMsgsOut;
                numBytes += channel.numBytesOut;
                lastBufferedPerSecond += channel.lastBufferedPerSecond;
            }
        }

        public virtual void GetStatsIn(out int numMsgs, out int numBytes)
        {
            numMsgs = 0;
            numBytes = 0;

            for (int channelId = 0; channelId < m_channels.Length; channelId++)
            {
                var channel = m_channels[channelId];
                numMsgs += channel.numMsgsIn;
                numBytes += channel.numBytesIn;
            }
        }

        public override string ToString()
        {
            return string.Format("hostId: {0} connectionId: {1} channel count: {2}", m_hostId, m_connectionId, (m_channels != null ? m_channels.Length : 0));
        }


        public virtual void TransportReceive(byte[] bytes, int numBytes, int channelId)
        {
            //Debug.Log("Num byte receive : " + numBytes);
            HandleBytes(bytes, numBytes, channelId);
        }

        public virtual bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
        {
            return NetworkTransport.Send(m_hostId, m_connectionId, channelId, bytes, numBytes, out error);
        }


        internal static void OnFragment(NetworkingMessage netMsg)
        {
            netMsg.m_connection.HandleFragment(netMsg.reader, netMsg.channelId);
        }


        void GenerateError(byte error) // vis2k: byte instead of int
        {
            Action<NetworkingMessage> handler;
            m_messageHandlers.TryGetValue(NetworkingMessageType.Error, out handler);

            if (handler != null)
            {
                ErrorMessage msg = new ErrorMessage();
                msg.errorCode = error;

                // write the message to a local buffer
                byte[] errorBuffer = new byte[200];
                NetworkingWriter writer = new NetworkingWriter(errorBuffer);
                msg.Serialize(writer);

                // pass a reader (attached to local buffer) to handler
                NetworkingReader reader = new NetworkingReader(errorBuffer);

                ErrorMessage netMsg = new ErrorMessage();
                netMsg.m_type = NetworkingMessageType.Error;
                netMsg.reader = reader;
                netMsg.m_connection = this;
                netMsg.channelId = 0;
                handler(netMsg);
            }
        }

        void GenerateConnectError(byte error) // vis2k: byte instead of int
        {
            if (LogFilter.logError) { Debug.LogError("UNet Client Error Connect Error: " + error); }
            GenerateError(error);
        }

        void GenerateDataError(byte error) // vis2k: byte instead of int
        {
            NetworkError dataError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Client Data Error: " + dataError); }
            GenerateError(error);
        }

        void GenerateDisconnectError(byte error) // vis2k: byte instead of int
        {
            NetworkError disconnectError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Client Disconnect Error: " + disconnectError); }
            GenerateError(error);
        }


#region handlers

        void InternalOnConnectionConnect()
        {
            Debug.Log("Client connect");

            if (OnConnectionConnect != null)
                OnConnectionConnect(this);
        }


        void InternalOnConnectionDisconnect()
        {
            Debug.Log("Connection disconnect");

            if (OnConnectionDisconnect != null)
                OnConnectionDisconnect(this);

            NetworkTransport.RemoveHost(m_hostId);
            this.Dispose();
            m_hostId = -1;
        }

#endregion
    }
}
