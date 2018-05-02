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
using UnityEngine.SceneManagement;
using System;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingSceneSystem : Singleton<NetworkingSceneSystem>
    {
#if SERVER
        public Action<NetworkingConnection, Scene> OnServerLoadScene;

#endif
#if CLIENT
        public Action<NetworkingConnection, Scene> OnClientSendScene;
#endif

        protected override void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
#if SERVER
            NetworkingSystem.RegisterServerHandler(NetworkingMessageType.ConnectionLoadScene, HandleServerSceneConnection);
#endif
#if CLIENT
            NetworkingConnection.OnConnectionConnect += OnConnectionConnect;
#endif
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
#if SERVER
            NetworkingSystem.UnRegisterServerHandler(NetworkingMessageType.ConnectionLoadScene, HandleServerSceneConnection);
#endif
#if CLIENT
            NetworkingConnection.OnConnectionConnect -= OnConnectionConnect;
#endif
        }


        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(SendServerSceneLoaded(scene));
        }

        IEnumerator SendServerSceneLoaded(Scene scene)
        {
            yield return null; //Wait 1 frame to fully load scene

#if SERVER && CLIENT
            foreach (NetworkingConnection conn in NetworkingSystem.Instance.connections)
            {
                if (conn.m_server == null) //not host
                {
                    conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(scene.name), NetworkingChannel.DefaultReliableSequenced);

                    if (OnClientSendScene != null)
                        OnClientSendScene(conn, scene);
                }
             
            }
#elif CLIENT
                foreach (NetworkingConnection conn in NetworkingSystem.Instance.connections)
                {
                    conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(scene.name), NetworkingChannel.DefaultReliableSequenced);
                   
                    if (OnClientSendScene != null)
                        OnClientSendScene(conn, scene);
                }
#endif
        }

#if SERVER
        void HandleServerSceneConnection(NetworkingMessage netMsg)
        {
            string sceneName = netMsg.As<StringMessage>().m_value;
            Scene scene = SceneManager.GetSceneByName(sceneName);

            if (scene.name == null)
            {
                Debug.LogWarning("The server doesn't know the scene : " + sceneName); //Can be normal
            }
            else
            {

                if (OnServerLoadScene != null)
                    OnServerLoadScene(netMsg.m_connection, scene);
            }

        }
#endif

#if CLIENT
        void OnConnectionConnect(NetworkingConnection conn)
        {
#if SERVER
            if (conn.m_server == null)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(scene.name));

                    if (OnClientSendScene != null)
                        OnClientSendScene(conn, scene);
                }
            }
#elif CLIENT
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    conn.Send(NetworkingMessageType.ConnectionLoadScene, new StringMessage(scene.name));

                  if (OnClientSendScene != null)
                       OnClientSendScene(conn, scene);
                }
#endif
        }
#endif

    }
}
