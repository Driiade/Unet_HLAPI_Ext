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
     //   public static Action<NetworkingIdentity> OnNetworkingIdentityDestroy;

        // configuration
        [SerializeField] internal ushort m_sceneId;
        [SerializeField] internal ushort m_assetId;
        [SerializeField] bool m_ServerOnly;
        [SerializeField] bool m_localPlayerAuthority;
        [SerializeField] bool m_isSceneObject = false;
        public bool destroyOnDisconnect = true;
        public bool destroyOnStop = true;

        // runtime data
        internal bool m_hasAuthority;

        internal ushort m_netId; //Gain place with ushort
        bool m_isLocalPlayer;

        /// <summary>
        /// The server this networkIdentity belong to
        /// </summary>
       // NetworkingServer m_networkingServer;

       // NetworkingConnection m_connectionToServer;
        //NetworkingConnection m_connectionToClient;

        short m_PlayerId = -1;

        [SerializeField]
        NetworkingBehaviour[] m_networkingBehaviours;

        // there is a list AND a hashSet of connections, for fast verification of dupes, but the main operation is iteration over the list.
        HashSet<int> m_observerConnections;
        List<NetworkingConnection> m_observers;
        internal NetworkingConnection m_connection;

        // properties
        public bool isClient { get { return m_connection.m_server == null; } }
        public bool isServer{ get { return m_connection.m_server != null; } }


        public bool hasAuthority { get { return m_hasAuthority; } }

        public ushort netId { get { return m_netId; } }
        public ushort sceneId { get { return m_sceneId; } }
        public ushort assetID { get { return m_assetId; } }
        public NetworkingConnection connection { get { return m_connection; } }

        public bool serverOnly { get { return m_ServerOnly; } set { m_ServerOnly = value; } }
        public bool localPlayerAuthority { get { return m_localPlayerAuthority; } set { m_localPlayerAuthority = value; } }

        public bool isLocalPlayer { get { return m_isLocalPlayer; } }
        public short playerControllerId { get { return m_PlayerId; } }



        internal void HandleMethodCall(NetworkingReader reader)
        {
            byte networkBehaviourIndex = reader.ReadByte();
            m_networkingBehaviours[networkBehaviourIndex].HandleMethodCall(reader);
        }

        // public NetworkingConnection connectionToClient { get { return m_connectionToClient; } } USELESS ?


        //Because we don't work only with one server, don't use static here
        /* static ushort s_lastAssignedNetworkId = 0;
         static internal ushort GetNextNetworkId()
         {
             s_lastAssignedNetworkId++;
             return s_lastAssignedNetworkId;
         }

         static internal void AddNetworkId(ushort id)
         {
             if (id >= s_lastAssignedNetworkId)
             {
                 s_lastAssignedNetworkId = (ushort)(id + 1);
             }
         } */


        static NetworkingWriter s_updateWriter = new NetworkingWriter();

        /*public NetworkHash128 assetId //Shit
        {
            get
            {
#if UNITY_EDITOR
                // This is important because sometimes OnValidate does not run (like when adding view to prefab with no child links)
                if (!m_assetId.IsValid())
                    SetupIDs();
#endif
                return m_assetId;
            }
        }*/




        /* internal void SetDynamicAssetId(NetworkHash128 newAssetId)
         {
             if (!m_assetId.IsValid() || m_assetId.Equals(newAssetId))
             {
                 m_assetId = newAssetId;
             }
             else
             {
                 if (LogFilter.logWarn) { Debug.LogWarning("SetDynamicAssetId object already has an assetId <" + m_assetId + ">"); }
             }
         }*/



        // used when adding players
        /*internal void SetClientOwner(NetworkingConnection conn)
        {
            if (m_connectionAuthorityOwner != null)
            {
                if (LogFilter.logError) { Debug.LogError("SetClientOwner m_ClientAuthorityOwner already set!"); }
            }
            m_connectionAuthorityOwner = conn;
            m_connectionAuthorityOwner.AddOwnedObject(this);
        }*/

        // used during dispose after disconnect
        internal void ClearClientOwner()
        {
         //   m_connectionAuthorityOwner = null;
        }

        internal void ForceAuthority(bool authority)
        {
            if (m_hasAuthority == authority)
            {
                return;
            }

            m_hasAuthority = authority;
            if (authority)
            {
                OnStartAuthority();
            }
            else
            {
                OnStopAuthority();
            }
        }


        public ReadOnlyCollection<NetworkingConnection> observers
        {
            get
            {
                if (m_observers == null)
                    return null;

                return new ReadOnlyCollection<NetworkingConnection>(m_observers);
            }
        }

        //A function for that ? ok ok
        /* void CacheBehaviours()
         {
             if (m_NetworkBehaviours == null)
             {
                 m_NetworkBehaviours = GetComponents<NetworkBehaviour>();
             }
         } */

        /*  public delegate void ClientAuthorityCallback(NetworkingConnection conn, NetworkingIdentity uv, bool authorityState);
          public static ClientAuthorityCallback clientAuthorityCallback;*/



        // only used when fixing duplicate scene IDs duing post-processing
        // Developpers has to do that check.
        /*  public void ForceSceneId(int newSceneId)
          {
              m_sceneId = new NetworkSceneId((uint)newSceneId);
          }*/

        // only used in SetLocalObject
       /* internal void UpdateClientServer(bool isClientFlag, bool isServerFlag)
        {
            m_isClient |= isClientFlag;
            m_isServer |= isServerFlag;
        }*/

        // used when the player object for a connection changes
        internal void SetNotLocalPlayer()
        {
            m_isLocalPlayer = false;

            /*  if (NetworkServer.active && NetworkServer.localClientActive)  WHY ?
              {
                  // dont change authority for objects on the host
                  return;
              } */
            m_hasAuthority = false;
        }

        // this is used when a connection is destroyed, since the "observers" property is read-only
        internal void RemoveObserverInternal(NetworkingConnection conn)
        {
            if (m_observers != null)
            {
                m_observers.Remove(conn);
                m_observerConnections.Remove(conn.m_connectionId);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_ServerOnly && m_localPlayerAuthority)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Disabling Local Player Authority for " + gameObject + " because it is server-only."); }
                m_localPlayerAuthority = false;
            }

            m_networkingBehaviours = GetComponentsInChildren<NetworkingBehaviour>();

            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                m_networkingBehaviours[i].m_netId = (byte)i;
            }

            //SetupIDs();
        }

        /* void AssignAssetID(GameObject prefab)
         {
             string path = AssetDatabase.GetAssetPath(prefab);
             m_AssetId = NetworkHash128.Parse(AssetDatabase.AssetPathToGUID(path));
         }

         bool ThisIsAPrefab()
         {
             PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
             if (prefabType == PrefabType.Prefab)
                 return true;
             return false;
         }

         bool ThisIsASceneObjectWithPrefabParent(out GameObject prefab)
         {
             prefab = null;
             PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
             if (prefabType == PrefabType.None)
                 return false;
             prefab = (GameObject)PrefabUtility.GetPrefabParent(gameObject);
             if (prefab == null)
             {
                 if (LogFilter.logError) { Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]"); }
                 return false;
             }
             return true;
         }

         void SetupIDs()
         {
             GameObject prefab;
             if (ThisIsAPrefab())
             {
                 if (LogFilter.logDev) { Debug.Log("This is a prefab: " + gameObject.name); }
                 ForceSceneId(0);
                 AssignAssetID(gameObject);
             }
             else if (ThisIsASceneObjectWithPrefabParent(out prefab))
             {
                 if (LogFilter.logDev) { Debug.Log("This is a scene object with prefab link: " + gameObject.name); }
                 AssignAssetID(prefab);
             }
             else
             {
                 if (LogFilter.logDev) { Debug.Log("This is a pure scene object: " + gameObject.name); }
                 m_AssetId.Reset();
             }
         }*/

#endif
        /* void OnDestroy()  //If so .. the programmer didn't do his job correctly (sorry :) )
         {
             if (m_IsServer && NetworkServer.active)
             {
                 NetworkServer.Destroy(gameObject);
             }
         }*/

        internal void OnStartServer(NetworkingServer networkingServer)
        {
            //m_networkingServer = networkingServer;

            if (isServer)
            {
                return;
            }

            if (m_localPlayerAuthority)
            {
                // local player on server has NO authority
                m_hasAuthority = false;
            }
            else
            {
                // enemy on server has authority
                m_hasAuthority = true;
            }

            m_observers = new List<NetworkingConnection>();
            m_observerConnections = new HashSet<int>();


            // If the instance/net ID is invalid here then this is an object instantiated from a prefab and the server should assign a valid ID
            if (netId == 0)
            {
                m_netId = networkingServer.GetNextNetworkId();
            }
            else
            {
                if (m_isSceneObject)
                {
                    //allowed
                }
                else
                {
                    if (LogFilter.logError) { Debug.LogError("Object has non-zero netId " + netId + " for " + gameObject); }
                    return;
                }
            }

            if (LogFilter.logDev) { Debug.Log("OnStartServer " + gameObject + " GUID:" + netId); }
            //NetworkServer.instance.SetLocalObjectOnServer(netId, gameObject);
           // UpdateClientServer(false, true);

            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                try
                {
                    comp.OnStartServer(networkingServer);
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStartServer:" + e.Message + " " + e.StackTrace);
                }
            }

            /*if (NetworkClient.active && NetworkServer.localClientActive)
            {
                // there will be no spawn message, so start the client here too
                ClientScene.SetLocalObject(netId, gameObject);
                OnStartClient();
            }*/

            if (m_hasAuthority)
            {
                OnStartAuthority();
            }
        }

        internal void OnStartClient()
        {
           /* if (!m_isClient)
            {
                m_isClient = true;
            }*/
            //CacheBehaviours(); NO NEED

            if (LogFilter.logDev) { Debug.Log("OnStartClient " + gameObject + " GUID:" + netId + " localPlayerAuthority:" + localPlayerAuthority); }
            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                try
                {
                    //comp.PreStartClient(); // generated startup to resolve object references, It only search syncVar/command
                    comp.OnStartClient(); // user implemented startup
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStartClient:" + e.Message + " " + e.StackTrace);
                }
            }
        }

        internal void OnStartAuthority()
        {
            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                try
                {
                    comp.OnStartAuthority();
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStartAuthority:" + e.Message + " " + e.StackTrace);
                }
            }
        }

        internal void OnStopAuthority()
        {
            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                try
                {
                    comp.OnStopAuthority();
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStopAuthority:" + e.Message + " " + e.StackTrace);
                }
            }
        }

        internal void OnSetLocalVisibility(bool vis)
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
        }

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

        // happens on client
        internal void HandleClientAuthority(bool authority)
        {
            if (!localPlayerAuthority)
            {
                if (LogFilter.logError) { Debug.LogError("HandleClientAuthority " + gameObject + " does not have localPlayerAuthority"); }
                return;
            }

            ForceAuthority(authority);
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

        internal void SetLocalPlayer(short localPlayerControllerId)
        {
            m_isLocalPlayer = true;
            m_PlayerId = localPlayerControllerId;

            // there is an ordering issue here that originAuthority solves. OnStartAuthority should only be called if m_HasAuthority was false when this function began,
            // or it will be called twice for this object. But that state is lost by the time OnStartAuthority is called below, so the original value is cached
            // here to be checked below.
            bool originAuthority = m_hasAuthority;
            if (localPlayerAuthority)
            {
                m_hasAuthority = true;
            }

            for (int i = 0; i < m_networkingBehaviours.Length; i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                comp.OnStartLocalPlayer();

                if (localPlayerAuthority && !originAuthority)
                {
                    comp.OnStartAuthority();
                }
            }
        }

       /* internal void SetConnectionToServer(NetworkingConnection conn)
        {
            m_connectionToServer = conn;
        }

        internal void SetConnectionToClient(NetworkingConnection conn, short newPlayerControllerId)
        {
            m_PlayerId = newPlayerControllerId;
            m_connectionToClient = conn;
        }*/

        internal void OnNetworkDestroy()
        {
            for (int i = 0;
                 m_networkingBehaviours != null && i < m_networkingBehaviours.Length;
                 i++)
            {
                NetworkingBehaviour comp = m_networkingBehaviours[i];
                comp.OnNetworkDestroy();
            }
            //m_isServer = false;
        }

        internal void ClearObservers()
        {
            if (m_observers != null)
            {
                /* int count = m_observers.Count;
                 for (int i = 0; i < count; i++)
                 {
                     var c = m_observers[i];
                     c.RemoveFromVisList(this, true);
                 }*/
                m_observers.Clear();
                m_observerConnections.Clear();
            }
        }

        internal void AddObserver(NetworkingConnection conn)
        {
            //What the case ?
            /* if (m_observers == null)
             {
                 if (LogFilter.logError) { Debug.LogError("AddObserver for " + gameObject + " observer list is null"); }
                 return;
             }*/

            // uses hashset for better-than-list-iteration lookup performance.
            /*  if (m_observerConnections.Contains(conn.connectionId))
              {
                  if (LogFilter.logDebug) { Debug.Log("Duplicate observer " + conn.address + " added for " + gameObject); }
                  return;
              } */ //Not an error if you look for...

            if (LogFilter.logDev) { Debug.Log("Added observer " + conn.m_serverAddress + " added for " + gameObject); }

            if (!m_observerConnections.Contains(conn.m_connectionId))
            {
                m_observers.Add(conn);
                m_observerConnections.Add(conn.m_connectionId);
            }
            //conn.AddToVisList(this);
        }

        internal void RemoveObserver(NetworkingConnection conn)
        {
            if (m_observers == null)
                return;

            // NOTE this is linear performance now..
            m_observers.Remove(conn);
            m_observerConnections.Remove(conn.m_connectionId);
            //conn.RemoveFromVisList(this, false);
        }

        //What is the use case ?
        /* public void RebuildObservers(bool initialize)
         {
             //if (m_observers == null)
              //   return; //-_-

             bool changed = false;
             bool result = false;
             HashSet<NetworkConnection> newObservers = new HashSet<NetworkConnection>();
             HashSet<NetworkConnection> oldObservers = new HashSet<NetworkConnection>(m_observers);

             for (int i = 0; i < m_networkingBehaviours.Length; i++)
             {
                 NetworkBehaviour comp = m_networkingBehaviours[i];
                 result |= comp.OnRebuildObservers(newObservers, initialize);
             }
             if (!result)
             {
                 // none of the behaviours rebuilt our observers, use built-in rebuild method
                 if (initialize)
                 {
                     for (int i = 0; i < NetworkServer.connections.Count; i++)
                     {
                         var conn = NetworkServer.connections[i];
                         if (conn == null) continue;
                         if (conn.isReady)
                             AddObserver(conn);
                     }

                     for (int i = 0; i < NetworkServer.localConnections.Count; i++)
                     {
                         var conn = NetworkServer.localConnections[i];
                         if (conn == null) continue;
                         if (conn.isReady)
                             AddObserver(conn);
                     }
                 }
                 return;
             }

             // apply changes from rebuild
             foreach (var conn in newObservers)
             {
                 if (conn == null)
                 {
                     continue;
                 }

                 if (!conn.isReady)
                 {
                     if (LogFilter.logWarn) { Debug.LogWarning("Observer is not ready for " + gameObject + " " + conn); }
                     continue;
                 }

                 if (initialize || !oldObservers.Contains(conn))
                 {
                     // new observer
                     conn.AddToVisList(this);
                     if (LogFilter.logDebug) { Debug.Log("New Observer for " + gameObject + " " + conn); }
                     changed = true;
                 }
             }

             foreach (var conn in oldObservers)
             {
                 if (!newObservers.Contains(conn))
                 {
                     // removed observer
                     conn.RemoveFromVisList(this, false);
                     if (LogFilter.logDebug) { Debug.Log("Removed Observer for " + gameObject + " " + conn); }
                     changed = true;
                 }
             }

             // special case for local client.
             if (initialize)
             {
                 for (int i = 0; i < NetworkServer.localConnections.Count; i++)
                 {
                     if (!newObservers.Contains(NetworkServer.localConnections[i]))
                     {
                         OnSetLocalVisibility(false);
                     }
                 }
             }

             if (!changed)
                 return;

             m_observers = new List<NetworkConnection>(newObservers);

             // rebuild hashset once we have the final set of new observers
             m_observerConnections.Clear();
             for (int i = 0; i < m_observers.Count; i++)
             {
                 m_observerConnections.Add(m_observers[i].connectionId);
             }
         } */

        public bool RemoveClientAuthority(/*NetworkingConnection conn*/)
        {
            if (!isServer)
            {
                if (LogFilter.logError) { Debug.LogError("RemoveClientAuthority can only be call on the server for spawned objects."); }
                return false;
            }

            //One Player for a client is a bad design (sorry ^^ )
            /* if (connectionToClient != null)
             {
                 if (LogFilter.logError) { Debug.LogError("RemoveClientAuthority cannot remove authority for a player object"); }
                 return false;
             } */

          /*  if (m_connectionAuthorityOwner == null)
            {
                if (LogFilter.logError) { Debug.LogError("RemoveClientAuthority for " + gameObject + " has no clientAuthority owner."); }
                return false;
            } */

            /*  if (m_clientAuthorityOwner != conn)
              {
                  if (LogFilter.logError) { Debug.LogError("RemoveClientAuthority for " + gameObject + " has different owner."); }
                  return false;
              }*/

            //m_connectionAuthorityOwner.RemoveOwnedObject(this);
            //m_connectionAuthorityOwner = null;

            // server now has authority (this is only called on server)
            ForceAuthority(true);

            // send msg to that client
            var msg = new NetIdMessage();
            msg.m_netId = netId;

            //m_connectionAuthorityOwner.Send(NetworkingMessageType.UnassignClientAuthority, msg);

            /*  if (clientAuthorityCallback != null)
              {
                  clientAuthorityCallback(conn, this, false);
              } */
            return true;
        }

        public bool AssignClientAuthority(NetworkingConnection conn)
        {
            if (!isServer)
            {
                if (LogFilter.logError) { Debug.LogError("AssignClientAuthority can only be call on the server for spawned objects."); }
                return false;
            }
            if (!localPlayerAuthority)
            {
                if (LogFilter.logError) { Debug.LogError("AssignClientAuthority can only be used for NetworkingIdentity component with LocalPlayerAuthority set."); }
                return false;
            }

            //Nope, do this automatically
            /* if (m_clientAuthorityOwner != null && conn != m_clientAuthorityOwner)
             {
                 if (LogFilter.logError) { Debug.LogError("AssignClientAuthority for " + gameObject + " already has an owner. Use RemoveClientAuthority() first."); }
                 return false;
             }*/

            if (conn == null)
            {
                if (LogFilter.logError) { Debug.LogError("AssignClientAuthority for " + gameObject + " owner cannot be null. Use RemoveClientAuthority() instead."); }
                return false;
            }

            /*if (m_connectionAuthorityOwner != null && conn != m_connectionAuthorityOwner)
            {
                RemoveClientAuthority();
            }*/


            //m_connectionAuthorityOwner = conn;
            //m_connectionAuthorityOwner.AddOwnedObject(this);

            // server no longer has authority (this is called on server). Note that local client could re-acquire authority below
            ForceAuthority(false);

            // send msg to that client
            var msg = new NetIdMessage();
            msg.m_netId = netId;
            conn.Send(msg.m_type = NetworkingMessageType.AssignClientAuthority, msg);

            /* if (clientAuthorityCallback != null)
             {
                 clientAuthorityCallback(conn, this, true);
             }*/
            return true;
        }

        // marks the identity for future reset, this is because we cant reset the identity during destroy
        // as people might want to be able to read the members inside OnDestroy(), and we have no way
        // of invoking reset after OnDestroy is called.
        /* internal void MarkForReset()
         {
             m_Reset = true;
         }*/

        // if we have marked an identity for reset we do the actual reset.
        void Reset()
        {
            //  if (!m_Reset)
            //   return;

            // m_Reset = false;
          //  m_isServer = false;
           // m_isClient = false;
            m_hasAuthority = false;

            m_netId = 0;// NetworkInstanceId.Zero;
            m_isLocalPlayer = false;
            //m_connectionToServer = null;
            //m_connectionToClient = null;
            m_PlayerId = -1;
            m_networkingBehaviours = null;

            ClearObservers();
            //m_connectionAuthorityOwner = null;
            m_connection = null;
        }

#if UNITY_EDITOR
        // this is invoked by the UnityEngine when a Mono Domain reload happens in the editor.
        // the transport layer has state in C++, so when the C# state is lost (on domain reload), the C++ transport layer must be shutown as well.
        /*  static internal void UNetDomainReload()
          {
              NetworkManager.OnDomainReload(); /// ??? No
          } */

#endif

        // this is invoked by the UnityEngine (So cool, so we can't modify it ! nice Unity)

        /*   static internal void UNetStaticUpdate()
           {
               NetworkServer.Update();
               NetworkClient.UpdateClients();
               NetworkManager.UpdateScene();

   #if UNITY_EDITOR
               NetworkDetailStats.NewProfilerTick(Time.time); 
   #endif
           } */
    };
}
