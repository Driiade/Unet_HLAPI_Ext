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


using UnityEngine;
using UnityEngine.Networking;

namespace BC_Solution.UnetNetwork
{
    public class Rigidbody2DRotationSynchronizer : MovementSynchronizer
    {

        class RigidbodyState : State
        {
            public float m_rotation;
            public float m_angularVelocity;

            public RigidbodyState(Rigidbody2D r, float relativeTime)
            {
                this.m_relativeTime = relativeTime;
                m_rotation = r.rotation;
                m_angularVelocity = r.angularVelocity;
            }
        }

        [SerializeField]
        Rigidbody2D m_rigidbody2D;

        [Space(10)]
        [SerializeField]
        public bool synchronizeRotation = true;

        [SerializeField]
        public bool synchronizeAngularVelocity = false;

        [SerializeField]
        public float rotationThreshold = 0.1f;

        [Header("Compression")]
        public COMPRESS_MODE compressionRotationMode = COMPRESS_MODE.NONE;
        public float minRotationValue;
        public float maxRotationValue;

#if UNITY_EDITOR
        [Space(10)]
        [SerializeField]
        float precisionAfterCompression;
        [SerializeField]
        float returnedValueOnCurrentRotation;
# endif

        [Space(5)]
        public COMPRESS_MODE compressionAngularVelocityMode = COMPRESS_MODE.NONE;
        public float minAngularVelocityValue;
        public float maxAngularVelocityValue;


        private float lastRotation = 0;
        private float rotationError = 0;


        public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
        {
            this.m_rigidbody2D.angularVelocity = ((RigidbodyState)extrapolatingState).m_angularVelocity;

            this.m_rigidbody2D.constraints = RigidbodyConstraints2D.None;
        }

        public override void OnEndExtrapolation(State rhs)
        {
            Quaternion rhsQ = Quaternion.Euler(0, 0, ((RigidbodyState)rhs).m_rotation);
            Quaternion rigidBodyQ = Quaternion.Euler(0, 0, this.m_rigidbody2D.rotation);
            this.m_rigidbody2D.rotation = Quaternion.Slerp(rigidBodyQ, rhsQ, Time.deltaTime / interpolationErrorTime).eulerAngles.z;

            this.m_rigidbody2D.angularVelocity = 0;
            this.m_rigidbody2D.constraints = RigidbodyConstraints2D.FreezePosition;
        }

        public override void OnErrorCorrection()
        {
            this.m_rigidbody2D.rotation = this.m_rigidbody2D.rotation + rotationError;
            rotationError = Mathf.Lerp(rotationError, 0, Time.deltaTime / interpolationErrorTime);
            this.m_rigidbody2D.rotation = this.m_rigidbody2D.rotation - rotationError;
        }

        public override void OnExtrapolation()
        {
            //Nothing to do, just run Physics
        }

        public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
        {
            this.m_rigidbody2D.constraints = RigidbodyConstraints2D.FreezePosition;

            //ROTATION 
            float rotation = 0;

            Quaternion rhsQ = Quaternion.Euler(0, 0, ((RigidbodyState)rhs).m_rotation);
            Quaternion lhsQ = Quaternion.Euler(0, 0, ((RigidbodyState)lhs).m_rotation);

            rotation = Quaternion.Slerp(lhsQ, rhsQ, t).eulerAngles.z;

            if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
            {
                if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                    rotationError = this.m_rigidbody2D.rotation - rotation;
            }
            else
                rotationError = 0;

            this.m_rigidbody2D.rotation = rotation + rotationError;

            //ANGULAR VELOCITY
            this.m_rigidbody2D.angularVelocity = Mathf.Lerp(((RigidbodyState)lhs).m_angularVelocity, ((RigidbodyState)rhs).m_angularVelocity, t);
        }

        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();
            lastRotation = this.m_rigidbody2D.rotation;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) || this.m_rigidbody2D.isKinematic || Mathf.Abs(lastRotation - this.m_rigidbody2D.rotation) < rotationThreshold * rotationThreshold)
                return false;

            return true;
        }

        public override void GetCurrentState(NetworkWriter networkWriter)
        {
            lastRotation = this.m_rigidbody2D.rotation;

            float precision;

            if (synchronizeRotation)
            {
                switch (compressionRotationMode)
                {
                    case COMPRESS_MODE.NONE:
                        networkWriter.Write(this.m_rigidbody2D.rotation);
                        break;
                    case COMPRESS_MODE.USHORT:
                        networkWriter.Write(Math.CompressToShort(this.m_rigidbody2D.rotation, minRotationValue, maxRotationValue, out precision));
                        break;
                }
            }

            if (synchronizeAngularVelocity)
            {
                switch (compressionAngularVelocityMode)
                {
                    case COMPRESS_MODE.NONE:
                        networkWriter.Write(this.m_rigidbody2D.angularVelocity);
                        break;
                    case COMPRESS_MODE.USHORT:
                        networkWriter.Write(Math.CompressToShort(this.m_rigidbody2D.angularVelocity, minAngularVelocityValue, maxAngularVelocityValue, out precision));
                        break;
                }
            }
            else
            {
                byte direction = 0;
                if (this.m_rigidbody2D.angularVelocity > 0)
                    direction = (byte)(direction | (1 << 1));

                networkWriter.Write(direction);
            }
        }

        public override void ReceiveCurrentState(float relativeTime, NetworkReader networkReader)
        {
            RigidbodyState newState = new RigidbodyState(this.m_rigidbody2D, relativeTime);

            if (synchronizeRotation)
            {
                switch (compressionRotationMode)
                {
                    case COMPRESS_MODE.NONE:
                        newState.m_rotation = networkReader.ReadSingle();
                        break;
                    case COMPRESS_MODE.USHORT:
                        newState.m_rotation = Math.Decompress(networkReader.ReadUInt16(), minRotationValue, maxRotationValue);
                        break;
                }
            }

            if (synchronizeAngularVelocity)
            {
                switch (compressionAngularVelocityMode)
                {
                    case COMPRESS_MODE.NONE:
                        newState.m_angularVelocity = networkReader.ReadSingle();
                        break;
                    case COMPRESS_MODE.USHORT:
                        newState.m_angularVelocity = Math.Decompress(networkReader.ReadUInt16(), minAngularVelocityValue, maxAngularVelocityValue);
                        break;
                }
            }


            int place = AddState(newState);

            //If calcul are needed for velocity
            if (!synchronizeAngularVelocity)
            {
                byte direction = networkReader.ReadByte();
                if (place != -1 && place < currentStatesIndex - 1)
                {
                    if (((RigidbodyState)statesBuffer[place]).m_rotation > ((RigidbodyState)statesBuffer[place + 1]).m_rotation && ((direction & 1 << 1) != 0)
                        || ((RigidbodyState)statesBuffer[place]).m_rotation < ((RigidbodyState)statesBuffer[place + 1]).m_rotation && ((direction & 1 << 1) == 0))
                    {
                        newState.m_angularVelocity = (((RigidbodyState)statesBuffer[place]).m_rotation - ((RigidbodyState)statesBuffer[place + 1]).m_rotation) / (statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime);
                    }
                    else if (((RigidbodyState)statesBuffer[place]).m_rotation > ((RigidbodyState)statesBuffer[place + 1]).m_rotation)
                        newState.m_angularVelocity = (((RigidbodyState)statesBuffer[place]).m_rotation - (((RigidbodyState)statesBuffer[place + 1]).m_rotation + 360)) / (statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime);
                    else
                        newState.m_angularVelocity = (((RigidbodyState)statesBuffer[place]).m_rotation - (((RigidbodyState)statesBuffer[place + 1]).m_rotation - 360)) / (statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime);
                }
            }
        }

        public override void ReceiveSync(NetworkReader networkReader)
        {
            if (synchronizeRotation)
            {
                switch (compressionRotationMode)
                {
                    case COMPRESS_MODE.NONE:
                        this.m_rigidbody2D.rotation = networkReader.ReadSingle();
                        break;
                    case COMPRESS_MODE.USHORT:
                        this.m_rigidbody2D.rotation = Math.Decompress(networkReader.ReadUInt16(), minRotationValue, maxRotationValue);
                        break;
                }
            }

            if (synchronizeAngularVelocity)
            {
                switch (compressionAngularVelocityMode)
                {
                    case COMPRESS_MODE.NONE:
                        this.m_rigidbody2D.angularVelocity = networkReader.ReadSingle();
                        break;
                    case COMPRESS_MODE.USHORT:
                        this.m_rigidbody2D.angularVelocity = Math.Decompress(networkReader.ReadUInt16(), minAngularVelocityValue, maxAngularVelocityValue);
                        break;
                }
            }
            else
                networkReader.ReadByte();

            ResetStatesBuffer();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (compressionRotationMode)
            {
                case COMPRESS_MODE.USHORT:
                    returnedValueOnCurrentRotation = Math.Decompress(Math.CompressToShort(this.m_rigidbody2D.rotation, minRotationValue, maxRotationValue, out precisionAfterCompression), minRotationValue, maxRotationValue);
                    break;

                case COMPRESS_MODE.NONE:
                    returnedValueOnCurrentRotation = this.m_rigidbody2D.rotation;
                    break;
            }
        }
#endif
    }
}

