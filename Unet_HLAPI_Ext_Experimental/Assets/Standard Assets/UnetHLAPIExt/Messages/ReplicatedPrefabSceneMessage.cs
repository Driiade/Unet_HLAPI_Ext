using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class ReplicatedPrefabSceneMessage : NetworkingMessage
    {
        public ushort m_sceneId;
        public ushort m_serverAssetId;
        public string m_sceneName;
        public string m_serverId;
        public byte[] m_networkingBehaviourSyncVars;

        public ReplicatedPrefabSceneMessage()
        {
            m_sceneId = 0;
            m_serverAssetId = 0;
            m_sceneName = "";
        }

        public ReplicatedPrefabSceneMessage(ushort serverAssetId, ushort sceneId, string sceneName, string serverId, byte[] networkingBehaviourSyncVars)
        {
            m_serverAssetId = serverAssetId;
            m_sceneId = sceneId;
            m_sceneName = sceneName;
            m_serverId = serverId;
            m_networkingBehaviourSyncVars = networkingBehaviourSyncVars;
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_serverAssetId = reader.ReadUInt16();
            m_sceneId = reader.ReadUInt16();
            m_sceneName = reader.ReadString();
            m_serverId = reader.ReadString();
            m_networkingBehaviourSyncVars = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_serverAssetId);
            writer.Write(m_sceneId);
            writer.Write(m_sceneName);
            writer.Write(m_serverId);
            writer.WriteBytesFull(m_networkingBehaviourSyncVars);
        }
    }
}
