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


namespace BC_Solution.UnetNetwork
{
    /// <summary>
    /// use this class to sync a variable with Unet Network
    /// </summary>
    public class SyncVar<T> : IDirtable, ISerializable
    {
        protected T value;
        internal bool isDirty;

        public SyncVar(T val)
        {
            value = val;
        }

        public bool IsDirty()
        {
            return isDirty;
        }

        public virtual T Value
        {
            get
            {
                return value;
            }
            set
            {
                if (!value.Equals(this.value))
                {
                    isDirty = true;
                    this.value = value;
                }
            }
        }

        public virtual void OnSerialize(NetworkingWriter writer)
        {
            //UnityEngine.Debug.Log(value);
            writer.Write(value);
            isDirty = false;
        }

        public virtual void OnDeserialize(NetworkingReader reader, NetworkingConnection connection, NetworkingConnection serverConnection)
        {
            Value = reader.Read<T>(connection, serverConnection);
            //UnityEngine.Debug.Log(value);
        }
    }
}
