using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingGameObjectSystem : Singleton<NetworkingGameObjectSystem>
    {

        /// <summary>
        /// Networked GameObject which need to be registered
        /// </summary>
        [Space(2)]
        public GameObject[] spawnedGameObjectOnConnect;

        [Space(10)]
        public GameObject[] registeredNetworkedGameObjectsArray;


        /// <summary>
        /// NetworkIdentity by connection, only available on server
        /// </summary>
        private Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> m_serverNetworkedObjects = new Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>>();
        private Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> m_connectionNetworkedObjects = new Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>>();

        private Dictionary<ushort, GameObject> registeredNetworkedGameObjectsDictionnary = new Dictionary<ushort, GameObject>();

        /// <summary>
        /// Current netId index for each connection
        /// </summary>
        private Dictionary<NetworkingServer, ushort> m_serverCurrentNetId = new Dictionary<NetworkingServer, ushort>();


        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < registeredNetworkedGameObjectsArray.Length; i++)
            {
                GameObject go = registeredNetworkedGameObjectsArray[i];
                registeredNetworkedGameObjectsDictionnary.Add(go.GetComponent<NetworkingIdentity>().m_assetId, go);
            }

            NetworkingServer.OnConnectionReady += SpawnConnectionGameObjects;
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.ObjectSpawn, OnClientSpawnMessage);
            NetworkingIdentity.OnNetworkingIdentityDestroy += RemoveNetworkingIdentity;

            NetworkingConnection.OnConnectionDisconnect += OnConnectionDisconnect;
            NetworkingConnection.OnStopConnection += OnStopConnection;
            NetworkingServer.OnServerDisconnect += OnServerDisconnect;
            NetworkingServer.OnStopServer += OnStopServer;

            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.ObjectDestroy, OnConnectionDestroy);
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
        }

        private void OnDestroy()
        {
            NetworkingServer.OnConnectionReady -= SpawnConnectionGameObjects;
            NetworkingServer.OnConnectionReady -= SpawnConnectionGameObjects;
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectSpawn, OnClientSpawnMessage);

            NetworkingConnection.OnConnectionDisconnect -= OnConnectionDisconnect;
            NetworkingConnection.OnStopConnection -= OnStopConnection;
            NetworkingServer.OnServerDisconnect -= OnServerDisconnect;
            NetworkingServer.OnStopServer -= OnStopServer;

            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ObjectDestroy, OnConnectionDestroy);
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ObjectDestroy, OnServerDestroy);
        }

        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity)
        {
            if (networkingIdentity.connectionToServer != null)
            {
                if (m_serverNetworkedObjects.ContainsKey(networkingIdentity.connectionToServer))
                {
                    if (m_serverNetworkedObjects[networkingIdentity.connectionToServer].ContainsKey(networkingIdentity.m_netId))
                        m_serverNetworkedObjects[networkingIdentity.connectionToServer].Remove(networkingIdentity.m_netId);
                }
            }

            if (networkingIdentity.connectionAuthorityOwner != null)
            {
                if (m_connectionNetworkedObjects.ContainsKey(networkingIdentity.connectionToServer))
                {
                    if (m_connectionNetworkedObjects[networkingIdentity.connectionAuthorityOwner].ContainsKey(networkingIdentity.m_netId))
                        m_connectionNetworkedObjects[networkingIdentity.connectionAuthorityOwner].Remove(networkingIdentity.m_netId);
                }
            }
        }


        void SpawnConnectionGameObjects(NetworkingServer server, NetworkingMessage netMsg)
        {
            foreach (NetworkingConnection conn in m_serverNetworkedObjects.Keys)
            {
                Dictionary<ushort, NetworkingIdentity> dictionary = m_serverNetworkedObjects[conn];

                if (dictionary != null)
                {
                    foreach (NetworkingIdentity i in dictionary.Values)
                    {
                        netMsg.conn.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_assetId, i.m_netId));
                    }
                }
            }

            for (int i = 0; i < spawnedGameObjectOnConnect.Length; i++)
            {
                SpawnOnServer(server, netMsg.conn, spawnedGameObjectOnConnect[i]);
            }
        }

        /// <summary>
        /// Spawn an object accross network.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="go"></param>
        public void SpawnOnServer(NetworkingServer server, NetworkingConnection conn, GameObject gameObject)
        {
            ushort currentNetId;
            m_serverCurrentNetId.TryGetValue(server, out currentNetId);

            if (currentNetId == 0)
            {
                m_serverCurrentNetId.Add(server, currentNetId);
            }

            currentNetId++;
            m_serverCurrentNetId[server] = currentNetId;

            GameObject go = Instantiate(gameObject);
            NetworkingIdentity netIdentity = go.GetComponent<NetworkingIdentity>();

            netIdentity.connectionToServer = conn;
            netIdentity.m_netId = currentNetId;

            AddNetworkingIdentity(netIdentity, conn, m_serverNetworkedObjects);

            server.SendToReady(NetworkingMessageType.ObjectSpawn, new SpawnMessage(netIdentity.m_assetId, currentNetId));
        }


        public void OnClientSpawnMessage(NetworkingMessage netMsg)
        {
            SpawnMessage spawnMessage = netMsg.As<SpawnMessage>();
            if (!NetworkingSystem.Instance.ServerIsActive())           //Already spawn on server ;)
            {
                GameObject go = Instantiate(FindRegisteredGameObject(spawnMessage.m_gameObjectAssetId));
                NetworkingIdentity netIdentity = go.GetComponent<NetworkingIdentity>();
                netIdentity.m_netId = spawnMessage.m_gameObjectNetId;
                netIdentity.connectionAuthorityOwner = netMsg.conn;

                AddNetworkingIdentity(netIdentity, netMsg.conn, m_connectionNetworkedObjects);
            }
        }


        public GameObject FindRegisteredGameObject(ushort assetId)
        {
            GameObject go = null;
            registeredNetworkedGameObjectsDictionnary.TryGetValue(assetId, out go);
            return go;
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


        public NetworkingIdentity FindLocalNetworkIdentity(NetworkingConnection connection, ushort netId)
        {
            NetworkingIdentity networkingIdentity = FindLocalNetworkIdentity(connection, netId, m_connectionNetworkedObjects);
            if (networkingIdentity == null)
                networkingIdentity = FindLocalNetworkIdentity(connection, netId, m_serverNetworkedObjects);

            return networkingIdentity;
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



        void AddNetworkingIdentity(NetworkingIdentity networkingIdentity, NetworkingConnection conn, Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> connectionIdentityDictionary)
        {
            Dictionary<ushort, NetworkingIdentity> networkingIdentityDict = null;
            connectionIdentityDictionary.TryGetValue(conn, out networkingIdentityDict);

            if (networkingIdentityDict == null)
            {
                networkingIdentityDict = new Dictionary<ushort, NetworkingIdentity>();
                connectionIdentityDictionary.Add(conn, networkingIdentityDict);
            }

            networkingIdentityDict.Add(networkingIdentity.m_netId, networkingIdentity);
        }


        void OnConnectionDisconnect(NetworkingConnection conn, NetworkingMessage netMsg)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_connectionNetworkedObjects.TryGetValue(conn, out netIdentities);
            if (netIdentities != null)
            {
                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i.destroyOnDisconnect)
                        Destroy(i.gameObject);
                }
            }
        }

        void OnStopConnection(NetworkingConnection conn)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_connectionNetworkedObjects.TryGetValue(conn, out netIdentities);
            if (netIdentities != null)
            {
                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i.destroyOnStop)
                        Destroy(i.gameObject);
                }
            }
        }

        void OnServerDisconnect(NetworkingServer server, NetworkingMessage netMsg)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_serverNetworkedObjects.TryGetValue(netMsg.conn, out netIdentities);
            if (netIdentities != null)
            {
                List<NetworkingIdentity> suppIdentities = new List<NetworkingIdentity>();

                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i.destroyOnDisconnect)
                    {
                       i.connectionToServer.m_linkedServer.SendToAll(NetworkingMessageType.ObjectDestroy, new ObjectDestroyMessage(i.m_netId));
                       Destroy(i.gameObject);

                       suppIdentities.Add(i);
                    }              
                }

                for (int i = suppIdentities.Count-1; i >= 0; i--)
                {
                    netIdentities.Remove(suppIdentities[i].netId);
                }
            }
        }

        void OnStopServer(NetworkingServer server)
        {
            foreach (Dictionary<ushort, NetworkingIdentity> d in m_serverNetworkedObjects.Values)
                foreach (NetworkingIdentity i in d.Values)
                {
                    if (i && i.destroyOnStop)
                        Destroy(i.gameObject);
                }
        }

        private void OnConnectionDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.conn, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_connectionNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                Destroy(netIdentity.gameObject);
            }
        }

        private void OnServerDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.conn, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_serverNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                Destroy(netIdentity.gameObject);
            }
        }
    }
}
