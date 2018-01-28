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
        private Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> m_serverNetworkedObjects = new Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>>();
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
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.Rpc, OnConnectionRpc);
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.AutoRpc, OnConnectionRpc);
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
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.Command, OnServerCommand);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.Rpc, OnConnectionRpc);
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.AutoRpc, OnConnectionRpc);
        }

        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity)
        {
            if (networkingIdentity.connectionToServer != null)
            {
                if (m_serverNetworkedObjects.ContainsKey(networkingIdentity.connectionToServer.m_linkedServer))
                {
                    if (m_serverNetworkedObjects[networkingIdentity.connectionToServer.m_linkedServer].ContainsKey(networkingIdentity.m_netId))
                        m_serverNetworkedObjects[networkingIdentity.connectionToServer.m_linkedServer].Remove(networkingIdentity.m_netId);
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

            Dictionary<ushort, NetworkingIdentity> dictionary;
            m_serverNetworkedObjects.TryGetValue(server, out dictionary);

            if (dictionary != null)
            {
                foreach (NetworkingIdentity i in dictionary.Values)
                {
                    netMsg.conn.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_assetId, i.m_netId));
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

            AddNetworkingIdentity(netIdentity, server, m_serverNetworkedObjects);

            server.SendToReady(NetworkingMessageType.ObjectSpawn, new SpawnMessage(netIdentity.m_assetId, currentNetId));
        }


        void OnClientSpawnMessage(NetworkingMessage netMsg)
        {
            SpawnMessage spawnMessage = netMsg.As<SpawnMessage>();
            NetworkingIdentity netIdentity = null;

            if (netMsg.conn.m_linkedServer == null)           //Already spawn on server ;)
            {
                GameObject go = Instantiate(FindRegisteredGameObject(spawnMessage.m_gameObjectAssetId));
                netIdentity = go.GetComponent<NetworkingIdentity>();
                netIdentity.m_netId = spawnMessage.m_gameObjectNetId;
            }
            else
            {
                netIdentity = FindLocalNetworkIdentity(netMsg.conn.m_linkedServer, spawnMessage.m_gameObjectNetId, m_serverNetworkedObjects);
            }

            netIdentity.connectionAuthorityOwner = netMsg.conn;
            AddNetworkingIdentity(netIdentity, netMsg.conn, m_connectionNetworkedObjects);
        }

        void OnServerCommand(NetworkingMessage netMsg)
        {
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.conn.m_linkedServer, networkingIdentityID, m_serverNetworkedObjects);
            
            if(netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
        }

        void OnConnectionRpc(NetworkingMessage netMsg)
        {
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.conn, networkingIdentityID, m_connectionNetworkedObjects);

            if (netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
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
            NetworkingIdentity networkingIdentity = null;

            if (connection.m_linkedServer == null)
                networkingIdentity = FindLocalNetworkIdentity(connection, netId, m_connectionNetworkedObjects);
            else
                networkingIdentity = FindLocalNetworkIdentity(connection.m_linkedServer, netId, m_serverNetworkedObjects);

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

            m_connectionNetworkedObjects.Remove(conn);
        }

        void OnServerDisconnect(NetworkingServer server, NetworkingMessage netMsg)
        {
            Dictionary<ushort, NetworkingIdentity> netIdentities = null;

            m_serverNetworkedObjects.TryGetValue(server, out netIdentities);
            if (netIdentities != null)
            {
                List<NetworkingIdentity> suppIdentities = new List<NetworkingIdentity>();

                foreach (NetworkingIdentity i in netIdentities.Values)
                {
                    if (i.destroyOnDisconnect && i.connectionToServer == netMsg.conn)
                    {
                        i.connectionToServer.m_linkedServer.SendToAll(NetworkingMessageType.ObjectDestroy, new ObjectDestroyMessage(i.m_netId));
                        Destroy(i.gameObject);

                        suppIdentities.Add(i);
                    }
                }

                for (int i = suppIdentities.Count - 1; i >= 0; i--)
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

            m_serverNetworkedObjects.Remove(server);
        }

        private void OnConnectionDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.conn, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_connectionNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                Destroy(netIdentity.gameObject);
            }

            m_connectionNetworkedObjects.Remove(netMsg.conn);
        }

        private void OnServerDestroy(NetworkingMessage netMsg)
        {
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.conn.m_linkedServer, netMsg.As<ObjectDestroyMessage>().m_gameObjectNetId, m_serverNetworkedObjects);
            if (netIdentity != null)
            {
                netIdentity.OnNetworkDestroy();
                Destroy(netIdentity.gameObject);
            }
        }
    }
}
