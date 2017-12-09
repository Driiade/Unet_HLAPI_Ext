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
    public class NetworkMessages
    {
        #region Standards Assets
        public const short LocalClientConnectMessage = 100;
        public const short ClientConnectFromServerMessage = 101;
        public const short ClientReadyFromServerMessage = 102;

        public const short ServerReadyMessage = 104;

        public const short TimerSynchronisationMessage = 200;
        public const short TimerUpdateMessage = 201;
        public const short TimerStartMessage = 202;
        public const short TimerStopMessage = 203;
        public const short TimerAvortMessage = 204;

        #endregion;

        #region Custom Game Messages
        public const int realityMsg = 301;
        public const int endGameMsg = 302;
        #endregion;
    }
}
