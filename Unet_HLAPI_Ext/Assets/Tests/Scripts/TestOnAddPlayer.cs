using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

namespace BC_Solution.UnetNetwork
{
    public class TestOnAddPlayer : MonoBehaviour
    {

        [SerializeField]
        int numberOfSurcharge = 10;

        void OnEnable()
        {
            for (int i = 0; i < numberOfSurcharge; i++)
               NetworkingSystem.OnServerAddPlayer += TestFunction;
        }

        void TestFunction(NetworkMessage netMsg)
        {
            // GameObject.FindObjectOfType<Player>().PlayerManager.SpawnIncarnationPreference(Player.INCARNATION_NAME.DOLL);
            throw new System.Exception("LOOK THE FUNCTION PLZ");
        }
    }
}
