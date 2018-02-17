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

        #region core
        // internal system messages - cannot be replaced by user code
        public const ushort ObjectDestroy = 1;
        public const ushort Command = 2;
        public const ushort Rpc = 3;
        public const ushort AutoRpc = 4;
        public const ushort ObjectSpawn = 5;
        public const ushort SceneObjectNetId = 6;
        public const ushort ReplicatedPrefabScene = 7;
        public const ushort ObjectSpawnFinish = 8;
        //public const ushort Owner = 4;

        public const ushort SyncEvent = 9;
        public const ushort UpdateVars = 10;
        public const ushort SyncList = 11;
        public const ushort NetworkInfo = 12;

        public const ushort CRC = 13;
        public const ushort AssignClientAuthority = 14;
        public const ushort UnassignClientAuthority = 15;
        public const ushort Fragment = 16;
        public const ushort PeerClientAuthority = 17;
        public const ushort ConnectionLoadScene = 18;

        // used for profiling
        internal const ushort UserMessage = 0;

        // public system messages - can be replaced by user code
        public const ushort Connect = 32;
        public const ushort Disconnect = 33;
        public const ushort Error = 34;
       // public const ushort Ready = 35;
       // public const ushort NotReady = 36;
       // public const ushort AddPlayer = 37;
       // public const ushort RemovePlayer = 38;
        public const ushort Scene = 39;

       // public const ushort LocalClientConnectMessage = 40;
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
