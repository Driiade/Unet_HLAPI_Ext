using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class ErrorMessage : NetworkingMessage
    {

        public int errorCode;

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            errorCode = reader.ReadUInt16();
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write((ushort)errorCode);
        }

    }
}
