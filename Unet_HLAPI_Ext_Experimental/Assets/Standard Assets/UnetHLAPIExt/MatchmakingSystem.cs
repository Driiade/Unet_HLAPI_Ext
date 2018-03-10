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


using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.Networking.Match;
using UnityEngine.Networking.Types;
using System;
using UnityEngine.SceneManagement;
using System.Collections.Generic;


namespace BC_Solution.UnetNetwork
{
    public class MatchmakingSystem : Singleton<MatchmakingSystem>
    {

        [SerializeField]
        [Tooltip("The version of your matchmaking, use it to separate match of different game version")]
        int version;


        [Tooltip("The matchmaking server adress (By default : Unet)")]
        public string matchAdress = "https://mm.unet.unity3d.com";


        [Tooltip("The matchmaking port adress (By default : Unet)")]
        public int matchPort = 443;


        [Tooltip("The max time to join match between the relaunch of the matchmaking")]
        public float maxTimeRelaunchMatchmakingAutomatically = 5f;


        [Space(20)]
        [SerializeField]
        int m_broadcastPort = 47777;

        [SerializeField]
        int m_broadcastKey = 2222;

        [SerializeField]
        int m_broadcastVersion = 1;

        [SerializeField]
        int m_broadcastSubVersion = 1;

#if SERVER
        [SerializeField]
        int m_broadcastInterval = 1000;
#endif

        [SerializeField]
        float timeToRemoveALanMatchFromList = 2f;


        /// <summary>
        /// Called when a match is joined
        /// </summary>
        public static NetworkMatch.DataResponseDelegate<MatchInfo> OnMatchJoined;

        /// <summary>
        /// Called when a match is created
        /// </summary>
        public static NetworkMatch.DataResponseDelegate<MatchInfo> OnMatchCreated;

        /// <summary>
        /// Called when a match is locked
        /// </summary>
        public static NetworkMatch.BasicResponseDelegate OnHideServers;


        public static Action<string, string> OnReceivedBroadcast;


        /// <summary>
        /// True if Matchmaking is searching match on server or ceating a new one.
        /// </summary>
        static bool isWaitingResponse = false;
        public static bool IsWaitingResponse
        {
            get { return isWaitingResponse; }
        }


        private NetworkMatch matchMaker;


        /// <summary>
        /// List online match with a coroutine
        /// </summary>
        public bool ListOnlineMatch
        {
            get { return listOnlineMatch; }
        }
        bool listOnlineMatch = false;

        /// <summary>
        /// List lan match with a coroutine
        /// </summary>
        public bool ListLanMatch
        {
            get { return listLANMatch; }
        }

        bool listLANMatch = false;
        public bool ListMatch
        {
            get { return listOnlineMatch || listLANMatch; }
        }


        static bool isOnAutomatch = false;
        static public bool MatchIsAnAutomatch
        {
            get { return isOnAutomatch; }
        }

        [HideInInspector]
        public List<MatchInfoSnapshot> m_onlineMatchAvailables = new List<MatchInfoSnapshot>();

        ulong currentMatchNetworkId = (ulong)NetworkID.Invalid;

        List<ulong> bannedMatchsList = new List<ulong>(); //Banned match because no host

        Coroutine relaunchMatchmakingCoroutine;

        MatchInfoSnapshot currentMatch = null;

        Coroutine listMatchCoroutine = null;


#region LAN

        public class LanMatchInfo
        {
            public string name = "";
            public string serverAdress = "";
            public int serverPort = -1;
            public float timer = -1;

            public LanMatchInfo(string _serverName, string _serverAdress, int _serverPort, float timeToRemoveALanMatchFromList)
            {
                name = _serverName;
                serverAdress = _serverAdress;
                serverPort = _serverPort;
                timer = Time.time + timeToRemoveALanMatchFromList;
            }
        }

        public List<LanMatchInfo> m_LANMatchsAvailables = new List<LanMatchInfo>();

        const int k_MaxBroadcastMsgSize = 1024;

        HostTopology m_LANTopology;
        int m_LANHostId = -1;

        byte[] m_msgOutBuffer;
        byte[] m_msgInBuffer;

#endregion

        protected override void Awake()
        {
            base.Awake();

            relaunchMatchmakingCoroutine = null;

            matchMaker = gameObject.AddComponent<NetworkMatch>();

            matchMaker.baseUri = new System.Uri(matchAdress);

            NetworkingConnection.OnConnectionConnect += StopRelaunchingMatchmaking;
            NetworkingConnection.OnConnectionDisconnect += Reset;
            NetworkingSystem.OnStopAllConnections += Reset;

#if SERVER
            NetworkingServer.OnStopServer += Reset;
            NetworkingServer.OnStopServer += OnStopServer;
#endif
        }


        public void OnDestroy()
        {
            NetworkingConnection.OnConnectionConnect -= StopRelaunchingMatchmaking;
            NetworkingConnection.OnConnectionDisconnect -= Reset;
            NetworkingSystem.OnStopAllConnections -= Reset;

#if SERVER
            NetworkingServer.OnStopServer -= OnStopServer;
            NetworkingServer.OnStopServer -= Reset;
#endif
        }

#if CLIENT
        /// <summary>
        /// Connect to the match
        /// </summary>
        /// <param name="matchInfo"></param>
        public void ConnectToOnlineMatch(MatchInfoSnapshot matchInfo, string password = "", NetworkMatch.DataResponseDelegate<MatchInfo> callback = null)
        {
            callback += InternalOnOnlineMatchJoined;

            currentMatch = matchInfo;

            isWaitingResponse = true;

            if (matchInfo.name.StartsWith("AutoMatch_"))
                isOnAutomatch = true;

            matchMaker.JoinMatch(matchInfo.networkId, password, "", "", 0, version, callback);
        }

        public void ConnectToLANMatch(string serverAdress, int serverPort)
        {
            NetworkingSystem.Instance.StartConnection(serverAdress, serverPort);

            if (OnMatchJoined != null)
                OnMatchJoined(true, "", null);
        }
#endif

#if SERVER
        /// <summary>
        /// Create a match
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password"></param>
        /// <param name="visible"></param>
        public void CreatMainOnlineMatch(string name, string password, bool visible = true)
        {
            isWaitingResponse = true;
            matchMaker.CreateMatch(name, NetworkingSystem.Instance.maxPlayer, visible, password, "", "", 0, version, InternalOnMainOnlineMatchCreated);
        }
#endif

#if SERVER
        /// <summary>
        /// Create a LAN main server/host
        /// </summary>
        /// <param name="name"></param>
        /// <param name="serverAdress"></param>
        /// <param name="serverPort"></param>
        public bool CreateMainLANServer(string name,string serverAdress, int serverPort)
        {
            NetworkingServer server;
            server = NetworkingSystem.Instance.StartMainServer(serverAdress,serverPort);


            if (!ShowServerOnLan(server))
            {
                Debug.LogError("Error on Start Broadcast Discovery");
                m_LANHostId = -1;

                NetworkingSystem.Instance.StopServer(NetworkingSystem.Instance.mainServer);

                return false;
            }
            else
            {
                if (OnMatchCreated != null)
                    OnMatchCreated(true, "", null);
            }

            return true;
        }
#endif

#if SERVER
        public bool ShowServerOnLan(NetworkingServer server)
        {
            string m_broadcastData = "NetworkManager:" + server.m_serverAddress + ":" + server.m_serverPort + ": MatchInfo :" + name;

            m_msgOutBuffer = StringToBytes(m_broadcastData);

            m_LANTopology = new HostTopology(NetworkingSystem.Instance.Configuration(), (int)NetworkingSystem.Instance.maxPlayer);
            m_LANHostId = NetworkTransport.AddHost(m_LANTopology, 0);

            if (m_LANHostId == -1)
            {
                Debug.LogError("LANHostId == -1, not accepted");
                return false;
            }

            byte err;

            if (!NetworkTransport.StartBroadcastDiscovery(m_LANHostId, m_broadcastPort, m_broadcastKey, m_broadcastVersion, m_broadcastSubVersion, m_msgOutBuffer, m_msgOutBuffer.Length, m_broadcastInterval, out err))
            {
                NetworkTransport.RemoveHost(m_LANHostId);
                return false;
            }


            return true;
        }
#endif


        public void HideServers()
        {
            if (currentMatchNetworkId != (ulong)NetworkID.Invalid)
            {
                matchMaker.SetMatchAttributes((NetworkID)currentMatchNetworkId, false, version, OnHideServers);
                currentMatchNetworkId = (ulong)NetworkID.Invalid;
            }
            else if (m_LANHostId != -1)
            {
                NetworkTransport.StopBroadcastDiscovery();
                NetworkTransport.RemoveHost(m_LANHostId);

                if (OnHideServers != null)
                    OnHideServers(true, "");
            }
        }

#if SERVER
        /// <summary>
        /// For the moment we can have only one broadcast, so server param is useless
        /// TODO : 1 broadcast for 1 server
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public bool IsShowingOnLAN(NetworkingServer server)
        {
            return NetworkTransport.IsBroadcastDiscoveryRunning();
        }
#endif

        /// <summary>
        /// Start to list match. Launch a coroutine for that.
        /// </summary>
        /// <param name="withPrivateMatch"></param>
        /// <param name="maxPageSize"></param>
        public void StartListOnlineMatch(bool withPrivateMatch = true, int maxPageSize = 10)
        {
            listOnlineMatch = true;
            listMatchCoroutine = StartCoroutine(ListOnlineMatchCoroutine(withPrivateMatch, maxPageSize));
        }


        public void StartListLANMatch()
        {
            listLANMatch = true;
            listMatchCoroutine = StartCoroutine(ListLANMatchCoroutine());
        }


        /// <summary>
        /// Stop listening for match. Stop a coroutine for that.
        /// </summary>
        public void StopListMatch()
        {
            if (ListMatch)
            {
                StopCoroutine(listMatchCoroutine);
                listOnlineMatch = false;
                listLANMatch = false;

                if (m_LANHostId != -1)
                {
                    NetworkTransport.RemoveHost(m_LANHostId);
                    m_LANHostId = -1;
                }
            }
        }

        /// <summary>
        /// Search match and fill m_matcheAvailables
        /// </summary>
        /// <param name="withPrivateMatch"></param>
        /// <param name="maxPageSize"></param>
        IEnumerator ListOnlineMatchCoroutine(bool withPrivateMatch = true, int maxPageSize = 10)
        {
            while (listOnlineMatch)
            {
                yield return matchMaker.ListMatches(0, maxPageSize, "", !withPrivateMatch, 0, version, ListMatchResponse);
            }
        }


        IEnumerator ListLANMatchCoroutine()
        {
            if (!NetworkTransport.IsStarted)
            {
                NetworkTransport.Init();
            }

            if (m_LANHostId != -1)
            {
                NetworkTransport.RemoveHost(m_LANHostId);
                m_LANHostId = -1;
            }

            m_msgInBuffer = new byte[k_MaxBroadcastMsgSize];

            m_LANTopology = new HostTopology(NetworkingSystem.Instance.Configuration(), (int)NetworkingSystem.Instance.maxPlayer);
            m_LANHostId = NetworkTransport.AddHost(m_LANTopology, m_broadcastPort);

            byte error;
            NetworkTransport.SetBroadcastCredentials(m_LANHostId, m_broadcastKey, m_broadcastVersion, m_broadcastSubVersion, out error);

            while (listLANMatch)
            {
                NetworkEventType networkEvent;
                do
                {
                    int connectionId;
                    int channelId;
                    int receivedSize;

                    networkEvent = NetworkTransport.ReceiveFromHost(m_LANHostId, out connectionId, out channelId, m_msgInBuffer, k_MaxBroadcastMsgSize, out receivedSize, out error);

                    if (networkEvent == NetworkEventType.BroadcastEvent)
                    {
                        NetworkTransport.GetBroadcastConnectionMessage(m_LANHostId, m_msgInBuffer, k_MaxBroadcastMsgSize, out receivedSize, out error);

                        string senderAddr = "";
                        int senderPort = 0;
                        NetworkTransport.GetBroadcastConnectionInfo(m_LANHostId, out senderAddr, out senderPort, out error);

                        var recv = new NetworkBroadcastResult();
                        recv.serverAddress = senderAddr;
                        recv.broadcastData = new byte[receivedSize];
                        Buffer.BlockCopy(m_msgInBuffer, 0, recv.broadcastData, 0, receivedSize);

                        string serverBroadcastInfo = BytesToString(m_msgInBuffer);

                        InternalOnReceivedBroadcast(senderAddr, serverBroadcastInfo);

                        string[] serverInfos = serverBroadcastInfo.Split(':');

                        int serverPort = Convert.ToInt32(serverInfos[2]);
                        LanMatchInfo lanMatchInfo = m_LANMatchsAvailables.Find((LanMatchInfo x) => { return x.serverAdress.Equals(senderAddr) && x.serverPort == serverPort; });

                        //3 correspond to server name
                        if (lanMatchInfo == null)
                        {
                            m_LANMatchsAvailables.Add(new LanMatchInfo(serverInfos[4], senderAddr, serverPort, timeToRemoveALanMatchFromList));
                        }
                        else
                        {
                            lanMatchInfo.timer = Time.time + timeToRemoveALanMatchFromList;
                        }
                    }
                }
                while (networkEvent != NetworkEventType.Nothing);


                for (int i = m_LANMatchsAvailables.Count - 1; i >= 0; i--)
                {
                    if (m_LANMatchsAvailables[i].timer < Time.time)
                    {
                        m_LANMatchsAvailables.RemoveAt(i);
                    }
                }

                yield return null;
            }

            m_LANMatchsAvailables.Clear();


            if (m_LANHostId != -1)
            {
                NetworkTransport.RemoveHost(m_LANHostId);
                m_LANHostId = -1;
            }
        }

        private void ListMatchResponse(bool success, string extendedInfo, List<MatchInfoSnapshot> responses)
        {
            if (!success)
                return;

            m_onlineMatchAvailables = responses;
        }

        /// <summary>
        ///  Launch the automatic matchmaking 
        /// </summary>
        /// <param name="loop">if true, will relaunch a request after maxTimeRelaunchMatchmakingAutomatically</param>
        /// <param name="destroyObjects"></param>
        public void AutomaticMatchmaking()
        {
            currentMatch = null;
            isWaitingResponse = true;
            isOnAutomatch = true;
            matchMaker.ListMatches(0, 10, "", false, 0, version, AutomaticResponse);
        }

        IEnumerator RelaunchAutomatchmaking()
        {
            stopRelaunch = false;
            yield return new WaitForSeconds(maxTimeRelaunchMatchmakingAutomatically);

            if (!stopRelaunch)
            {
                if (bannedMatchsList.Count > 100)
                    bannedMatchsList.Clear();

                bannedMatchsList.Add(currentMatchNetworkId);
                AutomaticMatchmaking();
                Debug.Log("Automatchmaking relaunch");
            }
        }

        bool stopRelaunch = true;
        public void StopRelaunchingMatchmaking(NetworkingConnection conn)
        {
            stopRelaunch = true;
            if (relaunchMatchmakingCoroutine != null)
                StopCoroutine(relaunchMatchmakingCoroutine);
            isWaitingResponse = false;
        }

        void AutomaticResponse(bool success, string extendedInfo, List<MatchInfoSnapshot> responses)
        {
            isWaitingResponse = false;

            //A Unet problem occured
            if (!success)
            {
                return;
            }

            m_onlineMatchAvailables = responses;

            currentMatch = null;

            foreach (MatchInfoSnapshot i in m_onlineMatchAvailables)
            {
                if (bannedMatchsList.Contains((ulong)i.networkId))
                    continue;

                if ((currentMatch == null) || ((i.currentSize > currentMatch.currentSize)))
                    currentMatch = i;
            }

#if SERVER
            if (currentMatch == null)
            {
                isOnAutomatch = true;
                Debug.Log("Create");
                CreatMainOnlineMatch("AutoMatch_" + System.DateTime.Now + "_" + version, "", true);
                return;
            }
#endif

#if CLIENT
            ConnectToOnlineMatch(currentMatch, "", InternalOnOnlineAutoMatchJoined);
            return;
#endif
        }

#if SERVER
        private void InternalOnMainOnlineMatchCreated(bool success, string extendedInfo, MatchInfo matchInfo)
        {
            isWaitingResponse = false;

            if (success)
            {
                Debug.Log("Match created");
                currentMatchNetworkId = (ulong)matchInfo.networkId;

                NetworkingSystem.Instance.StartMainServer(matchInfo);

                if (OnMatchCreated != null)
                    OnMatchCreated(success, extendedInfo, matchInfo);
            }
            else
                Debug.Log(extendedInfo);

        }
#endif


        private void InternalOnOnlineMatchJoined(bool success, string extendedInfo, MatchInfo matchInfo)
        {
            isWaitingResponse = false;

            if (success)
            {
                Debug.Log("Match joined");
                currentMatchNetworkId = (ulong)matchInfo.networkId;
            }
            else
                isOnAutomatch = false;

            if (OnMatchJoined != null)
                OnMatchJoined(success, extendedInfo, matchInfo);
        }

        private void InternalOnOnlineAutoMatchJoined(bool success, string extendedInfo, MatchInfo matchInfo)
        {
            if (success)
            {
                isOnAutomatch = false;
                InternalOnOnlineMatchJoined(success, extendedInfo, matchInfo);
            }
            StartCoroutine(RelaunchAutomatchmaking());
        }


#if SERVER
        /// <summary>
        /// Automatically lock a match when server disconnect
        /// </summary>
        /// <param name="netMsg"></param>
        void OnStopServer(NetworkingServer server)
        {
            HideServers();
        }

        void Reset(NetworkingServer server)
        {
            Reset();
        }
#endif

        void Reset(NetworkingConnection conn)
        {
            Reset();
        }

        void Reset(NetworkingConnection conn, NetworkingMessage msg)
        {
            Reset();
        }

        void Reset()
        {
            if (m_LANHostId != -1)
                NetworkTransport.RemoveHost(m_LANHostId);

            m_LANHostId = -1;
        }


        void InternalOnReceivedBroadcast(string fromAddress, string data)
        {
            if (OnReceivedBroadcast != null)
                OnReceivedBroadcast(fromAddress, data);
        }


        static byte[] StringToBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static string BytesToString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }
    }
}

