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

        [SerializeField]
        int m_broadcastInterval = 1000;

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
        public static NetworkMatch.BasicResponseDelegate OnMatchLocked;


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


        static bool isOnLanMatch = false;
        static public bool IsOnLanMatch
        {
            get { return isOnLanMatch; }
        }

        static bool isOnOnlineMatch = false;
        static public bool IsOnOnlineMatch
        {
            get { return isOnOnlineMatch; }
        }


        static bool isOnAutomatch = false;
        static public bool MatchIsAnAutomatch
        {
            get { return isOnAutomatch; }
        }

        [HideInInspector]
        public List<MatchInfoSnapshot> m_onlineMatchAvailables = new List<MatchInfoSnapshot>();

        NetworkID currentMatchNetworkId = NetworkID.Invalid;

        List<NetworkID> bannedMatchsList = new List<NetworkID>(); //Banned match because no host

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
        string m_broadcastData = "";
        #endregion

        protected override void Awake()
        {
            base.Awake();

            relaunchMatchmakingCoroutine = null;

            matchMaker = gameObject.AddComponent<NetworkMatch>();

            matchMaker.baseUri = new System.Uri(matchAdress);

            NetworkingSystem.OnStopServer += LockMatchOnStopServer;
            NetworkingSystem.OnClientConnect += StopRelaunchingMatchmaking;

            NetworkingSystem.OnClientDisconnect += ResetBehaviour;
            NetworkingSystem.OnStopClient += ResetBehaviour;
            NetworkingSystem.OnStopServer += ResetBehaviour;
        }


        public void OnDestroy()
        {
            NetworkingSystem.OnStopServer -= LockMatchOnStopServer;
            NetworkingSystem.OnClientConnect -= StopRelaunchingMatchmaking;

            NetworkingSystem.OnClientDisconnect -= ResetBehaviour;
            NetworkingSystem.OnStopServer -= ResetBehaviour;
            NetworkingSystem.OnStopClient -= ResetBehaviour;
        }


        /// <summary>
        /// Connect to the match
        /// </summary>
        /// <param name="matchInfo"></param>
        public void ConnectToOnlineMatch(MatchInfoSnapshot matchInfo, string password = "", NetworkMatch.DataResponseDelegate<MatchInfo> callback = null)
        {
            if (NetworkServer.active)
                NetworkingSystem.Instance.StopServer();
            if (NetworkClient.active)
                NetworkingSystem.Instance.StopClient();


            callback += BaseOnOnlineMatchJoined;

            currentMatch = matchInfo;

            isWaitingResponse = true;

            if (matchInfo.name.StartsWith("AutoMatch_"))
                isOnAutomatch = true;

            matchMaker.JoinMatch(matchInfo.networkId, password, "", "", 0, version, callback);
        }

        public void ConnectToLANMatch(string serverAdress, int serverPort)
        {
            if (NetworkServer.active)
                NetworkingSystem.Instance.StopServer();
            if (NetworkClient.active)
                NetworkingSystem.Instance.StopClient();

            isOnLanMatch = true;

            NetworkingSystem.Instance.StartClient(serverAdress, serverPort);

            if (OnMatchJoined != null)
                OnMatchJoined(true, "", null);
        }


        /// <summary>
        /// Create a match
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password"></param>
        /// <param name="visible"></param>
        public void CreatOnlineMatch(string name, string password, bool visible = true)
        {
            if (NetworkServer.active)
                NetworkingSystem.Instance.StopServer();
            if (NetworkClient.active)
                NetworkingSystem.Instance.StopClient();

            isWaitingResponse = true;
            matchMaker.CreateMatch(name, NetworkingSystem.Instance.maxPlayer, visible, password, "", "", 0, version, BaseOnOnlineMatchCreated);
        }


        public void CreateLANMatch(string name)
        {
            if (NetworkServer.active)
                NetworkingSystem.Instance.StopServer();
            if (NetworkClient.active)
                NetworkingSystem.Instance.StopClient();

            NetworkTransport.Shutdown();

            if (!NetworkTransport.IsStarted)
            {
                NetworkTransport.Init();
            }

            NetworkingSystem.Instance.StartHost();

            m_broadcastData = "NetworkManager:" + NetworkingSystem.Instance.serverAdress + ":" + NetworkingSystem.Instance.serverPort + ": MatchInfo :" + name;

            m_msgOutBuffer = StringToBytes(m_broadcastData);

            m_LANTopology = new HostTopology(NetworkingSystem.Instance.Configuration(), (int)NetworkingSystem.Instance.maxPlayer);
            m_LANHostId = NetworkTransport.AddHost(m_LANTopology, 0);


            if (m_LANHostId == -1)
            {
                Debug.LogError("LANHostId == -1, not accepted");
                return;
            }

            byte err;
            if (!NetworkTransport.StartBroadcastDiscovery(m_LANHostId, m_broadcastPort, m_broadcastKey, m_broadcastVersion, m_broadcastSubVersion, m_msgOutBuffer, m_msgOutBuffer.Length, m_broadcastInterval, out err))
            {
                Debug.LogError("Error on Start Broadcast Discovery");
                m_LANHostId = -1;

                NetworkingSystem.Instance.StopHost();

                if (NetworkingSystem.Instance.autoReconnectOnLocal)
                    NetworkingSystem.Instance.StartHost();

                return;
            }
            else
            {
                isOnLanMatch = true;

                if (OnMatchCreated != null)
                    OnMatchCreated(true, "", null);
            }
        }



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

                        BaseOnReceivedBroadcast(senderAddr, serverBroadcastInfo);

                        string[] serverInfos = serverBroadcastInfo.Split(':');


                        LanMatchInfo lanMatchInfo = m_LANMatchsAvailables.Find((LanMatchInfo x) => { return x.serverAdress.Equals(senderAddr) && x.serverPort == senderPort; });

                        //3 correspond to server name
                        if (lanMatchInfo == null)
                        {
                            m_LANMatchsAvailables.Add(new LanMatchInfo(serverInfos[4], senderAddr, Convert.ToInt32(serverInfos[2]), timeToRemoveALanMatchFromList));
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
            //It will bug if we stop networking later (Unity bug ?)
            if (NetworkServer.active)
                NetworkingSystem.Instance.StopServer();
            if (NetworkClient.active)
                NetworkingSystem.Instance.StopClient();

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
        public void StopRelaunchingMatchmaking(NetworkMessage netMsg)
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
                if (bannedMatchsList.Contains(i.networkId))
                    continue;

                if ((currentMatch == null) || ((i.currentSize > currentMatch.currentSize)))
                    currentMatch = i;
            }

            if (currentMatch == null)
            {
                isOnAutomatch = true;
                Debug.Log("Createé");
                CreatOnlineMatch("AutoMatch_" + System.DateTime.Now + "_" + version, "", true);
                return;
            }
            else
            {
                ConnectToOnlineMatch(currentMatch, "", BaseOnOnlineAutoMatchJoined);
            }
        }


        private void BaseOnOnlineMatchCreated(bool success, string extendedInfo, MatchInfo matchInfo)
        {
            isWaitingResponse = false;

            if (success)
            {
                Debug.Log("Match created");
                currentMatchNetworkId = matchInfo.networkId;
                NetworkingSystem.Instance.StartHost(matchInfo);

                isOnOnlineMatch = true;

                if (OnMatchCreated != null)
                    OnMatchCreated(success, extendedInfo, matchInfo);
            }
            else
                Debug.Log(extendedInfo);

        }


        private void BaseOnOnlineMatchJoined(bool success, string extendedInfo, MatchInfo matchInfo)
        {
            isWaitingResponse = false;

            if (success)
            {
                Debug.Log("Match joined");
                currentMatchNetworkId = matchInfo.networkId;
                NetworkingSystem.Instance.StartClient(matchInfo);

                isOnOnlineMatch = true;
            }
            else
                isOnAutomatch = false;

            if (OnMatchJoined != null)
                OnMatchJoined(success, extendedInfo, matchInfo);
        }

        private void BaseOnOnlineAutoMatchJoined(bool success, string extendedInfo, MatchInfo matchInfo)
        {
            if (success)
            {
                isOnAutomatch = false;
                isOnOnlineMatch = true;
                BaseOnOnlineMatchJoined(success, extendedInfo, matchInfo);
            }
            StartCoroutine(RelaunchAutomatchmaking());
        }


        /// <summary>
        /// Lock the match so it will be no more joinable
        /// </summary>
        /// <param name="b"></param>
        public void LockOnlineMatch(bool b)
        {
            Debug.Log("Match locked");
            matchMaker.SetMatchAttributes(currentMatchNetworkId, !b, version, OnLockMatch);
        }

        public void LockLANMatch(bool b)
        {
            if (!b && !NetworkTransport.IsBroadcastDiscoveryRunning())
            {
                byte err;
                NetworkTransport.StartBroadcastDiscovery(NetworkServer.serverHostId, m_broadcastPort, m_broadcastKey, m_broadcastVersion, m_broadcastSubVersion, m_msgOutBuffer, m_msgOutBuffer.Length, m_broadcastInterval, out err);
            }
            else if (b && NetworkTransport.IsBroadcastDiscoveryRunning())
            {
                NetworkTransport.StopBroadcastDiscovery();

                if (OnMatchLocked != null)
                    OnMatchLocked(true, "");
            }
        }

        public void LockMatch(bool b)
        {
            if (isOnLanMatch)
                LockLANMatch(b);

            if (isOnOnlineMatch)
                LockOnlineMatch(b);
        }

        /// <summary>
        /// Automatically lock a match when server disconnect
        /// </summary>
        /// <param name="netMsg"></param>
        void LockMatchOnStopServer(NetworkMessage netMsg)
        {
            if (currentMatchNetworkId != NetworkID.Invalid && isOnOnlineMatch)
            {
                LockOnlineMatch(true);
                currentMatchNetworkId = NetworkID.Invalid;
            }

            if (isOnLanMatch)
                LockLANMatch(true);
        }

        void ResetBehaviour(NetworkMessage netMsg)
        {
            isOnLanMatch = false;
            isOnOnlineMatch = false;
            isOnAutomatch = false;

            m_LANHostId = -1;
        }

        void OnLockMatch(bool success, string extendedInfo)
        {
            if (OnMatchLocked != null)
                OnMatchLocked(success, extendedInfo);
        }


        void BaseOnReceivedBroadcast(string fromAddress, string data)
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

