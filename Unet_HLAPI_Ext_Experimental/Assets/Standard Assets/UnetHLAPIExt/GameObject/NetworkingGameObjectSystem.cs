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
        private Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> m_serverSpawnedNetworkedObjects = new Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>>();
        private Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> m_connectionSpawnedNetworkedObjects = new Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>>();

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
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ConnectionLoadScene, OnConnectionLoadScene);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ReplicatedPrefabScene, OnServerReplicatePrefabScene);
        }


        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            NetworkingServer.OnServerConnect -= OnServerConnect;
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectSpawn, OnConnectionSpawnMessage);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.SceneObjectNetId, OnConnectionSceneObjectMessage);

            NetworkingConnection.OnConnectionConnect -= OnConnectionConnect;
            NetworkingServer.OnStartServer -= OnStartServer;
            NetworkingConnection.OnConnectionDisconnect -= OnConnectionDisconnect;
            NetworkingConnection.OnStopConnection -= OnStopConnection;
            NetworkingServer.OnServerDisconnect -= OnServerDisconnect;
            NetworkingServer.OnStopServer -= OnStopServer;

            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ConnectionLoadScene, OnConnectionLoadScene);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectDestroy, OnConnectionDestroy);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.Rpc, OnConnectionRpc);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.AutoRpc, OnConnectionRpc);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ReplicatedPrefabScene, OnServerReplicatePrefabScene);
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

        void OnServerConnect(NetworkingServer server, NetworkingMessage netMsg)
        {
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
        }

        /// <summary>
        /// Spawn an object accross network.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="go"></param>
        public void SpawnOnServer(NetworkingServer server, NetworkingConnection conn, GameObject gameObject, Scene scene)
        {
            if(scene != default(Scene))
                 SceneManager.SetActiveScene(scene);


            GameObject go = Instantiate(gameObject);
            NetworkingIdentity networkingIdentity = go.GetComponent<NetworkingIdentity>();
            ServerAssignNetId(server, networkingIdentity);
            networkingIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;

            server.SendTo(conn.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(networkingIdentity.m_assetId, networkingIdentity.netId, networkingIdentity.localPlayerAuthority, scene.name));

            foreach (NetworkingConnection c in server.connections)
            {
                if (c != null && c != conn)
                {
                    server.SendTo(c.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(networkingIdentity.m_assetId, networkingIdentity.netId, false, scene.name));
                }
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
            AddNetworkingIdentity(neworkingtIdentity, server, m_serverSpawnedNetworkedObjects);
        }

        void OnConnectionSceneObjectMessage(NetworkingMessage netMsg)
        {
            SceneObjectNetIdMessage mess = netMsg.As<SceneObjectNetIdMessage>();
            ushort sceneId = mess.m_sceneId;
            NetworkingIdentity networkingIdentity = FindSceneNetworkingIdentity(sceneId);
            networkingIdentity.m_netId = mess.m_netId;
            networkingIdentity.m_connection = netMsg.m_connection;
            networkingIdentity.m_isClient = true;
            networkingIdentity.m_hasAuthority = true;
            AddNetworkingIdentity(networkingIdentity, netMsg.m_connection, m_connectionSpawnedNetworkedObjects);
        }


        public void Replicate(NetworkingIdentity netIdentity)
        {
            Debug.LogError("Not implemented");
        }


        void OnServerCommand(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;

            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityID, m_serverSpawnedNetworkedObjects);


            if (netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
        }

        void OnConnectionRpc(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;

            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection, networkingIdentityID, m_connectionSpawnedNetworkedObjects);


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

                if ( (netIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || netIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB) && netIdentity.m_sceneId == sceneId)
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


        void OnConnectionDisconnect(NetworkingConnection conn, NetworkingMessage netMsg)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_connectionSpawnedNetworkedObjects.TryGetValue(conn, out netIdentities);
            if (netIdentities != null)
            {
                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i)
                    {
                        if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                        {
                            i.m_connection = null;      //unassigne connection
                            i.m_isClient = false;
                            i.m_netId = 0;
                            i.m_hasAuthority = false;
                        }
                        else
                            Destroy(i.gameObject);
                    }
                }
            }

            m_connectionSpawnedNetworkedObjects.Remove(conn);
        }


        void OnStopConnection(NetworkingConnection conn)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_connectionSpawnedNetworkedObjects.TryGetValue(conn, out netIdentities);
            if (netIdentities != null)
            {
                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i)
                    {
                        if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                        {
                            i.m_connection = null;      //unassigne connection
                            i.m_isClient = false;
                            i.m_netId = 0;
                            i.m_hasAuthority = false;
                        }
                        else
                            Destroy(i.gameObject);
                    }
                }
            }

            m_connectionSpawnedNetworkedObjects.Remove(conn);
        }

        void OnServerDisconnect(NetworkingServer server, NetworkingMessage netMsg)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_serverSpawnedNetworkedObjects.TryGetValue(server, out netIdentities);
            if (netIdentities != null)
            {
                List<NetworkingIdentity> suppIdentities = new List<NetworkingIdentity>();

                foreach (NetworkingIdentity netIdentity in netIdentities.Values)
                {
                    if (netIdentity.m_connection == netMsg.m_connection && netIdentity.m_type != NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT && netIdentity.m_type != NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    {
                        suppIdentities.Add(netIdentity);
                    }
                }

                for (int i = suppIdentities.Count - 1; i >= 0; i--)
                {
                    NetworkingIdentity netIdentity = suppIdentities[i];
                    netIdentities.Remove(netIdentity.m_netId);
                    RemoveNetworkingIdentity(netIdentity, m_connectionSpawnedNetworkedObjects);//Because we can be host
                    server.SendToAll(NetworkingMessageType.ObjectDestroy, new ObjectDestroyMessage(netIdentity.m_netId), NetworkingMessageType.Channels.DefaultReliable);
                    Destroy(netIdentity.gameObject);
                }
            }
        }


        void OnStartServer(NetworkingServer server)
        {
            if (server.m_isMainServer)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    //All object in the scenes are for this server
                    AssignSceneObjectNetIds(server, SceneManager.GetSceneAt(i));
                }
            }
        }

        void OnSceneLoaded(Scene s, LoadSceneMode loadSceneMode)
        {
            if (NetworkingSystem.Instance)
            {
                if (NetworkingSystem.Instance.mainServer != null) //Server assign scene object ids
                {
                    AssignSceneObjectNetIds(NetworkingSystem.Instance.mainServer, s);

                    //No extra sending, connection has to ask informations instead. (Server has to be the first in the scene)
                    /* Dictionary<ushort, NetworkingIdentity> dictionary;
                     m_serverSpawnedNetworkedObjects.TryGetValue(NetworkingSystem.Instance.mainServer, out dictionary);

                     if (dictionary != null)
                     {
                         foreach (NetworkingIdentity i in dictionary.Values)
                         {
                             if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                                 NetworkingSystem.Instance.mainServer.SendToAll(NetworkingMessageType.SceneObjectConnection, new SceneObjectNetIdMessage(i.netId, i.assetID));
                         }
                     }*/
                }

                    //Connections ask scene object net ids
                    GameObject[] gameObjects = s.GetRootGameObjects();
                    List<NetworkingIdentity> netIdentities = new List<NetworkingIdentity>();
                    foreach (GameObject go in gameObjects)
                    {
                        netIdentities.AddRange(go.GetComponentsInChildren<NetworkingIdentity>());
                    }

                    for (int i = netIdentities.Count -1; i >= 0; i--)
                    {
                        if (netIdentities[i].m_type != NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                            netIdentities.RemoveAt(i);
                    }

                    foreach (NetworkingConnection conn in NetworkingSystem.Instance.connections)
                    {
                        if (conn.m_server == null) //not host
                        {
                            conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(s.name));
                            foreach (NetworkingIdentity i in netIdentities)
                            {
                                conn.Send(NetworkingMessageType.ReplicatedPrefabScene, new ReplicatedPrefabSceneMessage(i.assetID, i.sceneId, s.name));
                            }
                        }
                    }
            }
        }

        void OnConnectionLoadScene(NetworkingMessage netMsg)
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
                foreach(GameObject go in gameObjects)
                {
                    netIdentities.AddRange(go.GetComponentsInChildren<NetworkingIdentity>());
                }

                foreach (NetworkingIdentity i in netIdentities)
                {
                    if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                    {
                        if(netMsg.m_connection.m_server.m_isMainServer)
                             netMsg.m_connection.Send(NetworkingMessageType.SceneObjectNetId, new SceneObjectNetIdMessage(i.netId, i.assetID));
                    }
                    else if (i.m_type == NetworkingIdentity.TYPE.SPAWNED)
                    {
                        if (netMsg.m_connection.m_server == i.m_server && netMsg.m_connection != i.m_connection) //Connection is on the same server than the server of the gameObject or connection already know this object
                            netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_assetId, i.m_netId, false, sceneName));
                    }
                    else if (i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB && i.m_isClient)
                    {
                        if (netMsg.m_connection.m_server == i.m_server && netMsg.m_connection != i.m_connection) //Connection is on the same server than the server of the gameObject or connection already know this object
                            netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_assetId, i.m_netId, false, sceneName));
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
                if(default(Scene) == scene)
                {
                    Debug.LogError("Scene is unknown by server : " + replicatedMessage.m_sceneName);
                    return;
                }
                else
                    SceneManager.SetActiveScene(scene);

            }

            NetworkingIdentity networkingIdentity;

            networkingIdentity = FindSceneNetworkingIdentity(replicatedMessage.m_sceneId);

            if (networkingIdentity == null || networkingIdentity.m_isServer)
            {
                GameObject go = GameObject.Instantiate(FindRegisteredGameObject(replicatedMessage.m_assetId));
                networkingIdentity = go.GetComponent<NetworkingIdentity>();
                networkingIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
            }

            ServerAssignNetId(netMsg.m_connection.m_server, networkingIdentity);

            netMsg.m_connection.Send(NetworkingMessageType.SceneObjectNetId, new SceneObjectNetIdMessage(networkingIdentity.m_netId, replicatedMessage.m_sceneId)); //Just send the assigned netId for the connection which ask replication

            foreach(NetworkingConnection conn in netMsg.m_connection.m_server.connections)
            {
                if(conn != null && conn != netMsg.m_connection)
                {
                    conn.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(replicatedMessage.m_assetId, networkingIdentity.m_netId, false, replicatedMessage.m_sceneName));
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
                netIdentity.m_netId = spawnMessage.m_gameObjectNetId;
                netIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
            }
            else
            {
                netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, spawnMessage.m_gameObjectNetId, m_serverSpawnedNetworkedObjects);
            }

            netIdentity.m_connection = netMsg.m_connection;
            netIdentity.m_hasAuthority = spawnMessage.m_hasAuthority;
            netIdentity.m_isClient = true;
            AddNetworkingIdentity(netIdentity, netMsg.m_connection, m_connectionSpawnedNetworkedObjects);
        }

        void AssignSceneObjectNetIds(NetworkingServer server, Scene scene)
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
                if (netIdentity.netId == 0 && netIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                {
                    if (netIdentity.m_server == null)      //no server assigned
                    {
                        currentNetId++;
                        netIdentity.m_netId = currentNetId;
                        netIdentity.m_server = server;
                        netIdentity.m_isServer = true;
                        netIdentity.m_hasAuthority = true;
                    }

                    AddNetworkingIdentity(netIdentity, server, m_serverSpawnedNetworkedObjects);
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

                for (int j = netIdentities.Count - 1; j >= 0; j--)
                {
                    if (netIdentities[j].m_type != NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                        netIdentities.RemoveAt(j);
                }

                conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(scene.name));
                foreach (NetworkingIdentity netIdentity in netIdentities)
                {
                    conn.Send(NetworkingMessageType.ReplicatedPrefabScene, new ReplicatedPrefabSceneMessage(netIdentity.assetID, netIdentity.sceneId, scene.name));
                }
            }
        }

        void OnStopServer(NetworkingServer server)
        {
            foreach (Dictionary<ushort, NetworkingIdentity> d in m_serverSpawnedNetworkedObjects.Values)
                foreach (NetworkingIdentity i in d.Values)
                {
                    if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    {
                        i.m_server = null;      //unassigne server
                        i.m_isServer = false;
                    }
                    else
                        Destroy(i.gameObject);

                }

            m_serverSpawnedNetworkedObjects.Remove(server);
            m_serverCurrentNetId.Remove(server);
        }

        private void OnConnectionDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.m_connection, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_connectionSpawnedNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                RemoveNetworkingIdentity(netIdentity, m_connectionSpawnedNetworkedObjects);

                Destroy(netIdentity.gameObject);
            }
        }

        private void OnServerDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_serverSpawnedNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                RemoveNetworkingIdentity(netIdentity, m_serverSpawnedNetworkedObjects);
                RemoveNetworkingIdentity(netIdentity, m_connectionSpawnedNetworkedObjects); //because we can be host
                Destroy(netIdentity.gameObject);
            }
        }
    }
}
