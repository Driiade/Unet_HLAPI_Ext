using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class ReplicatedPrefabSceneMessage : NetworkingMessage
    {
        public ushort m_sceneId;
        public ushort m_assetId;
        public string m_sceneName;

        public ReplicatedPrefabSceneMessage()
        {
            m_sceneId = 0;
            m_assetId = 0;
            m_sceneName = "";
        }

        public ReplicatedPrefabSceneMessage(ushort assetId, ushort sceneId, string sceneName)
        {
            m_assetId = assetId;
            m_sceneId = sceneId;
            m_sceneName = sceneName;
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_assetId = reader.ReadUInt16();
            m_sceneId = reader.ReadUInt16();
            m_sceneName = reader.ReadString();
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_assetId);
            writer.Write(m_sceneId);
            writer.Write(m_sceneName);
        }
    }
}
