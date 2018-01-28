using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class SpawnMessage : NetworkingMessage
    {
        public ushort m_gameObjectAssetId;
        public ushort m_gameObjectNetId;

        public SpawnMessage() { }

        public SpawnMessage(ushort assetId, ushort netId)
        {
            m_gameObjectAssetId = assetId;
            m_gameObjectNetId = netId;
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_gameObjectAssetId);
            writer.Write(m_gameObjectNetId);
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_gameObjectAssetId = reader.ReadUInt16();
            m_gameObjectNetId = reader.ReadUInt16();
        }
    }
}
