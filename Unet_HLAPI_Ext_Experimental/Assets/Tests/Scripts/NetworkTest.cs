using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;


namespace BC_Solution.UnetNetwork
{
    public class NetworkTest : MonoBehaviour
    {

        void OnGUI()
        {
            if (GUI.Button(new Rect(10, 50, 200, 20), "Launch Matchmaking"))
            {
                MatchmakingSystem.Instance.AutomaticMatchmaking();
            }

            if (GUI.Button(new Rect(10, 100, 200, 20), "Start host"))
            {
                NetworkingSystem.Instance.StartHost();
            }

            if (GUI.Button(new Rect(10, 150, 200, 20), "Start server"))
            {
                NetworkingSystem.Instance.StartServer();
            }

            if (GUI.Button(new Rect(10, 200, 200, 20), "Start client"))
            {
                NetworkingSystem.Instance.StartConnection();
            }

            if (GUI.Button(new Rect(10, 250, 200, 20), "Create player"))
            {
                ClientScene.AddPlayer(0);
            }

            if (GUI.Button(new Rect(210, 50, 200, 20), "Lock current match"))
            {
                MatchmakingSystem.Instance.LockOnlineMatch(true);
            }

            if (GUI.Button(new Rect(210, 150, 200, 20), "Stop server"))
            {
                NetworkingSystem.Instance.StopAllServers();
                // NetworkingSystem.Instance.StartHost();           
            }

            if (GUI.Button(new Rect(210, 200, 200, 20), "Stop client"))
            {
                NetworkingSystem.Instance.StopAllConnections();
                // NetworkingSystem.Instance.StartHost();
            }

            if (GUI.Button(new Rect(210, 100, 200, 20), "Stop host"))
            {
                NetworkingSystem.Instance.StopHost();
                // NetworkingSystem.Instance.StartHost();
            }
        }
    }
}
