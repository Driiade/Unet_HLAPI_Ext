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

namespace BC_Solution.UnetNetwork
{
    public class NetworkingMessageType
    {
        public class Channels
        {
            public const int DefaultReliableSequenced = 0;
            public const int DefaultReliable = 1;
            public const int DefaultUnreliable = 2;
        }

        public enum ChannelOption
        {
            MaxPendingBuffers = 1,
            AllowFragmentation = 2,
            MaxPacketSize = 3
            // maybe add an InitialCapacity for Pending Buffers list if needed in the future
        }

        #region core
        // internal system messages - cannot be replaced by user code
        public const ushort ObjectDestroy = 1;
        public const ushort Rpc = 2;
        public const ushort ObjectSpawn = 3;
        public const ushort Owner = 4;
        public const ushort Command = 5;
        public const ushort LocalPlayerTransform = 6;
        public const ushort SyncEvent = 7;
        public const ushort UpdateVars = 8;
        public const ushort SyncList = 9;
        public const ushort ObjectSpawnScene = 10;
        public const ushort NetworkInfo = 11;
        public const ushort SpawnFinished = 12;
        public const ushort ObjectHide = 13;
        public const ushort CRC = 14;
        public const ushort AssignClientAuthority = 15;
        public const ushort UnassignClientAuthority = 16;
        public const ushort Fragment = 17;
        public const ushort PeerClientAuthority = 18;
        public const ushort AutoRpc = 19;

        // used for profiling
        internal const ushort UserMessage = 0;
        internal const ushort HLAPIMsg = 28;
        internal const ushort LLAPIMsg = 29;
        internal const ushort HLAPIResend = 30;
        internal const ushort HLAPIPending = 31;

        public const ushort InternalHighest = 31;

        // public system messages - can be replaced by user code
        public const ushort Connect = 32;
        public const ushort Disconnect = 33;
        public const ushort Error = 34;
        public const ushort Ready = 35;
        public const ushort NotReady = 36;
        public const ushort AddPlayer = 37;
        public const ushort RemovePlayer = 38;
        public const ushort Scene = 39;

        public const ushort LocalClientConnectMessage = 40;
        //public const ushort ClientConnectFromServerMessage = 41;
        //public const ushort ClientReadyFromServerMessage = 42;
#if ENABLE_UNET_HOST_MIGRATION
        public const short ReconnectPlayer = 47;
#endif
        #endregion;

        #region Standards Assets


        public const ushort TimerSynchronisationMessage = 100;
        public const ushort TimerUpdateMessage = 101;
        public const ushort TimerStartMessage = 102;
        public const ushort TimerStopMessage = 103;
        public const ushort TimerAvortMessage = 104;

        public const ushort ChatMessage = 105;
        #endregion;

        #region Custom Game Messages

        #endregion;
    }
}
