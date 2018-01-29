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
    public class RigidbodyRotationSynchronizer : MovementSynchronizer
    {

        class RigidbodyState : State
        {
            public Vector3 m_rotation;
            public Vector3 m_angularVelocity;

            public RigidbodyState(Rigidbody r, float relativeTime)
            {
                this.m_relativeTime = relativeTime;
                m_rotation = r.rotation.eulerAngles;
                m_angularVelocity = r.angularVelocity;
            }
        }

        [SerializeField]
        Rigidbody m_rigidbody;

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE rotationSynchronizationMode = SYNCHRONISATION_MODE.XYZ;

        [SerializeField]
        SYNCHRONISATION_MODE angularVelocitySynchronizationMode = SYNCHRONISATION_MODE.CALCUL;

        [SerializeField]
        public float rotationThreshold = 0.1f;

        [Header("Compression")]
        public COMPRESS_MODE compressionRotationMode = COMPRESS_MODE.NONE;
        public Vector3 minRotationValue;
        public Vector3 maxRotationValue;

#if UNITY_EDITOR
        [Space(10)]
        [SerializeField]
        Vector3 precisionAfterCompression;
        [SerializeField]
        Vector3 returnedValueOnCurrentRotation;
# endif

        [Space(5)]
        public COMPRESS_MODE compressionAngularVelocityMode = COMPRESS_MODE.NONE;
        public Vector3 minAngularVelocityValue;
        public Vector3 maxAngularVelocityValue;


        private Quaternion lastRotation = Quaternion.identity;
        private Quaternion rotationError = Quaternion.identity;


        public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
        {
            this.m_rigidbody.angularVelocity = ((RigidbodyState)extrapolatingState).m_angularVelocity;
        }

        public override void OnEndExtrapolation(State rhs)
        {
            this.m_rigidbody.MoveRotation(Quaternion.Slerp(this.m_rigidbody.rotation, Quaternion.Euler(((RigidbodyState)rhs).m_rotation), Time.deltaTime / interpolationErrorTime));
            this.m_rigidbody.angularVelocity = Vector3.Lerp(this.m_rigidbody.angularVelocity, ((RigidbodyState)rhs).m_angularVelocity, Time.deltaTime / interpolationErrorTime);
        }

        public override void OnErrorCorrection()
        {
            this.m_rigidbody.MoveRotation(Quaternion.Inverse(rotationError) * this.m_rigidbody.rotation);
            rotationError = Quaternion.Slerp(rotationError, Quaternion.identity, Time.deltaTime / interpolationErrorTime);
            this.m_rigidbody.MoveRotation(rotationError * this.m_rigidbody.rotation);
        }

        public override void OnExtrapolation()
        {
            //Nothing to do, just run Physics
        }

        public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
        {
            //ROTATION
            Vector3 val = this.transform.rotation.eulerAngles;
            GetVector3(rotationSynchronizationMode, ref val, Quaternion.Slerp(Quaternion.Euler(((RigidbodyState)lhs).m_rotation), Quaternion.Euler(((RigidbodyState)rhs).m_rotation), t).eulerAngles);


            if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
            {
                if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                    rotationError = this.m_rigidbody.rotation * Quaternion.Inverse(Quaternion.Euler(val));
            }
            else
                rotationError = Quaternion.identity;

            this.m_rigidbody.MoveRotation(rotationError * Quaternion.Euler(val));

            //ANGULAR VELOCITY
            this.m_rigidbody.angularVelocity = Vector3.Lerp(((RigidbodyState)lhs).m_angularVelocity, ((RigidbodyState)rhs).m_angularVelocity, t);
        }

        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();
            lastRotation = this.m_rigidbody.rotation;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) || Vector3.SqrMagnitude(lastRotation.eulerAngles - this.m_rigidbody.rotation.eulerAngles) < rotationThreshold * rotationThreshold)
                return false;

            return true;
        }

        public override void GetCurrentState(NetworkingWriter networkWriter)
        {
            SerializeVector3(rotationSynchronizationMode, this.m_rigidbody.rotation.eulerAngles, networkWriter, compressionRotationMode, minRotationValue, maxRotationValue);
            SerializeVector3(angularVelocitySynchronizationMode, this.m_rigidbody.angularVelocity, networkWriter, compressionAngularVelocityMode, minAngularVelocityValue, maxAngularVelocityValue);

            if (angularVelocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
            {
                byte direction = 0;
                direction = (byte)(direction & (this.m_rigidbody.angularVelocity.x > 0 ? ((byte)1 << 0) : (byte)0));  //optimization to know in wich direction the rigidobody is spinning
                direction = (byte)(direction & (this.m_rigidbody.angularVelocity.y > 0 ? ((byte)1 << 1) : (byte)0));
                direction = (byte)(direction & (this.m_rigidbody.angularVelocity.z > 0 ? ((byte)1 << 1) : (byte)0));

                networkWriter.Write(direction);
            }


            lastRotation = this.m_rigidbody.rotation;
        }

        public override void ReceiveCurrentState(float relativeTime, NetworkingReader networkReader)
        {
            RigidbodyState newState = new RigidbodyState(this.m_rigidbody, relativeTime);

            UnserializeVector3(rotationSynchronizationMode, ref newState.m_rotation, networkReader, compressionRotationMode, minRotationValue, maxRotationValue);
            UnserializeVector3(angularVelocitySynchronizationMode, ref newState.m_angularVelocity, networkReader, compressionAngularVelocityMode, minAngularVelocityValue, maxAngularVelocityValue);


            int place = AddState(newState);

            byte rotationSign = 0;

            if (angularVelocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
                rotationSign = networkReader.ReadByte();

            //If calcul are needed for velocity
            if (place != -1 && place < currentStatesIndex - 1)
            {
                if (angularVelocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
                {
                    Quaternion diffRotation = (Quaternion.Euler(((RigidbodyState)statesBuffer[place]).m_rotation) * Quaternion.Inverse(Quaternion.Euler(((RigidbodyState)statesBuffer[place + 1]).m_rotation)));
                    float rotationAngle;
                    Vector3 rotationAxis;
                    diffRotation.ToAngleAxis(out rotationAngle, out rotationAxis);

                    rotationAxis.x = Mathf.Abs(rotationAxis.x) * ((1 << 0) & rotationSign) != 0 ? 1 : -1;
                    rotationAxis.y = Mathf.Abs(rotationAxis.y) * ((1 << 1) & rotationSign) != 0 ? 1 : -1;
                    rotationAxis.z = Mathf.Abs(rotationAxis.z) * ((1 << 2) & rotationSign) != 0 ? 1 : -1;

                    newState.m_angularVelocity = rotationAxis * rotationAngle * Mathf.Deg2Rad / (statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime);
                }
            }
        }

        public override void ReceiveSync(NetworkingReader networkReader)
        {
            Vector3 val = Vector3.zero;

            UnserializeVector3(rotationSynchronizationMode, ref val, networkReader, compressionRotationMode, minRotationValue, maxRotationValue);
            this.m_rigidbody.rotation = Quaternion.Euler(val);

            val = Vector3.zero;
            UnserializeVector3(angularVelocitySynchronizationMode, ref val, networkReader, compressionAngularVelocityMode, minAngularVelocityValue, maxAngularVelocityValue);
            this.m_rigidbody.angularVelocity = val;

            if (angularVelocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
                networkReader.ReadByte();

            ResetStatesBuffer();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (compressionRotationMode)
            {
                case COMPRESS_MODE.USHORT:
                    returnedValueOnCurrentRotation.x = Math.Decompress(Math.CompressToShort(this.m_rigidbody.rotation.eulerAngles.x, minRotationValue.x, maxRotationValue.x, out precisionAfterCompression.x), minRotationValue.x, maxRotationValue.x);
                    returnedValueOnCurrentRotation.y = Math.Decompress(Math.CompressToShort(this.m_rigidbody.rotation.eulerAngles.y, minRotationValue.y, maxRotationValue.y, out precisionAfterCompression.y), minRotationValue.y, maxRotationValue.y);
                    returnedValueOnCurrentRotation.z = Math.Decompress(Math.CompressToShort(this.m_rigidbody.rotation.eulerAngles.z, minRotationValue.z, maxRotationValue.z, out precisionAfterCompression.z), minRotationValue.z, maxRotationValue.z);
                    break;

                case COMPRESS_MODE.NONE:
                    returnedValueOnCurrentRotation.x = this.m_rigidbody.rotation.eulerAngles.x;
                    returnedValueOnCurrentRotation.y = this.m_rigidbody.rotation.eulerAngles.y;
                    returnedValueOnCurrentRotation.z = this.m_rigidbody.rotation.eulerAngles.z;
                    break;
            }
        }
#endif
    }
}