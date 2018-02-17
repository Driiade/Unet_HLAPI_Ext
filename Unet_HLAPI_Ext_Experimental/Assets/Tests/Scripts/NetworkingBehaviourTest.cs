using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BC_Solution.UnetNetwork;

public class NetworkingBehaviourTest : NetworkingBehaviour {

    public float sendingRate = 0.5f;

    int cpt = 0;

    float timer;
	// Update is called once per frame
	void Update () {

        if (Time.time > timer)
        {
            timer = Time.time + sendingRate;
            if (isClient)
            {
                SendToServer("Test", NetworkingChannel.DefaultReliableSequenced, "[Command] hello world : ", cpt);
                AutoSendToConnections("Test", NetworkingChannel.DefaultReliableSequenced, "[Auto Rpc] hello world : ", cpt);
            }

            if (isServer)
                SendToAllConnections("Test", NetworkingChannel.DefaultReliableSequenced, "[Rpc] hello world : ", cpt);

            cpt++;
        }
    }

    [Networked]
    void Test(string message, int cpt)
    {
        Debug.Log(message + cpt);
    }
}
