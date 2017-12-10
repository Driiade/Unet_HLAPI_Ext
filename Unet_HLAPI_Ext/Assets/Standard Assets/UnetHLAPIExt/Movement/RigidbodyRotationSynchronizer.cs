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
using UnityEngine.Networking;
using System;

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
        new Rigidbody rigidbody;

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
            this.rigidbody.angularVelocity = ((RigidbodyState)extrapolatingState).m_angularVelocity;
        }

        public override void OnEndExtrapolation(State rhs)
        {
            this.rigidbody.MoveRotation(Quaternion.Slerp(this.rigidbody.rotation, Quaternion.Euler(((RigidbodyState)statesBuffer[0]).m_rotation), Time.deltaTime / interpolationErrorTime));
            this.rigidbody.angularVelocity = Vector3.Lerp(this.rigidbody.angularVelocity, ((RigidbodyState)statesBuffer[0]).m_angularVelocity, Time.deltaTime / interpolationErrorTime);
        }

        public override void OnErrorCorrection()
        {
            this.rigidbody.MoveRotation(Quaternion.Inverse(rotationError) * this.rigidbody.rotation);
            rotationError = Quaternion.Slerp(rotationError, Quaternion.identity, Time.deltaTime / interpolationErrorTime);
            this.rigidbody.MoveRotation(rotationError * this.rigidbody.rotation);
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
                    rotationError = this.rigidbody.rotation * Quaternion.Inverse(Quaternion.Euler(val));
            }
            else
                rotationError = Quaternion.identity;

            this.rigidbody.MoveRotation(rotationError * Quaternion.Euler(val));

            //ANGULAR VELOCITY
            this.rigidbody.angularVelocity = Vector3.Lerp(((RigidbodyState)lhs).m_angularVelocity, ((RigidbodyState)rhs).m_angularVelocity, t);
        }

        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();
            lastRotation = this.rigidbody.rotation;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) || Vector3.SqrMagnitude(lastRotation.eulerAngles - this.rigidbody.rotation.eulerAngles) < rotationThreshold * rotationThreshold)
                return false;

            return true;
        }

        public override void GetCurrentState(NetworkWriter networkWriter)
        {
            lastRotation = this.rigidbody.rotation;

            SerializeVector3(rotationSynchronizationMode, this.rigidbody.rotation.eulerAngles, networkWriter, compressionRotationMode, minRotationValue, maxRotationValue);
            SerializeVector3(angularVelocitySynchronizationMode, this.rigidbody.angularVelocity, networkWriter, compressionAngularVelocityMode, minAngularVelocityValue, maxAngularVelocityValue);
        }

        public override void ReceiveCurrentState(float relativeTime, NetworkReader networkReader)
        {
            RigidbodyState newState = new RigidbodyState(this.rigidbody, relativeTime);

            UnserializeVector3(rotationSynchronizationMode, ref newState.m_rotation, networkReader, compressionRotationMode, minRotationValue, maxRotationValue);
            UnserializeVector3(angularVelocitySynchronizationMode, ref newState.m_angularVelocity, networkReader, compressionAngularVelocityMode, minAngularVelocityValue, maxAngularVelocityValue);


            int place = AddState(newState);

            //If calcul are needed for velocity
            if (place != -1 && place < currentStatesIndex - 1)
            {
                if (angularVelocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
                {
                    Quaternion diffRotation = (Quaternion.Euler(((RigidbodyState)statesBuffer[place]).m_rotation) * Quaternion.Inverse(Quaternion.Euler(((RigidbodyState)statesBuffer[place + 1]).m_rotation)));
                    float rotationAngle;
                    Vector3 rotationAxis;
                    diffRotation.ToAngleAxis(out rotationAngle, out rotationAxis);

                    newState.m_angularVelocity = rotationAxis * rotationAngle * Mathf.Deg2Rad / ((statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime));
                }
            }
        }

        public override void ReceiveSync(NetworkReader networkReader)
        {
            Vector3 val = Vector3.zero;

            UnserializeVector3(rotationSynchronizationMode, ref val, networkReader, compressionRotationMode, minRotationValue, maxRotationValue);
            this.rigidbody.rotation = Quaternion.Euler(val);

            val = Vector3.zero;
            UnserializeVector3(angularVelocitySynchronizationMode, ref val, networkReader, compressionAngularVelocityMode, minAngularVelocityValue, maxAngularVelocityValue);
            this.rigidbody.angularVelocity = val;

            ResetStatesBuffer();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (compressionRotationMode)
            {
                case COMPRESS_MODE.USHORT:
                    returnedValueOnCurrentRotation.x = Math.Decompress(Math.CompressToShort(this.rigidbody.rotation.eulerAngles.x, minRotationValue.x, maxRotationValue.x, out precisionAfterCompression.x), minRotationValue.x, maxRotationValue.x);
                    returnedValueOnCurrentRotation.y = Math.Decompress(Math.CompressToShort(this.rigidbody.rotation.eulerAngles.y, minRotationValue.y, maxRotationValue.y, out precisionAfterCompression.y), minRotationValue.y, maxRotationValue.y);
                    returnedValueOnCurrentRotation.z = Math.Decompress(Math.CompressToShort(this.rigidbody.rotation.eulerAngles.z, minRotationValue.z, maxRotationValue.z, out precisionAfterCompression.z), minRotationValue.z, maxRotationValue.z);
                    break;

                case COMPRESS_MODE.NONE:
                    returnedValueOnCurrentRotation.x = this.rigidbody.rotation.eulerAngles.x;
                    returnedValueOnCurrentRotation.y = this.rigidbody.rotation.eulerAngles.y;
                    returnedValueOnCurrentRotation.z = this.rigidbody.rotation.eulerAngles.z;
                    break;
            }
        }
#endif
    }
}