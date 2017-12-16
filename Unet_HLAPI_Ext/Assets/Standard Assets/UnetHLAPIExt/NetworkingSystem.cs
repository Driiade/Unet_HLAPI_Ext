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

        public static bool ServerIsReady { get; private set; }


        public bool destroyNetworkObjectsOnDisconnection = true;

        public struct ConfigurationInfo
        {
            public short message;
            public NetworkMessageDelegate func;

            public ConfigurationInfo(short message, NetworkMessageDelegate func)
            {
                this.message = message;
                this.func = func;
            }
        }


        /// <summary>
        /// Client configurations are added to client to handle messages.
        /// </summary>
        static List<ConfigurationInfo> clientConfigurations = new List<ConfigurationInfo>();

        public static void RegisterClientHandler(ConfigurationInfo c)
        {
            clientConfigurations.Add(c);

            if(NetworkingSystem.Instance && NetworkingSystem.Instance.client != null)
            {
                NetworkingSystem.Instance.client.RegisterHandler(c.message, c.func);
            }
        }

        public static void UnRegisterClientHandler(ConfigurationInfo c)
        {
            clientConfigurations.Remove(clientConfigurations.Find(x => { return c.func == x.func && x.message == c.message; }));

            if (NetworkingSystem.Instance && NetworkingSystem.Instance.client != null)
            {
                NetworkingSystem.Instance.client.UnregisterHandler(c.message);
            }
        }


        /// <summary>
        /// Server configurations are added to client to handle messages.
        /// </summary>
        static List<ConfigurationInfo> serverConfigurations = new List<ConfigurationInfo>();

        public static void RegisterServerHandler(ConfigurationInfo c)
        {
            serverConfigurations.Add(c);
             NetworkServer.RegisterHandler(c.message, c.func);
        }

        public static void UnRegisterServerHandler(ConfigurationInfo c)
        {
            serverConfigurations.Remove(serverConfigurations.Find(x => { return c.func == x.func && x.message == c.message; }));
            NetworkServer.UnregisterHandler(c.message);
        }


        [Tooltip("Has the NetworkingSystem to autoConnect on local network on start ?")]
        public bool connectOnStart = true;

        [Tooltip("Has the NetworkingSystem to autoReconnect on local network when a connection with the server is lost ?")]
        public bool autoReconnectOnLocal = true;

        public bool setReadyOnConnect = false;

        [Tooltip("The server adress to use(By default : local network)")]
        public string serverAdress = "localhost";


        [Tooltip("The server port (By default : 7777 with +1 of increment if the port is busy)")]
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
        public float maxDelayForSendingData = 0.01f;
        public ushort maxSentMessageQueueSize = 150;
        public uint sendDelay = 5;

        [Space(20)]
        [SerializeField]
        [Tooltip("The GameObject that represent the player")]
        public GameObject playerPrefab;

        public bool instantiatePlayerObjectOnClientConnect = true;

        /// <summary>
        /// Networked GameObject which need to be registered
        /// </summary>
        [Space(2)]
        public GameObject[] registeredNetworkedGameObjectsArray;

        public int GetIndexOfRegisteredGameObject(GameObject go)
        {
            for (int i = 0; i < registeredNetworkedGameObjectsArray.Length; i++)
            {
                if (registeredNetworkedGameObjectsArray[i] == go)
                    return i;
            }

            return -1;
        }


        private bool currentMatchIsLocked;
        public bool CurrentMatchIsLocked
        {
            get { return currentMatchIsLocked; }
            set { currentMatchIsLocked = value; }
        }

#if DEVELOPMENT
        public bool debug = true;
#endif

        NetworkClient client = null;

        /// <summary>
        /// The client used by the networkSystem.
        /// </summary>
        public NetworkClient Client
        {
            get { return client; }
        }


        #region Server
        /// <summary>
        /// Called on server when a player gameObject is added by the server
        /// </summary>
        public static NetworkMessageDelegate OnServerAddPlayer;

        /// <summary>
        /// Called on server when a client connect
        /// </summary>
        public static NetworkMessageDelegate OnServerConnect;

        /// <summary>
        /// Called on server when a client disconnect
        /// </summary>
        public static NetworkMessageDelegate OnServerDisconnect;

        /// <summary>
        /// Called on server when a client become ready on server
        /// </summary>
        public static NetworkMessageDelegate OnClientReadyOnServer;

        /// <summary>
        /// Called on server when the server start
        /// </summary>
        public static NetworkMessageDelegate OnStartServer;

        /// <summary>
        /// Called on server when the server stop
        /// </summary>
        public static NetworkMessageDelegate OnStopServer;

        #endregion

        #region From server to every client
        /// <summary>
        /// Called on every client when a client connect
        /// </summary>
        public static NetworkMessageDelegate OnClientConnectFromServer;

        /// <summary>
        /// Called on every client when a client become ready on the server
        /// </summary>
        public static NetworkMessageDelegate OnClientReadyFromServer;
        #endregion

        #region Client
        /// <summary>
        /// Called on client when the local client disconnect
        /// </summary>
        public static NetworkMessageDelegate OnClientDisconnect;

        /// <summary>
        /// Called on client when the local client connect
        /// </summary>
        public static NetworkMessageDelegate OnClientConnect;

        /// <summary>
        /// Called on client when the local client become not ready
        /// </summary>
        public static NetworkMessageDelegate OnClientNotReady;


        /// <summary>
        /// Called on client when the local client stop volontary
        /// </summary>
        public static NetworkMessageDelegate OnStopClient;

        /// <summary>
        /// Called On client when the server ask to change the scene
        /// </summary>
        public static Action<SceneMessage> OnClientReceiveScene;

        #endregion

        protected override void Awake()
        {
            base.Awake();
            Application.runInBackground = true; //obligation for multiplayer games
        }


        void Start()
        {
            if (connectOnStart)
                StartHost();
        }

        /// <summary>
        /// Tell if the network is active (localhost or online)
        /// </summary>
        public static bool isNetworkActive
        {
            get { return NetworkClient.active || NetworkServer.active; }
        }

        void SyncServerIsReady(NetworkConnection conn)
        {
            conn.Send(NetworkMessages.ServerReadyMessage, new BooleanMessage(ServerIsReady));
        }

        public static void SetServerReady(bool ready)
        {
            if(NetworkServer.active)
            {
                ServerIsReady = false;

                NetworkServer.SendByChannelToAll(NetworkMessages.ServerReadyMessage, new BooleanMessage(ready), 0);
            }
        }

        public static void BaseOnServerReady(NetworkMessage netMsg)
        {
            ServerIsReady = netMsg.ReadMessage<BooleanMessage>().m_value;
        }


        public static bool AllClientsAreReady()
        {
            foreach(NetworkConnection c in NetworkServer.connections)
            {
                if(c != null)
                {
                    if (!c.isReady)
                        return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Start Server And Client
        /// </summary>
        /// <param name="matchInfo">if null, will not use relay but will connect locally</param>
        /// <returns></returns>
        public NetworkClient StartHost(MatchInfo matchInfo)
        {
            StartServer(matchInfo);
            StartLocalClient();

            return client;
        }

        /// <summary>
        /// Start Server And Client
        /// </summary>
        /// <returns></returns>
        public NetworkClient StartHost()
        {
            StartServer();
            StartLocalClient();

            return client;
        }


        /// <summary>
        /// Start a client using serverAdress and serverPort
        /// </summary>
        /// <returns></returns>
        public NetworkClient StartClient()
        {
            Debug.Log("Start client");
            return StartClient(serverAdress, serverPort);
        }

        /// <summary>
        /// Start a client using match
        /// </summary>
        /// <param name="matchInfo">if null, will start client with no relay</param>
        /// <returns></returns>
        public NetworkClient StartClient(MatchInfo matchInfo)
        {
            Debug.Log("Start client");

            if (client != null)
                StopClient();
            
            client = new NetworkClient();
            ConfigureClient(client);

            if (client == null) // A Problem occured when the client was configured.
                return null;

            client.Connect(matchInfo);

            return client;
        }


        /// <summary>
        /// Start a client using specific serverAdress and serverPort
        /// </summary>
        /// <param name="matchInfo">if null, will start client with no relay</param>
        /// <returns></returns>
        public NetworkClient StartClient(string serverAdress, int serverPort)
        {
            Debug.Log(serverAdress);
            Debug.Log(serverPort);
            Debug.Log("Start client");

            if (client != null)
                StopClient();

            client = new NetworkClient();
            ConfigureClient(client);

            if (client == null) // A Problem occured when the client was configured.
            {
                Debug.LogError("Client is null");
                return null;
            }


            client.Connect(serverAdress, serverPort);
  
            return client;
        }




        /// <summary>
        /// Start LocalClient (for the host)
        /// </summary>
        public NetworkClient StartLocalClient()
        {
            if (client != null)
                StopClient();

            Debug.Log("Start local client");

            client = ClientScene.ConnectLocalServer();
            ConfigureClient(client);

            return client;
        }

        /// <summary>
        /// Start a new server
        /// </summary>
        /// <param name="matchInfo"> if null, will start a server with no relay</param>
        public void StartServer(MatchInfo matchInfo)
        {
            if (NetworkServer.active)
                StopServer();

            if(!NetworkTransport.IsStarted)
                NetworkTransport.Init();

            ConfigureServer();

            NetworkServer.ListenRelay(matchInfo.address, matchInfo.port, matchInfo.networkId, SourceID.Invalid, matchInfo.nodeId);

            NetworkServer.SpawnObjects();

            if (OnStartServer != null)
                OnStartServer(null);
        }

        public void StartServer()
        {
            if (NetworkServer.active)
                StopServer();

            if (!NetworkTransport.IsStarted)
                NetworkTransport.Init();

            ConfigureServer();

            if (string.IsNullOrEmpty(serverAdress) || serverAdress.Equals("localhost") || serverAdress.Equals("127.0.0.1"))
            {
                while (!NetworkServer.Listen(serverPort))
                {
                    serverPort++;
                }
            }
            else
            {
                while (!NetworkServer.Listen(serverAdress, serverPort))
                {
                    serverPort++;
                }
            }

            NetworkServer.SpawnObjects();

            if (OnStartServer != null)
                OnStartServer(null);
        }


        public void ConfigureClient(NetworkClient client)
        {

            client.RegisterHandler(MsgType.Connect, BaseOnClientConnect);
            client.RegisterHandler(MsgType.Disconnect, BaseOnClientDisconnect);
            client.RegisterHandler(MsgType.AddPlayer, BaseOnClientAddPlayer);
            client.RegisterHandler(MsgType.Scene, BaseOnClientChangeScene);
            client.RegisterHandler(MsgType.NotReady, BaseOnClientNotReady);

            client.RegisterHandler(NetworkMessages.ClientConnectFromServerMessage, BaseOnClientConnectFromServer);
            client.RegisterHandler(NetworkMessages.ClientReadyFromServerMessage, BaseOnClientReadyFromServer);

            client.RegisterHandler(NetworkMessages.ServerReadyMessage, BaseOnServerReady);

            foreach (ConfigurationInfo i in clientConfigurations)
                client.RegisterHandler(i.message, i.func);


            client.Configure(Configuration(), (int)maxPlayer);

            if(playerPrefab)
                ClientScene.RegisterPrefab(playerPrefab);

            foreach (GameObject i in registeredNetworkedGameObjectsArray)
                ClientScene.RegisterPrefab(i);
        }

        public void ConfigureServer()
        {
            NetworkServer.RegisterHandler(MsgType.Connect, BaseOnServerConnect);
            NetworkServer.RegisterHandler(MsgType.AddPlayer, BaseOnServerAddPlayer);
            NetworkServer.RegisterHandler(MsgType.Disconnect, BaseOnServerDisconnect);
            NetworkServer.RegisterHandler(MsgType.Ready, BaseOnClientReadyOnServer);
            NetworkServer.RegisterHandler(MsgType.Error, BaseOnServerError);

            foreach (ConfigurationInfo i in serverConfigurations)
                NetworkServer.RegisterHandler(i.message, i.func);

            NetworkServer.Configure(Configuration(), (int)maxPlayer);
            NetworkServer.maxDelay = maxDelayForSendingData;
        }



        /// <summary>
        /// Stop the current client used by the NetworkingSystem
        /// </summary>
        public void StopClient()
        {
            if (client != null)
            {
                Debug.Log("Client stop");

                if (OnStopClient != null)
                    OnStopClient(null);


                if (destroyNetworkObjectsOnDisconnection)
                    ClientScene.DestroyAllClientObjects();

                    client.Disconnect();
                    client.Shutdown();
                    client = null;
            }    
        }


        /// <summary>
        /// Stop the current server used by the NetworkingSystem
        /// </summary>
        public void StopServer()
        {
            BaseOnStopServer();

            NetworkServer.Shutdown();
            NetworkServer.Reset();
        }


        /// <summary>
        /// Stop the current server and local client used by the NetworkingSystem
        /// </summary>
        public void StopHost()
        {
            if (NetworkServer.active)
            {
                StopServer();
            }
            if (NetworkClient.active)
            {
                StopClient();
            }

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


        public void ServerChangeScene(string sceneName, LoadSceneMode mode, bool async, bool immediate)
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
        }


        #region Server handlers

        void BaseOnServerConnect(NetworkMessage netMsg)
        {      
            Debug.Log("Server connect");

            SyncServerIsReady(netMsg.conn);

            if (OnServerConnect != null)
                OnServerConnect(netMsg);

            NetworkServer.SendToAll(NetworkMessages.ClientConnectFromServerMessage, new EmptyMessage());
        }


        void BaseOnServerAddPlayer(NetworkMessage netMsg)
        {
            Debug.Log("Server add player");

            GameObject obj = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity) as GameObject;
            NetworkServer.AddPlayerForConnection(netMsg.conn, obj, 0);

            if (OnServerAddPlayer != null)
                OnServerAddPlayer(netMsg);
        }


        void BaseOnServerError(NetworkMessage netMsg)
        {
           NetworkServer.DestroyPlayersForConnection(netMsg.conn);
        }

        void BaseOnServerDisconnect(NetworkMessage netMsg)
        {
            Debug.Log("Server disconnect");

           NetworkServer.DestroyPlayersForConnection(netMsg.conn);

            if (OnServerDisconnect != null)
                OnServerDisconnect(netMsg);
        }


        void BaseOnClientConnectFromServer(NetworkMessage netMsg)
        {
            Debug.Log("Client connect from server");        

            if (OnClientConnectFromServer != null)
                OnClientConnectFromServer(netMsg);
        }
      
        void BaseOnClientReadyOnServer(NetworkMessage netMsg)
        {
            Debug.Log("Client ready on server");

            AddObserverToInactive(netMsg.conn);
            NetworkServer.SetClientReady(netMsg.conn);

            if (OnClientReadyOnServer != null)
                OnClientReadyOnServer(netMsg);

            NetworkServer.SendToAll(NetworkMessages.ClientReadyFromServerMessage, new EmptyMessage());
        }


        void BaseOnClientReadyFromServer(NetworkMessage netMsg)
        {
            Debug.Log("Client ready from server");

            if (OnClientReadyFromServer != null)
                OnClientReadyFromServer(netMsg);
        }


        void BaseOnStopServer()
        {
            Debug.Log("Server stop");

            if (OnStopServer != null)
                OnStopServer(null);
        }


        public static int ServerConnectionsCount()
        {
            int cpt = 0;

            foreach(NetworkConnection i in NetworkServer.connections)
            {
                if (i != null)
                    cpt++;
            }

            return cpt;
        }

        #endregion


        #region Client handlers

        void BaseOnClientConnect(NetworkMessage netMsg)
        {
            Debug.Log("Client connect");

            client.connection.SetMaxDelay(maxDelayForSendingData);

            if(setReadyOnConnect)
                ClientScene.Ready(netMsg.conn);

            if (instantiatePlayerObjectOnClientConnect)
            {
               ClientScene.AddPlayer(0);
            }

            if (OnClientConnect != null)
                OnClientConnect(netMsg);
        }

         void BaseOnClientAddPlayer(NetworkMessage netMsg)
        {

        }

        void BaseOnClientNotReady(NetworkMessage netMsg)
        {
            Debug.Log("Client set as not ready");

            MethodInfo inf = typeof(ClientScene).GetMethod("SetNotReady", BindingFlags.Static | BindingFlags.NonPublic);
            inf.Invoke(null,null);

            if (OnClientNotReady != null)
                OnClientNotReady(netMsg);
        }

         void BaseOnClientDisconnect(NetworkMessage netMsg)
        {
            ClientScene.DestroyAllClientObjects();
            NetworkClient.ShutdownAll();
            client = null;

            Debug.Log("Client disconnect");


            if (OnClientDisconnect != null)
                OnClientDisconnect(netMsg);

            if(autoReconnectOnLocal)
                StartHost();
        }


         void BaseOnClientChangeScene(NetworkMessage netMsg)
        {
            SceneMessage sceneMessage = netMsg.ReadMessage<SceneMessage>();

            if (sceneMessage.m_immediate)
            {
                if (sceneMessage.m_async)
                    SceneManager.LoadSceneAsync(sceneMessage.m_sceneName, sceneMessage.m_loadSceneMode);
                else
                    SceneManager.LoadScene(sceneMessage.m_sceneName, sceneMessage.m_loadSceneMode);
            }

            if (OnClientReceiveScene != null)
                OnClientReceiveScene(sceneMessage);
        }


        #endregion


        private void AddObserverToInactive(NetworkConnection conn)
        {
            if (conn.connectionId == 0)
            {
                //local host has observer added to inactive NetworkBehaviours by SetClientReady already
                return;
            }

            foreach (NetworkIdentity netId in NetworkServer.objects.Values)
            {
                if (netId == null)
                {
                    Debug.LogError("Trying to add observer to object of null NetworkIdentity.", this);
                    continue;
                }
                if (!netId.gameObject.activeSelf)
                {
                    MethodInfo OnCheckObserver = typeof(NetworkIdentity).GetMethod("OnCheckObserver", BindingFlags.NonPublic | BindingFlags.Instance);
                    MethodInfo AddObserver = typeof(NetworkIdentity).GetMethod("AddObserver", BindingFlags.NonPublic | BindingFlags.Instance);

                    if ((bool)OnCheckObserver.Invoke(netId, new object[] { conn }))
                    {
                        AddObserver.Invoke(netId, new object[] { conn });
                    }
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (NetworkServer.active && NetworkServer.connections.Count == 1)
            {
                StopServer();
            }

            if (NetworkClient.active)
                StopClient();
        }
    }

}
