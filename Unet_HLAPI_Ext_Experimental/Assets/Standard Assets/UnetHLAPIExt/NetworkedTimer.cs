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
using UnityEngine.Networking;
using System;

namespace BC_Solution.UnetNetwork
{
    public class NetworkedTimer : Singleton<NetworkedTimer>
    {

      /*  public class TimerMessage : MessageBase
        {
            public float value;
            public string name;
            public bool isRunning;
            public int timeStamp;

            public TimerMessage()
            {
                this.value = -1;
                this.name = "";
                this.isRunning = false;
                this.timeStamp = 0;
            }

            public TimerMessage(float value, string name, bool isRunning, int timeStamp)
            {
                this.value = value;
                this.name = name;
                this.isRunning = isRunning;
                this.timeStamp = timeStamp;
            }
        }



        /// <summary>
        /// Is running or not are set LOCALLY, because all run in the server and only in it !
        /// </summary>
        [System.Serializable]
        public class NetworkedTimerInfo
        {
            public string timerName;
            public float value = -1;
            public bool isRunning = false;

            [Tooltip("Time in second between two update of all running timer")]
            public float updateRate = 0.2f;

            [HideInInspector]
            public float timer = 0;
        }


        [Space(5)]
        [SerializeField]
        List<NetworkedTimerInfo> networkedTimerInfos;



        public static Action<NetworkedTimerInfo> OnTimerSynchronise;
        public static Action<NetworkedTimerInfo> OnTimerUpdate;
        public static Action<NetworkedTimerInfo> OnTimerStart;
        public static Action<NetworkedTimerInfo> OnTimerStop;
        public static Action<NetworkedTimerInfo> OnTimerAvort;



        protected override void Awake()
        {
            base.Awake();

            NetworkingSystem.OnServerConnect += SynchroniseTimer;
            NetworkingSystem.OnClientDisconnect += LocalAvortTimersRunning;
            NetworkingSystem.OnStopAllConnections += LocalAvortTimersRunning;

            NetworkingSystem.RegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerUpdateMessage, BaseOnTimerUpdate));
            NetworkingSystem.RegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerSynchronisationMessage, BaseOnTimerSynchronise));
            NetworkingSystem.RegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerStartMessage, BaseOnTimerStart));
            NetworkingSystem.RegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerStopMessage, BaseOnTimerStop));
            NetworkingSystem.RegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerAvortMessage, BaseOnTimerAvort));
        }


        void Update()
        {
            if (NetworkServer.active)
            {
                foreach (NetworkedTimerInfo i in networkedTimerInfos)
                {
                    if (i.isRunning)
                    {
                        i.value -= Time.deltaTime;

                        if (Time.time >= i.timer)
                        {
                            UpdateTimerValue(i.value, i.timerName);
                            i.timer = Time.time + i.updateRate;
                        }
                    }
                }
            }
        }


        public void OnDestroy()
        {
            NetworkingSystem.OnServerConnect -= SynchroniseTimer;
            NetworkingSystem.OnClientDisconnect -= LocalAvortTimersRunning;
            NetworkingSystem.OnStopAllConnections -= LocalAvortTimersRunning;

            NetworkingSystem.UnRegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerUpdateMessage, BaseOnTimerUpdate));
            NetworkingSystem.UnRegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerSynchronisationMessage, BaseOnTimerSynchronise));
            NetworkingSystem.UnRegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerStartMessage, BaseOnTimerStart));
            NetworkingSystem.UnRegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerStopMessage, BaseOnTimerStop));
            NetworkingSystem.UnRegisterClientHandler(new NetworkingSystem.NetworkingConfiguration(NetworkingMessageType.TimerAvortMessage, BaseOnTimerAvort));
        }


        public void SynchroniseTimer(NetworkMessage netMsg)
        {
            if (NetworkServer.active)
            {
                foreach (NetworkedTimerInfo i in networkedTimerInfos)
                    NetworkingServer.SendToClient(netMsg.conn.connectionId, NetworkingMessageType.TimerSynchronisationMessage, new TimerMessage(i.value, i.timerName, i.isRunning, NetworkTransport.GetNetworkTimestamp()));
            }
        }


        public void UpdateTimerValue(float value, string timerName)
        {
            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerName))
                {
                    if (value > 0)
                        NetworkServer.SendToAll(NetworkingMessageType.TimerUpdateMessage, new TimerMessage(value, timerName, i.isRunning, NetworkTransport.GetNetworkTimestamp()));
                    else
                    {
                        NetworkServer.SendToAll(NetworkingMessageType.TimerStopMessage, new TimerMessage(-1, timerName, false, NetworkTransport.GetNetworkTimestamp()));
                    }
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerName + " doesn't exist");
#endif
        }

        /// <summary>
        /// Start a timer.
        /// Only the server can do this
        /// </summary>
        /// <param name="timerName"></param>
        /// <param name="value"></param>
        public void StartTimer(string timerName, float value)
        {
            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerName))
                {
                    NetworkServer.SendToAll(NetworkingMessageType.TimerStartMessage, new TimerMessage(value, timerName, true, NetworkTransport.GetNetworkTimestamp()));
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerName + " doesn't exist");
#endif
        }

        /// <summary>
        /// Stop the timer, but not call OnTimerStop
        /// </summary>
        /// <param name="timerName"></param>
        public void AvortTimer(string timerName)
        {
            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerName))
                {
                    NetworkServer.SendToAll(NetworkingMessageType.TimerAvortMessage, new TimerMessage(-1, timerName, false, NetworkTransport.GetNetworkTimestamp()));
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerName + " doesn't exist");
#endif
        }


        /// <summary>
        /// Return the current value of a timer
        /// </summary>
        /// <param name="timerName"></param>
        /// <returns></returns>
        public float GetTimerValue(string timerName)
        {
            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerName))
                {
                    return i.value;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerName + " doesn't exist");
#endif
            return -1;
        }


        public bool TimerIsRunning(string timerName)
        {
            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerName))
                {
                    return i.isRunning;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerName + " doesn't exist");
#endif
            return false;
        }


        #region TimerHandler

        void BaseOnTimerSynchronise(NetworkMessage netMsg)
        {
            TimerMessage timerMessage = netMsg.ReadMessage<TimerMessage>();

            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerMessage.name))
                {
                    byte error;

                    if (netMsg.conn.hostId != -1)
                        i.value = timerMessage.value + NetworkTransport.GetRemoteDelayTimeMS(netMsg.conn.hostId, netMsg.conn.connectionId, timerMessage.timeStamp, out error) / 1000f;
                    else
                        i.value = timerMessage.value;

                    if (i.isRunning && !timerMessage.isRunning)
                    {
                        if (OnTimerStop != null)
                            OnTimerStop(i);
                    }
                    else if (!i.isRunning && timerMessage.isRunning)
                    {
                        if (OnTimerStart != null)
                            OnTimerStart(i);
                    }

                    i.isRunning = timerMessage.isRunning;

                    if (OnTimerSynchronise != null)
                    {
                        OnTimerSynchronise(i);
                    }
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerMessage.name + " doesn't exist");
#endif
        }


        void BaseOnTimerUpdate(NetworkMessage netMsg)
        {
            TimerMessage timerMessage = netMsg.ReadMessage<TimerMessage>();

            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerMessage.name))
                {
                    byte error;

                    if (netMsg.conn.hostId != -1)
                        i.value = timerMessage.value + NetworkTransport.GetRemoteDelayTimeMS(netMsg.conn.hostId, netMsg.conn.connectionId, timerMessage.timeStamp, out error) / 1000f;
                    else
                        i.value = timerMessage.value;

                    i.isRunning = timerMessage.isRunning;


                    if (OnTimerUpdate != null)
                    {
                        OnTimerUpdate(i);
                    }
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerMessage.name + " doesn't exist");
#endif
        }



        void BaseOnTimerStart(NetworkMessage netMsg)
        {
            TimerMessage timerMessage = netMsg.ReadMessage<TimerMessage>();

            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerMessage.name))
                {
                    byte error;

                    if (netMsg.conn.hostId != -1)
                        i.value = timerMessage.value + NetworkTransport.GetRemoteDelayTimeMS(netMsg.conn.hostId, netMsg.conn.connectionId, timerMessage.timeStamp, out error) / 1000f;
                    else
                        i.value = timerMessage.value;

                    i.isRunning = true;

                    if (OnTimerStart != null)
                    {
                        OnTimerStart(i);
                    }
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerMessage.name + " doesn't exist");
#endif
        }

        void BaseOnTimerStop(NetworkMessage netMsg)
        {
            TimerMessage timerMessage = netMsg.ReadMessage<TimerMessage>();

            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerMessage.name))
                {
                    i.value = -1;
                    i.isRunning = false;

                    if (OnTimerStop != null)
                    {
                        OnTimerStop(i);
                    }
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerMessage.name + " doesn't exist");
#endif
        }


        void BaseOnTimerAvort(NetworkMessage netMsg)
        {
            TimerMessage timerMessage = netMsg.ReadMessage<TimerMessage>();

            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.timerName.Equals(timerMessage.name))
                {
                    i.value = -1;
                    i.isRunning = false;

                    if (OnTimerAvort != null)
                    {
                        OnTimerAvort(i);
                    }
                    return;
                }
            }

#if EQUILIBRE_GAMES_DEBUG
            Debug.LogError("This timer : " + timerMessage.name + " doesn't exist");
#endif
        }

        public void LocalAvortTimersRunning(NetworkMessage netMsg = null)
        {

            foreach (NetworkedTimerInfo i in networkedTimerInfos)
            {
                if (i.isRunning)
                {
                    i.value = -1;
                    i.isRunning = false;

                    if (OnTimerAvort != null)
                    {
                        OnTimerAvort(i);
                    }
                }
            }
        }
        #endregion
*/
    }
}
