using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace BC_Solution.UnetNetwork
{
    // This can't be an interface because users don't need to implement the
    // serialization functions, we'll code generate it for them when they omit it.
    public class NetworkingMessage
    {
        public const int MaxMessageSize = (64 * 1024) - 1;

        [NonSerialized]
        public ushort m_type;

        [NonSerialized]
        public NetworkingConnection conn;
        [NonSerialized]
        public NetworkingReader reader;
        [NonSerialized]
        public int channelId;

        public NetworkingMessage()
        {

        }


        /// <summary>
        /// De-serialize the contents of the reader into this message
        /// Don't forget to call base.Deserialize()
        /// </summary>
        /// <param name="reader"></param>
        public virtual void Deserialize(NetworkingReader reader) { }


        /// <summary>
        /// Serialize the contents of this message into the writer
        /// Don't forget to call base.Serialize()
        /// </summary>
        /// <param name="writer"></param>
        public virtual void Serialize(NetworkingWriter writer) { }


        public string Dump(byte[] payload, int sz)
        {
            string outStr = "[";
            for (int i = 0; i < sz; i++)
            {
                outStr += (payload[i] + " ");
            }
            outStr += "]";
            return outStr;
        }

        public T As<T>() where T : NetworkingMessage, new()
        {
            T msg = new T();
            NetworkingReader tempReader = new NetworkingReader(this.reader);    //So multiple call can be done
            msg.Deserialize(tempReader);
            return msg;
        }
}



}
