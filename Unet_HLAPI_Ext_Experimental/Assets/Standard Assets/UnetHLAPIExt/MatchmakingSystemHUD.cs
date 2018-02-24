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
using System.ComponentModel;

namespace BC_Solution.UnetNetwork
{
    [AddComponentMenu("BC_Solution/UnetNetwork/MatchmakingSystemHUD")]
    [RequireComponent(typeof(NetworkingSystem))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class MatchmakingSystemHUD : MonoBehaviour
    {
        public MatchmakingSystem matchmakingSystem;
        public bool showGUI = true;
        public string serverAdress = "localhost";
        public int serverPort = 7777;

        [SerializeField]
        public int offsetX;
        [SerializeField]
        public int offsetY;

        void OnGUI()
        {
            if (!showGUI)
                return;

            int xpos = 10 + offsetX;
            int ypos = 40 + offsetY;
            const int spacing = 24;

            if (!NetworkingSystem.Instance.MainServerIsActive())
            {
                if (GUI.Button(new Rect(xpos, ypos, 300, 20), "Create LAN server"))
                {
                    if (MatchmakingSystem.Instance.ListLanMatch)
                        MatchmakingSystem.Instance.StopListMatch();

                    matchmakingSystem.CreateLANMatch("DefaultMatch" + Random.Range(0, 100), serverAdress, serverPort);
                }
                else if (!MatchmakingSystem.Instance.ListLanMatch)
                {
                    ypos += spacing;
                    if (GUI.Button(new Rect(xpos, ypos, 300, 20), "List LAN match"))
                    {
                        MatchmakingSystem.Instance.StartListLANMatch();
                    }
                }
                else if (MatchmakingSystem.Instance.ListLanMatch)
                {
                    ypos += spacing;

                    if (GUI.Button(new Rect(xpos, ypos, 300, 20), "Stop List LAN match"))
                    {
                        MatchmakingSystem.Instance.StopListMatch();
                    }

                    foreach (MatchmakingSystem.LanMatchInfo info in MatchmakingSystem.Instance.m_LANMatchsAvailables)
                    {
                        ypos += spacing;
                        if (GUI.Button(new Rect(xpos, ypos, 300, 20), info.name + " : " + info.serverAdress + " : " + info.serverPort))
                        {
                            if (MatchmakingSystem.Instance.ListLanMatch)
                                MatchmakingSystem.Instance.StopListMatch();

                            MatchmakingSystem.Instance.ConnectToLANMatch(info.serverAdress, info.serverPort);
                        }
                    }
                }
            }
        }
    }
}
