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
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;

namespace BC_Solution.UnetNetwork {
    public class NetworkingServer  {

        #region Events
        /// <summary>
        /// Called on server when a player gameObject is added by the server
        /// </summary>
        public static Action<NetworkingServer, NetworkingMessage> OnServerAddPlayer;

        /// <summary>
        /// Called on server when a client connect
        /// </summary>
        public static Action<NetworkingServer, NetworkingMessage> OnServerConnect;

        /// <summary>
        /// Called on server when a client disconnect
        /// </summary>
        public static Action<NetworkingServer, NetworkingMessage> OnServerDisconnect;

        /// <summary>
        /// Called on server when a client become ready on server
        /// </summary>
        public static Action<NetworkingServer, NetworkingMessage> OnConnectionReady;

        /// <summary>
        /// Called on server when the server start
        /// </summary>
        public static Action<NetworkingServer, NetworkingMessage> OnStartServer;

        /// <summary>
        /// Called on server when the server stop
        /// </summary>
        public static Action<NetworkingServer> OnStopServer;

        #endregion


        public string m_serverAddress { get; private set; }
        public int m_serverPort { get; private set; }
        public string m_serverName { get; private set; }

        bool m_Initialized = false;

        internal int m_hostId = -1;
        int m_RelaySlotId = -1;
      //  bool m_UseWebSockets; // WTF ? If so, create a NetworkServer WebSocket xD

        byte[] m_MsgBuffer = null;
        NetworkingReader m_messageReader = null;

       // Type m_NetworkConnectionClass = typeof(NetworkConnection); //Nice
        public HostTopology m_hostTopology { get; private set; }
        public int numChannels { get { return m_hostTopology.DefaultConfig.ChannelCount; } }
        public int packetSize { get { return m_hostTopology.DefaultConfig.PacketSize; } }


        List<NetworkingConnection> m_connections = new List<NetworkingConnection>();
        ReadOnlyCollection<NetworkingConnection> m_connectionsReadOnly;
        Dictionary<ushort, Action<NetworkingMessage>> m_messageHandlers = new Dictionary<ushort, Action<NetworkingMessage>>();

        //public bool useWebSockets { get { return m_UseWebSockets; } set { m_UseWebSockets = value; } } No .. Create webSocket server if so
        public ReadOnlyCollection<NetworkingConnection> connections { get { return m_connectionsReadOnly; } }
        public Dictionary<ushort, Action<NetworkingMessage>> messageHandlers { get { return m_messageHandlers; } }

        public byte[] messageBuffer { get { return m_MsgBuffer; } }
        public NetworkingReader messageReader { get { return m_messageReader; } }

        public float m_maxDelay;

        public void SetMaxDelay(float maxDelay)
        {
            foreach(NetworkingConnection conn in connections)
            {
                conn.SetMaxDelay(maxDelay);
            }

            m_maxDelay = maxDelay;
        }

        /*  public Type networkConnectionClass
          {
              get { return m_NetworkConnectionClass; }
          }

          public void SetNetworkConnectionClass<T>() where T : NetworkConnection
          {
              m_NetworkConnectionClass = typeof(T);
          }*/


        public NetworkingServer()
        {
            m_connectionsReadOnly = new ReadOnlyCollection<NetworkingConnection>(m_connections);

            this.RegisterHandler(NetworkingMessageType.Connect, BaseOnServerConnect);
            this.RegisterHandler(NetworkingMessageType.AddPlayer, BaseOnServerAddPlayer);
            this.RegisterHandler(NetworkingMessageType.Disconnect, BaseOnServerDisconnect);
            this.RegisterHandler(NetworkingMessageType.Ready, BaseOnClientReadyOnServer);
            this.RegisterHandler(NetworkingMessageType.Error, BaseOnServerError);
        }

        ushort s_lastAssignedNetworkId = 0;
        internal ushort GetNextNetworkId()
        {
            s_lastAssignedNetworkId++;
            return s_lastAssignedNetworkId;
        }

        /*internal void AddNetworkId(ushort id)
        {
            if (id >= s_lastAssignedNetworkId)
            {
                s_lastAssignedNetworkId = (ushort)(id);
            }
        }*/

        /// <summary>
        /// Care to call Configure instead of this
        /// </summary>
       /* public virtual void Initialize()
        {
            //if (m_Initialized) //Plz no
            //  return;

             //Configure(hostTopology);
             m_Initialized = true;

            //NetworkTransport.Init();  //Has to be done by developper (sry ^^ )

            m_MsgBuffer = new byte[NetworkMessage.MaxMessageSize];
            m_messageReader = new NetworkingReader(m_MsgBuffer);

            //Sure... Why not ? -_-
            /*if (m_HostTopology == null)
            {
                var config = new ConnectionConfig();
                config.AddChannel(QosType.ReliableSequenced);
                config.AddChannel(QosType.Unreliable);
                m_HostTopology = new HostTopology(config, 8);
            }

            if (LogFilter.logDebug) { Debug.Log("NetworkingServer initialize."); }
        } */ 

        public bool Configure(ConnectionConfig config, int maxConnections, string serverName = "Default")
        {
            return Configure(new HostTopology(config, maxConnections), serverName);
        }

        public bool Configure(HostTopology topology, string serverName = "Default")
        {
            this.m_serverName = serverName;
            m_MsgBuffer = new byte[NetworkMessage.MaxMessageSize];
            m_messageReader = new NetworkingReader(m_MsgBuffer);
            m_hostTopology = topology;
            return true;
        }

        public virtual bool Listen(string ipAddress, int serverListenPort)
        {
           // Initialize(); Developper job
           m_serverPort = serverListenPort;
           m_serverAddress = ipAddress;
                //   if (m_UseWebSockets)  //If so create a websocket server ...
                // {
                //    m_ServerHostId = NetworkTransport.AddWebsocketHost(m_HostTopology, serverListenPort, ipAddress);
                // }
                // else
                // {
                m_hostId = NetworkTransport.AddHost(m_hostTopology, serverListenPort, ipAddress);
           // }

            if (m_hostId == -1)
            {
                return false;
            }

            if (LogFilter.logDebug) { Debug.Log("NetworkingServer listen: " + ipAddress + ":" + serverListenPort); }
            return true;
        }

        public virtual bool Listen(int serverListenPort)
        {
             m_serverAddress = "localhost";
             m_serverPort = serverListenPort;
             m_hostId = NetworkTransport.AddHost(m_hostTopology, serverListenPort);

            if (m_hostId == -1)
            {
                return false;
            }

            if (LogFilter.logDebug) { Debug.Log("NetworkingServer listen: " + serverListenPort); }
            return true;
        }
        //Useless if you follow a specific workflow (Configure-->Listen)
       /* public bool Listen(int serverListenPort, HostTopology topology)
        {
            m_HostTopology = topology;
            //Initialize();
            m_ListenPort = serverListenPort;

            if (m_UseWebSockets)
            {
                m_serverHostId = NetworkTransport.AddWebsocketHost(m_HostTopology, serverListenPort);
            }
            else
            {
                m_serverHostId = NetworkTransport.AddHost(m_HostTopology, serverListenPort);
            }

            if (m_serverHostId == -1)
            {
                return false;
            }

            if (LogFilter.logDebug) { Debug.Log("NetworkServerSimple listen " + m_ListenPort); }
            return true;
        }*/

        public bool ListenRelay(int listenPort, string relayIp, int relayPort, NetworkID netGuid, SourceID sourceId, NodeID nodeId)
        {
            // Initialize(); Developper can do this
            m_serverAddress = "localhost";
            m_serverPort = listenPort;
            m_hostId = NetworkTransport.AddHost(m_hostTopology, listenPort);

            if (m_hostId == -1)
            {
                return false;
            }

            if (LogFilter.logDebug) { Debug.Log("Server Host Slot Id: " + m_hostId); }

            //Update(); why ?

            byte error;
            NetworkTransport.ConnectAsNetworkHost(
                m_hostId,
                relayIp,
                relayPort,
                netGuid,
                sourceId,
                nodeId,
                out error);

            m_RelaySlotId = 0;
            if (LogFilter.logDebug) { Debug.Log("Relay Slot Id: " + m_RelaySlotId); }

            return true;
        }

        public void Stop()
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkingServer stop "); }

            BaseOnStopServer();

            DisconnectAllConnections();
            byte error;
            NetworkTransport.DisconnectNetworkHost(m_hostId, out error);

            m_hostId = -1;
        }


        public void RegisterHandler(ushort msgType, Action<NetworkingMessage> handler)
        {
            Action<NetworkingMessage> callbacks;
            m_messageHandlers.TryGetValue(msgType, out callbacks);

            if(callbacks != null)
            {
                callbacks += handler;
            }
            else
            {
                callbacks = handler;
                m_messageHandlers.Add(msgType, callbacks);
            }

            for (int i = 0; i < connections.Count; i++)
            {
                if(connections[i] != null)
                    connections[i].RegisterHandler(msgType, handler);
            }
        }

        public void UnregisterHandler(ushort msgType, Action<NetworkingMessage> handler)
        {
            Action<NetworkingMessage> callbacks;
            m_messageHandlers.TryGetValue(msgType, out callbacks);

            if (callbacks != null)
            {
                callbacks -= handler;
            }

            for (int i = 0; i < connections.Count; i++)
            {
                if(connections[i] != null)
                    connections[i].UnregisterHandler(msgType, handler);
            }
        }

        public void UnregisterHandler(ushort msgType)
        {
            m_messageHandlers.Remove(msgType);
        }

        public void ClearHandlers()
        {
            m_messageHandlers.Clear();
        }

        // this can be used independantly of Update() - such as when using external connections and not listening.
        public void UpdateConnections()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                NetworkingConnection conn = connections[i];
                if (conn != null)
                    conn.FlushChannels();
            }
        }

        public void Update()
        {
            if (m_hostId == -1)
                return;

            int connectionId;
            int channelId;
            int receivedSize;
            byte error;

            var networkEvent = NetworkEventType.DataEvent;

            if (m_RelaySlotId != -1)
            {
                networkEvent = NetworkTransport.ReceiveRelayEventFromHost(m_hostId, out error);
                if (NetworkEventType.Nothing != networkEvent)
                {
                    if (LogFilter.logDebug) { Debug.Log("NetGroup event:" + networkEvent); }
                }
                if (networkEvent == NetworkEventType.ConnectEvent)
                {
                    if (LogFilter.logDebug) { Debug.Log("NetGroup server connected"); }
                }
                if (networkEvent == NetworkEventType.DisconnectEvent)
                {
                    if (LogFilter.logDebug) { Debug.Log("NetGroup server disconnected"); }
                }
            }

            do
            {
                networkEvent = NetworkTransport.ReceiveFromHost(m_hostId, out connectionId, out channelId, m_MsgBuffer, (int)m_MsgBuffer.Length, out receivedSize, out error);
                if (networkEvent != NetworkEventType.Nothing)
                {
                    if (LogFilter.logDev) { Debug.Log("Server event: host=" + m_hostId + " event=" + networkEvent + " error=" + error); }
                }

                switch (networkEvent)
                {
                    case NetworkEventType.ConnectEvent:
                        {
                            HandleConnect(connectionId, error);
                            break;
                        }

                    case NetworkEventType.DataEvent:
                        {
                            HandleData(connectionId, channelId, receivedSize, error);
                            break;
                        }

                    case NetworkEventType.DisconnectEvent:
                        {
                            HandleDisconnect(connectionId, error);
                            break;
                        }

                    case NetworkEventType.Nothing:
                        break;

                    default:
                        if (LogFilter.logError) { Debug.LogError("Unknown network message type received: " + networkEvent); }
                        break;
                }
            }
            while (networkEvent != NetworkEventType.Nothing);

            UpdateConnections();
        }

        public NetworkingConnection FindConnection(int connectionId)
        {
            if (connectionId < 0 || connectionId >= connections.Count)
                return null;

            return m_connections[connectionId];
        }

        public bool SetConnectionAtIndex(NetworkingConnection conn)
        {
            while (connections.Count <= conn.m_connectionId)
            {
                m_connections.Add(null);
            }

            if (connections[conn.m_connectionId] != null)
            {
                // already a connection at this index
                return false;
            }

            conn.m_linkedServer = this;
            m_connections[conn.m_connectionId] = conn;
            //conn.SetHandlers(m_MessageHandlers); 
            return true;
        }

        public bool RemoveConnectionAtIndex(int connectionId)
        {
            if (connectionId < 0 || connectionId >= connections.Count)
                return false;

            m_connections[connectionId] = null;
            return true;
        }

        void HandleConnect(int connectionId, byte error)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkServerSimple accepted client:" + connectionId); }

            if (error != 0)
            {
                OnConnectError(connectionId, error);
                return;
            }

            string address;
            int port;
            NetworkID networkId;
            NodeID node;
            byte error2;
            NetworkTransport.GetConnectionInfo(m_hostId, connectionId, out address, out port, out networkId, out node, out error2);

            NetworkingConnection conn = new NetworkingConnection(address,port);

            conn.m_messageHandlers = new Dictionary<ushort, Action<NetworkingMessage>>(this.m_messageHandlers);

            conn.SetMaxDelay(m_maxDelay);
            conn.Configure(this.m_hostTopology.DefaultConfig, this.m_hostTopology.MaxDefaultConnections);
            conn.Initialize(address, m_hostId, connectionId);
            conn.m_lastError = (NetworkError)error2;

            // add connection at correct index
            while (connections.Count <= connectionId)
            {
                m_connections.Add(null);
            }

            conn.m_linkedServer = this;
            m_connections[connectionId] = conn;

            OnConnected(conn);
        }

        void HandleDisconnect(int connectionId, byte error)
        {
            if (LogFilter.logDebug) { Debug.Log("NetworkingServer disconnect client:" + connectionId); }

            var conn = FindConnection(connectionId);
            if (conn == null)
            {
                return;
            }
            conn.m_lastError = (NetworkError)error;

            if (error != 0)
            {
                if ((NetworkError)error != NetworkError.Timeout)
                {
                    m_connections[connectionId] = null;
                    if (LogFilter.logError) { Debug.LogError("Server client disconnect error, connectionId: " + connectionId + " error: " + (NetworkError)error); }

                    OnDisconnectError(conn, error);
                    return;
                }
            }

            OnDisconnected(conn);

            conn.Disconnect();
            m_connections[connectionId] = null;
            if (LogFilter.logDebug) { Debug.Log("Server lost client:" + connectionId); }
        }

        void HandleData(int connectionId, int channelId, int receivedSize, byte error)
        {
            var conn = FindConnection(connectionId);
            if (conn == null)
            {
                if (LogFilter.logError) { Debug.LogError("HandleData Unknown connectionId:" + connectionId); }
                return;
            }
            conn.m_lastError = (NetworkError)error;

            if (error != 0)
            {
                OnDataError(conn, error);
                return;
            }

            m_messageReader.SeekZero();
            OnData(conn, receivedSize, channelId);
        }


        //Sending purpose////////////////////////////////////////////////////////////


        public bool SendTo(int connectionId, byte[] bytes, int numBytes, int channelId = NetworkingMessageType.Channels.DefaultReliableSequenced)
        {
            var outConn = FindConnection(connectionId);
            if (outConn == null)
            {
                return false;
            }
           return  outConn.Send(bytes, numBytes, channelId);
        }

        public bool SendTo(int connectionId, NetworkingWriter writer, int channelId = NetworkingMessageType.Channels.DefaultReliableSequenced)
        {
            var outConn = FindConnection(connectionId);
            if (outConn == null)
            {
                return false;
            }
            return outConn.Send(writer, channelId);
        }

        public bool SendToAll(NetworkingWriter writer, int channelId = NetworkingMessageType.Channels.DefaultReliableSequenced)
        {
            bool result = true;
            foreach (NetworkingConnection conn in connections)
            {
                if (conn != null)
                    result &= conn.Send(writer, channelId);
            }

            return result;
        }


        public bool SendToAll(ushort msgType, NetworkingMessage msg, int channelId = NetworkingMessageType.Channels.DefaultReliableSequenced)
        {
            msg.m_type = msgType;

            bool result = true;
            foreach(NetworkingConnection conn in connections)
            {
                if(conn != null)
                    result &= conn.Send(msgType, msg, channelId);
            }

            return result;
        }

        public bool SendToReady(ushort msgType, NetworkingMessage msg, int channelId = NetworkingMessageType.Channels.DefaultReliableSequenced)
        {
            if (LogFilter.logDev) { Debug.Log("Server.SendToReady id:" + msgType); }

            foreach (NetworkingConnection conn in connections) // vis2k: foreach
            {
                if (conn != null && conn.isReady)
                {
                    conn.Send(msgType, msg, channelId);
                }
            }
            return true;
        }


        /// //////////////////////////////////////////////////////////////////////////////////


        public void Disconnect(int connectionId)
        {
            var outConn = FindConnection(connectionId);
            if (outConn == null)
            {
                return;
            }
            outConn.Disconnect();
            m_connections[connectionId] = null;
        }

        public void DisconnectAllConnections()
        {
            for (int i = 0; i < m_connections.Count; i++)
            {
                NetworkingConnection conn = m_connections[i];
                if (conn != null)
                {
                    conn.Disconnect();
                }
            }
        }

        // --------------------------- virtuals ---------------------------------------

        public virtual void OnConnectError(int connectionId, byte error)
        {
            Debug.LogError("OnConnectError error:" + error);
        }

        public virtual void OnDataError(NetworkingConnection conn, byte error)
        {
            Debug.LogError("OnDataError error:" + error);
        }

        public virtual void OnDisconnectError(NetworkingConnection conn, byte error)
        {
            Debug.LogError("OnDisconnectError error:" + error);
        }

        public virtual void OnConnected(NetworkingConnection conn)
        {
            conn.InvokeHandler(NetworkingMessageType.Connect, null, NetworkingMessageType.Channels.DefaultReliable);
        }

        public virtual void OnDisconnected(NetworkingConnection conn)
        {
            conn.InvokeHandler(NetworkingMessageType.Disconnect, null, NetworkingMessageType.Channels.DefaultReliable);
        }

        public virtual void OnData(NetworkingConnection conn, int receivedSize, int channelId)
        {
            conn.TransportReceive(m_MsgBuffer, receivedSize, channelId);
        }



        #region Server handlers

        void BaseOnServerConnect(NetworkingMessage netMsg)
        {
            Debug.Log("Server connect");

            if (OnServerConnect != null)
                OnServerConnect(this,netMsg);

           // this.SendToAll(NetworkingMessageType.ClientConnectFromServerMessage, new EmptyMessage());
        }


        void BaseOnServerAddPlayer(NetworkingMessage netMsg)
        {
            Debug.Log("Server add player");

            //GameObject obj = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity) as GameObject;
            //NetworkServer.AddPlayerForConnection(netMsg.conn, obj, 0);

            if (OnServerAddPlayer != null)
                OnServerAddPlayer(this,netMsg);
        }


        void BaseOnServerError(NetworkingMessage netMsg)
        {
           // NetworkServer.DestroyPlayersForConnection(this,netMsg.conn);
        }

        void BaseOnServerDisconnect(NetworkingMessage netMsg)
        {
            Debug.Log("Server disconnect");

           // NetworkServer.DestroyPlayersForConnection(netMsg.conn);

            if (OnServerDisconnect != null)
                OnServerDisconnect(this, netMsg);
        }


        void BaseOnClientReadyOnServer(NetworkingMessage netMsg)
        {
            Debug.Log("Client ready on server");

            //AddObserverToInactive(netMsg.conn);
            //NetworkServer.SetClientReady(netMsg.conn);

            netMsg.conn.isReady = true;

            if (OnConnectionReady != null)
                OnConnectionReady(this, netMsg);

           // this.SendToAll(NetworkingMessageType.ClientReadyFromServerMessage, new EmptyMessage());
        }


       internal void BaseOnStopServer()
        {
            Debug.Log("Server stop");

            if (OnStopServer != null)
                OnStopServer(this);
        }
        #endregion
    }
}