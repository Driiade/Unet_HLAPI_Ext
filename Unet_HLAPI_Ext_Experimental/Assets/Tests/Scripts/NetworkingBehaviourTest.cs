using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BC_Solution.UnetNetwork;

public class NetworkingBehaviourTest : NetworkingBehaviour {

	
	// Update is called once per frame
	void Update () {

        if (isClient)
            SendToServer("Test",NetworkingMessageType.Channels.DefaultReliable, "hello world : ", this.networkingIdentity.netId);

	}

    [Networked]
    void Test(string message, ushort id)
    {
        Debug.Log(message + id);
    }
}
