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
using UnityEngine.Networking;

namespace BC_Solution.UnetNetwork
{
    [NetworkSettings(sendInterval = 0)]
    public class NetworkMovementSynchronization : NetworkingBehaviour
    {
        [System.Serializable]
        public class Config
        {
            public float adaptativeSynchronizationBackTime = 0.15f;
            public float maxSynchronizationBacktime = 0.3f;
            public float minSynchronisationBacktime = 0.1f;
            public float adaptationAmount = 1.5f;
        }

        public Config onlineConfig;
        public Config lanConfig;
        public Config defaultConfig;
        public float nonAdaptativeBacktime = 0.1f;
        public float extraInterpolationTimeFactor = 10f;

        [Tooltip("Synchronised object will have their movement synchronised.")]
        public MovementSynchronizer[] movementSynchronizers;

        [Space(10)]
        public float lagAverage = 0.15f;

        NetworkingWriter writer = new NetworkingWriter();

        override protected void Awake()
        {
            base.Awake();
            for (int i = 0; i < movementSynchronizers.Length; i++)
            {
                movementSynchronizers[i].Init(this);
            }

            //  NetworkingSystem.RegisterServerHandler(NetworkingMessageType.Connect, ServerSyncPosition);
        }

        void OnDestroy()
        {
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.Connect, ServerSyncPosition);
        }

        public float AdaptativeSynchronizationBackTime()
        {
            if (MatchmakingSystem.IsOnOnlineMatch)
                return onlineConfig.adaptativeSynchronizationBackTime;
            else if (MatchmakingSystem.IsOnLanMatch)
                return lanConfig.adaptativeSynchronizationBackTime;
            else return defaultConfig.adaptativeSynchronizationBackTime;

        }

        public float MaxSynchronizationBacktime()
        {
            if (MatchmakingSystem.IsOnOnlineMatch)
                return onlineConfig.maxSynchronizationBacktime;
            else if (MatchmakingSystem.IsOnLanMatch)
                return lanConfig.maxSynchronizationBacktime;
            else return defaultConfig.maxSynchronizationBacktime;
        }

        public float AdaptationAmount()
        {
            if (MatchmakingSystem.IsOnOnlineMatch)
                return onlineConfig.adaptationAmount;
            else if (MatchmakingSystem.IsOnLanMatch)
                return lanConfig.adaptationAmount;
            else return defaultConfig.adaptationAmount;
        }

        public float MinSynchronisationBacktime()
        {

            if (MatchmakingSystem.IsOnOnlineMatch)
                return onlineConfig.minSynchronisationBacktime;
            else if (MatchmakingSystem.IsOnLanMatch)
                return lanConfig.minSynchronisationBacktime;
            else return defaultConfig.minSynchronisationBacktime;
        }

        public override void OnStartAuthority()     //Send position the first time
        {
            base.OnStartAuthority();
           // ServerSyncPosition(null);
        }

        /// <summary>
        /// Only server
        /// </summary>
        /// <param name="netMsg"></param>
        void ServerSyncPosition(NetworkingMessage netMsg)
        {
            writer.SeekZero(true);
            int updateMask = 0;
            for (int i = 0; i < movementSynchronizers.Length; i++)
            {
                updateMask = updateMask | (1 << i);
            }

            if (updateMask != 0)
            {
                if (movementSynchronizers.Length <= 4)
                    writer.Write((byte)updateMask);
                else if (movementSynchronizers.Length <= 8)
                    writer.Write((short)updateMask);
                else
                    writer.Write(updateMask);

                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    movementSynchronizers[i].GetCurrentState(writer);
                }

                SendToConnection(netMsg.m_connection, "GetMovementSyncInformations", NetworkingMessageType.Channels.DefaultReliable, writer.ToArray());
            }
        }




        void FixedUpdate()
        {
            if (!hasAuthority)
                return;

            writer.SeekZero(true);

            int updateMask = 0;

            if (movementSynchronizers.Length != 0)
            {
                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    MovementSynchronizer movementSynchronizer = movementSynchronizers[i];

                    if  //Real need of synchronization 
                    (Time.realtimeSinceStartup > movementSynchronizer.lastInterpolationUpdateTimer && movementSynchronizer.NeedToUpdate())
                    {
                        updateMask = updateMask | (1 << i);
                    }
                    else if //Other clients are extrapolating
                        (Time.realtimeSinceStartup - movementSynchronizer.lastInterpolationUpdateTimer > 0
                        && Time.realtimeSinceStartup - movementSynchronizer.lastInterpolationUpdateTimer < extraInterpolationTimeFactor * movementSynchronizer.extrapolationTime
                        && Time.realtimeSinceStartup > movementSynchronizer.lastExtrapolationUpdateTimer)
                    {
                        updateMask = updateMask | (1 << i);
                    }
                }
            }

            if (updateMask != 0)
            {
                if (movementSynchronizers.Length > 1)
                {
                    if (movementSynchronizers.Length <= 4)
                        writer.Write((byte)updateMask);
                    else if (movementSynchronizers.Length <= 8)
                        writer.Write((short)updateMask);
                    else
                        writer.Write(updateMask);
                }

                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    MovementSynchronizer movementSynchronizer = movementSynchronizers[i];

                    if ((updateMask & (1 << i)) != 0)
                    {
                        if (movementSynchronizer.NeedToUpdate())    //Other client interpolating
                        {
                            movementSynchronizer.lastInterpolationUpdateTimer = Time.realtimeSinceStartup + 1f / movementSynchronizer.UpdateRate();
                        }
                        else //Other clients extrapolating
                        {
                            movementSynchronizer.lastExtrapolationUpdateTimer = Time.realtimeSinceStartup + 1f / movementSynchronizer.UpdateRate();
                        }

                        movementSynchronizer.GetCurrentState(writer);
                    }
                }

                SendMovementsInformations(writer.ToArray());
            }
        }


        void SendMovementsInformations(byte[] info)
        {
            if (isServer)
                foreach (NetworkingConnection n in this.connection.m_server.connections)
                {
                    if (n == null)
                        continue;

                    if (this.connection.m_connectionId != n.m_connectionId)
                    {
                        SendToConnection(n, "RpcGetMovementInformations", NetworkingMessageType.Channels.DefaultUnreliable, NetworkTransport.GetNetworkTimestamp(), info);
                    }
                }
            else
                SendToServer("CmdGetMovementInformations", NetworkingMessageType.Channels.DefaultUnreliable, NetworkTransport.GetNetworkTimestamp(), info);
        }

        [Networked]
        void GetMovementSyncInformations(byte[] info)
        {
            NetworkingReader reader = new NetworkingReader(info);
            int updateMask = 0;

            if (movementSynchronizers.Length > 1)
            {
                if (movementSynchronizers.Length <= 4)
                    updateMask = reader.ReadByte();
                else if (movementSynchronizers.Length <= 8)
                    updateMask = reader.ReadInt16();
                else
                    updateMask = reader.ReadInt32();
            }

            while (reader.Position < reader.Length - 1)
            {
                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    if ((updateMask & (1 << i)) != 0 || movementSynchronizers.Length == 0)
                    {
                        movementSynchronizers[i].ReceiveSync(reader);
                    }
                }
            }
        }

        [Networked]
        void RpcGetMovementInformations(int timestamp, byte[] info)
        {
            if (hasAuthority)
                return;

            NetworkingReader reader = new NetworkingReader(info);

            float relativeTime = 0;
            byte error;
            relativeTime = Time.realtimeSinceStartup - NetworkTransport.GetRemoteDelayTimeMS(this.connection.m_hostId, this.connection.m_connectionId, timestamp, out error) / 1000f;
            /* if (!isServer)
             {
                 byte error;
                 relativeTime = Time.realtimeSinceStartup - NetworkTransport.GetRemoteDelayTimeMS(this.connection.m_hostId, this.connection.m_connectionId, timestamp, out error) / 1000f;
                 // Debug.Log(NetworkTransport.GetRemoteDelayTimeMS(NetworkingSystem.Instance.Client.connection.hostId, NetworkingSystem.Instance.Client.connection.connectionId, timestamp, out error) / 1000f);
                 Debug.Log(relativeTime);
             }
             else
             {
                 relativeTime = Time.realtimeSinceStartup - (NetworkTransport.GetNetworkTimestamp() - timestamp) / 1000f;
                 //Debug.Log((NetworkTransport.GetNetworkTimestamp() - timestamp) / 1000f);
             }*/

            lagAverage = 0.99f * lagAverage + 0.01f * (Time.realtimeSinceStartup - relativeTime);

            if (MatchmakingSystem.IsOnOnlineMatch)
                onlineConfig.adaptativeSynchronizationBackTime = Mathf.Max(lanConfig.minSynchronisationBacktime, Mathf.Min(onlineConfig.maxSynchronizationBacktime, lagAverage * (onlineConfig.adaptationAmount)));
            else if (MatchmakingSystem.IsOnLanMatch)
                lanConfig.adaptativeSynchronizationBackTime = Mathf.Max(lanConfig.minSynchronisationBacktime, Mathf.Min(lanConfig.maxSynchronizationBacktime, lagAverage * (lanConfig.adaptationAmount)));
            else
                defaultConfig.adaptativeSynchronizationBackTime = Mathf.Max(defaultConfig.minSynchronisationBacktime, Mathf.Min(defaultConfig.maxSynchronizationBacktime, lagAverage * (defaultConfig.adaptationAmount)));

            int updateMask = 0;


            if (movementSynchronizers.Length > 1)
            {
                if (movementSynchronizers.Length <= 4)
                    updateMask = reader.ReadByte();
                else if (movementSynchronizers.Length <= 8)
                    updateMask = reader.ReadInt16();
                else
                    updateMask = reader.ReadInt32();
            }

            while (reader.Position < reader.Length-1)
            {
                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    if ((updateMask & (1 << i)) != 0 || movementSynchronizers.Length == 1)
                    {
                        movementSynchronizers[i].ReceiveCurrentState(relativeTime, reader);
                    }
                }
            }
        }

        [Networked]
        void CmdGetMovementInformations(int timeStamp, byte[] info)
        {
            byte error;
            timeStamp = NetworkTransport.GetNetworkTimestamp() - NetworkTransport.GetRemoteDelayTimeMS(this.connection.m_hostId, this.connection.m_connectionId, timeStamp, out error);
            SendToAllConnections("RpcGetMovementInformations", NetworkingMessageType.Channels.DefaultUnreliable, timeStamp, info);
        }
    }
}
