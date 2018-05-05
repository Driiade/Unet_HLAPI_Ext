using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BC_Solution.UnetNetwork;

public class NetworkingBehaviourTest : NetworkingBehaviour {

    public class TestSerializationClass : ISerializable
    {
        public string message = "blabla";

        public void OnSerialize(NetworkingWriter writer)
        {
            writer.Write(": Serialization is possible " + Random.Range(0,100));
        }

        public void OnDeserialize(NetworkingReader reader, NetworkingConnection clientConn, NetworkingConnection serverConn)
        {
            message = reader.ReadString();
        }
    }


    enum TESTENUM { TEST1, TEST2, TEST3}

    public float sendingRate = 0.5f;

    public static List<NetworkingBehaviourTest> networkingBehaviourTests = new List<NetworkingBehaviourTest>();

    [NetworkedVariable]
    TESTENUM randomNum = TESTENUM.TEST1;

    int cpt = 0;

    float timer;

    TestSerializationClass testClass = new TestSerializationClass();

    void Awake()
    {
        networkingBehaviourTests.Add(this);
    }

    private void Start()
    {
        if(this.networkingIdentity.type == NetworkingIdentity.TYPE.REPLICATED_SCENE_PREFAB)
            randomNum =(TESTENUM)System.Enum.GetValues(typeof(TESTENUM)).GetValue(Random.Range(0,3));
    }

    // Update is called once per frame
    void Update () {

        if (Time.time > timer)
        {
            timer = Time.time + sendingRate;

#if CLIENT
            if (isClient)
            {
                SendToServer("Test", NetworkingChannel.DefaultReliableSequenced, "[Command] hello world : ", cpt, testClass);
                AutoSendToConnections("Test", NetworkingChannel.DefaultReliableSequenced, "[Auto Rpc] hello world : ", cpt, testClass);
            }
#endif

#if SERVER
            if (isServer)
               SendToAllConnections("Test", NetworkingChannel.DefaultReliableSequenced, "[Rpc] hello world : ", cpt, testClass);
#endif

            cpt++;
        }
    }

    void OnDestroy()
    {
        networkingBehaviourTests.Remove(this);
    }

    [NetworkedFunction]
    void Test(string message, int cpt, TestSerializationClass testClass)
    {
        Debug.Log(message + cpt + testClass.message);
    }

    [ContextMenu("TestSendToOwner")]
    public void TestSendToOwner()
    {
         SendToOwner(this.networkingIdentity, "HelloOwner", NetworkingChannel.DefaultReliable);
    }

    [NetworkedFunction]
    void HelloOwner(NetworkingBehaviourTest s)
    {
        Debug.LogWarning("Hello =) : " + s);
        GameObject.CreatePrimitive(PrimitiveType.Sphere).AddComponent<Rigidbody>();
    }

    void OnGUI()
    {
        int ypos = Screen.height - 50;

        for (int i = 0; i < networkingBehaviourTests.Count; i++)
        {
            if (networkingBehaviourTests[i] == this)
            {
                ypos -= i*25;
                if (GUI.Button(new Rect(50, ypos, 300, 20), "TestOwner   : " + randomNum))
                {
                    SendToOwner(this.networkingIdentity, "HelloOwner", NetworkingChannel.DefaultReliable, this);
                }
                return;
            }

        }
    }
}
