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
        public NetworkingConnection m_connection;
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
