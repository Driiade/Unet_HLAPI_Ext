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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingGameObjectSystem : Singleton<NetworkingGameObjectSystem>
    {
        /// <summary>
        /// Networked GameObject which need to be registered
        /// </summary>
        [Space(2)]
        public GameObject[] spawnedGameObjectOnConnect;
        public string spawnSceneOnConnect = "";

        [Space(10)]
        public GameObject[] registeredNetworkedGameObjectsArray;


        /// <summary>
        /// NetworkIdentity by connection, only available on server
        /// Used because netIdentity are specific by server (between server, same netIdentity can be found)
        /// </summary>
        private Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> m_serverNetworkedObjects = new Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>>();
        private Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> m_connectionNetworkedObjects = new Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>>();

        private Dictionary<ushort, GameObject> registeredNetworkedGameObjectsDictionnary = new Dictionary<ushort, GameObject>();

        /// <summary>
        /// Current netId index for each connection
        /// </summary>
        private Dictionary<NetworkingServer, ushort> m_serverCurrentNetId = new Dictionary<NetworkingServer, ushort>();

        /// <summary>
        /// Used when scene is loading and sceneObject can't be found
        /// </summary>
       // private Dictionary<ushort, NetworkingConnection> m_pendingSceneNetworkedObjects = new Dictionary<ushort, NetworkingConnection>();

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < registeredNetworkedGameObjectsArray.Length; i++)
            {
                GameObject go = registeredNetworkedGameObjectsArray[i];
                registeredNetworkedGameObjectsDictionnary.Add(go.GetComponent<NetworkingIdentity>().m_assetId, go);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            //Connection
            NetworkingConnection.OnConnectionConnect += OnConnectionConnect;
            NetworkingConnection.OnConnectionDisconnect += OnConnectionDisconnect;
            NetworkingConnection.OnStopConnection += OnStopConnection;
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.ObjectSpawn, OnConnectionSpawnMessage);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.SceneObjectNetId, OnConnectionSceneObjectMessage);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.ObjectDestroy, OnConnectionDestroy);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.Rpc, OnConnectionRpc);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.AutoRpc, OnConnectionRpc);
            //NetworkingIdentity.OnNetworkingIdentityDestroy += RemoveNetworkingIdentity;

            //Server
            NetworkingServer.OnServerDisconnect += OnServerDisconnect;
            NetworkingServer.OnStopServer += OnStopServer;
            NetworkingServer.OnStartServer += OnStartServer;
            NetworkingServer.OnServerConnect += OnServerConnect;
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ConnectionLoadScene, OnServerLoadScene);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ReplicatedPrefabScene, OnServerReplicatePrefabScene);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ObjectSpawnFinish, OnServerObjectSpawnFinish);
        }


        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            NetworkingConnection.OnConnectionConnect -= OnConnectionConnect;
            NetworkingConnection.OnConnectionDisconnect -= OnConnectionDisconnect;
            NetworkingConnection.OnStopConnection -= OnStopConnection;
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectSpawn, OnConnectionSpawnMessage);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.SceneObjectNetId, OnConnectionSceneObjectMessage);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.Rpc, OnConnectionRpc);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.AutoRpc, OnConnectionRpc);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectDestroy, OnConnectionDestroy);


            NetworkingServer.OnStartServer -= OnStartServer;
            NetworkingServer.OnServerDisconnect -= OnServerDisconnect;
            NetworkingServer.OnStopServer -= OnStopServer;
            NetworkingServer.OnServerConnect -= OnServerConnect;

            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ConnectionLoadScene, OnServerLoadScene);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ReplicatedPrefabScene, OnServerReplicatePrefabScene);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ObjectSpawnFinish, OnServerObjectSpawnFinish);
        }

        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity, Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            if (dictionary.ContainsKey(networkingIdentity.m_server))
            {
                dictionary[networkingIdentity.m_server].Remove(networkingIdentity.m_netId);
            }
        }

        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity, Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            if (dictionary.ContainsKey(networkingIdentity.m_connection))
            {
                dictionary[networkingIdentity.m_connection].Remove(networkingIdentity.m_netId);
            }
        }

        /// <summary>
        /// Spawn an object accross network.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="go"></param>
        public void SpawnOnServer(NetworkingServer server, NetworkingConnection conn, GameObject gameObject, Scene scene)
        {
            if (scene != default(Scene))
                SceneManager.SetActiveScene(scene);


            GameObject go = Instantiate(gameObject);
            NetworkingIdentity networkingIdentity = go.GetComponent<NetworkingIdentity>();
            ServerAssignNetId(server, networkingIdentity);
            networkingIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
            networkingIdentity.m_serverConnection = conn;

            server.SendTo(conn.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(networkingIdentity.m_assetId, networkingIdentity.netId, networkingIdentity.localPlayerAuthority, scene.name), NetworkingChannel.DefaultReliableSequenced);

            foreach (NetworkingConnection c in server.connections)
            {
                if (c != null && c != conn)
                {
                    server.SendTo(c.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(networkingIdentity.m_assetId, networkingIdentity.netId, false, scene.name), NetworkingChannel.DefaultReliableSequenced);
                }
            }
        }


        void OnServerCommand(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;

            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityID, m_serverNetworkedObjects);


            if (netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
        }

        void OnConnectionRpc(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;

            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection, networkingIdentityID, m_connectionNetworkedObjects);


            if (netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
        }


        public GameObject FindRegisteredGameObject(ushort assetId)
        {
            GameObject go = null;
            registeredNetworkedGameObjectsDictionnary.TryGetValue(assetId, out go);
            return go;
        }

        public NetworkingIdentity FindSceneNetworkingIdentity(ushort sceneId)
        {
            for (int i = 0; i < NetworkingIdentity.s_networkingIdentities.Count; i++)
            {
                NetworkingIdentity netIdentity = NetworkingIdentity.s_networkingIdentities[i];

                if ((netIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || netIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB) && netIdentity.m_sceneId == sceneId)
                    return netIdentity;
            }

            return null;
        }

        public int GetIndexOfRegisteredGameObject(GameObject go)
        {
            for (int i = 0; i < registeredNetworkedGameObjectsArray.Length; i++)
            {
                if (registeredNetworkedGameObjectsArray[i] == go)
                    return i;
            }

            return -1;
        }


        /*   public NetworkingIdentity FindLocalNetworkIdentity(NetworkingConnection connection, ushort netId)
           {
               NetworkingIdentity networkingIdentity = null;

               if (connection.m_server == null)
                   networkingIdentity = FindLocalNetworkIdentity(connection, netId, m_connectionSpawnedNetworkedObjects);
               else
                   networkingIdentity = FindLocalNetworkIdentity(connection.m_server, netId, m_serverSpawnedNetworkedObjects);

               return networkingIdentity;
           }*/


        public NetworkingIdentity FindLocalNetworkIdentity(NetworkingConnection connection, ushort netId, Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            NetworkingIdentity networkingIdentity = null;
            Dictionary<ushort, NetworkingIdentity> networkIdentities = null;

            dictionary.TryGetValue(connection, out networkIdentities);

            if (networkIdentities != null)
            {
                networkIdentities.TryGetValue(netId, out networkingIdentity);
            }

            return networkingIdentity;
        }


        public NetworkingIdentity FindLocalNetworkIdentity(NetworkingServer server, ushort netId, Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            NetworkingIdentity networkingIdentity = null;
            Dictionary<ushort, NetworkingIdentity> networkIdentities = null;

            dictionary.TryGetValue(server, out networkIdentities);

            if (networkIdentities != null)
            {
                networkIdentities.TryGetValue(netId, out networkingIdentity);
            }

            return networkingIdentity;
        }


        void AddNetworkingIdentity(NetworkingIdentity networkingIdentity, NetworkingConnection conn, Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            Dictionary<ushort, NetworkingIdentity> networkingIdentityDict = null;
            dictionary.TryGetValue(conn, out networkingIdentityDict);

            if (networkingIdentityDict == null)
            {
                networkingIdentityDict = new Dictionary<ushort, NetworkingIdentity>();
                dictionary.Add(conn, networkingIdentityDict);
            }
            networkingIdentityDict.Add(networkingIdentity.m_netId, networkingIdentity);
        }


        void AddNetworkingIdentity(NetworkingIdentity networkingIdentity, NetworkingServer server, Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            Dictionary<ushort, NetworkingIdentity> networkingIdentityDict = null;
            dictionary.TryGetValue(server, out networkingIdentityDict);

            if (networkingIdentityDict == null)
            {
                networkingIdentityDict = new Dictionary<ushort, NetworkingIdentity>();
                dictionary.Add(server, networkingIdentityDict);
            }

            networkingIdentityDict.Add(networkingIdentity.m_netId, networkingIdentity);
        }


        void OnStartServer(NetworkingServer server)
        {
            if (server.m_isMainServer)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    //All object in the scenes are for this server
                    ServerAssignSceneObjectNetIds(server, SceneManager.GetSceneAt(i));
                }
            }
        }

        void OnSceneLoaded(Scene s, LoadSceneMode loadSceneMode)
        {
            if (NetworkingSystem.Instance)
            {
                if (NetworkingSystem.Instance.mainServer != null) //Server assign scene object ids
                {
                    ServerAssignSceneObjectNetIds(NetworkingSystem.Instance.mainServer, s);
                }

                //Connections ask scene object net ids
                GameObject[] gameObjects = s.GetRootGameObjects();
                List<NetworkingIdentity> netIdentities = new List<NetworkingIdentity>();
                foreach (GameObject go in gameObjects)
                {
                    netIdentities.AddRange(go.GetComponentsInChildren<NetworkingIdentity>());
                }

                foreach (NetworkingConnection conn in NetworkingSystem.Instance.connections)
                {
                    if (!conn.m_isHost) //not host
                    {
                        conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(s.name));
                        foreach (NetworkingIdentity netIdentity in netIdentities)
                        {
                            if (netIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                                conn.Send(NetworkingMessageType.ReplicatedPrefabScene, new ReplicatedPrefabSceneMessage(netIdentity.assetID, netIdentity.sceneId, s.name), NetworkingChannel.DefaultReliable);
                        }
                    }
                    else if (conn.m_server != null && conn.m_server == NetworkingSystem.Instance.mainServer)
                    {
                        foreach (NetworkingIdentity netIdentity in netIdentities)
                        {
                            if (netIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB || netIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                                netIdentity.m_serverConnection = conn;
                        }
                    }
                }
            }
        }

        void OnServerLoadScene(NetworkingMessage netMsg)
        {
            string sceneName = netMsg.As<StringMessage>().m_value;
            Scene scene = SceneManager.GetSceneByName(sceneName);

            if (default(Scene) == scene)
            {
                Debug.LogWarning("The server don't know the scene : " + scene); //Can be normal
            }
            else
            {
                GameObject[] gameObjects = scene.GetRootGameObjects();
                List<NetworkingIdentity> netIdentities = new List<NetworkingIdentity>();
                foreach (GameObject go in gameObjects)
                {
                    netIdentities.AddRange(go.GetComponentsInChildren<NetworkingIdentity>());
                }

                foreach (NetworkingIdentity i in netIdentities)
                {
                    if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                    {
                        if (netMsg.m_connection.m_server.m_isMainServer)
                        {
                            netMsg.m_connection.Send(NetworkingMessageType.SceneObjectNetId, new SceneObjectNetIdMessage(i.netId, i.assetID));
                            i.OnServerSyncNetId(netMsg.m_connection);
                        }
                    }
                    else if (i.m_type == NetworkingIdentity.TYPE.SPAWNED)
                    {
                        if (netMsg.m_connection.m_server == i.m_server && netMsg.m_connection != i.m_serverConnection) //Connection is on the same server than the server of the gameObject or connection already know this object
                        {
                            netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_assetId, i.m_netId, false, sceneName), NetworkingChannel.DefaultReliableSequenced);
                            i.OnServerSyncNetId(netMsg.m_connection);
                        }
                    }
                    else if (i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    {
                        if (netMsg.m_connection.m_server == i.m_server && netMsg.m_connection != i.m_serverConnection) //Connection is on the same server than the server of the gameObject or connection already know this object
                        {
                            netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_assetId, i.m_netId, false, sceneName), NetworkingChannel.DefaultReliableSequenced);
                            i.OnServerSyncNetId(netMsg.m_connection);
                        }
                    }
                }
            }
        }

        void OnServerReplicatePrefabScene(NetworkingMessage netMsg)
        {
            if (!netMsg.m_connection.m_server.m_isMainServer)    //The server is not in charge of this behaviour
            {
                return;
            }

            ReplicatedPrefabSceneMessage replicatedMessage = netMsg.As<ReplicatedPrefabSceneMessage>();

            if (!string.IsNullOrEmpty(replicatedMessage.m_sceneName))
            {
                Scene scene = SceneManager.GetSceneByName(replicatedMessage.m_sceneName);
                if (default(Scene) == scene)
                {
                    Debug.LogError("Scene is unknown by server : " + replicatedMessage.m_sceneName);
                    return;
                }
                else
                    SceneManager.SetActiveScene(scene);

            }

            NetworkingIdentity networkingIdentity;

            GameObject go = GameObject.Instantiate(FindRegisteredGameObject(replicatedMessage.m_assetId));
            networkingIdentity = go.GetComponent<NetworkingIdentity>();
            networkingIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
            ServerAssignNetId(netMsg.m_connection.m_server, networkingIdentity);
            networkingIdentity.hasAuthority = true;
            networkingIdentity.m_serverConnection = netMsg.m_connection;

            netMsg.m_connection.Send(NetworkingMessageType.SceneObjectNetId, new SceneObjectNetIdMessage(networkingIdentity.m_netId, replicatedMessage.m_sceneId), NetworkingChannel.DefaultReliableSequenced); //Just send the assigned netId for the connection which ask replication

            foreach (NetworkingConnection conn in netMsg.m_connection.m_server.connections)
            {
                if (conn != null && conn != netMsg.m_connection)
                {
                    conn.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(replicatedMessage.m_assetId, networkingIdentity.m_netId, false, replicatedMessage.m_sceneName), NetworkingChannel.DefaultReliableSequenced);
                }
            }
        }


        void OnConnectionSpawnMessage(NetworkingMessage netMsg)
        {
            SpawnMessage spawnMessage = netMsg.As<SpawnMessage>();
            NetworkingIdentity netIdentity = null;

            if (!string.IsNullOrEmpty(spawnMessage.m_sceneName))
            {
                Scene scene = SceneManager.GetSceneByName(spawnMessage.m_sceneName);
                if (default(Scene) == scene)
                {
                    Debug.LogWarning("Client do not know scene : " + spawnMessage.m_sceneName);
                    return;
                }
                else
                    SceneManager.SetActiveScene(scene);

            }

            if (netMsg.m_connection.m_server == null)           //Already spawn on server ;)
            {
                GameObject go = Instantiate(FindRegisteredGameObject(spawnMessage.m_gameObjectAssetId));
                netIdentity = go.GetComponent<NetworkingIdentity>();
                netIdentity.m_netId = spawnMessage.m_netId;
                netIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
            }
            else
            {
                netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, spawnMessage.m_netId, m_serverNetworkedObjects);
            }

            netIdentity.m_connection = netMsg.m_connection;
            netIdentity.hasAuthority = spawnMessage.m_hasAuthority;
            netIdentity.m_isClient = true;
            AddNetworkingIdentity(netIdentity, netMsg.m_connection, m_connectionNetworkedObjects);

            netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawnFinish, new NetIdMessage(spawnMessage.m_netId), NetworkingChannel.DefaultReliableSequenced); //Inform server so it can add listener.
        }


        void OnConnectionSceneObjectMessage(NetworkingMessage netMsg)
        {
            SceneObjectNetIdMessage mess = netMsg.As<SceneObjectNetIdMessage>();
            ushort sceneId = mess.m_sceneId;
            NetworkingIdentity networkingIdentity = FindSceneNetworkingIdentity(sceneId);
            networkingIdentity.m_netId = mess.m_netId;
            networkingIdentity.m_connection = netMsg.m_connection;
            networkingIdentity.m_isClient = true;
            networkingIdentity.hasAuthority = true;
            AddNetworkingIdentity(networkingIdentity, netMsg.m_connection, m_connectionNetworkedObjects);

            netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawnFinish, new NetIdMessage(mess.m_netId), NetworkingChannel.DefaultReliableSequenced); //Inform server so it can add listener.
        }


        void OnServerObjectSpawnFinish(NetworkingMessage netMsg)
        {
            NetworkingIdentity networkingIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, netMsg.As<NetIdMessage>().m_netId, m_serverNetworkedObjects);

            if (networkingIdentity)
            {
                networkingIdentity.m_serverConnectionListeners.Add(netMsg.m_connection);
                networkingIdentity.OnServerAddListener(netMsg.m_connection);
            }
            else
                Debug.LogWarning("Server don't know object : " + netMsg.As<NetIdMessage>().m_netId);

        }

        void ServerAssignSceneObjectNetIds(NetworkingServer server, Scene scene)
        {
            GameObject[] gameObjects = scene.GetRootGameObjects();
            List<NetworkingIdentity> netIdentities = new List<NetworkingIdentity>();
            foreach (GameObject go in gameObjects)
            {
                netIdentities.AddRange(go.GetComponentsInChildren<NetworkingIdentity>());
            }

            ushort currentNetId;
            m_serverCurrentNetId.TryGetValue(server, out currentNetId);

            //All object in the scenes are for this server
            for (int i = 0; i < netIdentities.Count; i++)
            {
                NetworkingIdentity netIdentity = netIdentities[i];
                if (netIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || netIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                {
                    ServerAssignNetId(server, netIdentity);
                    netIdentity.hasAuthority = true;
                }
            }

            if (currentNetId != 0)
            {
                if (!m_serverCurrentNetId.ContainsKey(server))
                    m_serverCurrentNetId.Add(server, currentNetId);
                else
                    m_serverCurrentNetId[server] = currentNetId;
            }
        }

        void ServerAssignNetId(NetworkingServer server, NetworkingIdentity neworkingtIdentity)
        {
            ushort currentNetId;
            m_serverCurrentNetId.TryGetValue(server, out currentNetId);

            if (currentNetId == 0)
            {
                m_serverCurrentNetId.Add(server, currentNetId);
            }

            currentNetId++;
            m_serverCurrentNetId[server] = currentNetId;

            neworkingtIdentity.m_netId = currentNetId;
            neworkingtIdentity.m_isServer = true;
            neworkingtIdentity.m_server = server;
            AddNetworkingIdentity(neworkingtIdentity, server, m_serverNetworkedObjects);
        }




        void OnServerConnect(NetworkingServer server, NetworkingMessage netMsg)
        {
            if (!server.m_isMainServer)
                return;

            Scene scene = default(Scene);

            if (!string.IsNullOrEmpty(spawnSceneOnConnect))
            {
                scene = SceneManager.GetSceneByName(spawnSceneOnConnect);
                if (default(Scene) == scene)
                {
                    Debug.LogError("Scene unknown on server : " + spawnSceneOnConnect);
                    return;
                }
            }

            for (int i = 0; i < spawnedGameObjectOnConnect.Length; i++)
            {
                SpawnOnServer(server, netMsg.m_connection, spawnedGameObjectOnConnect[i], scene);
            }

            Dictionary<ushort, NetworkingIdentity> networkingIdentities = m_serverNetworkedObjects[server];
            if (networkingIdentities != null)
            {
                foreach (NetworkingIdentity i in networkingIdentities.Values)
                {
                    i.OnServerConnect(netMsg.m_connection);
                }
            }
        }

        void OnConnectionConnect(NetworkingConnection conn)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                GameObject[] gameObjects = scene.GetRootGameObjects();
                List<NetworkingIdentity> netIdentities = new List<NetworkingIdentity>();
                foreach (GameObject go in gameObjects)
                {
                    netIdentities.AddRange(go.GetComponentsInChildren<NetworkingIdentity>());
                }

                if (conn.m_server == null) //Not host (already know all information from server)
                {
                    conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(scene.name));
                    foreach (NetworkingIdentity netIdentity in netIdentities)
                    {
                        if(netIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                            conn.Send(NetworkingMessageType.ReplicatedPrefabScene, new ReplicatedPrefabSceneMessage(netIdentity.assetID, netIdentity.sceneId, scene.name));
                    }
                }
                else if (conn.m_server != null && conn.m_server == NetworkingSystem.Instance.mainServer)
                {
                    foreach (NetworkingIdentity netIdentity in netIdentities)
                    {
                        if (netIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB || netIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                            netIdentity.m_serverConnection = conn;
                    }
                }
            }
        }

        void OnServerDisconnect(NetworkingServer server, NetworkingMessage netMsg)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_serverNetworkedObjects.TryGetValue(server, out netIdentities);
            if (netIdentities != null)
            {
                List<NetworkingIdentity> suppIdentities = new List<NetworkingIdentity>();

                foreach (NetworkingIdentity netIdentity in netIdentities.Values)
                {
                    netIdentity.m_serverConnectionListeners.Remove(netMsg.m_connection);

                    if (netIdentity.m_serverConnection == netMsg.m_connection && netIdentity.m_type != NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT && netIdentity.m_type != NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    {
                        suppIdentities.Add(netIdentity);
                    }
                }

                for (int i = suppIdentities.Count - 1; i >= 0; i--)
                {
                    NetworkingIdentity netIdentity = suppIdentities[i];
                    netIdentities.Remove(netIdentity.m_netId);
                    RemoveNetworkingIdentity(netIdentity, m_connectionNetworkedObjects);//Because we can be host
                    server.SendToAll(NetworkingMessageType.ObjectDestroy, new ObjectDestroyMessage(netIdentity.m_netId), NetworkingChannel.DefaultReliable);
                    Destroy(netIdentity.gameObject);
                }
            }
        }

        void OnStopServer(NetworkingServer server)
        {
            foreach (Dictionary<ushort, NetworkingIdentity> d in m_serverNetworkedObjects.Values)
                foreach (NetworkingIdentity i in d.Values)
                {
                    if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    {
                        i.Reset();
                    }
                    else
                        Destroy(i.gameObject);

                }

            m_serverNetworkedObjects.Remove(server);
            m_serverCurrentNetId.Remove(server);
        }

        private void OnConnectionDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.m_connection, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_connectionNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                RemoveNetworkingIdentity(netIdentity, m_connectionNetworkedObjects);

                Destroy(netIdentity.gameObject);
            }
        }

        private void OnServerDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_serverNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                RemoveNetworkingIdentity(netIdentity, m_serverNetworkedObjects);
                RemoveNetworkingIdentity(netIdentity, m_connectionNetworkedObjects); //because we can be host
                Destroy(netIdentity.gameObject);
            }
        }


        void OnConnectionDisconnect(NetworkingConnection conn, NetworkingMessage netMsg)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_connectionNetworkedObjects.TryGetValue(conn, out netIdentities);
            if (netIdentities != null)
            {
                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i)
                    {
                        if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                        {
                            i.Reset();
                        }
                        else
                            Destroy(i.gameObject);
                    }
                }
            }

            m_connectionNetworkedObjects.Remove(conn);
        }


        void OnStopConnection(NetworkingConnection conn)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_connectionNetworkedObjects.TryGetValue(conn, out netIdentities);
            if (netIdentities != null)
            {
                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i)
                    {
                        if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                        {
                            i.m_connection = null;      //unassign connection
                            i.m_isClient = false;
                            i.m_netId = 0;
                            i.hasAuthority = false;
                        }
                        else
                            Destroy(i.gameObject);
                    }
                }
            }

            m_connectionNetworkedObjects.Remove(conn);
        }

    }
}
