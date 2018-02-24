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
    [ExecuteInEditMode]
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
        [SerializeField] bool m_ServerOnly;
        [SerializeField] bool m_localPlayerAuthority;
        [SerializeField] bool autoSetNetworkBehaviourNetId = true;
        // runtime data
        bool m_hasAuthority;

        internal ushort m_netId; //Gain space with ushort


        [SerializeField]
        NetworkingBehaviour[] m_networkingBehaviours;

        /// <summary>
        /// Connection on server which listen for RPC/Command
        /// </summary>
        public List<NetworkingConnection> m_serverConnectionListeners = new List<NetworkingConnection>();

        internal NetworkingConnection m_serverConnection;
        internal NetworkingConnection m_connection;
        internal NetworkingServer m_server;

        internal bool m_isClient;
        internal bool m_isServer;
        bool m_isLocalConnection;

        // properties
        public bool isClient { get { return m_isClient; } }
        public bool isServer{ get { return m_isServer; } }

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

        public bool isLocalConnection { get { return m_isLocalConnection; }
            set
            {
                if (value == m_isLocalConnection)
                    return;

                m_isLocalConnection = value;

                if (m_isLocalConnection)
                    OnStartLocalConnection();
            }
        }

        public ushort netId { get { return m_netId; } }
        public ushort sceneId { get { return m_sceneId; } }
        public ushort assetID { get { return m_assetId; } }
        public NetworkingConnection connection {get { return m_connection; } }

        public bool serverOnly { get { return m_ServerOnly; } set { m_ServerOnly = value; } }
        public bool localPlayerAuthority { get { return m_localPlayerAuthority; } set { m_localPlayerAuthority = value; } }


        private void Awake()
        {
            s_networkingIdentities.Add(this);
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
            if (m_ServerOnly && m_localPlayerAuthority)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Disabling Local Player Authority for " + gameObject + " because it is server-only."); }
                m_localPlayerAuthority = false;
            }

            m_networkingBehaviours = GetComponentsInChildren<NetworkingBehaviour>();

            if (autoSetNetworkBehaviourNetId)
            {
                for (int i = 0; i < m_networkingBehaviours.Length; i++)
                {
                    m_networkingBehaviours[i].m_netId = (byte)i;
                }
            }
        }
#endif

        internal void OnStartServer(NetworkingServer networkingServer)
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
                b.OnStartServer(networkingServer);
        }

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

        internal void OnServerAddListener(NetworkingConnection conn)
        {
            foreach (NetworkingBehaviour b in this.m_networkingBehaviours)
            {
                try
                {
                    b.OnServerAddListener(conn);
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


        /* internal void OnSetLocalVisibility(bool vis)
         {
             for (int i = 0; i < m_networkingBehaviours.Length; i++)
             {
                 NetworkingBehaviour comp = m_networkingBehaviours[i];
                 try
                 {
                     comp.OnSetLocalVisibility(vis);
                 }
                 catch (Exception e)
                 {
                     Debug.LogError("Exception in OnSetLocalVisibility:" + e.Message + " " + e.StackTrace);
                 }
             }
         }*/

        /*  internal bool OnCheckObserver(NetworkingConnection conn) //Disable for the moment
          {
              for (int i = 0; i < m_networkingBehaviours.Length; i++)
              {
                  NetworkingBehaviour comp = m_networkingBehaviours[i];
                  try
                  {
                      if (!comp.OnCheckObserver(conn))
                          return false;
                  }
                  catch (Exception e)
                  {
                      Debug.LogError("Exception in OnCheckObserver:" + e.Message + " " + e.StackTrace);
                  }
              }
              return true;
          } */

        // vis2k: readstring bug prevention: https://issuetracker.unity3d.com/issues/unet-networkwriter-dot-write-causing-readstring-slash-readbytes-out-of-range-errors-in-clients
        // -> OnSerialize writes length,componentData,length,componentData,...
        // -> OnDeserialize carefully extracts each data, then deserializes each component with separate readers
        //    -> it will be impossible to read too many or too few bytes in OnDeserialize
        //    -> we can properly track down errors
        internal bool OnSerializeSafely(NetworkingBehaviour comp, NetworkingWriter writer, bool initialState)
        {
            // serialize into a temporary writer
            NetworkingWriter temp = new NetworkingWriter();
            bool result = comp.OnSerialize(temp, initialState);
            byte[] bytes = temp.ToArray();
            if (LogFilter.logDebug) { Debug.Log("OnSerializeSafely written for object=" + comp.name + " component=" + comp.GetType() + " sceneId=" + m_sceneId + " length=" + bytes.Length); }

            // serialize length,data into the real writer, untouched by user code
            writer.WriteBytesAndSize(bytes, bytes.Length); // length,data
            return result;
        }

        internal void OnDeserializeAllSafely(NetworkingBehaviour[] components, NetworkingReader reader, bool initialState)
        {
            foreach (var comp in components)
            {
                // extract data length and data safely, untouched by user code
                // -> returns empty array if length is 0, so .Length is always the proper length
                byte[] bytes = reader.ReadBytesAndSize();
                if (LogFilter.logDebug) { Debug.Log("OnDeserializeSafely extracted: " + comp.name + " component=" + comp.GetType() + " sceneId=" + m_sceneId + " length=" + bytes.Length); }

                // call OnDeserialize with a temporary reader, so that the
                // original one can't be messed with. we also wrap it in a
                // try-catch block so there's no way to mess up another
                // component's deserialization
                try
                {
                    comp.OnDeserialize(new NetworkingReader(bytes), initialState);
                }
                catch (Exception e)
                {
                    // show a detailed error and let the user know what went wrong
                    Debug.LogError("OnDeserialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + m_sceneId + " length=" + bytes.Length + ". Possible Reasons:\n  * Do " + comp.GetType() + "'s OnSerialize and OnDeserialize calls write the same amount of data(" + bytes.Length + " bytes)? \n  * Was there an exception in " + comp.GetType() + "'s OnSerialize/OnDeserialize code?\n  * Are the server and client the exact same project?\n  * Maybe this OnDeserialize call was meant for another GameObject? The sceneIds can easily get out of sync if the Hierarchy was modified only in the client OR the server. Try rebuilding both.\n\n" + e.ToString());
                }
            }
        }
        ////////////////////////////////////////////////////////////////////////

        // happens on server
        internal void UNetSerializeAllVars(NetworkingWriter writer)
        {
            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                //comp.OnSerialize(writer, true);
                OnSerializeSafely(comp, writer, true); // vis2k
            }
        }


        //NOT AVAILABLE FOR THE MOMENT
        // helper function for Handle** functions
        /* bool GetInvokeComponent(int cmdHash, Type invokeClass, out NetworkingBehaviour invokeComponent)
         {
             // dont use GetComponent(), already have a list - avoids an allocation
             NetworkingBehaviour foundComp = null;
             for (int i = 0; i < m_networkingBehaviours.Length; i++)
             {
                 var comp = m_networkingBehaviours[i];
                 if (comp.GetType() == invokeClass || comp.GetType().IsSubclassOf(invokeClass))
                 {
                     // found matching class
                     foundComp = comp;
                     break;
                 }
             }
             if (foundComp == null)
             {
                 string errorCmdName = NetworkingBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logError) { Debug.LogError("Found no behaviour for incoming [" + errorCmdName + "] on " + gameObject + ",  the server and client should have the same NetworkBehaviour instances [netId=" + netId + "]."); }
                 invokeComponent = null;
                 return false;
             }
             invokeComponent = foundComp;
             return true;
         }

         // happens on client
         internal void HandleSyncEvent(int cmdHash, NetworkingReader reader)
         {
             // this doesn't use NetworkBehaviour.InvokeSyncEvent function (anymore). this method of calling is faster.
             // The hash is only looked up once, insted of twice(!) per NetworkBehaviour on the object.

             if (gameObject == null)
             {
                 string errorCmdName = NetworkingBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("SyncEvent [" + errorCmdName + "] received for deleted object [netId=" + netId + "]"); }
                 return;
             }

             // find the matching SyncEvent function and networkBehaviour class
             NetworkBehaviour.CmdDelegate invokeFunction;
             Type invokeClass;
             bool invokeFound = NetworkBehaviour.GetInvokerForHashSyncEvent(cmdHash, out invokeClass, out invokeFunction);
             if (!invokeFound)
             {
                 // We don't get a valid lookup of the command name when it doesn't exist...
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logError) { Debug.LogError("Found no receiver for incoming [" + errorCmdName + "] on " + gameObject + ",  the server and client should have the same NetworkBehaviour instances [netId=" + netId + "]."); }
                 return;
             }

             // find the right component to invoke the function on
             NetworkBehaviour invokeComponent;
             if (!GetInvokeComponent(cmdHash, invokeClass, out invokeComponent))
             {
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("SyncEvent [" + errorCmdName + "] handler not found [netId=" + netId + "]"); }
                 return;
             }

             invokeFunction(invokeComponent, reader);

 #if UNITY_EDITOR
             UnityEditor.NetworkDetailStats.IncrementStat(
                 UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                 MsgType.SyncEvent, NetworkBehaviour.GetCmdHashEventName(cmdHash), 1);
 #endif
         }

         // happens on client
         internal void HandleSyncList(int cmdHash, NetworkReader reader)
         {
             // this doesn't use NetworkBehaviour.InvokSyncList function (anymore). this method of calling is faster.
             // The hash is only looked up once, insted of twice(!) per NetworkBehaviour on the object.

             if (gameObject == null)
             {
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("SyncList [" + errorCmdName + "] received for deleted object [netId=" + netId + "]"); }
                 return;
             }

             // find the matching SyncList function and networkBehaviour class
             NetworkBehaviour.CmdDelegate invokeFunction;
             Type invokeClass;
             bool invokeFound = NetworkBehaviour.GetInvokerForHashSyncList(cmdHash, out invokeClass, out invokeFunction);
             if (!invokeFound)
             {
                 // We don't get a valid lookup of the command name when it doesn't exist...
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logError) { Debug.LogError("Found no receiver for incoming [" + errorCmdName + "] on " + gameObject + ",  the server and client should have the same NetworkBehaviour instances [netId=" + netId + "]."); }
                 return;
             }

             // find the right component to invoke the function on
             NetworkBehaviour invokeComponent;
             if (!GetInvokeComponent(cmdHash, invokeClass, out invokeComponent))
             {
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("SyncList [" + errorCmdName + "] handler not found [netId=" + netId + "]"); }
                 return;
             }

             invokeFunction(invokeComponent, reader);

 #if UNITY_EDITOR
             UnityEditor.NetworkDetailStats.IncrementStat(
                 UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                 MsgType.SyncList, NetworkBehaviour.GetCmdHashListName(cmdHash), 1);
 #endif
         }

         // happens on server
         internal void HandleCommand(int cmdHash, NetworkReader reader)
         {
             // this doesn't use NetworkBehaviour.InvokeCommand function (anymore). this method of calling is faster.
             // The hash is only looked up once, insted of twice(!) per NetworkBehaviour on the object.

             if (gameObject == null)
             {
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("Command [" + errorCmdName + "] received for deleted object [netId=" + netId + "]"); }
                 return;
             }

             // find the matching Command function and networkBehaviour class
             NetworkBehaviour.CmdDelegate invokeFunction;
             Type invokeClass;
             bool invokeFound = NetworkBehaviour.GetInvokerForHashCommand(cmdHash, out invokeClass, out invokeFunction);
             if (!invokeFound)
             {
                 // We don't get a valid lookup of the command name when it doesn't exist...
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logError) { Debug.LogError("Found no receiver for incoming [" + errorCmdName + "] on " + gameObject + ",  the server and client should have the same NetworkBehaviour instances [netId=" + netId + "]."); }
                 return;
             }

             // find the right component to invoke the function on
             NetworkBehaviour invokeComponent;
             if (!GetInvokeComponent(cmdHash, invokeClass, out invokeComponent))
             {
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("Command [" + errorCmdName + "] handler not found [netId=" + netId + "]"); }
                 return;
             }

             invokeFunction(invokeComponent, reader);

 #if UNITY_EDITOR
             UnityEditor.NetworkDetailStats.IncrementStat(
                 UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                 MsgType.Command, NetworkBehaviour.GetCmdHashCmdName(cmdHash), 1);
 #endif
         }

         // happens on client
         internal void HandleRPC(int cmdHash, NetworkReader reader)
         {
             // this doesn't use NetworkBehaviour.InvokeClientRpc function (anymore). this method of calling is faster.
             // The hash is only looked up once, insted of twice(!) per NetworkBehaviour on the object.

             if (gameObject == null)
             {
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("ClientRpc [" + errorCmdName + "] received for deleted object [netId=" + netId + "]"); }
                 return;
             }

             // find the matching ClientRpc function and networkBehaviour class
             NetworkBehaviour.CmdDelegate invokeFunction;
             Type invokeClass;
             bool invokeFound = NetworkBehaviour.GetInvokerForHashClientRpc(cmdHash, out invokeClass, out invokeFunction);
             if (!invokeFound)
             {
                 // We don't get a valid lookup of the command name when it doesn't exist...
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logError) { Debug.LogError("Found no receiver for incoming [" + errorCmdName + "] on " + gameObject + ",  the server and client should have the same NetworkBehaviour instances [netId=" + netId + "]."); }
                 return;
             }

             // find the right component to invoke the function on
             NetworkBehaviour invokeComponent;
             if (!GetInvokeComponent(cmdHash, invokeClass, out invokeComponent))
             {
                 string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                 if (LogFilter.logWarn) { Debug.LogWarning("ClientRpc [" + errorCmdName + "] handler not found [netId=" + netId + "]"); }
                 return;
             }

             invokeFunction(invokeComponent, reader);

 #if UNITY_EDITOR
             UnityEditor.NetworkDetailStats.IncrementStat(
                 UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                 MsgType.Rpc, NetworkBehaviour.GetCmdHashRpcName(cmdHash), 1);
 #endif
         }*/

        // invoked by unity runtime immediately after the regular "Update()" function.
        //Ok how i do this ? tss
       /* void Update()
        {
            // check if any behaviours are ready to send
            uint dirtyChannelBits = 0;
            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                int channelId = comp.GetDirtyChannel();
                if (channelId != -1)
                {
                    dirtyChannelBits |= (uint)(1 << channelId);
                }
            }
            if (dirtyChannelBits == 0)
                return;

            for (int channelId = 0; channelId < m_connection.m_server.numChannels; channelId++)
            {
                if ((dirtyChannelBits & (uint)(1 << channelId)) != 0)
                {
                    s_updateWriter.StartMessage();
                    s_updateWriter.Write(NetworkingMessageType.UpdateVars);
                    s_updateWriter.Write(netId);

                    bool wroteData = false;
                    short oldPos;
                    for (int i = 0; i < m_networkingBehaviours.Length; i++)
                    {
                        oldPos = s_updateWriter.Position;
                        NetworkingBehaviour comp = m_networkingBehaviours[i];
                        if (comp.GetDirtyChannel() != channelId)
                        {
                            // component could write more than one dirty-bits, so call the serialize func
                            //comp.OnSerialize(s_UpdateWriter, false);
                            OnSerializeSafely(comp, s_updateWriter, false);
                            continue;
                        }

                        //if (comp.OnSerialize(s_UpdateWriter, false))
                        if (OnSerializeSafely(comp, s_updateWriter, false))
                        {
                            comp.ClearAllDirtyBits();

#if UNITY_EDITOR
                            /*  UnityEditor.NetworkDetailStats.IncrementStat(
                                  UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                                  MsgType.UpdateVars, comp.GetType().Name, 1);
#endif

                            wroteData = true;
                        }
                        if (s_updateWriter.Position - oldPos > m_connection.m_server.packetSize)
                        {
                            if (LogFilter.logWarn) { Debug.LogWarning("Large state update of " + (s_updateWriter.Position - oldPos) + " bytes for netId:" + netId + " from script:" + comp); }
                        }
                    }

                    if (!wroteData)
                    {
                        // nothing to send.. this could be a script with no OnSerialize function setting dirty bits
                        continue;
                    }

                    s_updateWriter.FinishMessage();
                    //m_networkingServer.SendWriterToReady(gameObject, s_updateWriter, channelId);

                    //Only send to ready
                    try
                    {
                        bool success = true;
                        int count = this.observers.Count;
                        for (int i = 0; i < count; i++)
                        {
                            var conn = this.observers[i];
                            if (!conn.isReady)
                                continue;

                            if (!conn.Send(s_updateWriter.AsArraySegment().Array, s_updateWriter.AsArraySegment().Count, channelId))
                            {
                                success = false;
                            }
                        }
                        if (!success)
                        {
                            if (LogFilter.logWarn) { Debug.LogWarning("SendBytesToReady failed for " + this.gameObject); }
                        }
                    }
                    catch (NullReferenceException)
                    {
                        // observers may be null if object has not been spawned
                        if (LogFilter.logWarn) { Debug.LogWarning("SendBytesToReady object " + this.gameObject + " has not been spawned"); }
                    }
                }
            }
        }*/

        internal void OnUpdateVars(NetworkingReader reader, bool initialState)
        {
            //Totally not logic to do that
            /* if (initialState && m_networkingBehaviours == null)
             {
                 m_networkingBehaviours = GetComponents<NetworkBehaviour>();
             }*/

            // vis2k: deserialize safely
            OnDeserializeAllSafely(m_networkingBehaviours, reader, initialState);

            /* old unsafe deserialize code
            for (int i = 0; i < m_NetworkBehaviours.Length; i++)
            {
                NetworkBehaviour comp = m_NetworkBehaviours[i];


#if UNITY_EDITOR
                var oldReadPos = reader.Position;
#endif
                comp.OnDeserialize(reader, initialState);
#if UNITY_EDITOR
                if (reader.Position - oldReadPos > 1)
                {
                    //MakeFloatGizmo("Received Vars " + comp.GetType().Name + " bytes:" + (reader.Position - oldReadPos), Color.white);
                    UnityEditor.NetworkDetailStats.IncrementStat(
                        UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                        MsgType.UpdateVars, comp.GetType().Name, 1);
                }
#endif
            }
            */
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
            m_isLocalConnection = false;
            m_isServer = false;
            m_isClient = false;
            m_hasAuthority = false;
            m_serverConnection = null;
            m_netId = 0;
            m_serverConnectionListeners.Clear();
            m_connection = null;
        }
    };
}
