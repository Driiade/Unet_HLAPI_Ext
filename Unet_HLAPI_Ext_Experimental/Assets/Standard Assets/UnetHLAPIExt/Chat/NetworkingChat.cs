using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingChat : MonoBehaviour
    {
        [SerializeField]
        Text text;

        private void Awake()
        {
            NetworkingSystem.RegisterConnectionHandler(NetworkingMessageType.ChatMessage, ConnectionReceiveMessage);
        }


        private void OnDestroy()
        {
            NetworkingSystem.UnRegisterConnectionHandler(NetworkingMessageType.ChatMessage, ConnectionReceiveMessage);
        }


        public void Send(string message)
        {
            if (NetworkingSystem.Instance.ConnectionIsActive())
                NetworkingSystem.Instance.connections[0].Send(NetworkingMessageType.ChatMessage, new StringMessage(message));
        }

        public void ConnectionReceiveMessage(NetworkingMessage netMsg)
        {
            text.text = netMsg.As<StringMessage>().m_value;
        }
    }
}
