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
    public class NetworkMovementSynchronization : NetworkingBehaviour
    {
#if CLIENT || SERVER

        [Space(10)]
        public bool useAdaptativeSynchronizationBackTime = true;

        [Space(5)]
        public float m_nonAdaptativeBacktime = 0.1f;

        [Space(5)]
        public float m_adaptativeSynchronizationBackTime = 0.15f;
        public float m_maxSynchronizationBacktime = 0.3f;
        public float m_minSynchronisationBacktime = 0.1f;
        public float m_adaptationAmount = 2f;

        public float CurrentSynchronizationBackTime
        {
            get
            {
                if (useAdaptativeSynchronizationBackTime)
                    return m_adaptativeSynchronizationBackTime;
                else
                    return m_nonAdaptativeBacktime;
            }
        }

        [Space(10)]
#if CLIENT
        [Tooltip("Update x per second")]
        public float clientUpdateRate = 20f;
#endif

#if SERVER
        [Tooltip("Update x per second")]
        public float maxServerUpdate = 20f;
#endif


        [Tooltip("Synchronised object will have their movement synchronised.")]
        public MovementSynchronizer[] movementSynchronizers;

        [Space(10)]
        [SerializeField]
        float lagAverage = 0.15f;


        NetworkingWriter m_reliableWriter = new NetworkingWriter();
        NetworkingWriter m_unreliableWriter = new NetworkingWriter();

        void Awake()
        {
            for (int i = 0; i < movementSynchronizers.Length; i++)
            {
                movementSynchronizers[i].Init(this);
            }

            lagAverage = m_minSynchronisationBacktime / m_adaptationAmount;
        }

        void Update()
        {
            foreach (MovementSynchronizer m in movementSynchronizers)
                m.MovementUpdate();
        }



        void OwnerUpdate()
        {
            //2 cases  : server project and no owner or client and local client.
            //In this 2 case we send information directly to clients or server

#if SERVER
            if (isServer && this.serverConnection == null)
            {
                //Search new state and register it, it will be send the next serverUpdate

                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    MovementSynchronizer movementSynchronizer = movementSynchronizers[i];

                    if (Time.realtimeSinceStartup > movementSynchronizer.m_lastInterpolationUpdateTimer && movementSynchronizer.NeedToUpdate())
                    {
                        movementSynchronizer.AddCurrentStateToBuffer();
                        movementSynchronizer.m_lastInterpolationUpdateTimer = Time.realtimeSinceStartup + 1f / this.maxServerUpdate;
                    }
                }
                return;
            }
#endif

#if CLIENT
            if (isLocalClient)
            {
                m_reliableWriter.SeekZero(true);
                m_unreliableWriter.SeekZero(true);

                int reliableUpdateMask = 0;
                int unreliableUpdateMask = 0;


                for (int i = 0; i < movementSynchronizers.Length; i++)
                {
                    MovementSynchronizer movementSynchronizer = movementSynchronizers[i];

                    if  //Real need of synchronization 
                    (Time.realtimeSinceStartup > movementSynchronizer.m_lastInterpolationUpdateTimer && movementSynchronizer.NeedToUpdate())
                    {
                        unreliableUpdateMask = unreliableUpdateMask | (1 << i);
                        movementSynchronizer.m_lastInterpolationUpdateTimer = Time.realtimeSinceStartup + 1f / this.clientUpdateRate;
                        movementSynchronizer.m_clientSentEndInterpolation = false;
                    }
                    else if //Had to synchronize the lastest position
                        (Time.realtimeSinceStartup > movementSynchronizer.m_lastInterpolationUpdateTimer && !movementSynchronizer.m_clientSentEndInterpolation)
                    {
                        reliableUpdateMask = reliableUpdateMask | (1 << i);
                        movementSynchronizer.m_clientSentEndInterpolation = true;
                    }
                }


                if (reliableUpdateMask != 0)
                {
                    WriteCurrentMovements(movementSynchronizers, reliableUpdateMask, m_reliableWriter);
                    SendToServer("CmdGetDirectLastMovementInformations", NetworkingChannel.DefaultReliable, NetworkTransport.GetNetworkTimestamp(), m_reliableWriter.ToArray());
                }

                if (unreliableUpdateMask != 0)
                {
                    WriteCurrentMovements(movementSynchronizers, unreliableUpdateMask, m_unreliableWriter);
                    SendToServer("CmdGetDirectMovementInformations", NetworkingChannel.DefaultUnreliable, NetworkTransport.GetNetworkTimestamp(), m_unreliableWriter.ToArray());
                }
                return;
            }
#endif
        }


#if SERVER
        void ServerUpdate()
        {
            if (isServer) //Server has to send its current information to all clients except the owner
            {
                foreach (NetworkingConnection connection in this.m_serverConnectionListeners)
                {
#if CLIENT
                    if (this.connection != null && this.connection.m_connectionId == connection.m_connectionId)
                    {
                        continue;
                    }
#endif
                    if (connection != this.serverConnection)
                    {
                        m_reliableWriter.SeekZero(true);
                        m_unreliableWriter.SeekZero(true);

                        int reliableUpdateMask = 0;
                        int unreliableUpdateMask = 0;

                        if (movementSynchronizers.Length != 0)
                        {
                            for (int i = 0; i < movementSynchronizers.Length; i++)
                            {
                                MovementSynchronizer movementSynchronizer = movementSynchronizers[i];

                                float lastServerInterpolationTimer = movementSynchronizer.m_lastServerInterpolationUpdateTimers[connection];
                          
                                if  //Real need of synchronization 
                                (Time.realtimeSinceStartup > lastServerInterpolationTimer && movementSynchronizer.ServerNeedUpdateLastestStateTo(connection))
                                {
                                    unreliableUpdateMask = unreliableUpdateMask | (1 << i);
                                    movementSynchronizer.m_lastServerInterpolationUpdateTimers[connection] = Time.realtimeSinceStartup + 1f / this.maxServerUpdate; //Modif this by a sendRate modifier 
                                    movementSynchronizer.RegisterLastestState(connection);

                                    movementSynchronizer.m_serverSentEndInterpolation[connection] = false;
                                }
                                else if //Had to synchronize the lastest position
                                    (Time.realtimeSinceStartup > lastServerInterpolationTimer && !movementSynchronizer.m_serverSentEndInterpolation[connection] && movementSynchronizer.m_currentStatesIndex >= 0)
                                {
                                    reliableUpdateMask = reliableUpdateMask | (1 << i);
                                    movementSynchronizer.m_serverSentEndInterpolation[connection] = true;
                                }
                            }
                        }

                        if (reliableUpdateMask != 0)
                        {
                            WriteLastStateMovements(movementSynchronizers, reliableUpdateMask, m_reliableWriter);
                            SendToConnection(connection, "RpcGetLastStateMovementInformations", NetworkingChannel.DefaultReliable, m_reliableWriter.ToArray());
                        }

                        if (unreliableUpdateMask != 0)
                        {
                            WriteLastStateMovements(movementSynchronizers, unreliableUpdateMask, m_unreliableWriter);
                            SendToConnection(connection, "RpcGetStateMovementInformations", NetworkingChannel.DefaultUnreliable, m_unreliableWriter.ToArray());
                        }
                    }
                }
            }
        }
#endif

        void FixedUpdate()
        {
            OwnerUpdate();

#if SERVER
            ServerUpdate();
#endif
        }


    void WriteCurrentMovements(MovementSynchronizer[] movementSynchronizers, int updateMask, NetworkingWriter writer)
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


#if SERVER
        void WriteLastStateMovements(MovementSynchronizer[] movementSynchronizers, int updateMask, NetworkingWriter writer)
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
                    writer.Write(movementSynchronizer.m_statesBuffer[0].m_timestamp);
                    movementSynchronizer.GetLastState(writer);
                }
            }
        }
#endif




        public override void OnStartAuthority()
        {

            if (movementSynchronizers.Length > 0)
            {
                SerializeAllMovementSynchronizers(m_reliableWriter);

#if SERVER
                if (isServer)
                {
                    SendToAllConnections("GetMovementSyncInformations", NetworkingChannel.DefaultReliableSequenced, m_reliableWriter.ToArray());
                    return;
                }
#endif

#if CLIENT
                if (isClient)
                {
                    SendToServer("CmdGetMovementSyncInformations", NetworkingChannel.DefaultReliableSequenced, m_reliableWriter.ToArray());
                    return;
                }
#endif
            }
        }

#if SERVER

        public override void OnServerSyncNetId(NetworkingConnection conn)
        {
            if (movementSynchronizers.Length > 0)
            {
                SerializeAllMovementSynchronizers(m_reliableWriter);
                SendToConnection(conn, "GetMovementSyncInformations", NetworkingChannel.DefaultReliableSequenced, m_reliableWriter.ToArray());
            }
        }

        public override void OnServerAddListener(NetworkingConnection conn)
        {
            if (movementSynchronizers.Length > 0)
            {
                SerializeAllMovementSynchronizers(m_reliableWriter);
                SendToConnection(conn, "GetMovementSyncInformations", NetworkingChannel.DefaultReliableSequenced, m_reliableWriter.ToArray());
            }
        }
#endif


        void SerializeAllMovementSynchronizers(NetworkingWriter writer)
        {
            writer.SeekZero(true);
            int updateMask = 0;
            for (int i = 0; i < movementSynchronizers.Length; i++)
            {
                updateMask = updateMask | (1 << i);
            }

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
        }

        [NetworkedFunction]
        void CmdGetMovementSyncInformations(byte[] info)
        {
#if SERVER
            GetMovementSyncInformations(info);
            SendToAllConnections("GetMovementSyncInformations", NetworkingChannel.DefaultReliableSequenced, info);
#endif
        }

        [NetworkedFunction]
        void GetMovementSyncInformations(byte[] info)
        {
            if (hasAuthority)
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



        void LocalGetDirectMovementInformations(int timestamp, byte[] info, bool isLastState, NetworkingConnection conn)
        {
            NetworkingReader reader = new NetworkingReader(info);

            byte error;
            int remoteTimeDelay = NetworkTransport.GetRemoteDelayTimeMS(conn.m_hostId, conn.m_connectionId, timestamp, out error);
            float relativeTime = Time.realtimeSinceStartup - remoteTimeDelay / 1000f;
            int localTimestamp = NetworkTransport.GetNetworkTimestamp() - remoteTimeDelay;

            lagAverage = Mathf.Lerp(lagAverage, (Time.realtimeSinceStartup - relativeTime), 0.001f);

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
                        movementSynchronizers[i].ReceiveCurrentState(localTimestamp, relativeTime, isLastState, reader);
                    }
                }
            }
        }



        //Function duplicated 
        //TODO : anticheat system server side

        [NetworkedFunction]
        void CmdGetDirectLastMovementInformations(int timestamp, byte[] info)
        {
#if SERVER
            LocalGetDirectMovementInformations(timestamp, info, true, this.serverConnection);
#endif
        }

        [NetworkedFunction]
        void CmdGetDirectMovementInformations(int timestamp, byte[] info)
        {
#if SERVER
            LocalGetDirectMovementInformations(timestamp, info, false, this.serverConnection);
#endif
        }


        [NetworkedFunction]
        void RpcGetDirectLastMovementInformations(int timestamp, byte[] info)
        {
#if CLIENT
            LocalGetDirectMovementInformations(timestamp, info, true, this.connection);
#endif
        }

        [NetworkedFunction]
        void RpcGetDirectMovementInformations(int timestamp, byte[] info)
        {
#if CLIENT
            LocalGetDirectMovementInformations(timestamp, info, false, this.connection);
#endif
        }


#if CLIENT
        void LocalGetStateMovementInformations(byte[] info, bool isLastState)
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
                    if ((updateMask & (1 << i)) != 0 || movementSynchronizers.Length == 1)
                    {

                        byte error;
                        int remoteTimeDelay = NetworkTransport.GetRemoteDelayTimeMS(this.connection.m_hostId, this.connection.m_connectionId, reader.ReadInt32(), out error);
                        float relativeTime = Time.realtimeSinceStartup - remoteTimeDelay / 1000f;
                        int localTimestamp = NetworkTransport.GetNetworkTimestamp() - remoteTimeDelay;

                        lagAverage = Mathf.Lerp(lagAverage, (Time.realtimeSinceStartup - relativeTime), 0.001f);

                        m_adaptativeSynchronizationBackTime = Mathf.Max(m_minSynchronisationBacktime, Mathf.Min(m_maxSynchronizationBacktime, lagAverage * (m_adaptationAmount)));

                        movementSynchronizers[i].ReceiveCurrentState(localTimestamp, relativeTime, isLastState, reader);
                    }
                }
            }
        }
#endif


        [NetworkedFunction]
        void RpcGetLastStateMovementInformations(byte[] info)
        {
#if CLIENT
            LocalGetStateMovementInformations(info, true);
#endif
        }

        [NetworkedFunction]
        void RpcGetStateMovementInformations(byte[] info)
        {
#if CLIENT
            LocalGetStateMovementInformations(info, false);
#endif
        }
#endif
    }
}
