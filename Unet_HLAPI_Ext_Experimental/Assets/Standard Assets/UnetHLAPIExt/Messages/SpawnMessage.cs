/*Copyright(c) <2017> <Benoit Constantin ( France )>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. 
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class SpawnMessage : NetworkingMessage
    {
        public ushort m_gameObjectAssetId;
        public ushort m_netId;
        public bool m_hasAuthority;
        public bool m_isLocalConnection;
        public string m_sceneName;
        public byte[] m_networkingBehaviourSyncVars;

        public SpawnMessage() { }

        public SpawnMessage(ushort assetId, ushort netId, bool hasAuthority,bool isLocalConnection, string sceneName, byte[] networkingBehaviourSyncVars)
        {
            m_gameObjectAssetId = assetId;
            m_netId = netId;
            m_hasAuthority = hasAuthority;
            m_isLocalConnection = isLocalConnection;
            m_sceneName = sceneName;
            m_networkingBehaviourSyncVars = networkingBehaviourSyncVars;
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_gameObjectAssetId);
            writer.Write(m_netId);
            writer.Write(m_hasAuthority);
            writer.Write(m_isLocalConnection);
            writer.Write(m_sceneName);
            writer.WriteBytesFull(m_networkingBehaviourSyncVars);
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_gameObjectAssetId = reader.ReadUInt16();
            m_netId = reader.ReadUInt16();
            m_hasAuthority = reader.ReadBoolean();
            m_isLocalConnection = reader.ReadBoolean();
            m_sceneName = reader.ReadString();
            m_networkingBehaviourSyncVars = reader.ReadBytesAndSize();
        }
    }
}
