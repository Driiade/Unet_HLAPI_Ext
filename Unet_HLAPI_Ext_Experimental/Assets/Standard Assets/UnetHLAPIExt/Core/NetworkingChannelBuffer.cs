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
using System;
using UnityEngine.Networking;
using System.IO;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingChannelBuffer {

        //NetworkingConnection m_connection;

        MemoryStream m_currentPacket = new MemoryStream();

        //float m_LastFlushTime;

        byte m_ChannelId;
        int m_MaxPacketSize;
        bool m_IsReliable;
        bool m_AllowFragmentation;
        bool m_IsBroken;
        int m_MaxPendingPacketCount;

       // const int k_MaxFreePacketCount = 512; //  this is for all connections. maybe make this configurable
        public const int MaxPendingPacketCount = 16;  // this is per connection. each is around 1400 bytes (MTU)
        public const int MaxBufferedPackets = 512;

        Queue<MemoryStream> m_PendingPackets;
        //static List<MemoryStream> s_FreePackets;
        static internal int pendingPacketCount; // this is across all connections. only used for profiler metrics.

        // config
      //  public float maxDelay = 0.01f; ? already in networkTransport ?

        // stats
        float m_LastBufferedMessageCountTimer = Time.realtimeSinceStartup;

        public int numMsgsOut { get; private set; }
        public int numBufferedMsgsOut { get; private set; }
        public int numBytesOut { get; private set; }

        public int numMsgsIn { get; private set; }
        public int numBytesIn { get; private set; }

        public int numBufferedPerSecond { get; private set; }
        public int lastBufferedPerSecond { get; private set; }

        static NetworkingWriter s_sendWriter = new NetworkingWriter();
        static NetworkingWriter s_fragmentWriter = new NetworkingWriter();

        // We need to reserve some space for header information, this will be taken off the total channel buffer size
        const int k_PacketHeaderReserveSize = 100;

        public NetworkingChannelBuffer(int bufferSize, byte cid, bool isReliable, bool isSequenced)
        {
            m_MaxPacketSize = bufferSize - k_PacketHeaderReserveSize;      

            m_ChannelId = cid;
            m_MaxPendingPacketCount = MaxPendingPacketCount;
            m_IsReliable = isReliable;
            m_AllowFragmentation = (isReliable && isSequenced);
            if (isReliable)
            {
                m_PendingPackets = new Queue<MemoryStream>();
            }
        }



        public bool SetOption(NetworkingChannel.ChannelOption option, int value)
        {
            switch (option)
            {
                case NetworkingChannel.ChannelOption.MaxPendingBuffers:
                    {
                        if (!m_IsReliable)
                        {
                            // not an error
                            //if (LogFilter.logError) { Debug.LogError("Cannot set MaxPendingBuffers on unreliable channel " + m_ChannelId); }
                            return false;
                        }
                        if (value < 0 || value >= MaxBufferedPackets)
                        {
                            if (LogFilter.logError) { Debug.LogError("Invalid MaxPendingBuffers for channel " + m_ChannelId + ". Must be greater than zero and less than " + MaxBufferedPackets); }
                            return false;
                        }
                        m_MaxPendingPacketCount = value;
                        return true;
                    }

                case NetworkingChannel.ChannelOption.AllowFragmentation:
                    {
                        m_AllowFragmentation = (value != 0);
                        return true;
                    }

                case NetworkingChannel.ChannelOption.MaxPacketSize:
                    {
                        if (m_currentPacket.Position > 0 || m_PendingPackets.Count > 0)
                        {
                            if (LogFilter.logError) { Debug.LogError("Cannot set MaxPacketSize after sending data."); }
                            return false;
                        }

                        if (value <= 0)
                        {
                            if (LogFilter.logError) { Debug.LogError("Cannot set MaxPacketSize less than one."); }
                            return false;
                        }

                        if (value > m_MaxPacketSize)
                        {
                            if (LogFilter.logError) { Debug.LogError("Cannot set MaxPacketSize to greater than the existing maximum (" + m_MaxPacketSize + ")."); }
                            return false;
                        }
                        m_MaxPacketSize = value;
                        return true;
                    }
            }
            return false;
        }

        public void CheckInternalBuffer(NetworkingConnection conn)
        {
            // if (Time.realtimeSinceStartup - m_LastFlushTime > maxDelay && !m_currentPacket.IsEmpty())
            //{
            if (m_currentPacket.Position > 0)
            {
                SendInternalBuffer(conn);
                //m_LastFlushTime = Time.realtimeSinceStartup;
            }
            //}

            if (Time.realtimeSinceStartup - m_LastBufferedMessageCountTimer > 1.0f)
            {
                lastBufferedPerSecond = numBufferedPerSecond;
                numBufferedPerSecond = 0;
                m_LastBufferedMessageCountTimer = Time.realtimeSinceStartup;
            }
        }

        public bool SendWriter(NetworkingConnection conn, NetworkingWriter writer)
        {
            byte[] bytes = writer.ToArray();
            return SendBytes(conn, bytes, bytes.Length);
        }

        public bool Send(NetworkingConnection conn, NetworkingMessage netMsg)
        {
            // build the stream
            s_sendWriter.StartMessage();
            netMsg.Serialize(s_sendWriter);
            s_sendWriter.FinishMessage();

            numMsgsOut += 1;
            return SendWriter(conn, s_sendWriter);
        }

        internal MemoryStream fragmentBuffer = new MemoryStream();
        bool readingFragment = false;

        internal bool HandleFragment(NetworkingReader reader)
        {
            int state = reader.ReadByte();
            if (state == 0)
            {
                if (readingFragment == false)
                {
                    fragmentBuffer.Seek(0, SeekOrigin.Begin);
                    readingFragment = true;
                }

                byte[] data = reader.ReadBytesAndSize();
                fragmentBuffer.Write(data,0, data.Length);
                return false;
            }
            else
            {
                readingFragment = false;
                return true;
            }
        }

        internal bool SendFragmentBytes(NetworkingConnection conn, byte[] bytes, int bytesToSend)
        {
            const int fragmentHeaderSize = 32;
            int pos = 0;
            while (bytesToSend > 0)
            {
                int diff = System.Math.Min(bytesToSend, m_MaxPacketSize - fragmentHeaderSize);
                byte[] buffer = new byte[diff];
                Array.Copy(bytes, pos, buffer, 0, diff);

                s_fragmentWriter.StartMessage();
                s_fragmentWriter.Write(NetworkingMessageType.Fragment);
                s_fragmentWriter.Write((byte)0);
                s_fragmentWriter.WriteBytesFull(buffer);
                s_fragmentWriter.FinishMessage();
                SendWriter(conn, s_fragmentWriter);

                pos += diff;
                bytesToSend -= diff;
            }

            // send finish
            s_fragmentWriter.StartMessage();
            s_fragmentWriter.Write(NetworkingMessageType.Fragment);
            s_fragmentWriter.Write((byte)1);
            s_fragmentWriter.FinishMessage();
            SendWriter(conn, s_fragmentWriter);

            return true;
        }

        internal bool SendBytes(NetworkingConnection conn, byte[] bytes, int bytesToSend)
        {
#if UNITY_EDITOR
          /*  UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.HLAPIMsg, "msg", 1); */
#endif
            if (bytesToSend >= UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("ChannelBuffer:SendBytes cannot send packet larger than " + UInt16.MaxValue + " bytes"); }
                return false;
            }

            if (bytesToSend <= 0)
            {
                // zero length packets getting into the packet queues are bad.
                if (LogFilter.logError) { Debug.LogError("ChannelBuffer:SendBytes cannot send zero bytes"); }
                return false;
            }

            if (bytesToSend > m_MaxPacketSize)
            {
                if (m_AllowFragmentation)
                {
                    return SendFragmentBytes(conn, bytes, bytesToSend);
                }
                else
                {
                    // cannot do HLAPI fragmentation on this channel
                    if (LogFilter.logError) { Debug.LogError("Failed to send big message of " + bytesToSend + " bytes. The maximum is " + m_MaxPacketSize + " bytes on channel:" + m_ChannelId); }
                    return false;
                }
            }

            // not enough space for bytes to send?
            if (m_currentPacket.Position + bytesToSend > m_MaxPacketSize)
            {
                if (m_IsReliable)
                {
                    if (m_PendingPackets.Count == 0)
                    {
                        // nothing in the pending queue yet, just flush and write
                        if (!SendToTransport(conn, m_currentPacket,m_IsReliable, m_ChannelId))
                        {
                            QueuePacket();
                        }
                        m_currentPacket.Write(bytes,0, bytesToSend);
                        return true;
                    }

                    if (m_PendingPackets.Count >= m_MaxPendingPacketCount)
                    {
                        if (!m_IsBroken)
                        {
                            // only log this once, or it will spam the log constantly
                            if (LogFilter.logError) { Debug.LogError("ChannelBuffer buffer limit of " + m_PendingPackets.Count + " packets reached."); }
                        }
                        m_IsBroken = true;
                        return false;
                    }

                    // calling SendToTransport here would write out-of-order data to the stream. just queue
                    QueuePacket();
                    m_currentPacket.Write(bytes,0, bytesToSend);
                    return true;
                }

                if (!SendToTransport(conn, m_currentPacket, m_IsReliable, m_ChannelId))
                {
                    if (LogFilter.logError) { Debug.Log("ChannelBuffer SendBytes no space on unreliable channel " + m_ChannelId); }
                    return false;
                }

                m_currentPacket.Write(bytes,0, bytesToSend);
                return true;
            }

            m_currentPacket.Write(bytes,0, bytesToSend);
            // if (maxDelay == 0.0f)
            //{
            // return SendInternalBuffer(conn);
            //}
            return SendInternalBuffer(conn);
        }

        void QueuePacket()
        {
            pendingPacketCount += 1;
            m_PendingPackets.Enqueue(m_currentPacket);
            m_currentPacket = new MemoryStream();
        }


        public bool SendInternalBuffer(NetworkingConnection conn)
        {
#if UNITY_EDITOR
            /*  UnityEditor.NetworkDetailStats.IncrementStat(
                  UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                  MsgType.LLAPIMsg, "msg", 1); */
#endif
            if (m_IsReliable && m_PendingPackets.Count > 0)
            {
                // send until transport can take no more
                while (m_PendingPackets.Count > 0)
                {
                    MemoryStream packet = m_PendingPackets.Dequeue();
                    if (!SendToTransport(conn, packet, m_IsReliable, m_ChannelId))
                    {
                        m_PendingPackets.Enqueue(packet);
                        break;
                    }
                    pendingPacketCount -= 1;

                    if (m_IsBroken && m_PendingPackets.Count < (m_MaxPendingPacketCount / 2))
                    {
                        if (LogFilter.logWarn) { Debug.LogWarning("ChannelBuffer recovered from overflow but data was lost."); }
                        m_IsBroken = false;
                    }
                }
                return true;
            }
            return SendToTransport(conn, m_currentPacket, m_IsReliable, m_ChannelId);
        }

        // vis2k: send packet to transport (moved here from old ChannelPacket.cs)
        static bool SendToTransport(NetworkingConnection conn, MemoryStream packet, bool reliable, int channelId)
        {
            //Debug.Log("Paquet size : " + packet.ToArray().Length);
            //Debug.Log("Paquet position : " + (ushort)packet.Position);

            // vis2k: control flow improved to something more logical and shorter
            byte error;
            if (conn.TransportSend(packet.ToArray(), (ushort)packet.Position, channelId, out error))
            {
                packet.Position = 0;
                return true;
            }
            else
            {
                // NoResources and reliable? Then it will be resent, so don't reset position, just return false.
                if (error == (int)NetworkError.NoResources && reliable)
                {
#if UNITY_EDITOR
                 /*   UnityEditor.NetworkDetailStats.IncrementStat(
                        UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                        MsgType.HLAPIResend, "msg", 1);*/
#endif
                    return false;
                }

                // otherwise something unexpected happened. log the error, reset position and return.
                if (LogFilter.logError) { Debug.LogError("SendToTransport failed. error:" + (NetworkError)error + " channel:" + channelId + " bytesToSend:" + packet.Position); }
                packet.Position = 0;
                return false;
            }
        }


        public static bool IsSequencedQoS(QosType qos) // vis2k: made public
        {
            return (qos == QosType.ReliableSequenced || qos == QosType.UnreliableSequenced);
        }

        public static bool IsReliableQoS(QosType qos) // vis2k: made public
        {
            return (qos == QosType.Reliable || qos == QosType.ReliableFragmented || qos == QosType.ReliableSequenced || qos == QosType.ReliableStateUpdate);
        }

        public static bool IsUnreliableQoS(QosType qos) // vis2k added this one too
        {
            return (qos == QosType.Unreliable || qos == QosType.UnreliableFragmented || qos == QosType.UnreliableSequenced || qos == QosType.StateUpdate);
        }
    }
}
