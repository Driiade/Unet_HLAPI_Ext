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
        uint m_syncVarDirtyBits;
        float m_LastSendTime;

        // this prevents recursion when SyncVar hook functions are called.
        bool m_SyncVarGuard;

        public bool localPlayerAuthority { get { return m_networkingIdentity.localPlayerAuthority; } }
        public bool isServer { get { return m_networkingIdentity.isServer; } }
        public bool isClient { get { return m_networkingIdentity.isClient; } }
        public bool hasAuthority { get { return m_networkingIdentity.hasAuthority; } }

        public NetworkingConnection serverConnection { get { return m_networkingIdentity.m_serverConnection; } }
        public NetworkingConnection connection { get { return m_networkingIdentity.m_connection; } }
        public NetworkingServer server { get { return m_networkingIdentity.m_server; } }

        //public short playerControllerId { get { return m_networkingIdentity.playerControllerId; } }
        protected uint syncVarDirtyBits { get { return m_syncVarDirtyBits; } }
        protected bool syncVarHookGuard { get { return m_SyncVarGuard; } set { m_SyncVarGuard = value; } }

        [SerializeField]
        internal byte m_netId;

       // const float k_DefaultSendInterval = 0.1f;

        private NetworkingIdentity m_networkingIdentity;
        public NetworkingIdentity networkingIdentity { get { return m_networkingIdentity; } }

        private MethodInfo[] networkedMethods;
        private NetworkingWriter writer = new NetworkingWriter();

        protected virtual void Awake()
        {
            m_networkingIdentity = this.GetComponentInParent<NetworkingIdentity>();
            networkedMethods = GetNetworkedMethods();
        }

        public void SendToServer(string methodName, int channelId,params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.Command);
            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            connection.Send(writer, channelId);
        }

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

            foreach(NetworkingConnection connection in m_networkingIdentity.m_serverConnectionListeners)
            {
                connection.Send(writer, channelId);
            }
        }

        public void SendToConnection(NetworkingConnection conn,string methodName, int channelId, params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.Rpc);
            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            server.SendTo(conn.m_connectionId, writer, channelId);
        }

        public void AutoSendToConnections(string methodName, int channelId, params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.AutoRpc);
            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            connection.Send(writer, channelId);
        }

        void SerializeCall(NetworkingWriter writer, string methodName, object[] parameters)
        {
            writer.Write(this.networkingIdentity.netId);
            writer.Write(this.m_netId);
            writer.Write(GetNetworkMethodIndex(methodName, networkedMethods));
            //Debug.Log(methodName + " : " + GetNetworkMethodIndex(methodName, networkedMethods));
            for (int i = 0; i < parameters.Length; i++)
            {
                object param = parameters[i];

                if (param is int)
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
                    writer.WriteBytesAndSize(((byte[])param), ((byte[])param).Length);
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
                if (info.ParameterType == typeof(int))
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
                else
                    throw new System.Exception("UnSerialization is impossible : " + info.ParameterType.GetType());
            }

            method.Invoke(this, parameters);
        }


        MethodInfo[] GetNetworkedMethods()
        {
            MethodInfo[] allMethods = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            List<MethodInfo> networkedMethods = new List<MethodInfo>();
            for (int i = 0; i < allMethods.Length; i++)
            {
                object[] attributes = allMethods[i].GetCustomAttributes(typeof(Networked),true);
                for (int j = 0; j < attributes.Length; j++)
                {
                    if(attributes[j] is Networked)
                    {
                        networkedMethods.Add(allMethods[i]);
                    }
                }
            }

            return networkedMethods.ToArray();
        }

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



     

        // these are masks, not bit numbers, ie. 0x004 not 2
       /* public void SetDirtyBit(uint dirtyBit)
        {
            m_SyncVarDirtyBits |= dirtyBit;
        }*/

        public void ClearAllDirtyBits()
        {
            m_LastSendTime = Time.time;
            m_syncVarDirtyBits = 0;
        } 

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

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void PreStartClient()
        {
        }

        public virtual void OnNetworkDestroy()
        {
        }

        /// <summary>
        /// Called when the gameObject is attached to a server
        /// </summary>
        /// <param name="networkingServer"></param>
        public virtual void OnStartServer(NetworkingServer networkingServer)
        {
        }

        /// <summary>
        /// Called when the gameObject is attached to a connection
        /// </summary>
        public virtual void OnStartConnection()
        {
        }

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

        /// <summary>
        /// Called on server when a new connection is added
        /// Can be called if connection don't know the gameObject.
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerConnect(NetworkingConnection conn)
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
        /// Called on the server when it is syncing the netId of an object with a connection
        /// </summary>
        /// <param name="conn"></param>
        public virtual void OnServerSyncNetId(NetworkingConnection conn)
        {

        }

        public virtual int GetNetworkChannel()
        {
            return NetworkingChannel.DefaultReliableSequenced;
        }

      /*  public virtual float GetNetworkSendInterval()
        {
            return k_DefaultSendInterval;
        } */
    }
}
