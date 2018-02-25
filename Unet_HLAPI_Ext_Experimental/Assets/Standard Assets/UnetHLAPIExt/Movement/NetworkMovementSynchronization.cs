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


        NetworkingWriter m_reliableWriter = new NetworkingWriter();
        NetworkingWriter m_unreliableWriter = new NetworkingWriter();

        override protected void Awake()
        {
            base.Awake();
            for (int i = 0; i < movementSynchronizers.Length; i++)
            {
                movementSynchronizers[i].Init(this);
            }
        }

        void Update()
        {
            foreach (MovementSynchronizer m in movementSynchronizers)
                m.ManualUpdate();
        }

        void FixedUpdate()
        {
            if (!isLocalConnection)
                return;

            m_reliableWriter.SeekZero(true);
            m_unreliableWriter.SeekZero(true);

            int reliableUpdateMask = 0;
            int unreliableUpdateMask = 0;

            bool hasToSendByReliableChannel = false; //Will be true if one component stop interpolation

            if (movementSynchronizers.Length != 0)
            {
                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    MovementSynchronizer movementSynchronizer = movementSynchronizers[i];

                    if  //Real need of synchronization 
                    (Time.realtimeSinceStartup > movementSynchronizer.m_lastInterpolationUpdateTimer && movementSynchronizer.NeedToUpdate())
                    {
                        unreliableUpdateMask = unreliableUpdateMask | (1 << i);
                        movementSynchronizer.m_lastInterpolationUpdateTimer = Time.realtimeSinceStartup + 1f / this.updateRate;
                    }
                    else if //Had to synchronize the lastest position
                        (Time.realtimeSinceStartup > movementSynchronizer.m_lastInterpolationUpdateTimer && !movementSynchronizer.m_sentEndInterpolation)
                    {
                        reliableUpdateMask = reliableUpdateMask | (1 << i);
                        movementSynchronizer.m_sentEndInterpolation = true;
                        hasToSendByReliableChannel = true;
                    }
                }
            }

            if (reliableUpdateMask != 0)
            {
                WriteMovementSycnhronizers(movementSynchronizers, reliableUpdateMask, m_reliableWriter);
                SendMovementsInformations(m_reliableWriter.ToArray(), hasToSendByReliableChannel);
            }

            if (unreliableUpdateMask != 0)
            {
                WriteMovementSycnhronizers(movementSynchronizers, unreliableUpdateMask, m_unreliableWriter);
                SendMovementsInformations(m_unreliableWriter.ToArray(), hasToSendByReliableChannel);
            }
        }

        void WriteMovementSycnhronizers(MovementSynchronizer[] movementSynchronizers, int updateMask, NetworkingWriter writer)
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
                    movementSynchronizer.GetCurrentState(writer);
                }
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
            ServerSync(conn, NetworkingChannel.DefaultReliable, m_reliableWriter);
        }

        public override void OnServerSyncNetId(NetworkingConnection conn)
        {
            ServerSync(conn, NetworkingChannel.DefaultReliableSequenced, m_reliableWriter);
        }

        public void ServerSync(NetworkingConnection conn, int channel, NetworkingWriter writer)
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


        [NetworkedFunction]
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

        [NetworkedFunction]
        void RpcGetMovementInformations(int timestamp, byte[] info)
        {
            if (hasAuthority || isServer)
                return;

            LocalGetMovementInformations(timestamp, info, this.connection);
        }

        [NetworkedFunction]
        void CmdSendMovementInformations(int timeStamp, byte[] info)
        {
            byte error;
            LocalGetMovementInformations(timeStamp, info, this.serverConnection);

            timeStamp = NetworkTransport.GetNetworkTimestamp() - NetworkTransport.GetRemoteDelayTimeMS(this.serverConnection.m_hostId, this.serverConnection.m_connectionId, timeStamp, out error);
            SendToAllConnections("RpcGetMovementInformations", NetworkingChannel.DefaultUnreliable, timeStamp, info);
        }

        /// <summary>
        /// Unlike Networked, uncheck autority and server condition
        /// </summary>
        /// <param name="timeStam"></param>
        /// <param name="info"></param>
        void LocalGetMovementInformations(int timestamp, byte[] info, NetworkingConnection conn)
        {
            NetworkingReader reader = new NetworkingReader(info);

            float relativeTime = 0;
            byte error;
            relativeTime = Time.realtimeSinceStartup - NetworkTransport.GetRemoteDelayTimeMS(conn.m_hostId, conn.m_connectionId, timestamp, out error) / 1000f;

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
