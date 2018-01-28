using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class NetIdMessage : NetworkingMessage
    {
        public ushort m_netId;

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_netId = reader.ReadUInt16();
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_netId);
        }
    }
}
