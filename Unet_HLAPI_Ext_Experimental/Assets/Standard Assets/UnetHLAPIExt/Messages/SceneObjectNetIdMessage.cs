using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class SceneObjectNetIdMessage : NetworkingMessage
    {
        public ushort m_sceneId;
        public ushort m_netId;
        public byte[] m_networkingBehaviourSyncVars;

        public SceneObjectNetIdMessage()
        {
            m_sceneId = 0;
            m_netId = 0;
        }

        public SceneObjectNetIdMessage(ushort netId, ushort sceneId, byte[] networkingBehaviourSyncVars)
        {
            m_netId = netId;
            m_sceneId = sceneId;
            m_networkingBehaviourSyncVars = networkingBehaviourSyncVars;
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_netId = reader.ReadUInt16();
            m_sceneId = reader.ReadUInt16();
            m_networkingBehaviourSyncVars = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_netId);
            writer.Write(m_sceneId);
            writer.WriteBytesFull(m_networkingBehaviourSyncVars);
        }
    }
}
