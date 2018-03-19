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


namespace BC_Solution.UnetNetwork
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Networking/NetworkingIdentity")]
    public class NetworkingIdentity : MonoBehaviour
    {
        public enum TYPE { SPAWNED =0, SINGLE_SCENE_OBJECT=1, REPLICATED_SCENE_PREFAB=2}

        public static List<NetworkingIdentity> s_networkingIdentities = new List<NetworkingIdentity>();

        // configuration
        [SerializeField] internal TYPE m_type;
        public TYPE type
        {
            get { return m_type; }
        }

        [SerializeField] internal ushort m_sceneId;
        [SerializeField] internal ushort m_assetId;

#if SERVER
        [SerializeField, Tooltip("The server that manages this networkingIdentity on scene, only used for SINGLE_SCENE_OBJECT and REPLICATED PREFAB_SCENE_PREFAB")] internal string m_serverId = "MainServer";
#endif

        [SerializeField] bool m_localPlayerAuthority;
        [SerializeField] bool m_autoSetNetworkBehaviourNetId = true;

#if SERVER
        [Tooltip("if the networking Behaviour is not present on server and a send to owner is called, will automatically redirect the call to owner")]
        public bool m_serverAutoSendToOwner = true;

        [Tooltip("if the networking Behaviour is not present on server and a autoRpc is called, will automatically redirect the call every aware connection")]
        public bool m_serverAutoRpc = true;
#endif

        public bool localPlayerAuthority { get { return m_localPlayerAuthority; } set { m_localPlayerAuthority = value; } }

        // runtime data
        bool m_hasAuthority;

        internal ushort m_netId; //Gain space with ushort

        [Space(20)]
        [SerializeField]
        NetworkingBehaviour[] m_networkingBehaviours;

        public NetworkingBehaviour[] NetworkingBehaviours { get { return m_networkingBehaviours; } }

        NetworkingWriter writer = new NetworkingWriter();

#if SERVER
        internal NetworkingConnection m_serverConnection;
#endif
#if CLIENT
        internal NetworkingConnection m_connection;
#endif

#if SERVER
        internal NetworkingServer m_server;
#endif

#if CLIENT
        internal bool m_isClient;
        bool m_isLocalClient;
#endif

#if SERVER
        internal bool m_isServer;
#endif

#if CLIENT
        public bool isClient { get { return m_isClient; } }

#endif

#if SERVER
        public bool isServer{ get { return m_isServer; } }
#endif

        public bool hasAuthority {
            get { return m_hasAuthority; }
            set {
                if(value != m_hasAuthority)
                {
                    m_hasAuthority = value;

                    if (m_hasAuthority)
                        OnStartAuthority();
                    else
                        OnStopAuthority();
                }
            } }

#if CLIENT
        public bool isLocalClient { get { return m_isLocalClient; }
            set
            {
                if (value == m_isLocalClient)
                    return;

                m_isLocalClient = value;

                if (m_isLocalClient)
                    OnStartLocalConnection();
            }
        }
#endif

        public ushort netId { get { return m_netId; } }
        public ushort sceneId { get { return m_sceneId; } }
        public ushort assetID { get { return m_assetId; } }

#if CLIENT
        public NetworkingConnection connection {get { return m_connection; } }
#endif

#if SERVER
        /// <summary>
        /// Reference connection aware of the gameObject
        /// </summary>
        internal List<NetworkingConnection> m_serverAwareConnections = new List<NetworkingConnection>();
#endif

        private void Awake()
        {
            s_networkingIdentities.Add(this);

            foreach (NetworkingBehaviour networkingBehaviour in m_networkingBehaviours)
                networkingBehaviour.Init(this);
        }

        private void Update()
        {
            foreach (NetworkingBehaviour networkingBehaviour in m_networkingBehaviours)
                networkingBehaviour.CheckSyncVars();
        }


        private void OnDestroy()
        {
            s_networkingIdentities.Remove(this);
        }


        internal void HandleMethodCall(NetworkingReader reader)
        {
            byte networkBehaviourNetId = reader.ReadByte();

            foreach (NetworkingBehaviour n in m_networkingBehaviours)
            {
                if (n.m_netId == networkBehaviourNetId)
                {
                    n.HandleMethodCall(reader);
                    return;
                }
            }

            Debug.LogError("NetworkingBehaviour not found : " + networkBehaviourNetId + " on : " + this.gameObject);
        }


#if SERVER
        internal void ServerHandleAutoRpc(NetworkingReader reader, int channelId)
        {
            byte networkBehaviourNetId = reader.ReadByte();

            foreach (NetworkingBehaviour n in m_networkingBehaviours)
            {
                if (n.m_netId == networkBehaviourNetId)
                {
                    n.ServerHandleAutoRpc(reader, channelId);
                    return;
                }
            }

            if (m_serverAutoRpc)
            {
                ServerSendAutoRpc(reader, channelId, networkBehaviourNetId);
            }
            else
                throw new Exception("NetworkingBehaviour of id : " + networkBehaviourNetId + " not find on server");
        }


        internal void ServerHandleSendToOwner(NetworkingConnection owner, NetworkingReader reader, int channelId)
        {
            byte networkBehaviourNetId = reader.ReadByte();

            foreach (NetworkingBehaviour n in m_networkingBehaviours)
            {
                if (n.m_netId == networkBehaviourNetId)
                {
                    n.ServerHandleSendToOwner(owner, reader, channelId);
                    return;
                }
            }

            if (m_serverAutoRpc)
            {
                ServerSendToOwner(owner, reader, channelId, networkBehaviourNetId);
            }
            else
                throw new Exception("NetworkingBehaviour of id : " + networkBehaviourNetId + " not find on server");
        }

        internal void ServerSendToOwner(NetworkingConnection owner, NetworkingReader reader, int channelId, byte networkingBehaviourId)
        {
            byte[] callInfo = reader.Flush(); //only information relative to serialization call
            writer.SeekZero(true);
            writer.StartMessage();

            writer.Write(NetworkingMessageType.Rpc);
            writer.Write(this.netId);
            writer.Write(networkingBehaviourId);

            writer.Write(callInfo);
            writer.FinishMessage();

            owner.Send(writer, channelId);
        }

        internal void ServerSendAutoRpc(NetworkingReader reader, int channelId, byte networkingBehaviourId)
        {
            byte[] callInfo = reader.Flush(); //only information relative to serialization call
            writer.SeekZero(true);
            writer.StartMessage();

            writer.Write(NetworkingMessageType.Rpc);
            writer.Write(this.netId);
            writer.Write(networkingBehaviourId);

            writer.Write(callInfo);
            writer.FinishMessage();

            foreach (NetworkingConnection conn in m_serverAwareConnections)
            {
                conn.Send(writer, channelId);
            }
        }
#endif

        internal void HandleUpdateVars(NetworkingReader reader)
        {
            byte networkBehaviourNetId = reader.ReadByte();

            foreach (NetworkingBehaviour n in m_networkingBehaviours)
            {
                if (n.m_netId == networkBehaviourNetId)
                {
                    n.UnSerializeSyncVars(reader);
                    return;
                }
            }                          
        }

        // only used in SetLocalObject
        /* internal void UpdateClientServer(bool isClientFlag, bool isServerFlag)
         {
             m_isClient |= isClientFlag;
             m_isServer |= isServerFlag;
         }*/



#if UNITY_EDITOR
        void OnValidate()
        {
            m_networkingBehaviours = GetComponentsInChildren<NetworkingBehaviour>(true);

            if (m_autoSetNetworkBehaviourNetId)
            {
                for (int i = 0; i < m_networkingBehaviours.Length; i++)
                {
                    m_networkingBehaviours[i].m_netId = (byte)i;
                }
            }

            if (!string.IsNullOrEmpty(this.gameObject.scene.name))
            {
                if (m_type == TYPE.SPAWNED)
                {
                    m_type = TYPE.SINGLE_SCENE_OBJECT;
                }
            }
            else
                m_type = TYPE.SPAWNED;
        }
#endif

#if SERVER
        internal void OnStartServer(NetworkingServer networkingServer)
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
                b.OnStartServer(networkingServer);
        }
#endif

#if CLIENT
        internal void OnStartLocalConnection()
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnStartLocalConnection();
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }
#endif

        internal void OnStartAuthority()
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnStartAuthority();
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        internal void OnStopAuthority()
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnStopAuthority();
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

#if SERVER
        internal void OnServerConnect(NetworkingConnection conn)
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnServerConnect(conn);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        internal void OnServerSyncNetId(NetworkingConnection conn)
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnServerSyncNetId(conn);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        internal void OnServerAware(NetworkingConnection conn)
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnServerAware(conn);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }
#endif

        /// <summary>
        /// Serialize all syncVar of its networkingBehaviour in NetworkingWriter
        /// </summary>
        /// <param name="writer"></param>
        internal byte[] SerializeAllNetworkingBehaviourSyncVar()
        {
            writer.SeekZero(true);
            int dirtyMask = int.MaxValue; //All is dirty

            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                m_networkingBehaviours[i].SerializeSyncVars(writer, dirtyMask);
            }
            return writer.ToArray();
        }

        internal void GetAllSyncVars(byte[] syncVarsData)
        {
            NetworkingReader reader = new NetworkingReader(syncVarsData);

            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                m_networkingBehaviours[i].UnSerializeSyncVars(reader);
            }
        }

      
        internal void OnNetworkDestroy()
        {
            foreach(NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnNetworkDestroy();
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }


        internal void Reset()
        {
            m_hasAuthority = false;
            m_netId = 0;

#if CLIENT
            m_isLocalClient = false;
            m_isClient = false;
            m_connection = null;
#endif

#if SERVER
            m_serverConnection = null;
            m_isServer = false;
            foreach (NetworkingBehaviour networkingBehaviour in m_networkingBehaviours)
            {
                networkingBehaviour.m_serverConnectionListeners.Clear();
            }
#endif
        }
    };
}
