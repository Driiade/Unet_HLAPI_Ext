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
using System.Text;
using System;
using System.IO;

namespace BC_Solution.UnetNetwork
{
    public class NetworkingReader
    {
        MemoryStream m_memoryStream;

        public long Position { get { return m_memoryStream.Position; } } // changed to long for best support
        public long Length { get { return m_memoryStream.Length; } } // changed to long for best support
        const int k_MaxStringLength = 1024 * 32;
        const int k_InitialStringBufferSize = 1024;
        static byte[] s_StringReaderBuffer = new byte[k_InitialStringBufferSize];
        static Encoding s_Encoding = new UTF8Encoding();


        public NetworkingReader(NetworkingWriter writer)
        {
            m_memoryStream = new MemoryStream(writer.ToArray());
        }

        public NetworkingReader(NetworkingReader reader)
        {
            m_memoryStream = new MemoryStream(reader.ToArray());
        }

        public NetworkingReader(byte[] buffer)
        {
            m_memoryStream = new MemoryStream(buffer);
        }

        //Return the entire array of the memoryStream
        public byte[] ToArray()
        {
            return m_memoryStream.ToArray();
        }

        /// <summary>
        /// Only return the rest of the unused memoryStream
        /// </summary>
        /// <returns></returns>
        public byte[] Flush()
        {
            return ReadBytes((int)(m_memoryStream.Length - m_memoryStream.Position));
        }

        public void SeekZero()
        {
            m_memoryStream.Seek(0, SeekOrigin.Begin);
        }

        internal void Replace(byte[] buffer)
        {
            m_memoryStream = new MemoryStream(buffer);
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        // NOTE: big endian.

        public UInt32 ReadPackedUInt32()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }
            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return (UInt32)(240 + 256 * (a0 - 241) + a1);
            }
            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return (UInt32)(2288 + 256 * a1 + a2);
            }
            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16);
            }
            byte a4 = ReadByte();
            if (a0 >= 251)
            {
                return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16) + (((UInt32)a4) << 24);
            }
            throw new IndexOutOfRangeException("ReadPackedUInt32() failure: " + a0);
        }

        public UInt64 ReadPackedUInt64()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }
            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + 256 * (a0 - ((UInt64)241)) + a1;
            }
            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return 2288 + (((UInt64)256) * a1) + a2;
            }
            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16);
            }
            byte a4 = ReadByte();
            if (a0 == 251)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24);
            }


            byte a5 = ReadByte();
            if (a0 == 252)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32);
            }


            byte a6 = ReadByte();
            if (a0 == 253)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40);
            }


            byte a7 = ReadByte();
            if (a0 == 254)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48);
            }


            byte a8 = ReadByte();
            if (a0 == 255)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48) + (((UInt64)a8) << 56);
            }
            throw new IndexOutOfRangeException("ReadPackedUInt64() failure: " + a0);
        }

        /*  public NetworkInstanceId ReadNetworkId() //USELESS AS FUCK
          {
              return new NetworkInstanceId(ReadPackedUInt32());
          }

          public NetworkSceneId ReadSceneId()
          {
              return new NetworkSceneId(ReadPackedUInt32());
          }*/

        public byte ReadByte()
        {
            return (byte)m_memoryStream.ReadByte();
        }

        public sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        public short ReadInt16()
        {
            ushort value = 0;
            value |= ReadByte();
            value |= (ushort)(ReadByte() << 8);
            return (short)value;
        }

        public ushort ReadUInt16()
        {
            ushort value = 0;
            value |= ReadByte();
            value |= (ushort)(ReadByte() << 8);
            return value;
        }

        public int ReadInt32()
        {
            uint value = 0;
            value |= ReadByte();
            value |= (uint)(ReadByte() << 8);
            value |= (uint)(ReadByte() << 16);
            value |= (uint)(ReadByte() << 24);
            return (int)value;
        }

        public uint ReadUInt32()
        {
            uint value = 0;
            value |= ReadByte();
            value |= (uint)(ReadByte() << 8);
            value |= (uint)(ReadByte() << 16);
            value |= (uint)(ReadByte() << 24);
            return value;
        }

        public long ReadInt64()
        {
            ulong value = 0;

            ulong other = ReadByte();
            value |= other;

            other = ((ulong)ReadByte()) << 8;
            value |= other;

            other = ((ulong)ReadByte()) << 16;
            value |= other;

            other = ((ulong)ReadByte()) << 24;
            value |= other;

            other = ((ulong)ReadByte()) << 32;
            value |= other;

            other = ((ulong)ReadByte()) << 40;
            value |= other;

            other = ((ulong)ReadByte()) << 48;
            value |= other;

            other = ((ulong)ReadByte()) << 56;
            value |= other;

            return (long)value;
        }

        public ulong ReadUInt64()
        {
            ulong value = 0;
            ulong other = ReadByte();
            value |= other;

            other = ((ulong)ReadByte()) << 8;
            value |= other;

            other = ((ulong)ReadByte()) << 16;
            value |= other;

            other = ((ulong)ReadByte()) << 24;
            value |= other;

            other = ((ulong)ReadByte()) << 32;
            value |= other;

            other = ((ulong)ReadByte()) << 40;
            value |= other;

            other = ((ulong)ReadByte()) << 48;
            value |= other;

            other = ((ulong)ReadByte()) << 56;
            value |= other;
            return value;
        }

        public decimal ReadDecimal()
        {
            Int32[] bits = new Int32[4];

            bits[0] = ReadInt32();
            bits[1] = ReadInt32();
            bits[2] = ReadInt32();
            bits[3] = ReadInt32();

            return new decimal(bits);
        }

        public float ReadSingle()
        {
            byte[] bytes = ReadBytes(sizeof(float));
            return BitConverter.ToSingle(bytes, 0);
        }

        public double ReadDouble()
        {
            byte[] bytes = ReadBytes(sizeof(double));
            return BitConverter.ToDouble(bytes, 0);
        }

        public string ReadString()
        {
            UInt16 numBytes = ReadUInt16();
            if (numBytes == 0)
                return "";

            if (numBytes >= k_MaxStringLength)
            {
                throw new IndexOutOfRangeException("ReadString() too long: " + numBytes);
            }

            while (numBytes > s_StringReaderBuffer.Length)
            {
                s_StringReaderBuffer = new byte[s_StringReaderBuffer.Length * 2];
            }

            int read = m_memoryStream.Read(s_StringReaderBuffer, 0, numBytes);
            if (read != numBytes)
            {
                throw new Exception("ReadString() read result mismatch: " + numBytes + " => " + read);
            }

            char[] chars = s_Encoding.GetChars(s_StringReaderBuffer, 0, numBytes);
            return new string(chars);
        }

        public char ReadChar()
        {
            return (char)ReadByte();
        }

        public bool ReadBoolean()
        {
            return ReadByte() == 1;
        }

        public byte[] ReadBytes(int count)
        {
            byte[] value = new byte[count];
            int read = m_memoryStream.Read(value, 0, count);
            if (read != count)
            {
                throw new Exception("ReadBytes() read result mismatch: " + count + " => " + read);
            }
            return value;
        }

        public byte[] ReadBytesAndSize()
        {
            ushort sz = ReadUInt16();
            if (sz == 0)
                return new byte[0];

            return ReadBytes(sz);
        }


        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        public Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Vector4 ReadVector4()
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Color ReadColor()
        {
            return new Color(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Color32 ReadColor32()
        {
            return new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Rect ReadRect()
        {
            return new Rect(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Plane ReadPlane()
        {
            return new Plane(ReadVector3(), ReadSingle());
        }

        public Ray ReadRay()
        {
            return new Ray(ReadVector3(), ReadVector3());
        }

        public Matrix4x4 ReadMatrix4x4()
        {
            Matrix4x4 m = new Matrix4x4();
            m.m00 = ReadSingle();
            m.m01 = ReadSingle();
            m.m02 = ReadSingle();
            m.m03 = ReadSingle();
            m.m10 = ReadSingle();
            m.m11 = ReadSingle();
            m.m12 = ReadSingle();
            m.m13 = ReadSingle();
            m.m20 = ReadSingle();
            m.m21 = ReadSingle();
            m.m22 = ReadSingle();
            m.m23 = ReadSingle();
            m.m30 = ReadSingle();
            m.m31 = ReadSingle();
            m.m32 = ReadSingle();
            m.m33 = ReadSingle();
            return m;
        }

        /* public NetworkHash128 ReadNetworkHash128() //Not used , sry
         {
             NetworkHash128 hash;
             hash.i0 = ReadByte();
             hash.i1 = ReadByte();
             hash.i2 = ReadByte();
             hash.i3 = ReadByte();
             hash.i4 = ReadByte();
             hash.i5 = ReadByte();
             hash.i6 = ReadByte();
             hash.i7 = ReadByte();
             hash.i8 = ReadByte();
             hash.i9 = ReadByte();
             hash.i10 = ReadByte();
             hash.i11 = ReadByte();
             hash.i12 = ReadByte();
             hash.i13 = ReadByte();
             hash.i14 = ReadByte();
             hash.i15 = ReadByte();
             return hash;
         } */

        /* public Transform ReadTransform()      USELESS JUST READ<T>
         {
             ushort netId = ReadNetworkId();
             if (netId.IsEmpty())
             {
                 return null;
             }
             GameObject go = ClientScene.FindLocalObject(netId);
             if (go == null)
             {
                 if (LogFilter.logDebug) { Debug.Log("ReadTransform netId:" + netId); }
                 return null;
             }

             return go.transform;
         }*/


        /// <summary>
        /// Retrieve the networkingIdentity base on netId
        /// </summary>
        /// <param name="clientConnection">the client connection (if any)</param>
        /// <param name="serverConnection">the server connection (if any)</param>
        /// <returns></returns>
        public GameObject ReadGameObject(NetworkingConnection clientConnection, NetworkingConnection serverConnection)
        {
            ushort netId = ReadUInt16();
            NetworkingIdentity netIdentity = null;

#if SERVER && CLIENT
            netIdentity = NetworkingIdentity.s_networkingIdentities.Find((x) =>
            {
                return (x.m_connection == clientConnection || x.m_serverConnection == serverConnection) && x.netId == netId;

            });
#endif
#if SERVER
            netIdentity = NetworkingIdentity.s_networkingIdentities.Find((x) =>
            {
                return (x.m_serverConnection == serverConnection) && x.netId == netId;

            });
#endif
#if CLIENT
            netIdentity = NetworkingIdentity.s_networkingIdentities.Find((x) =>
            {
                return (x.m_connection == clientConnection) && x.netId == netId;

            });
#endif

            if (netIdentity == null)
            {
                Debug.LogWarning("ReadGameObject netId:" + netId + " go: null");
                Debug.LogWarning("Make sure gameobject exist or on the same server than the caller");
                return null;
            }
            else
                return netIdentity.gameObject;
        }

        /// <summary>
        /// Retrieve the first occurence of a component, based on the networkingIdentity netId
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="clientConnection">the client connection (if any)</param>
        /// <param name="serverConnection">the server connection (if any)</param>
        /// <returns></returns>
        public T ReadComponent<T>(NetworkingConnection clientConnection, NetworkingConnection serverConnection) where T : Component
        {
            GameObject go = ReadGameObject(clientConnection, serverConnection);
            if (go != null)
                return go.GetComponent<T>();

            return null;
        }

        /// <summary>
        /// generic read (check type )
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Read<T>(NetworkingConnection clientConnection, NetworkingConnection serverConnection)
        {
            return (T)Read(typeof(T), clientConnection, serverConnection);
        }

        public int ReadMask(int maxDifferentValue)
        {
            int mask = 0;

            if(maxDifferentValue < 0)
            {
                return 0;
            }
            else if (maxDifferentValue == 1)
            {
                return 1;
            }
            else if (maxDifferentValue <= sizeof(byte) * 8)
            {
                mask = ReadByte();
            }
            else if (maxDifferentValue <= sizeof(short) * 8)
            {
                mask = ReadInt16();
            }
            else if (maxDifferentValue <= sizeof(int) * 8)
            {
                mask = ReadInt32();
            }

            return mask;
        }

        /// <summary>
        /// generic read (check type )
        /// </summary>
        /// <returns></returns>
        public object Read(Type type, NetworkingConnection clientConnection, NetworkingConnection serverConnection)
        {
            if (type == typeof(byte))
            {
                return ReadByte();
            }
            else if (type == typeof(int))
            {
                return ReadInt32();
            }
            else if (type == typeof(string))
            {
                return ReadString();
            }
            else if (type == typeof(ushort))
            {
                return ReadUInt16();
            }
            else if (type == typeof(byte[]))
            {
                return ReadBytesAndSize();
            }
            else if (type == typeof(bool))
            {
                return ReadBoolean();
            }
            else if (type == typeof(Vector3))
            {
                return ReadVector3();
            }
            else if (type == typeof(Vector2))
            {
                return ReadVector2();
            }
            else if (type == typeof(GameObject))
            {
#if SERVER && CLIENT
                return ReadGameObject(clientConnection, serverConnection);
#elif SERVER
                    return ReadGameObject(null, serverConnection);
#elif CLIENT
                    return ReadGameObject(clientConnection, null);
#endif
            }
            else if (type.IsSubclassOf(typeof(UnityEngine.Component)))
            {
#if SERVER && CLIENT

                return ReadComponent(clientConnection, serverConnection, type);
#elif SERVER
                    return ReadComponent(null, serverConnection, type);
#elif CLIENT
                    return ReadComponent(clientConnection, null, type);
#endif
            }
            else if (type.IsEnum)
            {
                return ReadInt32();
            }
            else if (type.GetInterface("ISerializable") != null)
            {
                object newObj = System.Activator.CreateInstance(type);

                if (newObj == null)
                    throw new System.Exception("Creation of instance impossible : " + type.GetType());

#if SERVER && CLIENT
                ((ISerializable)newObj).OnDeserialize(this, clientConnection, serverConnection);
#elif SERVER
                ((ISerializable)newObj).OnDeserialize(this, null, serverConnection);
#elif CLIENT
                ((ISerializable)newObj).OnDeserialize(this, clientConnection, null);
#endif

                return newObj;
            }
            else
                throw new System.Exception("UnSerialization is impossible : " + type.GetType());

        }

        /// <summary>
        /// Retrieve the first occurence of a component, based on the networkingIdentity netId
        /// </summary>
        /// <param name="clientConnection"></param>
        /// <param name="serverConnection"></param>
        /// <param name="componentType"></param>
        /// <returns></returns>
        public Component ReadComponent(NetworkingConnection clientConnection, NetworkingConnection serverConnection, Type componentType)
        {
            GameObject go = ReadGameObject(clientConnection, serverConnection);
            if (go != null)
                return go.GetComponent(componentType);

            return null;
        }

        public override string ToString()
        {
            return m_memoryStream.ToString();
        }

        public TMsg ReadMessage<TMsg>() where TMsg : NetworkingMessage, new()
        {
            var msg = new TMsg();
            msg.Deserialize(this);
            return msg;
        }
    }

}
