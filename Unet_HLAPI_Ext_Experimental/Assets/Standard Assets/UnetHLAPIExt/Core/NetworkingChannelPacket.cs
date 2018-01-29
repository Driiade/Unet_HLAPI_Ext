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


namespace BC_Solution.UnetNetwork
{
    // This is used by the ChannelBuffer when buffering traffic.
    // Unreliable channels have a single ChannelPacket, Reliable channels have single "current" packet and a list of buffered ChannelPackets
    public struct NetworkingChannelPacket  {

        int m_Position;
        byte[] m_Buffer;
        bool m_IsReliable;

        public NetworkingChannelPacket(int packetSize, bool isReliable)
        {
            m_Position = 0;
            m_Buffer = new byte[packetSize];
            m_IsReliable = isReliable;
        }

        public void Reset()
        {
            m_Position = 0;
        }

        public bool IsEmpty()
        {
            return m_Position == 0;
        }

        public void Write(byte[] bytes, int numBytes)
        {
            Array.Copy(bytes, 0, m_Buffer, m_Position, numBytes);
            m_Position += numBytes;
        }

        public bool HasSpace(int numBytes)
        {
            return m_Position + numBytes <= m_Buffer.Length;
        }

        public bool SendToTransport(NetworkingConnection conn, int channelId)
        {
            byte error;

            if (!conn.TransportSend(m_Buffer, (ushort)m_Position, channelId, out error))
            {
                if (m_IsReliable && error == (int)NetworkError.NoResources)
                {
                    // this packet will be buffered by the containing ChannelBuffer, so this is not an error

#if UNITY_EDITOR
                    /*     UnityEditor.NetworkDetailStats.IncrementStat(
                             UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                             MsgType.HLAPIResend, "msg", 1);*/
#endif

                    return false;
                }
                else
                {
                    //Happen when disconnecting (if you find a solution : benoit.constantin@hotmail.com)
                    if (error != (int)NetworkError.WrongConnection)
                    {
                        if (LogFilter.logError) { Debug.LogError("Failed to send internal buffer channel:" + channelId + " bytesToSend:" + m_Position); }
                        if (LogFilter.logError) { Debug.LogError("Send Error: " + (NetworkError)error + " channel:" + channelId + " bytesToSend:" + m_Position); }
                    }
                    return false;
                }
            }

            m_Position = 0;
            return true;
        }
    }
}
