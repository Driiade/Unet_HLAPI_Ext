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
        [SerializeField] internal bool automaticAddListener = true;

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
        private NetworkingWriter writer = new NetworkingWriter();

        internal void Init(NetworkingIdentity networkingIdentity)
        {
            m_networkingIdentity = networkingIdentity;
            networkedMethods = GetNetworkedMethods();
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

            foreach(NetworkingConnection connection in this.m_serverConnectionListeners)
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
                    throw new System.Exception("Parameter mismatch : called with : " + param.GetType() + " expected : " + methodParams[i].ParameterType);
                }

                if (param is byte)
                {
                    writer.Write((byte)param);
                }
                else if (param is int)
                {
                    writer.Write((int)param);
                }
                else if (param is string)
                {
                    writer.Write((string)param);
                }
                else if (param is ushort)
                {
                    writer.Write((ushort)param);
                }
                else if (param is byte[])
                {
                    writer.WriteBytesFull(((byte[])param));
                }
                else if (param is bool)
                {
                    writer.Write((bool)param);
                }
                else if (param is Vector3)
                {
                    writer.Write((Vector3)param);
                }
                else if (param is Vector2)
                {
                    writer.Write((Vector2)param);
                }
                else if (param is GameObject)
                {
                    writer.Write((GameObject)param);
                }
                else if(param.GetType().IsSubclassOf(typeof(UnityEngine.Component)))
                {
                    writer.Write((UnityEngine.Component)param);
                }
                else
                    throw new System.Exception("Serialization is impossible : " + param.GetType());
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

                if(info.ParameterType == typeof(byte))
                {
                    parameters[i] = reader.ReadByte();
                }
                else   if (info.ParameterType == typeof(int))
                {
                    parameters[i] = reader.ReadInt32();
                }
                  else if(info.ParameterType == typeof(string))
                {
                    parameters[i] = reader.ReadString();
                }
                else if (info.ParameterType == typeof(ushort))
                {
                    parameters[i] = reader.ReadUInt16();
                }
                else if (info.ParameterType == typeof(byte[]))
                {
                    parameters[i] = reader.ReadBytesAndSize();
                }
                else if (info.ParameterType == typeof(bool))
                {
                    parameters[i] = reader.ReadBoolean();
                }
                else if (info.ParameterType == typeof(Vector3))
                {
                    parameters[i] = reader.ReadVector3();
                }
                else if (info.ParameterType == typeof(Vector2))
                {
                    parameters[i] = reader.ReadVector2();
                }
                else if (info.ParameterType == typeof(GameObject))
                {
#if SERVER && CLIENT
                    parameters[i] = reader.ReadGameObject(this.connection, this.serverConnection);
#elif SERVER
                    parameters[i] = reader.ReadGameObject(null, this.serverConnection);
#elif CLIENT
                    parameters[i] = reader.ReadGameObject(this.connection, null);
#endif
                }
                else if (info.ParameterType.IsSubclassOf(typeof(UnityEngine.Component)))
                {
#if SERVER && CLIENT
                    parameters[i] = reader.ReadComponent(this.connection, this.serverConnection, info.ParameterType);
#elif SERVER
                    parameters[i] = reader.ReadComponent(null, this.serverConnection, info.ParameterType);
#elif CLIENT
                    parameters[i] = reader.ReadComponent(this.connection, null, info.ParameterType);
#endif
                }
                else
                    throw new System.Exception("UnSerialization is impossible : " + info.ParameterType.GetType());
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
                object[] attributes = allMethods[i].GetCustomAttributes(typeof(NetworkedFunction),true);
                for (int j = 0; j < attributes.Length; j++)
                {
                    if(attributes[j] is NetworkedFunction)
                    {
                        networkedMethods.Add(allMethods[i]);
                    }
                }
            }

            return networkedMethods.ToArray();
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
                this.OnServerAddListener(conn);
            }
            else
                Debug.LogWarning("Server connection listener already contain : " + conn + " Or conneciton is not aware of this gameObject : " + gameObject);
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
                if(methodInfo[i].Name == methodName)
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

        // these are masks, not bit numbers, ie. 0x004 not 2
        /* public void SetDirtyBit(uint dirtyBit)
         {
             m_SyncVarDirtyBits |= dirtyBit;
         }*/

        /* public void ClearAllDirtyBits()
         {
             m_LastSendTime = Time.time;
             m_syncVarDirtyBits = 0;
         } */

        internal int GetDirtyChannel()
        {
         /*   if (Time.time - m_LastSendTime > GetNetworkSendInterval())
            {
                if (m_syncVarDirtyBits != 0)
                {
                    return GetNetworkChannel();
                }
            }*/
            return -1;
        }

        public virtual bool OnSerialize(NetworkingWriter writer, bool initialState)
        {
            if (!initialState)
            {
                writer.WritePackedUInt32(0);
            }
            return false;
        }

        public virtual void OnDeserialize(NetworkingReader reader, bool initialState)
        {
            if (!initialState)
            {
                reader.ReadPackedUInt32();
            }
        }

        public virtual void OnNetworkDestroy()
        {
        }

#if SERVER
        /// <summary>
        /// Called when the gameObject is attached to a server
        /// </summary>
        /// <param name="networkingServer"></param>
        public virtual void OnStartServer(NetworkingServer networkingServer)
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
