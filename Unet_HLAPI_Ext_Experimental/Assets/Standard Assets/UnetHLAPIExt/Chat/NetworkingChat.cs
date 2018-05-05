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
using UnityEngine.UI;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingChat : NetworkingBehaviour
    {
        [SerializeField]
        Text text;

        [NetworkedVariable(callbackName ="SetText", syncMode = NetworkedVariable.SYNC_MODE.ONLY_SERVER)]
        string message1 = new string("blabla".ToCharArray());
       // SyncVar<string> message2 = new SyncVar<string>("test");


       /* private void Awake()
        {
            message1.callback += SetText;
        }*/

        public void Send(string message)
        {
           SetSyncVar("message1",ref this.message1, message);
               // this.message2.Value = message + " 2";
        }

        private void SetText(string message)
        {
            Debug.Log(message);
            text.text = message;
           // Debug.Log(message2.Value);
        }
    }
}
