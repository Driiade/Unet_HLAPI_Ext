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
using System.ComponentModel;
using System.Reflection;
using UnityEngine;


namespace BC_Solution.UnetNetwork
{
    [AddComponentMenu("")]
    public class NetworkingBehaviour : MonoBehaviour
    {
        [SerializeField]
        internal bool automaticAddListener = true;

        //uint m_syncVarDirtyBits;
        //float m_LastSendTime;

        // this prevents recursion when SyncVar hook functions are called.
        bool m_SyncVarGuard;

#if CLIENT
        public bool localPlayerAuthority { get { return m_networkingIdentity.localPlayerAuthority; } }
#endif
#if SERVER
        public bool isServer { get { return m_networkingIdentity.isServer; } }
#endif

#if CLIENT
        public bool isClient { get { return m_networkingIdentity.isClient; } }
#endif
        public bool hasAuthority { get { return m_networkingIdentity.hasAuthority; } }

#if CLIENT
        public bool isLocalClient { get { return m_networkingIdentity.isLocalClient; } }
#endif

        /// <summary>
        /// Connection on server which listen for RPC/Command
        /// If you want to add / remove a NetworkingConnection and fire event : 
        /// Call Add / Remove ServerConnectionListener
        /// </summary>
        public List<NetworkingConnection> m_serverConnectionListeners = new List<NetworkingConnection>();

#if SERVER
        public NetworkingConnection serverConnection { get { return m_networkingIdentity.m_serverConnection; } }
#endif

#if CLIENT
        public NetworkingConnection connection { get { return m_networkingIdentity.m_connection; } }
#endif

#if SERVER
        public NetworkingServer server { get { return m_networkingIdentity.m_server; } }
#endif

#if SERVER
        public List<NetworkingConnection> serverAwareConnections { get { return m_networkingIdentity.m_serverAwareConnections; } }
#endif

        //public short playerControllerId { get { return m_networkingIdentity.playerControllerId; } }
        //protected uint syncVarDirtyBits { get { return m_syncVarDirtyBits; } }
        //protected bool syncVarHookGuard { get { return m_SyncVarGuard; } set { m_SyncVarGuard = value; } }

        [SerializeField]
        internal byte m_netId;

        // const float k_DefaultSendInterval = 0.1f;

        private NetworkingIdentity m_networkingIdentity;
        public NetworkingIdentity networkingIdentity { get { return m_networkingIdentity; } }

        private MethodInfo[] networkedMethods;
        private FieldInfo[] syncVars;

        private NetworkingWriter writer = new NetworkingWriter();

        internal void Init(NetworkingIdentity networkingIdentity)
        {
            m_networkingIdentity = networkingIdentity;
            networkedMethods = GetNetworkedMethods();
            syncVars = GetSyncVars();
        }

        internal void CheckSyncVars()
        {
            int dirtyMask = GetSyncVarDirtyMask();

            if (dirtyMask != 0)
            {
                writer.SeekZero(true);
                writer.StartMessage();
                writer.Write(NetworkingMessageType.UpdateVars);

                writer.Write(this.networkingIdentity.netId);
                writer.Write(this.m_netId);

                SerializeSyncVars(writer, dirtyMask);
                writer.FinishMessage();


#if SERVER && CLIENT
                if (isServer) //Server can sync var except to the owner of the object
                {
                    foreach (NetworkingConnection connection in this.m_serverConnectionListeners)
                    {
                        if (connection != this.serverConnection)
                        {
                            connection.Send(writer, NetworkingChannel.DefaultReliableSequenced);
                        }
                    }
                }
                else if (isLocalClient) //only local client can sync var
                {
                    connection.Send(writer, NetworkingChannel.DefaultReliableSequenced);
                }
#elif SERVER
                if (isServer)
                {
                    foreach (NetworkingConnection connection in this.m_serverConnectionListeners)
                    {
                        connection.Send(writer, NetworkingChannel.DefaultReliableSequenced);
                    }
                }
#elif CLIENT
                if (isLocalClient)
                {
                    connection.Send(writer, NetworkingChannel.DefaultReliableSequenced);
                }
#endif

            }
        }

        internal int GetSyncVarDirtyMask()
        {
            int dirtyMask = 0;

            //Check for dirty value
            for (int i = 0; i < syncVars.Length; i++)
            {
                if (((IDirtable)((syncVars[i]).GetValue(this))).IsDirty())
                {
                    dirtyMask = ((1 << i)) | dirtyMask;
                }
            }

            return dirtyMask;
        }

        internal byte[] SerializeSyncVars(NetworkingWriter writer, int dirtyMask)
        {
            if (syncVars.Length > sizeof(int)*8)
                throw new System.Exception("Too many syncVars in the networkingBehaviour : " + this);

            if (syncVars.Length <= sizeof(byte)*8)
            {
                writer.Write((byte)dirtyMask);
            }
            else if (syncVars.Length <= sizeof(short)*8)
            {
                writer.Write((short)dirtyMask);
            }
            else if (syncVars.Length <= sizeof(int)*8)
            {
                writer.Write(dirtyMask);
            }

            if (dirtyMask != 0)
            {
                for (int i = 0; i < syncVars.Length; i++)
                {
                    if( ((1<< i) & dirtyMask) != 0)
                    {
                        writer.Write((syncVars[i]).GetValue(this));
                    }
                }
            }

            return writer.ToArray();
        }

        internal void UnSerializeSyncVars(NetworkingReader reader)
        {
            int dirtyMask = 0;

            if (syncVars.Length <= sizeof(byte)*8)
            {
                dirtyMask = reader.ReadByte();
            }
            else if (syncVars.Length <= sizeof(short)*8)
            {
                dirtyMask = reader.ReadInt16();
            }
            else if (syncVars.Length <= sizeof(int)*8)
            {
                dirtyMask = reader.ReadInt32();
            }

            for (int i = 0; i < syncVars.Length; i++)
            {
                if (((1 << i) & dirtyMask) != 0)
                {
#if CLIENT && SERVER
                    ((ISerializable)((syncVars[i]).GetValue(this))).OnDeserialize(reader, this.connection, this.serverConnection);
#elif CLIENT
                    ((ISerializable)((syncVars[i]).GetValue(this))).OnDeserialize(reader, this.connection, null);
#elif SERVER
                    ((ISerializable)((syncVars[i]).GetValue(this))).OnDeserialize(reader, null, this.serverConnection);
#endif
                }
            }
        }


#if CLIENT
        public void SendToServer(string methodName, int channelId,params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.Command);

            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            connection.Send(writer, channelId);
        }
#endif

        /// <summary>
        /// Send to all connection which listen
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="channelId"></param>
        /// <param name="parameters"></param>
        public void SendToAllConnections(string methodName, int channelId, params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.Rpc);
            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            foreach (NetworkingConnection connection in this.m_serverConnectionListeners)
            {
                connection.Send(writer, channelId);
            }
        }

#if SERVER
        public void SendToConnection(NetworkingConnection conn,string methodName, int channelId, params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.Rpc);
            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            server.SendTo(conn.m_connectionId, writer, channelId);
        }
#endif

        /// <summary>
        /// Can be called on client or server
        /// Call this with a reliable sequenced channel (to be sure NetworkingIdentity is set on the other side)
        /// </summary>
        /// <param name="networkingIdentity"></param>
        /// <param name="methodName"></param>
        /// <param name="channelID"></param>
        /// <param name="parameters"></param>
        public void SendToOwner(NetworkingIdentity networkingIdentity, string methodName, int channelId, params object[] parameters)
        {
#if SERVER
            if(networkingIdentity.m_serverConnection != null) //On Server
            {
                SendToConnection(networkingIdentity.m_serverConnection, methodName, channelId, parameters);
              return;
            }
#endif
#if CLIENT
            if (connection != null)
            {
                writer.SeekZero(true);
                writer.StartMessage();
                writer.Write(NetworkingMessageType.SendToOwner);
                writer.Write(networkingIdentity.netId);
                SerializeCall(writer, methodName, parameters);
                writer.FinishMessage();

                connection.Send(writer, channelId);
            return;
            }
#endif

            Debug.LogWarning(this + " is not client or server or has no owner");
        }

#if CLIENT
        public void AutoSendToConnections(string methodName, int channelId, params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.AutoRpc);
            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            connection.Send(writer, channelId);
        }
#endif

        void SerializeCall(NetworkingWriter writer, string methodName, object[] parameters)
        {
            writer.Write(this.networkingIdentity.netId);
            writer.Write(this.m_netId);

            byte methodIndex = GetNetworkMethodIndex(methodName, networkedMethods);
            writer.Write(methodIndex);

            ParameterInfo[] methodParams = networkedMethods[methodIndex].GetParameters();

            //Debug.Log(methodName + " : " + GetNetworkMethodIndex(methodName, networkedMethods));
            for (int i = 0; i < parameters.Length; i++)
            {
                object param = parameters[i];

                if (param.GetType() != methodParams[i].ParameterType)
                {
                    throw new System.Exception(methodName + " on " + this.gameObject + " Parameter mismatch : called with : " + param.GetType() + " expected : " + methodParams[i].ParameterType);
                }

                writer.Write(param);
            }
        }

        internal void HandleMethodCall(NetworkingReader reader)
        {
            byte methodIndex = reader.ReadByte();
            MethodInfo method = networkedMethods[methodIndex];
            ParameterInfo[] parameterInfos = method.GetParameters();
            object[] parameters = new object[parameterInfos.Length];

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                ParameterInfo info = parameterInfos[i];
#if SERVER && CLIENT
                parameters[i] = reader.Read(info.ParameterType, this.connection, this.serverConnection);
#elif CLIENT
                parameters[i] = reader.Read(info.ParameterType, this.connection, null);
#elif SERVER
                parameters[i] = reader.Read(info.ParameterType, null, this.serverConnection);
#endif
            }

            method.Invoke(this, parameters);
        }



#if SERVER
        internal virtual void ServerHandleAutoRpc(NetworkingReader reader, int channelId)
        {
            this.m_networkingIdentity.ServerSendAutoRpc(reader, channelId, this.m_netId);
        }

        internal virtual void ServerHandleSendToOwner(NetworkingConnection owner, NetworkingReader reader, int channelId)
        {
            this.networkingIdentity.ServerSendToOwner(owner, reader, channelId, this.m_netId);
        }
#endif

        MethodInfo[] GetNetworkedMethods()
        {
            MethodInfo[] allMethods = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            List<MethodInfo> networkedMethods = new List<MethodInfo>();
            for (int i = 0; i < allMethods.Length; i++)
            {
                object[] attributes = allMethods[i].GetCustomAttributes(typeof(NetworkedFunction), true);
                for (int j = 0; j < attributes.Length; j++)
                {
                    if (attributes[j] is NetworkedFunction)
                    {
                        networkedMethods.Add(allMethods[i]);
                    }
                }
            }

            return networkedMethods.ToArray();
        }

        FieldInfo[] GetSyncVars()
        {
            FieldInfo[] allVars = this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            List<FieldInfo> syncVars = new List<FieldInfo>();
            for (int i = 0; i < allVars.Length; i++)
            {
                //if (allVars[i].FieldType.IsGenericType)
                // Debug.Log(allVars[i].FieldType + " : " + typeof(SyncVar<>) + " : " + allVars[i].FieldType.IsGenericType + " : " + allVars[i].FieldType.GetGenericTypeDefinition().IsSubclassOf(typeof(SyncVar<>)));

                System.Type genericType = allVars[i].FieldType;

                if (allVars[i].GetCustomAttributes(typeof(NetworkedVariable),true) != null && genericType.GetInterface("IDirtable") != null && genericType.GetInterface("ISerializable") != null)
                {
                    syncVars.Add(allVars[i]);
                }
            }

            return syncVars.ToArray();
        }

#if SERVER
        /// <summary>
        /// Can't be called on client only
        /// </summary>
        /// <param name="conn"></param>
        public void AddServerConnectionListener(NetworkingConnection conn)
        {
            if (!m_serverConnectionListeners.Contains(conn) && serverAwareConnections.Contains(conn))
            {
                m_serverConnectionListeners.Add(conn);

                //Re-sync all vars
                writer.SeekZero(true);
                writer.StartMessage();
                writer.Write(NetworkingMessageType.UpdateVars);

                writer.Write(this.networkingIdentity.netId);
                writer.Write(this.m_netId);

                SerializeSyncVars(writer, int.MaxValue);
                writer.FinishMessage();

                conn.Send(writer, NetworkingChannel.DefaultReliableSequenced);
                //

                this.OnServerAddListener(conn);
            }
            else
                Debug.LogWarning("Server connection listener already contain : " + conn + " Or connection is not aware of this gameObject : " + gameObject);
        }


        /// <summary>
        /// Can't be called on client only
        /// </summary>
        /// <param name="conn"></param>
        public void RemoveServerConnectionListener(NetworkingConnection conn)
        {
            if (m_serverConnectionListeners.Contains(conn))
            {
                m_serverConnectionListeners.Remove(conn);
                this.OnServerRemoveListener(conn);
            }
        }
#endif

        byte GetNetworkMethodIndex(string methodName, MethodInfo[] methodInfo)
        {
            for (int i = 0; i < methodInfo.Length; i++)
            {
                if (methodInfo[i].Name == methodName)
                {
                    return (byte)(i);
                }
            }
            throw new System.Exception("Method not found : " + methodName + " on " + this.gameObject + "//" + this);
        }

        public void AddAllAwareConnectionToListen()
        {
#if SERVER
            if (isServer)
            {
                ServerAddAllAwareConnectionToListen();
                return;
            }
#endif

#if CLIENT
            if (isClient)
            {
                SendToServer("ServerAddAllAwareConnectionToListen", NetworkingChannel.DefaultReliableSequenced);
                return;
            }
#endif
        }


        public void RemoveAllAwareConnectionToListen()
        {
#if SERVER
            if (isServer)
               {
            ServerRemoveAllAwareConnectionToListen();
            return;
            }
#endif
#if CLIENT
            if (isClient)
            {
                SendToServer("ServerRemoveAllAwareConnectionToListen", NetworkingChannel.DefaultReliableSequenced);
                return;
            }
#endif
        }

        [NetworkedFunction]
        public void ServerAddAllAwareConnectionToListen()
        {
#if SERVER
            foreach (NetworkingConnection conn in serverAwareConnections)
            {
                AddServerConnectionListener(conn);
            }
#endif
        }

        [NetworkedFunction]
        public void ServerRemoveAllAwareConnectionToListen()
        {
#if SERVER
            foreach (NetworkingConnection conn in serverAwareConnections)
            {
                RemoveServerConnectionListener(conn);
            }
#endif
        }

        /// <summary>
        /// Called when the Destroy function is called by server
        /// </summary>
        public virtual void OnNetworkDestroy()
        {
        }

#if SERVER
        /// <summary>
        /// Called when the gameObject is attached to a server (only on server side)
        /// </summary>
        /// <param name="networkingServer"></param>
        public virtual void OnStartServer(NetworkingServer networkingServer)
        {
        }

        /// <summary>
        /// Called when the server gameobejct stop (only on server side)
        /// </summary>
        public virtual void OnStopServer()
        {
        }
#endif

#if CLIENT
        /// <summary>
        /// Called when the isLocalConnection is checked
        /// </summary>
        public virtual void OnStartLocalConnection()
        {
        }
#endif

        /// <summary>
        /// Called when status of m_hasAutority change to true
        /// </summary>
        public virtual void OnStartAuthority()
        {
        }

        /// <summary>
        /// Called when status of m_hasAutority change to false
        /// </summary>
        public virtual void OnStopAuthority()
        {
        }

#if SERVER
        /// <summary>
        /// Called on server when a new connection is added
        /// Can be called if connection don't know the gameObject.
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerConnect(NetworkingConnection conn)
        {

        }


        /// <summary>
        /// Called on server when a new connection is acknowledge of the gameObject
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerAware(NetworkingConnection conn)
        {

        }


        /// <summary>
        /// Called on server when a connection is disconnected
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerDisconnect(NetworkingConnection conn)
        {

        }

        /// <summary>
        /// Called on server when server add a listener on a networkingIdentity
        /// This networkingBehaviour will be known by connection
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerAddListener(NetworkingConnection conn)
        {

        }


        /// <summary>
        /// Called on server when server add a listener on a networkingIdentity
        /// This networkingBehaviour will be known by connection
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerRemoveListener(NetworkingConnection conn)
        {

        }

        /// <summary>
        /// Called on the server when it is syncing the netId of an object with a connection
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerSyncNetId(NetworkingConnection conn)
        {

        }
#endif

        public virtual int GetNetworkChannel()
        {
            return NetworkingChannel.DefaultReliableSequenced;
        }
    }
}
