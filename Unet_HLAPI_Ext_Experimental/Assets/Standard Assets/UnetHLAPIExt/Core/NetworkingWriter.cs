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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using BC_Solution;
using System.IO;

namespace BC_Solution.UnetNetwork
{
    /*
  // Binary stream Writer. Supports simple types, buffers, arrays, structs, and nested types
      */
    public class NetworkingWriter
    {
        public ushort Position { get { return (ushort)m_memoryStream.Position; } } // changed to ushort so at least we can support 64k and not 32k bytes
        const int k_MaxStringLength = 1024 * 32;
        MemoryStream m_memoryStream = new MemoryStream();
        static Encoding s_Encoding = new UTF8Encoding();
        static byte[] s_StringWriteBuffer = new byte[k_MaxStringLength];

        public NetworkingWriter()
        {

        }

        public NetworkingWriter(byte[] buffer)
        {
            m_memoryStream.Write(buffer, 0, buffer.Length);
        }

        public byte[] ToArray()
        {
            return m_memoryStream.ToArray(); // documentation: "omits unused bytes"
        }


        public void Write(byte value)
        {
            m_memoryStream.WriteByte(value);
        }

        /// <summary>
        /// Only write the bytes, not the length !
        /// </summary>
        /// <param name="bytes"></param>
        public void Write(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                Write(bytes[i]);
            }
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki

        public void WritePackedUInt32(UInt32 value)
        {
            if (value <= 240)
            {
                Write((byte)value);
                return;
            }
            if (value <= 2287)
            {
                Write((byte)((value - 240) / 256 + 241));
                Write((byte)((value - 240) % 256));
                return;
            }
            if (value <= 67823)
            {
                Write((byte)249);
                Write((byte)((value - 2288) / 256));
                Write((byte)((value - 2288) % 256));
                return;
            }
            if (value <= 16777215)
            {
                Write((byte)250);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                return;
            }

            // all other values of uint
            Write((byte)251);
            Write((byte)(value & 0xFF));
            Write((byte)((value >> 8) & 0xFF));
            Write((byte)((value >> 16) & 0xFF));
            Write((byte)((value >> 24) & 0xFF));
        }

        public void WritePackedUInt64(UInt64 value)
        {
            if (value <= 240)
            {
                Write((byte)value);
                return;
            }
            if (value <= 2287)
            {
                Write((byte)((value - 240) / 256 + 241));
                Write((byte)((value - 240) % 256));
                return;
            }
            if (value <= 67823)
            {
                Write((byte)249);
                Write((byte)((value - 2288) / 256));
                Write((byte)((value - 2288) % 256));
                return;
            }
            if (value <= 16777215)
            {
                Write((byte)250);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                Write((byte)251);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                Write((byte)252);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                Write((byte)253);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                Write((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                Write((byte)254);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                Write((byte)((value >> 40) & 0xFF));
                Write((byte)((value >> 48) & 0xFF));
                return;
            }

            // all others
            {
                Write((byte)255);
                Write((byte)(value & 0xFF));
                Write((byte)((value >> 8) & 0xFF));
                Write((byte)((value >> 16) & 0xFF));
                Write((byte)((value >> 24) & 0xFF));
                Write((byte)((value >> 32) & 0xFF));
                Write((byte)((value >> 40) & 0xFF));
                Write((byte)((value >> 48) & 0xFF));
                Write((byte)((value >> 56) & 0xFF));
            }
        }

        /*   public void Write(NetworkInstanceId value)    //Just for writing an uint..sure ...
           {
               WritePackedUInt32(value.Value);
           }

           public void Write(NetworkSceneId value)
           {
               WritePackedUInt32(value.Value);
           } */

        public void Write(sbyte value)
        {
            m_memoryStream.WriteByte((byte)value);
        }

        public void Write(char value)
        {
            Write((byte)value);
        }

        public void Write(short value)
        {
            Write((byte)(value & 0xff));
            Write((byte)((value >> 8) & 0xff));
        }

        public void Write(ushort value)
        {
            Write((byte)(value & 0xff));
            Write((byte)((value >> 8) & 0xff));
        }

        public void Write(int value)
        {
            // little endian...
            Write((byte)(value & 0xff));
            Write((byte)((value >> 8) & 0xff));
            Write((byte)((value >> 16) & 0xff));
            Write((byte)((value >> 24) & 0xff));
        }

        public void Write(uint value)
        {
            Write((byte)(value & 0xff));
            Write((byte)((value >> 8) & 0xff));
            Write((byte)((value >> 16) & 0xff));
            Write((byte)((value >> 24) & 0xff));
        }

        public void Write(long value)
        {
            Write((byte)(value & 0xff));
            Write((byte)((value >> 8) & 0xff));
            Write((byte)((value >> 16) & 0xff));
            Write((byte)((value >> 24) & 0xff));
            Write((byte)((value >> 32) & 0xff));
            Write((byte)((value >> 40) & 0xff));
            Write((byte)((value >> 48) & 0xff));
            Write((byte)((value >> 56) & 0xff));
        }

        public void Write(ulong value)
        {
            Write((byte)(value & 0xff));
            Write((byte)((value >> 8) & 0xff));
            Write((byte)((value >> 16) & 0xff));
            Write((byte)((value >> 24) & 0xff));
            Write((byte)((value >> 32) & 0xff));
            Write((byte)((value >> 40) & 0xff));
            Write((byte)((value >> 48) & 0xff));
            Write((byte)((value >> 56) & 0xff));
        }

        public void Write(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Write(bytes, bytes.Length);
        }

        public void Write(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Write(bytes, bytes.Length);
        }

        public void Write(decimal value)
        {
            Int32[] bits = decimal.GetBits(value);
            Write(bits[0]);
            Write(bits[1]);
            Write(bits[2]);
            Write(bits[3]);
        }

        public void Write(string value)
        {
            if (value == null)
            {
                Write((ushort)0);
                return;
            }

            int length = s_Encoding.GetByteCount(value);

            if (length >= k_MaxStringLength)
            {
                throw new IndexOutOfRangeException("Serialize(string) too long: " + value.Length);
            }

            Write((ushort)length);
            int numBytes = s_Encoding.GetBytes(value, 0, value.Length, s_StringWriteBuffer, 0);
            m_memoryStream.Write(s_StringWriteBuffer, 0, numBytes);
        }

        public void Write(bool value)
        {
            m_memoryStream.WriteByte((byte)(value ? 1 : 0));
        }

        public void Write(byte[] buffer, int count)
        {
            if (count > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter Write: buffer is too large (" + count + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }
            m_memoryStream.Write(buffer, 0, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (count > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter Write: buffer is too large (" + count + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }
            m_memoryStream.Write(buffer, offset, count);
        }

        public void WriteBytesAndSize(byte[] buffer, int count)
        {
            if (buffer == null || count == 0)
            {
                Write((UInt16)0);
                return;
            }

            if (count > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter WriteBytesAndSize: buffer is too large (" + count + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }

            Write((UInt16)count);
            m_memoryStream.Write(buffer, 0, count);
        }

        //NOTE: this will write the entire buffer.. including trailing empty space!
        public void WriteBytesFull(byte[] buffer)
        {
            if (buffer == null)
            {
                Write((UInt16)0);
                return;
            }
            if (buffer.Length > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("NetworkWriter WriteBytes: buffer is too large (" + buffer.Length + ") bytes. The maximum buffer size is 64K bytes."); }
                return;
            }
            Write((UInt16)buffer.Length);
            m_memoryStream.Write(buffer, 0, buffer.Length);
        }

        public void Write(Vector2 value)
        {
            Write(value.x);
            Write(value.y);
        }

        public void Write(Vector3 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
        }

        public void Write(Vector4 value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        public void Write(Color value)
        {
            Write(value.r);
            Write(value.g);
            Write(value.b);
            Write(value.a);
        }

        public void Write(Color32 value)
        {
            Write(value.r);
            Write(value.g);
            Write(value.b);
            Write(value.a);
        }

        public void Write(Quaternion value)
        {
            Write(value.x);
            Write(value.y);
            Write(value.z);
            Write(value.w);
        }

        public void Write(Rect value)
        {
            Write(value.xMin);
            Write(value.yMin);
            Write(value.width);
            Write(value.height);
        }

        public void Write(Plane value)
        {
            Write(value.normal);
            Write(value.distance);
        }

        public void Write(Ray value)
        {
            Write(value.direction);
            Write(value.origin);
        }

        public void Write(Matrix4x4 value)
        {
            Write(value.m00);
            Write(value.m01);
            Write(value.m02);
            Write(value.m03);
            Write(value.m10);
            Write(value.m11);
            Write(value.m12);
            Write(value.m13);
            Write(value.m20);
            Write(value.m21);
            Write(value.m22);
            Write(value.m23);
            Write(value.m30);
            Write(value.m31);
            Write(value.m32);
            Write(value.m33);
        }

      /*  public void Write(NetworkHash128 value)
        {
            Write(value.i0);
            Write(value.i1);
            Write(value.i2);
            Write(value.i3);
            Write(value.i4);
            Write(value.i5);
            Write(value.i6);
            Write(value.i7);
            Write(value.i8);
            Write(value.i9);
            Write(value.i10);
            Write(value.i11);
            Write(value.i12);
            Write(value.i13);
            Write(value.i14);
            Write(value.i15);
        }*/

        public void Write(NetworkingIdentity value)
        {
            if (value == null)
            {
                WritePackedUInt32(0);
                return;
            }
            Write(value.netId);
        }

        public void Write<T>(T value) where T : Component
        {
            Write(value.gameObject);
        }

        public void Write(GameObject value)
        {
            NetworkingIdentity uv = value.GetComponentInParent<NetworkingIdentity>();
            if (uv != null)
            {
                Write(uv.netId);            
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("NetworkWriter " + value + " has no NetworkIdentity"); }
            }
        }

        public void Write(NetworkingMessage msg)
        {
            msg.Serialize(this);
        }

        public void Seek(int position)
        {
            m_memoryStream.Seek(position, SeekOrigin.Begin);
        }

        public void SeekZero(bool reset)
        {
            if (reset)
                m_memoryStream.SetLength(0);

            m_memoryStream.Seek(0, SeekOrigin.Begin);
        }

        public void StartMessage() //Not message type in it :)
        {
            SeekZero(true);

            // two bytes for size, will be filled out in FinishMessage
             Write((ushort)0);

            // two bytes for message type
            // Write(msgType);
        }


        public void FinishMessage()
        {
            // jump to zero, replace size (short) in header, jump back
            long oldPosition = m_memoryStream.Position;
            //Debug.Log("OldPosition : " + oldPosition);
            ushort sz = (ushort)(m_memoryStream.Position - sizeof(ushort)); // length - header(short,short)
            //Debug.Log("Write size : " + sz);
            SeekZero(false);
            Write(sz);
            //Debug.Log(m_memoryStream.Length);
            m_memoryStream.Position = oldPosition;
        }
    }
}
