using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class SceneObjectNetIdMessage : NetworkingMessage
    {
        public ushort m_sceneId;
        public ushort m_netId;

        public SceneObjectNetIdMessage()
        {
            m_sceneId = 0;
            m_netId = 0;
        }

        public SceneObjectNetIdMessage(ushort netId, ushort sceneId)
        {
            m_netId = netId;
            m_sceneId = sceneId;
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_netId = reader.ReadUInt16();
            m_sceneId = reader.ReadUInt16();
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_netId);
            writer.Write(m_sceneId);
        }
    }
}
