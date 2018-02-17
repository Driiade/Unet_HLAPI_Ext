using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingChannel 
    {
        public const int DefaultReliableSequenced = 0;
        public const int DefaultReliable = 1;
        public const int DefaultUnreliable = 2;

        public enum ChannelOption
        {
            MaxPendingBuffers = 1,
            AllowFragmentation = 2,
            MaxPacketSize = 3
            // maybe add an InitialCapacity for Pending Buffers list if needed in the future
        }
    }
}