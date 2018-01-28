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
    public class NetworkMovementSynchronization : NetworkBehaviour
    {
        public NetworkIdentity networkIdentity;

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

        private void Awake()
        {
            for (int i = 0; i < movementSynchronizers.Length; i++)
            {
                movementSynchronizers[i].networkMovementSynchronization = this;
            }

         //   NetworkingSystem.OnClientReadyFromServer += SyncPosition;
        }

        void OnDestroy()
        {
          //  NetworkingSystem.OnClientReadyFromServer -= SyncPosition;
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
            SyncPosition(null);
        }

        void SyncPosition(NetworkMessage netMsg)
        {
            if (!hasAuthority)
                return;

            NetworkWriter writer = new NetworkWriter();
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

                if (isServer)
                {
                    foreach (NetworkConnection n in NetworkServer.connections)
                    {
                        if (n == null)
                            continue;

                       /* if (NetworkingSystem.Instance.Client.connection.connectionId != n.connectionId)
                        {
                            TargetSendMovementsSyncInformations(n, writer.ToArray());
                        }*/
                    }
                }
                else
                {
                    CmdSendMovementsSyncInformations(writer.ToArray());
                }
            }
        }




        void FixedUpdate()
        {
            if (!hasAuthority)
                return;

            NetworkWriter writer = new NetworkWriter();

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
                if (movementSynchronizers.Length <= 4)
                    writer.Write((byte)updateMask);
                else if (movementSynchronizers.Length <= 8)
                    writer.Write((short)updateMask);
                else
                    writer.Write(updateMask);

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
                foreach (NetworkConnection n in NetworkServer.connections)
                {
                    if (n == null)
                        continue;

                   /* if (NetworkingSystem.Instance.Client.connection.connectionId != n.connectionId)
                    {
                        TargetSendMovementsInformations(n, NetworkTransport.GetNetworkTimestamp(), info);
                    }*/
                }
            else
                CmdSendMovementsInformations(NetworkTransport.GetNetworkTimestamp(), info);
        }


        [TargetRpc(channel = 2)]
        void TargetSendMovementsInformations(NetworkConnection target,int timeStamp, byte[] info)
        {
            GetMovementsInformation(timeStamp, info);
        }

        [TargetRpc(channel = 0)]
        void TargetSendMovementsSyncInformations(NetworkConnection target, byte[] info)
        {
            NetworkReader reader = new NetworkReader(info);
            int updateMask = 0;

            if (movementSynchronizers.Length != 0)
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

        void GetMovementsInformation(int timestamp, byte[] info)
        {
            NetworkReader reader = new NetworkReader(info);

            float relativeTime = 0;

            if (!NetworkServer.active)
            {
                byte error;
               // relativeTime = Time.realtimeSinceStartup - NetworkTransport.GetRemoteDelayTimeMS(NetworkingSystem.Instance.Client.connection.hostId, NetworkingSystem.Instance.Client.connection.connectionId, timestamp, out error) / 1000f;
               // Debug.Log(NetworkTransport.GetRemoteDelayTimeMS(NetworkingSystem.Instance.Client.connection.hostId, NetworkingSystem.Instance.Client.connection.connectionId, timestamp, out error) / 1000f);
            }
            else
            {
                relativeTime = Time.realtimeSinceStartup - (NetworkTransport.GetNetworkTimestamp() - timestamp) / 1000f;
                //Debug.Log((NetworkTransport.GetNetworkTimestamp() - timestamp) / 1000f);
            }

            lagAverage = 0.99f * lagAverage + 0.01f * (Time.realtimeSinceStartup - relativeTime);

            if (MatchmakingSystem.IsOnOnlineMatch)
                onlineConfig.adaptativeSynchronizationBackTime = Mathf.Max(lanConfig.minSynchronisationBacktime, Mathf.Min(onlineConfig.maxSynchronizationBacktime, lagAverage * (onlineConfig.adaptationAmount)));
            else if (MatchmakingSystem.IsOnLanMatch)
                lanConfig.adaptativeSynchronizationBackTime = Mathf.Max(lanConfig.minSynchronisationBacktime, Mathf.Min(lanConfig.maxSynchronizationBacktime, lagAverage * (lanConfig.adaptationAmount)));
            else
                defaultConfig.adaptativeSynchronizationBackTime = Mathf.Max(defaultConfig.minSynchronisationBacktime, Mathf.Min(defaultConfig.maxSynchronizationBacktime, lagAverage * (defaultConfig.adaptationAmount)));

            int updateMask = 0;

            if (movementSynchronizers.Length != 0)
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
                        movementSynchronizers[i].ReceiveCurrentState(relativeTime, reader);
                    }
                }
            }
        }


        [Command(channel = 2)]
        void CmdSendMovementsInformations(int timeStamp, byte[] info)
        {
            byte error;
            timeStamp = NetworkTransport.GetNetworkTimestamp() - NetworkTransport.GetRemoteDelayTimeMS(this.networkIdentity.clientAuthorityOwner.hostId, this.networkIdentity.clientAuthorityOwner.connectionId, timeStamp, out error);

            foreach (NetworkConnection n in NetworkServer.connections)
            {
                if (n == null)
                    continue;

               /* if (NetworkingSystem.Instance.Client.connection != n)
                {
                    if (n != this.connectionToClient)
                    {
                        TargetSendMovementsInformations(n, timeStamp, info);
                    }
                }
                else
                    GetMovementsInformation(timeStamp,info);*/
            }
        }

        [Command(channel = 0)]
        void CmdSendMovementsSyncInformations(byte[] info)
        {
            foreach (NetworkConnection n in NetworkServer.connections)
            {
                if (n == null)
                    continue;

               /* if (NetworkingSystem.Instance.Client.connection != n)
                {
                    if (n != this.connectionToClient)
                    {
                        TargetSendMovementsSyncInformations(n, info);
                    }
                }*/
            }
        }

    }
}
