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
        public float m_adaptativeSynchronizationBackTime = 0.15f;
        public float m_maxSynchronizationBacktime = 0.3f;
        public float m_minSynchronisationBacktime = 0.1f;
        public float m_adaptationAmount = 1.5f;

        [Tooltip("Update x per second")]
        public float updateRate = 20f;

        public float m_nonAdaptativeBacktime = 0.1f;

        [Tooltip("Synchronised object will have their movement synchronised.")]
        public MovementSynchronizer[] movementSynchronizers;

        [Space(10)]
        public float lagAverage = 0.15f;

        /// <summary>
        /// Used when real Update is needed for interpolation
        /// </summary>
        public float lastInterpolationUpdateTimer = -1;

        bool m_sendEndingLastInterpolation = false;
        NetworkingWriter writer = new NetworkingWriter();

        override protected void Awake()
        {
            base.Awake();
            for (int i = 0; i < movementSynchronizers.Length; i++)
            {
                movementSynchronizers[i].Init(this);
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
                    (Time.realtimeSinceStartup > this.lastInterpolationUpdateTimer && movementSynchronizer.NeedToUpdate())
                    {
                        updateMask = updateMask | (1 << i);
                    }
                    else if //Had to synchronize the lastest position
                        (Time.realtimeSinceStartup > this.lastInterpolationUpdateTimer && !this.m_sendEndingLastInterpolation)
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
                            this.lastInterpolationUpdateTimer = Time.realtimeSinceStartup + 1f / this.updateRate;
                            this.m_sendEndingLastInterpolation = false;
                        }
                        else //Other clients extrapolating
                        {
                            this.lastInterpolationUpdateTimer = Time.realtimeSinceStartup + 1f / this.updateRate; //It's like an interpolation
                            this.m_sendEndingLastInterpolation = true;
                        }

                        movementSynchronizer.GetCurrentState(writer);
                    }
                }

                SendMovementsInformations(writer.ToArray(), m_sendEndingLastInterpolation);
            }
        }


        void SendMovementsInformations(byte[] info, bool useReliableChannel)
        {
            if (isServer)
                foreach (NetworkingConnection n in this.serverConnectionListeners)
                {
                    if (n == null)
                        continue;

                    if (this.connection== null || this.connection.m_connectionId != n.m_connectionId)
                    {
                        SendToConnection(n, "RpcGetMovementInformations", useReliableChannel ? NetworkingChannel.DefaultReliable : NetworkingChannel.DefaultUnreliable, NetworkTransport.GetNetworkTimestamp(), info);
                    }
                }
            else if (isClient)
                SendToServer("CmdSendMovementInformations", useReliableChannel ? NetworkingChannel.DefaultReliable : NetworkingChannel.DefaultUnreliable, NetworkTransport.GetNetworkTimestamp(), info);
        }



        public override void OnServerAddListener(NetworkingConnection conn)
        {
            ServerSync(conn, NetworkingChannel.DefaultReliable);
        }

        public override void OnServerSyncNetId(NetworkingConnection conn)
        {
            ServerSync(conn, NetworkingChannel.DefaultReliableSequenced);
        }

        public void ServerSync(NetworkingConnection conn, int channel)
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

                SendToConnection(conn, "RpcGetMovementSyncInformations", channel, writer.ToArray());
            }
        }


        [Networked]
        void RpcGetMovementSyncInformations(byte[] info)
        {
            if (hasAuthority || isServer)
                return;

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
            if (hasAuthority || isServer)
                return;

            LocalGetMovementInformations(timestamp, info);
        }

        [Networked]
        void CmdSendMovementInformations(int timeStamp, byte[] info)
        {
            byte error;
            LocalGetMovementInformations(timeStamp, info);

            timeStamp = NetworkTransport.GetNetworkTimestamp() - NetworkTransport.GetRemoteDelayTimeMS(this.serverConnection.m_hostId, this.serverConnection.m_connectionId, timeStamp, out error);
            SendToAllConnections("RpcGetMovementInformations", NetworkingChannel.DefaultUnreliable, timeStamp, info);
        }

        /// <summary>
        /// Unlike Networked, uncheck autority and server condition
        /// </summary>
        /// <param name="timeStam"></param>
        /// <param name="info"></param>
        void LocalGetMovementInformations(int timestamp, byte[] info)
        {
            NetworkingReader reader = new NetworkingReader(info);

            float relativeTime = 0;
            byte error;
            relativeTime = Time.realtimeSinceStartup - NetworkTransport.GetRemoteDelayTimeMS(this.connection.m_hostId, this.connection.m_connectionId, timestamp, out error) / 1000f;

            lagAverage = 0.99f * lagAverage + 0.01f * (Time.realtimeSinceStartup - relativeTime);

            m_adaptativeSynchronizationBackTime = Mathf.Max(m_minSynchronisationBacktime, Mathf.Min(m_maxSynchronizationBacktime, lagAverage * (m_adaptationAmount)));

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
                    if ((updateMask & (1 << i)) != 0 || movementSynchronizers.Length == 1)
                    {
                        movementSynchronizers[i].ReceiveCurrentState(relativeTime, reader);
                    }
                }
            }
        }
    }
}
