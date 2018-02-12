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
        public bool isLocalPlayer { get { return m_networkingIdentity.isLocalPlayer; } }
        public bool hasAuthority { get { return m_networkingIdentity.hasAuthority; } }

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

        public void SendToAllConnections(string methodName, int channelId, params object[] parameters)
        {
            writer.SeekZero(true);
            writer.StartMessage();
            writer.Write(NetworkingMessageType.Rpc);
            SerializeCall(writer, methodName, parameters);
            writer.FinishMessage();

            server.SendToAll(writer, channelId);
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
            writer.Write((byte)this.networkingIdentity.m_type);
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


        // ----------------------------- Commands --------------------------------

      /*  [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendCommandInternal(NetworkWriter writer, int channelId, string cmdName)
        {
            // local players can always send commands, regardless of authority, other objects must have authority.
            if (!(isLocalPlayer || hasAuthority))
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Trying to send command for object without authority."); }
                return;
            }

            if (ClientScene.readyConnection == null)
            {
                if (LogFilter.logError) { Debug.LogError("Send command attempted with no client running [client=" + connectionToServer + "]."); }
                return;
            }

            writer.FinishMessage();
            ClientScene.readyConnection.SendWriter(writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.Command, cmdName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeCommand(int cmdHash, NetworkReader reader)
        {
            if (InvokeCommandDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Client RPCs --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendRPCInternal(NetworkWriter writer, int channelId, string rpcName)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ClientRpc call on un-spawned object"); }
                return;
            }

            writer.FinishMessage();
            NetworkServer.SendWriterToReady(gameObject, writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.Rpc, rpcName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendTargetRPCInternal(NetworkConnection conn, NetworkWriter writer, int channelId, string rpcName)
        {
            // This cannot use NetworkServer.active, as that is not specific to this object.
            if (!isServer)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("TargetRpc call on un-spawned object"); }
                return;
            }

            writer.FinishMessage();

            conn.SendWriter(writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.Rpc, rpcName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeRPC(int cmdHash, NetworkReader reader)
        {
            if (InvokeRpcDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Sync Events --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SendEventInternal(NetworkWriter writer, int channelId, string eventName)
        {
            if (!NetworkServer.active)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("SendEvent no server?"); }
                return;
            }

            writer.FinishMessage();
            NetworkServer.SendWriterToReady(gameObject, writer, channelId);

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.SyncEvent, eventName, 1);
#endif
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncEvent(int cmdHash, NetworkReader reader)
        {
            if (InvokeSyncEventDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Sync Lists --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool InvokeSyncList(int cmdHash, NetworkReader reader)
        {
            if (InvokeSyncListDelegate(cmdHash, reader))
            {
                return true;
            }
            return false;
        }

        // ----------------------------- Code Gen Path Helpers  --------------------------------

        public delegate void CmdDelegate(NetworkBehaviour obj, NetworkReader reader);
        protected delegate void EventDelegate(List<Delegate> targets, NetworkReader reader);

        protected enum UNetInvokeType
        {
            Command,
            ClientRpc,
            SyncEvent,
            SyncList
        };

        protected class Invoker
        {
            public UNetInvokeType invokeType;
            public Type invokeClass;
            public CmdDelegate invokeFunction;

            public string DebugString()
            {
                return invokeType + ":" +
                    invokeClass + ":" +
                    invokeFunction.GetMethodName();
            }
        };

        static Dictionary<int, Invoker> s_CmdHandlerDelegates = new Dictionary<int, Invoker>();

        [EditorBrowsable(EditorBrowsableState.Never)]
        static protected void RegisterCommandDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.Command;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterCommandDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        static protected void RegisterRpcDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.ClientRpc;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterRpcDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        static protected void RegisterEventDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.SyncEvent;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterEventDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        static protected void RegisterSyncListDelegate(Type invokeClass, int cmdHash, CmdDelegate func)
        {
            if (s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return;
            }
            Invoker inv = new Invoker();
            inv.invokeType = UNetInvokeType.SyncList;
            inv.invokeClass = invokeClass;
            inv.invokeFunction = func;
            s_CmdHandlerDelegates[cmdHash] = inv;
            if (LogFilter.logDev) { Debug.Log("RegisterSyncListDelegate hash:" + cmdHash + " " + func.GetMethodName()); }
        }

        internal static string GetInvoker(int cmdHash)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return null;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            return inv.DebugString();
        }

        // wrapper fucntions for each type of network operation
        internal static bool GetInvokerForHashCommand(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.Command, out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashClientRpc(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.ClientRpc, out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashSyncList(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.SyncList, out invokeClass, out invokeFunction);
        }

        internal static bool GetInvokerForHashSyncEvent(int cmdHash, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            return GetInvokerForHash(cmdHash, UNetInvokeType.SyncEvent, out invokeClass, out invokeFunction);
        }

        static bool GetInvokerForHash(int cmdHash, NetworkBehaviour.UNetInvokeType invokeType, out Type invokeClass, out CmdDelegate invokeFunction)
        {
            Invoker invoker = null;
            if (!s_CmdHandlerDelegates.TryGetValue(cmdHash, out invoker))
            {
                if (LogFilter.logDev) { Debug.Log("GetInvokerForHash hash:" + cmdHash + " not found"); }
                invokeClass = null;
                invokeFunction = null;
                return false;
            }

            if (invoker == null)
            {
                if (LogFilter.logDev) { Debug.Log("GetInvokerForHash hash:" + cmdHash + " invoker null"); }
                invokeClass = null;
                invokeFunction = null;
                return false;
            }

            if (invoker.invokeType != invokeType)
            {
                if (LogFilter.logError) { Debug.LogError("GetInvokerForHash hash:" + cmdHash + " mismatched invokeType"); }
                invokeClass = null;
                invokeFunction = null;
                return false;
            }

            invokeClass = invoker.invokeClass;
            invokeFunction = invoker.invokeFunction;
            return true;
        }

        internal static void DumpInvokers()
        {
            Debug.Log("DumpInvokers size:" + s_CmdHandlerDelegates.Count);
            foreach (var inv in s_CmdHandlerDelegates)
            {
                Debug.Log("  Invoker:" + inv.Value.invokeClass + ":" + inv.Value.invokeFunction.GetMethodName() + " " + inv.Value.invokeType + " " + inv.Key);
            }
        }

        internal bool ContainsCommandDelegate(int cmdHash)
        {
            return s_CmdHandlerDelegates.ContainsKey(cmdHash);
        }

        internal bool InvokeCommandDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.Command)
            {
                return false;
            }

            if (GetType() != inv.invokeClass)
            {
                if (GetType().IsSubclassOf(inv.invokeClass))
                {
                    // allowed, commands function is on a base class.
                }
                else
                {
                    return false;
                }
            }

            inv.invokeFunction(this, reader);
            return true;
        }

        internal bool InvokeRpcDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.ClientRpc)
            {
                return false;
            }

            if (GetType() != inv.invokeClass)
            {
                if (GetType().IsSubclassOf(inv.invokeClass))
                {
                    // allowed, rpc function is on a base class.
                }
                else
                {
                    return false;
                }
            }

            inv.invokeFunction(this, reader);
            return true;
        }

        internal bool InvokeSyncEventDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.SyncEvent)
            {
                return false;
            }

            inv.invokeFunction(this, reader);
            return true;
        }

        internal bool InvokeSyncListDelegate(int cmdHash, NetworkReader reader)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return false;
            }

            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            if (inv.invokeType != UNetInvokeType.SyncList)
            {
                return false;
            }

            if (GetType() != inv.invokeClass)
            {
                return false;
            }

            inv.invokeFunction(this, reader);
            return true;
        }

        static internal string GetCmdHashHandlerName(int cmdHash)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return cmdHash.ToString();
            }
            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            return inv.invokeType + ":" + inv.invokeFunction.GetMethodName();
        }

        static string GetCmdHashPrefixName(int cmdHash, string prefix)
        {
            if (!s_CmdHandlerDelegates.ContainsKey(cmdHash))
            {
                return cmdHash.ToString();
            }
            Invoker inv = s_CmdHandlerDelegates[cmdHash];
            var name = inv.invokeFunction.GetMethodName();

            int index = name.IndexOf(prefix);
            if (index > -1)
            {
                name = name.Substring(prefix.Length);
            }
            return name;
        }

        internal static string GetCmdHashCmdName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeCmd");
        }

        internal static string GetCmdHashRpcName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeRpc");
        }

        internal static string GetCmdHashEventName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeSyncEvent");
        }

        internal static string GetCmdHashListName(int cmdHash)
        {
            return GetCmdHashPrefixName(cmdHash, "InvokeSyncList");
        }

        // ----------------------------- Helpers  --------------------------------

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, uint dirtyBit, ref NetworkInstanceId netIdField)
        {
            if (m_SyncVarGuard)
                return;

            NetworkInstanceId newGameObjectNetId = new NetworkInstanceId();
            if (newGameObject != null)
            {
                var uv = newGameObject.GetComponent<NetworkIdentity>();
                if (uv != null)
                {
                    newGameObjectNetId = uv.netId;
                    if (newGameObjectNetId.IsEmpty())
                    {
                        if (LogFilter.logWarn) { Debug.LogWarning("SetSyncVarGameObject GameObject " + newGameObject + " has a zero netId. Maybe it is not spawned yet?"); }
                    }
                }
            }

            NetworkInstanceId oldGameObjectNetId = new NetworkInstanceId();
            if (gameObjectField != null)
            {
                oldGameObjectNetId = gameObjectField.GetComponent<NetworkIdentity>().netId;
            }

            if (newGameObjectNetId != oldGameObjectNetId)
            {
                if (LogFilter.logDev) { Debug.Log("SetSyncVar GameObject " + GetType().Name + " bit [" + dirtyBit + "] netfieldId:" + oldGameObjectNetId + "->" + newGameObjectNetId); }
                SetDirtyBit(dirtyBit);
                gameObjectField = newGameObject;
                netIdField = newGameObjectNetId;
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected void SetSyncVar<T>(T value, ref T fieldValue, uint dirtyBit)
        {
            bool changed = false;
            if (value == null)
            {
                if (fieldValue != null)
                    changed = true;
            }
            else
            {
                changed = !value.Equals(fieldValue);
            }
            if (changed)
            {
                if (LogFilter.logDev) { Debug.Log("SetSyncVar " + GetType().Name + " bit [" + dirtyBit + "] " + fieldValue + "->" + value); }
                SetDirtyBit(dirtyBit);
                fieldValue = value;
            }
        }

        // these are masks, not bit numbers, ie. 0x004 not 2
        public void SetDirtyBit(uint dirtyBit)
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

        public virtual void OnStartServer(NetworkingServer networkingServer)
        {
        }

        public virtual void OnStartClient()
        {
        }

        public virtual void OnStartLocalPlayer()
        {
        }

        public virtual void OnStartAuthority()
        {
        }

        public virtual void OnStopAuthority()
        {
        }

        public virtual bool OnRebuildObservers(HashSet<NetworkingConnection> observers, bool initialize)
        {
            return false;
        } 

        public virtual void OnSetLocalVisibility(bool vis)
        {
        }

        public virtual bool OnCheckObserver(NetworkingConnection conn)
        {
            return true;
        }

        public virtual int GetNetworkChannel()
        {
            return NetworkingMessageType.Channels.DefaultReliableSequenced;
        }

      /*  public virtual float GetNetworkSendInterval()
        {
            return k_DefaultSendInterval;
        } */
    }
}
