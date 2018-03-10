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
#if SERVER || CLIENT

        public bool showGUI = true;
        [SerializeField]
        public int offsetX;
        [SerializeField]
        public int offsetY;

        // Runtime variable
        bool m_ShowServer;

        public KeyCode startMainServerKeyCode = KeyCode.S;
        public KeyCode startHostKeyCode = KeyCode.H;
        public KeyCode startConnectionKeyCode = KeyCode.C;
        public KeyCode stopKeyCode = KeyCode.X;

        private NetworkingSystem networkingSystem;
        void Awake()
        {
            networkingSystem = GetComponent<NetworkingSystem>();
        }

        void Update()
        {
            if (!showGUI)
                return;

            if (networkingSystem == null)
                networkingSystem = GetComponent<NetworkingSystem>();
#if SERVER && CLIENT
            if (!networkingSystem.MainServerIsActive() && !networkingSystem.ConnectionIsActive())
#elif SERVER
            if (!networkingSystem.MainServerIsActive())
#elif CLIENT
            if (!networkingSystem.ConnectionIsActive())
#endif
            {
                if (UnityEngine.Application.platform != RuntimePlatform.WebGLPlayer)
                {
#if SERVER
                    if (Input.GetKeyDown(startMainServerKeyCode))
                    {
                        networkingSystem.StartMainServer();
                    }
#endif
#if SERVER && CLIENT
                    if (Input.GetKeyDown(startHostKeyCode))
                    {
                        networkingSystem.StartHost();
                    }
#endif
                }
#if CLIENT
                if (Input.GetKeyDown(startConnectionKeyCode))
                {
                    networkingSystem.StartConnection();
                }
#endif
            }
#if SERVER
            else if (networkingSystem.MainServerIsActive())
            {
#if CLIENT
                if (networkingSystem.connections.Count > 0)
                {
                    if (Input.GetKeyDown(stopKeyCode))
                    {
                        networkingSystem.StopHost();
                    }
                }
                else
                {
#endif
                    if (Input.GetKeyDown(stopKeyCode))
                    {
                        networkingSystem.StopAllServers();
                    }
#if CLIENT
                }
#endif

                return;
            }
#endif

#if CLIENT
            if (networkingSystem.ConnectionIsActive())
            {
                if (Input.GetKeyDown(stopKeyCode))
                {
                    networkingSystem.StopAllConnections();
                }
            }
#endif
        }

        void OnValidate()
        {
            if (networkingSystem == null)
                this.gameObject.GetComponent<NetworkingSystem>();
        }


        void OnGUI()
        {
            if (!showGUI)
                return;

            int xpos = 10 + offsetX;
            int ypos = 40 + offsetY;
            const int spacing = 24;

#if SERVER && CLIENT
            if (!networkingSystem.MainServerIsActive() && !networkingSystem.ConnectionIsActive())

#elif SERVER
            if ( !networkingSystem.MainServerIsActive())

#elif CLIENT
            if (!networkingSystem.ConnectionIsActive())
#endif
            {
#if SERVER && CLIENT
                if (UnityEngine.Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    if (GUI.Button(new Rect(xpos, ypos, 400, 20), "Host with NetworkingSystem configuration"))
                    {
                        networkingSystem.StartHost();
                    }
                    ypos += spacing;
                }
#endif
#if CLIENT
                if (GUI.Button(new Rect(xpos, ypos, 400, 20), "Join with NetworkingSystem configuration"))
                {
                    networkingSystem.StartConnection();
                }
                ypos += spacing;
#endif

                if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    // cant be a server in webgl build
                    GUI.Box(new Rect(xpos, ypos, 200, 25), "(  WebGL cannot be server  )");
                    ypos += spacing;
                }
#if SERVER
                else
                {
                    if (GUI.Button(new Rect(xpos, ypos, 400, 20), "Server Only(S) with NetworkingSystem configuration"))
                    {
                        networkingSystem.StartMainServer();
                    }
                    ypos += spacing;
                }
#endif
            }
            else
            {
#if SERVER
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
#endif

#if CLIENT
                if (networkingSystem.ConnectionIsActive())
                {
                    GUI.Label(new Rect(xpos, ypos, 300, 20), "Client: address=" + networkingSystem.connections[0].m_serverAddress + " port=" + networkingSystem.connections[0].m_serverPort);
                    ypos += spacing;
                }
#endif
            }

#if SERVER && CLIENT
            if (networkingSystem.MainServerIsActive() || networkingSystem.ConnectionIsActive())
            {
                if (GUI.Button(new Rect(xpos, ypos, 200, 20), "Stop (X)"))
                {
                    networkingSystem.StopHost();
                }
                ypos += spacing;
            }
#endif
        }
#endif
    }
}
