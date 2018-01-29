using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BC_Solution.UnetNetwork;

public class NetworkingBehaviourTest : NetworkingBehaviour {

    int cpt = 0;

	// Update is called once per frame
	void Update () {

        if (isClient)
        {
            SendToServer("Test", NetworkingMessageType.Channels.DefaultReliableSequenced, "[Command] hello world : ", cpt);
            AutoSendToConnections("Test", NetworkingMessageType.Channels.DefaultReliableSequenced, "[Auto Rpc] hello world : ", cpt);
        }

        if (isServer)
            SendToConnections("Test", NetworkingMessageType.Channels.DefaultReliableSequenced, "[Rpc] hello world : ", cpt);

        cpt++;
    }

    [Networked]
    void Test(string message, ushort id)
    {
        //Debug.Log(message + id);
    }
}
