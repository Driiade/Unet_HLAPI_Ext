using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class ObjectDestroyMessage : NetworkingMessage
    {
        public ushort m_gameObjectNetId;

        public ObjectDestroyMessage() { }

        public ObjectDestroyMessage(ushort netId)
        {
            m_gameObjectNetId = netId;
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_gameObjectNetId);
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_gameObjectNetId = reader.ReadUInt16();
        }
    }
}
