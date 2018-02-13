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
using System.ComponentModel;
using UnityEngine.Networking;

namespace BC_Solution.UnetNetwork
{
    [AddComponentMenu("BC_Solution/UnetNetwork/NetworkingSytemHUD")]
    [RequireComponent(typeof(NetworkingSystem))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class NetworkingSystemHUD : MonoBehaviour
    {

        public NetworkingSystem networkingSystem;
        [SerializeField]
        public bool showGUI = true;
        [SerializeField]
        public int offsetX;
        [SerializeField]
        public int offsetY;

        // Runtime variable
        bool m_ShowServer;

        void Update()
        {
            if (!showGUI)
                return;

            if (!networkingSystem.MainServerIsActive() && !networkingSystem.ConnectionIsActive())
            {
                if (UnityEngine.Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    if (Input.GetKeyDown(KeyCode.S))
                    {
                        networkingSystem.StartMainServer();
                    }
                    if (Input.GetKeyDown(KeyCode.H))
                    {
                        networkingSystem.StartHost();
                    }
                }
                if (Input.GetKeyDown(KeyCode.C))
                {
                    networkingSystem.StartConnection();
                }
            }
            if (networkingSystem.MainServerIsActive())
            {
                if (networkingSystem.connections.Count > 0)
                {
                    if (Input.GetKeyDown(KeyCode.X))
                    {
                        networkingSystem.StopHost();
                    }
                }
                else
                {
                    if (Input.GetKeyDown(KeyCode.X))
                    {
                        networkingSystem.StopAllServers();
                    }
                }
            }
        }

        void OnGUI()
        {
            if (!showGUI)
                return;

            int xpos = 10 + offsetX;
            int ypos = 40 + offsetY;
            const int spacing = 24;


            if (!networkingSystem.MainServerIsActive() && !networkingSystem.ConnectionIsActive())
            {
                if (networkingSystem.connections.Count == 0)
                {
                    if (UnityEngine.Application.platform != RuntimePlatform.WebGLPlayer)
                    {
                        if (GUI.Button(new Rect(xpos, ypos, 400, 20), "Host with NetworkingSystem configuration"))
                        {
                            networkingSystem.StartHost();
                        }
                        ypos += spacing;
                    }

                    if (GUI.Button(new Rect(xpos, ypos, 400, 20), "Join with NetworkingSystem configuration"))
                    {
                        networkingSystem.StartConnection();
                    }

                    //networkingSystem.serverAdress = GUI.TextField(new Rect(xpos + 100, ypos, 95, 20), networkingSystem.serverAdress);
                    ypos += spacing;

                    if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
                    {
                        // cant be a server in webgl build
                        GUI.Box(new Rect(xpos, ypos, 200, 25), "(  WebGL cannot be server  )");
                        ypos += spacing;
                    }
                    else
                    {
                        if (GUI.Button(new Rect(xpos, ypos, 400, 20), "Server Only(S) with NetworkingSystem configuration"))
                        {
                            networkingSystem.StartMainServer();
                        }
                        ypos += spacing;
                    }
                }
                else
                {
                    GUI.Label(new Rect(xpos, ypos, 200, 20), "Connecting to " + networkingSystem.serverAdress + ":" + networkingSystem.serverPort + "..");
                    ypos += spacing;


                    if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Cancel Connection Attempt"))
                    {
                        networkingSystem.StopAllConnections();
                    }
                }
            }
            else
            {
                if (networkingSystem.MainServerIsActive())
                {
                    string serverMsg = "Server: port=" + networkingSystem.mainServer.m_serverAddress;
                    /*  if (networkingSystem.useWebSockets)
                      {
                          serverMsg += " (Using WebSockets)";
                      } */
                    GUI.Label(new Rect(xpos, ypos, 300, 20), serverMsg);
                    ypos += spacing;
                }
                if (networkingSystem.connections.Count != 0)
                {
                    GUI.Label(new Rect(xpos, ypos, 300, 20), "Client: address=" + networkingSystem.connections[0].m_serverAddress + " port=" + networkingSystem.connections[0].m_serverPort);
                    ypos += spacing;
                }
            }

          /*  if (NetworkClient.active && !ClientScene.ready)
            {
                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Client Ready"))
                {
                    ClientScene.Ready(networkingSystem.Client.connection);

                    if (ClientScene.localPlayers.Count == 0)
                    {
                        ClientScene.AddPlayer(0);
                    }
                }
                ypos += spacing;
            }*/

            if (networkingSystem.MainServerIsActive() || networkingSystem.ConnectionIsActive())
            {
                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Stop (X)"))
                {
                    networkingSystem.StopHost();
                }
                ypos += spacing;
            }

            if (!networkingSystem.MainServerIsActive() && networkingSystem.ConnectionIsActive())
            {
                ypos += 10;

                if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    GUI.Box(new Rect(xpos - 5, ypos, 220, 25), "(WebGL cannot use Match Maker)");
                    return;
                }

                /* if (networkingSystem.matchMaker == null)
                 {
                     if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Enable Match Maker (M)"))
                     {
                         networkingSystem.StartMatchMaker();
                     }
                     ypos += spacing;
                 }
                 else
                 {
                     if (networkingSystem.matchInfo == null)
                     {
                         if (networkingSystem.matches == null)
                         {
                             if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Create Internet Match"))
                             {
                                 networkingSystem.matchMaker.CreateMatch(networkingSystem.matchName, networkingSystem.matchSize, true, "", "", "", 0, 0, networkingSystem.OnMatchCreate);
                             }
                             ypos += spacing;

                             GUI.Label(new Rect(xpos, ypos, 100, 20), "Room Name:");
                             networkingSystem.matchName = GUI.TextField(new Rect(xpos + 100, ypos, 100, 20), networkingSystem.matchName);
                             ypos += spacing;

                             ypos += 10;

                             if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Find Internet Match"))
                             {
                                 networkingSystem.matchMaker.ListMatches(0, 20, "", false, 0, 0, networkingSystem.OnMatchList);
                             }
                             ypos += spacing;
                         }
                         else
                         {
                             for (int i = 0; i < networkingSystem.matches.Count; i++)
                             {
                                 var match = networkingSystem.matches[i];
                                 if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Join Match:" + match.name))
                                 {
                                     networkingSystem.matchName = match.name;
                                     networkingSystem.matchMaker.JoinMatch(match.networkId, "", "", "", 0, 0, networkingSystem.OnMatchJoined);
                                 }
                                 ypos += spacing;
                             }

                             if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Back to Match Menu"))
                             {
                                 networkingSystem.matches = null;
                             }
                             ypos += spacing;
                         }
                     } */

                /* if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Change MM server"))
                 {
                     m_ShowServer = !m_ShowServer;
                 }
                 if (m_ShowServer)
                 {
                     ypos += spacing;
                     if (GUI.Button(new Rect(xpos, ypos, 100, 20), "Local"))
                     {
                         networkingSystem.SetMatchHost("localhost", 1337, false);
                         m_ShowServer = false;
                     }
                     ypos += spacing;
                     if (GUI.Button(new Rect(xpos, ypos, 100, 20), "Internet"))
                     {
                         networkingSystem.SetMatchHost("mm.unet.unity3d.com", 443, true);
                         m_ShowServer = false;
                     }
                     ypos += spacing;
                     if (GUI.Button(new Rect(xpos, ypos, 100, 20), "Staging"))
                     {
                         networkingSystem.SetMatchHost("staging-mm.unet.unity3d.com", 443, true);
                         m_ShowServer = false;
                     }
                 } */

                /*ypos += spacing;

                GUI.Label(new Rect(xpos, ypos, 300, 20), "MM Uri: " + networkingSystem.matchMaker.baseUri);
                ypos += spacing;

                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Disable Match Maker"))
                {
                    networkingSystem.StopMatchMaker();
                }
                ypos += spacing; */
            }
        }
    }
}
