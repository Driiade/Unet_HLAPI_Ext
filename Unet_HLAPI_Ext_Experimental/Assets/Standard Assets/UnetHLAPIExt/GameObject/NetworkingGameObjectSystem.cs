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
    [RequireComponent(typeof(NetworkingSceneSystem))]
    public class NetworkingGameObjectSystem : Singleton<NetworkingGameObjectSystem>
    {
#if CLIENT || SERVER
        /// <summary>
        /// Networked GameObject which need to be registered
        /// </summary>
        [Space(2), Tooltip("Object spawn when a new connection connect, these objects are own by the new connected connection")]
        public GameObject[] spawnedGameObjectOnConnect;
        public string spawnSceneOnConnect = "";



        /// <summary>
        /// Registered gameObject with dictionary to find them quickly
        /// </summary>
        private Dictionary<ushort, GameObject> registeredNetworkedGameObjectsDictionnary = new Dictionary<ushort, GameObject>();


#if SERVER
        /// <summary>
        /// NetworkIdentities by server, only available on server
        /// Used because netIdentity are specific by server (between server, same netIdentity can be found)
        /// </summary>
        private Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> m_serverNetworkedObjects = new Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>>();
#endif

#if CLIENT
        /// <summary>
        /// NetworkIdentities by connection, only available on server
        /// Used because netIdentities are specific by connection (between connection, same netIdentity can be found)
        /// </summary>
        private Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> m_connectionNetworkedObjects = new Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>>();
#endif

#if SERVER
        /// <summary>
        /// Current netId index for each connection
        /// </summary>
        private Dictionary<NetworkingServer, ushort> m_serverCurrentNetId = new Dictionary<NetworkingServer, ushort>();

        NetworkingWriter writer;
#endif
        [SerializeField, HideInInspector]
        NetworkingSceneSystem networkingSceneSystem;

        protected override void Awake()
        {
            base.Awake();

            NetworkingIdentity[] registeredNetworkedGameObjectsArray = Resources.LoadAll<NetworkingIdentity>("");
            registeredNetworkedGameObjectsDictionnary.Clear();
            for (int i = 0; i < registeredNetworkedGameObjectsArray.Length; i++)
            {
                registeredNetworkedGameObjectsDictionnary.Add(registeredNetworkedGameObjectsArray[i].m_assetId, registeredNetworkedGameObjectsArray[i].gameObject);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

#if CLIENT
            //Connection
            NetworkingConnection.OnConnectionConnect += OnConnectionConnect;
            NetworkingConnection.OnConnectionDisconnect += OnConnectionDisconnect;
            NetworkingConnection.OnStopConnection += OnStopConnection;
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.ObjectSpawn, OnConnectionSpawnMessage);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.SceneObjectNetId, OnConnectionSceneObjectMessage);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.ObjectDestroy, OnConnectionDestroy);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.Rpc, OnConnectionRpc);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.AutoRpc, OnConnectionRpc);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.UpdateVars, OnConnectionUpdateVars);
#endif

#if SERVER
            networkingSceneSystem.OnServerLoadScene += OnServerLoadScene;

            //Server
            NetworkingServer.OnServerDisconnect += OnServerDisconnect;
            NetworkingServer.OnStopServer += OnStopServer;
            NetworkingServer.OnStartServer += OnStartServer;
            NetworkingServer.OnServerConnect += OnServerConnect;
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.AutoRpc, OnServerAutoRpc);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.SendToOwner, OnServerSendToOwner);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.UpdateVars, OnServerUpdateVars);

            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ReplicatedPrefabScene, OnServerReplicatePrefabScene);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ObjectSpawnFinish, OnServerObjectSpawnFinish);
#endif
        }


        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

#if CLIENT
            NetworkingConnection.OnConnectionConnect -= OnConnectionConnect;
            NetworkingConnection.OnConnectionDisconnect -= OnConnectionDisconnect;
            NetworkingConnection.OnStopConnection -= OnStopConnection;
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectSpawn, OnConnectionSpawnMessage);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.SceneObjectNetId, OnConnectionSceneObjectMessage);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.Rpc, OnConnectionRpc);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.AutoRpc, OnConnectionRpc);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectDestroy, OnConnectionDestroy);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.UpdateVars, OnConnectionUpdateVars);
#endif

#if SERVER
            networkingSceneSystem.OnServerLoadScene -= OnServerLoadScene;

            NetworkingServer.OnStartServer -= OnStartServer;
            NetworkingServer.OnServerDisconnect -= OnServerDisconnect;
            NetworkingServer.OnStopServer -= OnStopServer;
            NetworkingServer.OnServerConnect -= OnServerConnect;

            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.AutoRpc, OnServerAutoRpc);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.SendToOwner, OnServerSendToOwner);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ReplicatedPrefabScene, OnServerReplicatePrefabScene);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ObjectSpawnFinish, OnServerObjectSpawnFinish);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.UpdateVars, OnServerUpdateVars);
#endif
        }

#if SERVER
        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity, Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            if (networkingIdentity.m_server != null && dictionary.ContainsKey(networkingIdentity.m_server))
            {
                dictionary[networkingIdentity.m_server].Remove(networkingIdentity.m_netId);
            }
        }
#endif

#if CLIENT
        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity, Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            if (networkingIdentity.m_connection != null && dictionary.ContainsKey(networkingIdentity.m_connection))
            {
                dictionary[networkingIdentity.m_connection].Remove(networkingIdentity.m_netId);
            }
        }
#endif

#if SERVER
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
            networkingIdentity.hasAuthority = !networkingIdentity.localPlayerAuthority;

            //SyncVar
            byte[] syncVars = networkingIdentity.SerializeAllNetworkingBehaviourSyncVar();

            server.SendTo(conn.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(networkingIdentity.m_ownerAssetId, networkingIdentity.netId, networkingIdentity.localPlayerAuthority, true, scene.name, syncVars), NetworkingChannel.DefaultReliableSequenced);

            foreach (NetworkingConnection c in server.connections)
            {
                if (c != null && c != conn)
                {
                    server.SendTo(c.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(networkingIdentity.m_otherClientAssetId, networkingIdentity.netId, false, false, scene.name, syncVars), NetworkingChannel.DefaultReliableSequenced);
                }
            }
        }
#endif

#if SERVER
        void OnServerAutoRpc(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;

            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityID, m_serverNetworkedObjects);

            if (netIdentity)
            {
                netIdentity.ServerHandleAutoRpc(netMsg.reader, netMsg.channelId);
            }
            else
                Debug.LogError("GameObject not find on server : " + networkingIdentityID);
        }

        void OnServerCommand(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityID, m_serverNetworkedObjects);


            if (netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
            else
                Debug.LogWarning("GameObject not find on server : " + networkingIdentityID);
        }

        void OnServerSendToOwner(NetworkingMessage netMsg)
        {
            NetworkingIdentity networkingIdentityObjectOwner = null;
            ushort networkingIdentityObjectOwnerID = netMsg.reader.ReadUInt16();
            networkingIdentityObjectOwner = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityObjectOwnerID, m_serverNetworkedObjects);

            NetworkingIdentity networkingIdentityObjectCall = null;
            ushort networkingIdentityObjectCallID = netMsg.reader.ReadUInt16();
            networkingIdentityObjectCall = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityObjectCallID, m_serverNetworkedObjects);


            if (networkingIdentityObjectCall && networkingIdentityObjectOwner)
                networkingIdentityObjectCall.ServerHandleSendToOwner(networkingIdentityObjectOwner.m_serverConnection, netMsg.reader, netMsg.channelId);
            else
                Debug.LogError("GameObject not find on server : " + networkingIdentityObjectCallID + " or GameObject not find on server: " + networkingIdentityObjectOwnerID);
        }

        void OnServerUpdateVars(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityID, m_serverNetworkedObjects);


            if (netIdentity)
            {
                netIdentity.HandleUpdateVars(netMsg.reader);
            }
            else
                Debug.LogWarning("GameObject not find on server : " + networkingIdentityID + " for updateVars "); //Can be normal due to non reliability      
        }

#endif
#if CLIENT
        void OnConnectionRpc(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection, networkingIdentityID, m_connectionNetworkedObjects);


            if (netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
            else
                Debug.LogWarning("Connection doesn't know : " + networkingIdentityID + " for RPC"); //Can be normal due to non reliability
        }

        void OnConnectionUpdateVars(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = null;
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            netIdentity = FindLocalNetworkIdentity(netMsg.m_connection, networkingIdentityID, m_connectionNetworkedObjects);

            if (netIdentity)
            {
                netIdentity.HandleUpdateVars(netMsg.reader);
            }
            else
                Debug.LogWarning("Connection doesn't know : " + networkingIdentityID + " for updateVars"); //Can be normal due to non reliability
        }
#endif

#if SERVER
        void OnStartServer(NetworkingServer server)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                //All object in the scenes are for this server
                ServerAssignSceneObjectNetIds(server, SceneManager.GetSceneAt(i));
            }
        }
#endif


        /// <summary>
        /// Server has to be the first to load a new scene, however strange behaviour will bagin
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="loadSceneMode"></param>
        void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            StartCoroutine(HandleSceneLoaded(scene, loadSceneMode));
        }

        IEnumerator HandleSceneLoaded(Scene scene, LoadSceneMode moe)
        {
            yield return null;

            if (NetworkingSystem.Instance)
            {
#if SERVER
                if (NetworkingSystem.Instance.mainServer != null) //Server assign scene object ids
                {
                    ServerAssignSceneObjectNetIds(NetworkingSystem.Instance.mainServer, scene);
                }

                foreach (NetworkingServer server in NetworkingSystem.Instance.additionnalServers)
                {
                    ServerAssignSceneObjectNetIds(server, scene);
                }
#endif

                //Connections ask scene object net ids
                GameObject[] gameObjects = scene.GetRootGameObjects();
                List<NetworkingIdentity> netIdentities = new List<NetworkingIdentity>();
                foreach (GameObject go in gameObjects)
                {
                    netIdentities.AddRange(go.GetComponentsInChildren<NetworkingIdentity>());
                }


#if SERVER && CLIENT
                foreach (NetworkingConnection conn in NetworkingSystem.Instance.connections)
                {
                    if (conn.m_server == null) //not host
                    {
                        foreach (NetworkingIdentity networkingIdentity in netIdentities)
                        {
                            if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                            {
                                //SyncVar
                                byte[] syncVars = networkingIdentity.SerializeAllNetworkingBehaviourSyncVar();

                                conn.Send(NetworkingMessageType.ReplicatedPrefabScene, new ReplicatedPrefabSceneMessage(networkingIdentity.m_serverAssetId, networkingIdentity.sceneId, scene.name, networkingIdentity.m_serverId, syncVars), NetworkingChannel.DefaultReliableSequenced);
                            }
                        }
                    }
                    else
                    {

                        NetworkingConnection serverConnection = null;
                        foreach (NetworkingConnection c in conn.m_server.connections)
                        {
                            if (c != null && c.m_connectionId == conn.m_connectionId)
                            {
                                serverConnection = c;
                                break;
                            }
                        }

                        foreach (NetworkingIdentity networkingIdentity in netIdentities)
                        {
                            if ((networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB || networkingIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT) && networkingIdentity.m_serverId == conn.m_server.m_serverId)
                            {
                                if (serverConnection != null)
                                {
                                    if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                                        networkingIdentity.m_serverConnection = serverConnection;

                                    networkingIdentity.m_serverAwareConnections.Add(conn);

                                    foreach (NetworkingBehaviour b in networkingIdentity.NetworkingBehaviours)
                                    {
                                        if (b.automaticAddListener)
                                            b.AddServerConnectionListener(serverConnection);
                                    }
                                }

                                networkingIdentity.m_connection = conn;

                                if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                                    networkingIdentity.isLocalClient = true;

                                networkingIdentity.m_isClient = true;
                                networkingIdentity.hasAuthority = true;
                            }
                        }
                    }
                }
#elif CLIENT
                foreach (NetworkingConnection conn in NetworkingSystem.Instance.connections)
                {
                    foreach (NetworkingIdentity networkingIdentity in netIdentities)
                    {
                        if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                        {
                            //SyncVar
                            byte[] syncVars = networkingIdentity.SerializeAllNetworkingBehaviourSyncVar();

                            conn.Send(NetworkingMessageType.ReplicatedPrefabScene, new ReplicatedPrefabSceneMessage(networkingIdentity.assetID, networkingIdentity.sceneId, scene.name, networkingIdentity.m_serverId, syncVars), NetworkingChannel.DefaultReliableSequenced);
                        }
                    }
                }
#endif
            }
        }

#if SERVER
        void OnServerLoadScene(NetworkingConnection conn, Scene scene)
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
                    if (conn.m_server.m_serverId == i.m_serverId)
                    {
                        //SyncVar
                        byte[] syncVars = i.SerializeAllNetworkingBehaviourSyncVar();

                        conn.Send(NetworkingMessageType.SceneObjectNetId, new SceneObjectNetIdMessage(i.netId, i.sceneId, syncVars), NetworkingChannel.DefaultReliableSequenced);
                        i.OnServerSyncNetId(conn);
                    }
                }
                else if (i.m_type == NetworkingIdentity.TYPE.SPAWNED)
                {
                    if (conn.m_server == i.m_server && conn != i.m_serverConnection) //Connection is on the same server than the server of the gameObject or connection already know this object
                    {
                        //SyncVar
                        byte[] syncVars = i.SerializeAllNetworkingBehaviourSyncVar();

                        conn.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_otherClientAssetId, i.m_netId, false, false, scene.name, syncVars), NetworkingChannel.DefaultReliableSequenced);
                        i.OnServerSyncNetId(conn);
                    }
                }
                else if (i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                {
                    if (conn.m_server == i.m_server) //Connection is on the same server than the server of the gameObject
                    {
                        //SyncVar
                        byte[] syncVars = i.SerializeAllNetworkingBehaviourSyncVar();

                        conn.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_otherClientAssetId, i.m_netId, false, false, scene.name, syncVars), NetworkingChannel.DefaultReliableSequenced);
                        i.OnServerSyncNetId(conn);
                    }
                }
            }
        }
#endif

#if SERVER
        void OnServerReplicatePrefabScene(NetworkingMessage netMsg)
        {
            ReplicatedPrefabSceneMessage replicatedMessage = netMsg.As<ReplicatedPrefabSceneMessage>();

            if (netMsg.m_connection.m_server.m_serverId != replicatedMessage.m_serverId)
                return;

            if (!string.IsNullOrEmpty(replicatedMessage.m_sceneName))
            {
                Scene scene = SceneManager.GetSceneByName(replicatedMessage.m_sceneName);
                if (string.IsNullOrEmpty(scene.name))
                {
                    Debug.LogError("Scene is unknown by server : " + replicatedMessage.m_sceneName);
                    return;
                }
                else
                    SceneManager.SetActiveScene(scene);

            }

            NetworkingIdentity networkingIdentity;

            GameObject go = GameObject.Instantiate(FindRegisteredGameObject(replicatedMessage.m_serverAssetId));
            networkingIdentity = go.GetComponent<NetworkingIdentity>();
            networkingIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
            ServerAssignNetId(netMsg.m_connection.m_server, networkingIdentity);

            networkingIdentity.m_serverConnection = netMsg.m_connection;
            networkingIdentity.m_serverId = ""; //No information about this (to be reliable with connection behaviour

            networkingIdentity.GetAllSyncVars(replicatedMessage.m_networkingBehaviourSyncVars);

            netMsg.m_connection.Send(NetworkingMessageType.SceneObjectNetId, new SceneObjectNetIdMessage(networkingIdentity.m_netId, replicatedMessage.m_sceneId, replicatedMessage.m_networkingBehaviourSyncVars), NetworkingChannel.DefaultReliableSequenced); //Just send the assigned netId for the connection which ask replication
            networkingIdentity.OnServerSyncNetId(netMsg.m_connection);

            foreach (NetworkingConnection conn in netMsg.m_connection.m_server.connections)
            {
                if (conn != null && conn != netMsg.m_connection)
                {
                    conn.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(networkingIdentity.m_otherClientAssetId, networkingIdentity.m_netId, false, false, replicatedMessage.m_sceneName, replicatedMessage.m_networkingBehaviourSyncVars), NetworkingChannel.DefaultReliableSequenced);
                    networkingIdentity.OnServerSyncNetId(conn);
                }
            }
        }
#endif

#if CLIENT
        void OnConnectionSpawnMessage(NetworkingMessage netMsg)
        {
            SpawnMessage spawnMessage = netMsg.As<SpawnMessage>();
            NetworkingIdentity netIdentity = null;

            if (!string.IsNullOrEmpty(spawnMessage.m_sceneName))
            {
                bool findScene = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene.name == spawnMessage.m_sceneName)
                    {
                        SceneManager.SetActiveScene(scene);
                        findScene = true;
                        break;
                    }
                }

                if (!findScene)
                {
                    Debug.LogWarning("Client do not know scene : " + spawnMessage.m_sceneName);
                    return;
                }
            }

            //Security check (remove this if you are sure this message is not threw)
            if (m_connectionNetworkedObjects.ContainsKey(netMsg.m_connection) && m_connectionNetworkedObjects[netMsg.m_connection].ContainsKey(spawnMessage.m_netId))
            {
                Debug.LogWarning("The connection: " + netMsg.m_connection + " already know the gameObject : " + spawnMessage.m_netId);
                return;
            }

#if SERVER
            if (netMsg.m_connection.m_server == null)           //Already spawn on server ;)
            {
                GameObject go = Instantiate(FindRegisteredGameObject(spawnMessage.m_gameObjectAssetId));
                netIdentity = go.GetComponent<NetworkingIdentity>();
                netIdentity.m_netId = spawnMessage.m_netId;
                netIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
                netIdentity.m_serverId = ""; //No information about this

                netIdentity.GetAllSyncVars(spawnMessage.m_networkingBehaviourSyncVars);
            }
            else
            {
                netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, spawnMessage.m_netId, m_serverNetworkedObjects);
            }
#else
                GameObject go = Instantiate(FindRegisteredGameObject(spawnMessage.m_gameObjectAssetId));
                netIdentity = go.GetComponent<NetworkingIdentity>();
                netIdentity.m_netId = spawnMessage.m_netId;
                netIdentity.m_type = NetworkingIdentity.TYPE.SPAWNED;
                netIdentity.m_serverId = ""; //No information about this

                netIdentity.GetAllSyncVars(spawnMessage.m_networkingBehaviourSyncVars);
#endif

            if (netIdentity != null)
            {
                netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawnFinish, new NetIdMessage(spawnMessage.m_netId), NetworkingChannel.DefaultReliableSequenced); //Inform server so it can add listener.

                netIdentity.m_connection = netMsg.m_connection;
                netIdentity.isLocalClient = spawnMessage.m_isLocalConnection;
                netIdentity.m_isClient = true;
                netIdentity.hasAuthority = spawnMessage.m_hasAuthority;
                AddNetworkingIdentity(netIdentity, netMsg.m_connection, m_connectionNetworkedObjects);
            }
            else
                Debug.LogWarning("Object not spawn on connection : " + spawnMessage.m_netId);
        }
#endif

#if CLIENT
        void OnConnectionSceneObjectMessage(NetworkingMessage netMsg)
        {
            SceneObjectNetIdMessage mess = netMsg.As<SceneObjectNetIdMessage>();
            ushort sceneId = mess.m_sceneId;
            NetworkingIdentity networkingIdentity = FindSceneNetworkingIdentity(sceneId);

            if (networkingIdentity != null)
            {
                netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawnFinish, new NetIdMessage(mess.m_netId), NetworkingChannel.DefaultReliableSequenced); //Inform server so it can add listener.
                networkingIdentity.m_netId = mess.m_netId;
                networkingIdentity.m_connection = netMsg.m_connection;
                networkingIdentity.m_isClient = true;

                if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    networkingIdentity.isLocalClient = true;
                else
                    networkingIdentity.GetAllSyncVars(mess.m_networkingBehaviourSyncVars);

                networkingIdentity.hasAuthority = true;
                AddNetworkingIdentity(networkingIdentity, netMsg.m_connection, m_connectionNetworkedObjects);
            }
            else
                Debug.LogWarning("Object not find on connection : " + mess.m_sceneId);
        }
#endif

#if SERVER
        void OnServerObjectSpawnFinish(NetworkingMessage netMsg)
        {
            NetworkingIdentity networkingIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, netMsg.As<NetIdMessage>().m_netId, m_serverNetworkedObjects);

            if (networkingIdentity)
            {
                networkingIdentity.m_serverAwareConnections.Add(netMsg.m_connection);
                foreach (NetworkingBehaviour networkingBehaviour in networkingIdentity.NetworkingBehaviours)
                {
                    if (networkingBehaviour.automaticAddListener)
                        networkingBehaviour.AddServerConnectionListener(netMsg.m_connection);
                }

                networkingIdentity.OnServerAware(netMsg.m_connection);
            }
            else
                Debug.LogWarning("Server don't know object : " + netMsg.As<NetIdMessage>().m_netId);

        }
#endif

#if SERVER
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
                NetworkingIdentity networkingIdentity = netIdentities[i];
                if ((networkingIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    && networkingIdentity.m_serverId == server.m_serverId
                    && networkingIdentity.netId == 0)
                {
                    ServerAssignNetId(server, networkingIdentity);
                    networkingIdentity.hasAuthority = true;
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
#endif

#if SERVER
        void ServerAssignNetId(NetworkingServer server, NetworkingIdentity networkingIdentity)
        {
            ushort currentNetId;
            m_serverCurrentNetId.TryGetValue(server, out currentNetId);

            if (currentNetId == 0)
            {
                m_serverCurrentNetId.Add(server, currentNetId);
            }

            currentNetId++;
            m_serverCurrentNetId[server] = currentNetId;

            networkingIdentity.m_netId = currentNetId;
            networkingIdentity.m_isServer = true;
            networkingIdentity.m_server = server;

            AddNetworkingIdentity(networkingIdentity, server, m_serverNetworkedObjects);
        }
#endif

#if SERVER
        void OnServerConnect(NetworkingServer server, NetworkingConnection conn)
        {
#if CLIENT
            Dictionary<ushort, NetworkingIdentity> networkingIdentities;
            m_serverNetworkedObjects.TryGetValue(server, out networkingIdentities);

            bool hostConnection = false;
            foreach (NetworkingConnection connection in NetworkingSystem.Instance.connections)
            {
                if (connection != null && connection.m_connectionId == conn.m_connectionId)
                {
                    hostConnection = true;
                }
            }

            foreach (NetworkingIdentity networkingIdentity in networkingIdentities.Values)
            {
                if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB || networkingIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                {
                    if (networkingIdentity.m_server == conn.m_server)
                    {
                        if (hostConnection)
                        {
                            if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                                networkingIdentity.m_serverConnection = conn;

                            networkingIdentity.m_serverAwareConnections.Add(conn);

                            foreach (NetworkingBehaviour b in networkingIdentity.NetworkingBehaviours)
                            {
                                if (b.automaticAddListener)
                                    b.AddServerConnectionListener(conn);
                            }
                        }
                    }
                }

                networkingIdentity.OnServerConnect(conn);
            }
#endif

            if (!server.m_isMainServer) //For the moment. TODO : spawn object by server
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
                SpawnOnServer(server, conn, spawnedGameObjectOnConnect[i], scene);
            }
        }
#endif

#if CLIENT
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

#if SERVER
                if (conn.m_server == null) //Not host (already know all informations from server)
                {
                    conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(scene.name));
                    foreach (NetworkingIdentity networkingIdentity in netIdentities)
                    {
                        if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB && networkingIdentity.m_connection == null)
                        {
                            //SyncVar
                            byte[] syncVars = networkingIdentity.SerializeAllNetworkingBehaviourSyncVar();

                            conn.Send(NetworkingMessageType.ReplicatedPrefabScene, new ReplicatedPrefabSceneMessage(networkingIdentity.m_serverAssetId, networkingIdentity.sceneId, scene.name, networkingIdentity.m_serverId, syncVars));
                        }
                    }
                }
                else
                {
                    Dictionary<ushort, NetworkingIdentity> networkingIdentities;
                    m_serverNetworkedObjects.TryGetValue(conn.m_server, out networkingIdentities);


                    foreach (NetworkingIdentity networkingIdentity in netIdentities)
                    {
                        if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB || networkingIdentity.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT)
                        {
                            if (networkingIdentity.m_server == conn.m_server)
                            {
                                networkingIdentity.m_connection = conn;
                                networkingIdentity.m_isClient = true;

                                if (networkingIdentity.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                                    networkingIdentity.isLocalClient = true;

                                AddNetworkingIdentity(networkingIdentity, conn, m_connectionNetworkedObjects);
                            }
                        }
                    }
                }
#endif
            }
        }
#endif

#if SERVER
        void OnServerDisconnect(NetworkingServer server, NetworkingConnection conn)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_serverNetworkedObjects.TryGetValue(server, out netIdentities);
            if (netIdentities != null)
            {
                List<NetworkingIdentity> suppIdentities = new List<NetworkingIdentity>();

                foreach (NetworkingIdentity networkingIdentity in netIdentities.Values)
                {
                    networkingIdentity.m_serverAwareConnections.Remove(conn);
                    foreach (NetworkingBehaviour networkingBehaviour in networkingIdentity.NetworkingBehaviours)
                    {
                        networkingBehaviour.RemoveServerConnectionListener(conn);
                    }

                    if (networkingIdentity.m_serverConnection == conn && networkingIdentity.m_type != NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT && networkingIdentity.m_type != NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    {
                        suppIdentities.Add(networkingIdentity);
                    }

                    networkingIdentity.OnServerDisconnect(conn);
                }

                for (int i = suppIdentities.Count - 1; i >= 0; i--)
                {
                    NetworkingIdentity netIdentity = suppIdentities[i];
                    netIdentities.Remove(netIdentity.m_netId);
#if CLIENT
                    RemoveNetworkingIdentity(netIdentity, m_connectionNetworkedObjects);//Because we can be host
#endif
                    server.SendToAll(NetworkingMessageType.ObjectDestroy, new ObjectDestroyMessage(netIdentity.m_netId), NetworkingChannel.DefaultReliable);
                    Destroy(netIdentity.gameObject);
                }
            }
        }
#endif

#if CLIENT
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
#endif

#if SERVER
        private void OnServerDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_serverNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                RemoveNetworkingIdentity(netIdentity, m_serverNetworkedObjects);
#if CLIENT
                RemoveNetworkingIdentity(netIdentity, m_connectionNetworkedObjects); //because we can be host
#endif
                Destroy(netIdentity.gameObject);
            }
        }
#endif

#if CLIENT
        void OnConnectionDisconnect(NetworkingConnection conn)
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
#endif

#if SERVER
        void OnStopServer(NetworkingServer server)
        {
            foreach (Dictionary<ushort, NetworkingIdentity> d in m_serverNetworkedObjects.Values)
                foreach (NetworkingIdentity i in d.Values)
                {
                    if (i.m_type == NetworkingIdentity.TYPE.SINGLE_SCENE_OBJECT || i.m_type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
                    {
                        i.Reset();
                        i.OnStopServer();
                    }
                    else
                        Destroy(i.gameObject);

                }

            m_serverNetworkedObjects.Remove(server);
            m_serverCurrentNetId.Remove(server);
        }
#endif

#if CLIENT
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
                            i.Reset();
                        }
                        else
                            Destroy(i.gameObject);
                    }
                }
            }

            m_connectionNetworkedObjects.Remove(conn);
        }
#endif

        #region utility


        public GameObject FindRegisteredGameObject(ushort assetId)
        {
            GameObject go = null;
            registeredNetworkedGameObjectsDictionnary.TryGetValue(assetId, out go);

            if (go == null)
                Debug.LogError("[Unet] GameObject not find for assetId : " + assetId);

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

#if SERVER
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
#endif

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

#if SERVER
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
#endif
        #endregion
#endif

#if UNITY_EDITOR
        void OnValidate()
        {
            if(networkingSceneSystem == null)
                networkingSceneSystem = this.GetComponent<NetworkingSceneSystem>();
        }

#endif
    }
}
