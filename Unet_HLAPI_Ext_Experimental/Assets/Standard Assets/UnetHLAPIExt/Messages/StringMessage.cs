using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    public class StringMessage : NetworkingMessage
    {

        public string m_value;

        public StringMessage()
        {

        }

        public StringMessage(string value)
        {
            this.m_value = value;
        }

        public override void Deserialize(NetworkingReader reader)
        {
            base.Deserialize(reader);
            m_value = reader.ReadString();
        }

        public override void Serialize(NetworkingWriter writer)
        {
            base.Serialize(writer);
            writer.Write(m_value);
        }

    }

}
