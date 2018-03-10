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

using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.Networking.Match;
using UnityEngine.Networking.Types;
using System;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Reflection;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingSystem : Singleton<NetworkingSystem> {

        //public static bool ServerIsReady { get; private set; }

        /// <summary>
        /// Called on client when the local client stop volontary
        /// </summary>
        public static Action OnStopAllConnections;


        public struct NetworkingConfiguration
        {
            public ushort message;
            public Action<NetworkingMessage> func;

            public NetworkingConfiguration(ushort message, Action<NetworkingMessage> func)
            {
                this.message = message;
                this.func = func;
            }
        }


        /// <summary>
        /// Client configurations are added to client to handle messages.
        /// </summary>
        static List<NetworkingConfiguration> clientConfigurations = new List<NetworkingConfiguration>();

#if CLIENT
        /// <summary>
        /// Register Handler for all connected or futur connected connections on all server
        /// </summary>
        /// <param name="c"></param>
        public static void RegisterConnectionHandler(ushort msgType, Action<NetworkingMessage> callback)
        {
            clientConfigurations.Add(new NetworkingConfiguration(msgType, callback));

            if(NetworkingSystem.Instance)
            {
                for (int i = 0; i < NetworkingSystem.Instance.connections.Count; i++)
                {
                    NetworkingSystem.Instance.connections[i].RegisterHandler(msgType, callback);
                }
            }
        }

        /// <summary>
        /// Register Handler for all connected or futur connected client on all server
        /// </summary>
        /// <param name="c"></param>
        public static void UnRegisterConnectionHandler(ushort msgType, Action<NetworkingMessage> callback)
        {
            clientConfigurations.Remove(clientConfigurations.Find(x => { return x.func == callback && x.message == msgType; }));

            if (NetworkingSystem.Instance)
            {
                for (int i = 0; i < NetworkingSystem.Instance.connections.Count; i++)
                {
                    NetworkingSystem.Instance.connections[i].UnregisterHandler(msgType, callback);
                }
            }
        }
#endif

#if SERVER
        /// <summary>
        /// Server configurations are added to servers to handle messages.
        /// </summary>
        static List<NetworkingConfiguration> serverConfigurations = new List<NetworkingConfiguration>();

        public static void RegisterServerHandler(ushort msgType, Action<NetworkingMessage> callback)
        {
            serverConfigurations.Add(new NetworkingConfiguration(msgType,callback));

            if (NetworkingSystem.Instance)
            {
                for (int i = 0; i < NetworkingSystem.Instance.additionnalServers.Count; i++)
                {
                    NetworkingSystem.Instance.additionnalServers[i].RegisterHandler(msgType, callback);
                }

                if (NetworkingSystem.Instance.mainServer != null)
                    NetworkingSystem.Instance.mainServer.RegisterHandler(msgType, callback);
            }

        }

        public static void UnRegisterServerHandler(ushort msgType, Action<NetworkingMessage> callback)
        {
            serverConfigurations.Remove(serverConfigurations.Find(x => { return x.func == callback && x.message == msgType; }));

            if (NetworkingSystem.Instance)
            {
                for (int i = 0; i < NetworkingSystem.Instance.additionnalServers.Count; i++)
                {
                    NetworkingSystem.Instance.additionnalServers[i].UnregisterHandler(msgType, callback);
                }

                if (NetworkingSystem.Instance.mainServer != null)
                    NetworkingSystem.Instance.mainServer.UnregisterHandler(msgType, callback);
            }
        }
#endif


        [Tooltip("Has the NetworkingSystem to autoReconnect on local network when a connection with the server is lost ?")]
        public bool autoReconnectOnLocal = true;

        public bool setReadyOnConnect = false;

        [Tooltip("The server adress to use(By default : local network)")]
        public string serverAdress = "localhost";

        [Tooltip("The server port (By default : 7777)")]
        public int serverPort = 7777;



        [Space(20)]
        [Tooltip("Max player allowed in a match")]
        public uint maxPlayer = 5;


        [Space(20)]
        [SerializeField]
        [Tooltip ("Additionnal channels for user. Base channels are :\n0-ReliableSequenced\n1-Reliable\n2-Unreliable\n3-AllCostDelivery\n4-ReliableStateUpdate\n5-StateUpdate")]
        private QosType[] additionnalChannel;

        [Space(20)]
        public uint connectionTimeOut = 1000;
        public uint disconnectionTimeOut = 5000;
    
        [Space(20)]
        public ushort packetSize = 1470;
        public ushort fragmentSize = 500;
        public ushort maxSentMessageQueueSize = 150;

        [Tooltip("Message will be packed during this delay in ms")]
        public uint sendDelay = 5;

        private bool currentMatchIsLocked;
        public bool CurrentMatchIsLocked
        {
            get { return currentMatchIsLocked; }
            set { currentMatchIsLocked = value; }
        }

#if DEVELOPMENT
        public bool debug = true;
#endif

#if CLIENT
        /// <summary>
        /// List of all connections running on this app
        /// </summary>
        public List<NetworkingConnection> connections = new List<NetworkingConnection>();
        public bool ConnectionIsActive() { return connections.Count > 0; }
#endif

#if SERVER
        /// <summary>
        /// The main server used for every basic aspect of a game (like gameObject, general timing...)
        /// </summary>
        public NetworkingServer mainServer;

        /// <summary>
        /// List of all additionnal servers running on this app
        /// </summary>
        public List<NetworkingServer> additionnalServers = new List<NetworkingServer>();
    
        public bool MainServerIsActive(){ return mainServer != null; }
#endif

        /// <summary>
        /// Called On client when the server ask to change the scene
        /// </summary>
        //public static Action<SceneMessage> OnClientReceiveScene;


        protected override void Awake()
        {
            base.Awake();
            Application.runInBackground = true; //obligation for multiplayer games
            NetworkTransport.Init();

#if CLIENT
            NetworkingConnection.OnConnectionDisconnect += InternalOnConnectionDisconnect;
#endif
        }


        private void Update()
        {
#if SERVER
            if (mainServer != null)
                mainServer.Update();

            for (int i = 0; i < additionnalServers.Count; i++)
            {
                additionnalServers[i].Update();
            }
#endif

#if CLIENT
            for (int i = 0; i < connections.Count; i++)
            {
                connections[i].Update();
            }
#endif
        }

        private void OnDestroy()
        {
#if CLIENT
            NetworkingConnection.OnConnectionDisconnect -= InternalOnConnectionDisconnect;
#endif
        }


#if SERVER && CLIENT
        /// <summary>
        /// Start Server And Client
        /// </summary>
        /// <param name="matchInfo">if null, will not use relay but will connect locally</param>
        /// <returns></returns>
        public void StartHost(MatchInfo matchInfo, out NetworkingServer server, out NetworkingConnection conn)
        {
            server = StartMainServer(matchInfo);
            server.m_isHost = true;
            conn = StartConnection(matchInfo);
            conn.m_server = server;
        }

        /// <summary>
        /// Start Server And Client
        /// </summary>
        /// <returns></returns>
        public void StartHost()
        {
            NetworkingServer server;
            NetworkingConnection connection;

            StartHost(out server, out connection);
        }


        /// <summary>
        /// Start Server And Client
        /// </summary>
        /// <returns></returns>
        public void StartHost(out NetworkingServer server, out NetworkingConnection conn)
        {
            server = StartMainServer();
            server.m_isHost = true;
            conn = StartConnection();
            conn.m_server = server;
        }
#endif

#if CLIENT
        /// <summary>
        /// Start a client using serverAdress and serverPort
        /// </summary>
        /// <returns></returns>
        public NetworkingConnection StartConnection()
        {
            Debug.Log("Start connection");
            return StartConnection(serverAdress, serverPort);
        }

        /// <summary>
        /// Start a client using match
        /// </summary>
        /// <param name="matchInfo">if null, will start client with no relay</param>
        /// <returns></returns>
        public NetworkingConnection StartConnection(MatchInfo matchInfo)
        {
            Debug.Log("Start client");

            if (connections.Count > 0)
                StopAllConnections();

            connections.Add(new NetworkingConnection());
            ConfigureConnection(connections[0]);

            if (connections[0] == null) // A Problem occured when the client was configured.
                return null;

            connections[0].Connect(matchInfo);

            return connections[0];
        }


        /// <summary>
        /// Start a client using specific serverAdress and serverPort
        /// </summary>
        /// <param name="matchInfo">if null, will start client with no relay</param>
        /// <returns></returns>
        public NetworkingConnection StartConnection(string serverAdress, int serverPort)
        {
            if (connections.Count > 0)
                StopAllConnections();

            NetworkingConnection conn = new NetworkingConnection();
            ConfigureConnection(conn);

            if (conn == null) // A Problem occured when the client was configured.
                return null;

            connections.Add(conn);
            conn.Connect(serverAdress, serverPort);

            return conn;
        }
#endif

#if SERVER

        /// <summary>
        /// Start a new server
        /// </summary>
        /// <param name="matchInfo"> if null, will start a server with no relay</param>
        public NetworkingServer StartMainServer(MatchInfo matchInfo)
        {
            if(mainServer != null)
                StopServer(mainServer);

            mainServer = new NetworkingServer();
            ConfigureServer(mainServer, true);

            mainServer.ListenRelay(serverPort,matchInfo.address, matchInfo.port, matchInfo.networkId, SourceID.Invalid, matchInfo.nodeId);

            InternalOnStartServer(mainServer);

            return mainServer;
        }

        /// <summary>
        /// Start a main server with server adress and port of the NetworkingSystem
        /// </summary>
        /// <returns></returns>
        public NetworkingServer StartMainServer()
        {
           return StartMainServer(this.serverAdress, this.serverPort);
        }


        public NetworkingServer StartMainServer(string serverAdress, int serverPort)
        {
            NetworkingServer server = new NetworkingServer();
            ConfigureServer(server, true);

            if (string.IsNullOrEmpty(serverAdress) || serverAdress.Equals("localhost") || serverAdress.Equals("127.0.0.1"))
            {
                if (!server.Listen(serverPort))
                {
                    return null;
                }
            }
            else
            {
                if (!server.Listen(serverAdress, serverPort))
                {
                    return null;
                }
            }

            InternalOnStartServer(server);

            mainServer = server;
            return server;
        }
#endif

#if CLIENT
        public void ConfigureConnection(NetworkingConnection connection)
        {
            foreach (NetworkingConfiguration i in clientConfigurations)
                connection.RegisterHandler(i.message, i.func);

            connection.Configure(Configuration(), (int)maxPlayer);
        }
#endif

#if SERVER
        public void ConfigureServer(NetworkingServer server, bool isMainServer)
        {
            foreach (NetworkingConfiguration i in serverConfigurations)
                server.RegisterHandler(i.message, i.func);

           server.Configure(Configuration(), (int)maxPlayer, isMainServer);
        }
#endif

#if CLIENT
        /// <summary>
        /// Stop the current connections used by the NetworkingSystem
        /// </summary>
        public void StopAllConnections()
        {
            if (OnStopAllConnections != null)
                OnStopAllConnections();

            for (int i = 0; i < connections.Count; i++)
            {
                StartCoroutine(RemoveHostCoroutine(connections[i].m_hostId));
                connections[i].Stop();
            }

            connections.Clear();
        }
#endif

#if SERVER
        /// <summary>
        /// Stop the current server used by the NetworkingSystem
        /// </summary>
        public void StopAllServers()
        {
            if(mainServer != null)
                 StopServer(mainServer);

            for (int i = 0; i < additionnalServers.Count; i++)
            {
                StopServer(additionnalServers[i]);
            }
        }

        public void StopServer(NetworkingServer server)
        {
            StartCoroutine(RemoveHostCoroutine(server.m_hostId));
            server.Stop();

            if (mainServer == server)
                mainServer = null;

            additionnalServers.Remove(server);
        }
#endif

#if SERVER && CLIENT
        /// <summary>
        /// Stop the current server and local client used by the NetworkingSystem
        /// </summary>
        public void StopHost()
        {
             StopAllServers();
             StopAllConnections();
        }
#endif

        IEnumerator RemoveHostCoroutine(int hostId)
        {
            yield return new WaitForSecondsRealtime(1f);
            NetworkTransport.RemoveHost(hostId);
        }



       public ConnectionConfig Configuration()
        {
            ConnectionConfig config = new ConnectionConfig();

            config.AddChannel(QosType.ReliableSequenced);
            config.AddChannel(QosType.Reliable);
            config.AddChannel(QosType.Unreliable);
            config.AddChannel(QosType.AllCostDelivery);
            config.AddChannel(QosType.ReliableStateUpdate);
            config.AddChannel(QosType.StateUpdate);

            if (additionnalChannel != null)
            {
                foreach (QosType i in additionnalChannel)
                    config.AddChannel(i);
            }

            config.ConnectTimeout = connectionTimeOut;
            config.DisconnectTimeout = disconnectionTimeOut;
            config.PacketSize = packetSize;
            config.FragmentSize = fragmentSize;
            config.MaxSentMessageQueueSize = maxSentMessageQueueSize;
            config.SendDelay = sendDelay;

            return config;
        }


       /* public void ServerChangeScene(string sceneName, LoadSceneMode mode, bool async, bool immediate)
        {
            SceneMessage msg = new SceneMessage(sceneName, mode, async, immediate);
            NetworkServer.SendToAll(MsgType.Scene, msg);

            if (immediate)
            {
                if (!async)
                    SceneManager.LoadScene(sceneName, mode);
                else
                    SceneManager.LoadSceneAsync(sceneName, mode);
            }
        }*/

       /* void BaseOnClientChangeScene(NetworkingMessage netMsg)
        {
            SceneMessage sceneMessage = netMsg.reader.ReadMessage<SceneMessage>();

            if (sceneMessage.m_immediate)
            {
                if (sceneMessage.m_async)
                    SceneManager.LoadSceneAsync(sceneMessage.m_sceneName, sceneMessage.m_loadSceneMode);
                else
                    SceneManager.LoadScene(sceneMessage.m_sceneName, sceneMessage.m_loadSceneMode);
            }

            if (OnClientReceiveScene != null)
                OnClientReceiveScene(sceneMessage);
        }*/


#if SERVER
        void InternalOnStartServer(NetworkingServer server)
        {
            Debug.Log("Start server");

            if (NetworkingServer.OnStartServer != null)
                NetworkingServer.OnStartServer(server);
        }
#endif

#if CLIENT
        void InternalOnConnectionDisconnect(NetworkingConnection conn)
        {
            this.connections.Remove(conn);
        }
#endif

        private void OnApplicationQuit()
        {
#if SERVER
            StopAllServers();
#endif
#if CLIENT
            StopAllConnections();
#endif
        }
    }

}
