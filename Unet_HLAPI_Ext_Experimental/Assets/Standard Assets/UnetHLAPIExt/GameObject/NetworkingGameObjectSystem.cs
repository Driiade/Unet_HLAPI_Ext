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
            //NetworkingIdentity.OnNetworkingIdentityDestroy += RemoveNetworkingIdentity;

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

        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity, Dictionary<NetworkingServer, Dictionary<ushort, NetworkingIdentity>> dictionary )
        {
                if (dictionary.ContainsKey(networkingIdentity.m_connection.m_server))
                {
                   dictionary[networkingIdentity.m_connection.m_server].Remove(networkingIdentity.m_netId);
                }
        }

        void RemoveNetworkingIdentity(NetworkingIdentity networkingIdentity, Dictionary<NetworkingConnection, Dictionary<ushort, NetworkingIdentity>> dictionary)
        {
            if (dictionary.ContainsKey(networkingIdentity.m_connection))
            {
               dictionary[networkingIdentity.m_connection].Remove(networkingIdentity.m_netId);
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
                    netMsg.m_connection.Send(NetworkingMessageType.ObjectSpawn, new SpawnMessage(i.m_assetId, i.m_netId, false));
                }
            }


            for (int i = 0; i < spawnedGameObjectOnConnect.Length; i++)
            {
                SpawnOnServer(server, netMsg.m_connection, spawnedGameObjectOnConnect[i]);
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

            netIdentity.m_connection = conn;
            netIdentity.m_netId = currentNetId;
            netIdentity.m_isServer = true;
            netIdentity.ChangeType(NetworkingIdentity.TYPE.SPAWNED);
            AddNetworkingIdentity(netIdentity, server, m_serverNetworkedObjects);

            server.SendTo(conn.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(netIdentity.m_assetId, currentNetId, netIdentity.localPlayerAuthority));

            foreach (NetworkingConnection c in server.connections)
            {
                if(c != null && c != conn)
                {
                    server.SendTo(c.m_connectionId, NetworkingMessageType.ObjectSpawn, new SpawnMessage(netIdentity.m_assetId, currentNetId, false));
                }
            }        
        }


        void OnClientSpawnMessage(NetworkingMessage netMsg)
        {
            SpawnMessage spawnMessage = netMsg.As<SpawnMessage>();
            NetworkingIdentity netIdentity = null;

            if (netMsg.m_connection.m_server == null)           //Already spawn on server ;)
            {
                GameObject go = Instantiate(FindRegisteredGameObject(spawnMessage.m_gameObjectAssetId));
                netIdentity = go.GetComponent<NetworkingIdentity>();
                netIdentity.m_netId = spawnMessage.m_gameObjectNetId;
                netIdentity.m_connection = netMsg.m_connection;
                netIdentity.ChangeType(NetworkingIdentity.TYPE.SPAWNED);
            }
            else
            {
                netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, spawnMessage.m_gameObjectNetId, m_serverNetworkedObjects);
            }
            netIdentity.m_hasAuthority = spawnMessage.m_hasAuthority;
            netIdentity.m_isClient = true;
            AddNetworkingIdentity(netIdentity, netMsg.m_connection, m_connectionNetworkedObjects);
        }


        public zeqze void Replicate(NetworkingIdentity netIdentity
        {

        }



        void OnServerCommand(NetworkingMessage netMsg)
        {
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.m_connection.m_server, networkingIdentityID, m_serverNetworkedObjects);
            
            if(netIdentity)
                netIdentity.HandleMethodCall(netMsg.reader);
        }

        void OnConnectionRpc(NetworkingMessage netMsg)
        {
            ushort networkingIdentityID = netMsg.reader.ReadUInt16();
            NetworkingIdentity netIdentity = FindLocalNetworkIdentity(netMsg.m_connection, networkingIdentityID, m_connectionNetworkedObjects);

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

            if (connection.m_server == null)
                networkingIdentity = FindLocalNetworkIdentity(connection, netId, m_connectionNetworkedObjects);
            else
                networkingIdentity = FindLocalNetworkIdentity(connection.m_server, netId, m_serverNetworkedObjects);

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
                    if (i && i.destroyOnDisconnect)
                        Destroy(i.gameObject);
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
                    if (i && i.destroyOnStop)
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

                foreach (NetworkingIdentity netIdentity in netIdentities.Values)
                {
                    if (netIdentity.destroyOnDisconnect && netIdentity.m_connection == netMsg.m_connection)
                    {                      
                        suppIdentities.Add(netIdentity);
                    }
                }

                for (int i = suppIdentities.Count - 1; i >= 0; i--)
                {
                    NetworkingIdentity netIdentity = suppIdentities[i];
                    netIdentities.Remove(netIdentity.m_netId);
                    RemoveNetworkingIdentity(netIdentity, m_connectionNetworkedObjects);//Because we can be host
                    server.SendToAll(NetworkingMessageType.ObjectDestroy, new ObjectDestroyMessage(netIdentity.m_netId), NetworkingMessageType.Channels.DefaultReliable);
                    Destroy(netIdentity.gameObject);
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
    }
}
