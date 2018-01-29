﻿/*Copyright(c) <2017> <Benoit Constantin ( France )>

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
    public class TransformRotationSynchronizer : MovementSynchronizer
    {
        class TransformRotationState : State
        {
            public Vector3 m_rotation;

            public TransformRotationState(Transform t, float relativeTime, bool local)
            {
                this.m_relativeTime = relativeTime;

                if(local)
                {
                    m_rotation = t.localRotation.eulerAngles;
                }
                else
                    m_rotation = t.rotation.eulerAngles;
            }
        }

        [SerializeField]
        Transform m_transform;

        public bool localSync = false;

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE rotationSynchronizationMode;

        [SerializeField]
        [Tooltip("Angle in degree for minimum update")]
        float rotationThreshold = 0.1f;

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


        private Quaternion lastRotation = Quaternion.identity;

        private Quaternion extrapolationAngularVelocity;

        private Quaternion rotationError = Quaternion.identity;



        public override void OnEndExtrapolation(State rhs)
        {
            if (localSync)
            {
                this.m_transform.localRotation = Quaternion.Slerp(this.m_transform.localRotation, Quaternion.Euler(((TransformRotationState)rhs).m_rotation), Time.deltaTime / interpolationErrorTime);
            }
            else
                this.m_transform.rotation = Quaternion.Slerp(this.m_transform.rotation, Quaternion.Euler(((TransformRotationState)rhs).m_rotation), Time.deltaTime / interpolationErrorTime);
        }

        public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
        {
            extrapolationAngularVelocity = (Quaternion.Euler(((TransformRotationState)statesBuffer[0]).m_rotation) * Quaternion.Inverse(Quaternion.Euler(((TransformRotationState)statesBuffer[1]).m_rotation)));
            Vector3 axis;
            float angle;
            extrapolationAngularVelocity.ToAngleAxis(out angle, out axis);
            angle /= ((statesBuffer[0].m_relativeTime - statesBuffer[1].m_relativeTime));
            extrapolationAngularVelocity = Quaternion.AngleAxis(angle, axis);
        }

        public override void OnErrorCorrection()
        {
            if (localSync)
            {
                this.m_transform.localRotation = Quaternion.Inverse(rotationError) * this.m_transform.localRotation;
                rotationError = Quaternion.Slerp(rotationError, Quaternion.identity, Time.deltaTime / interpolationErrorTime);
                this.m_transform.localRotation = rotationError * this.m_transform.localRotation;
            }
            else
            {
                this.m_transform.rotation = Quaternion.Inverse(rotationError) * this.m_transform.rotation;
                rotationError = Quaternion.Slerp(rotationError, Quaternion.identity, Time.deltaTime / interpolationErrorTime);
                this.m_transform.rotation = rotationError * this.m_transform.rotation;
            }
        }

        public override void OnExtrapolation()
        {
            Vector3 axis;
            float angle;
            extrapolationAngularVelocity.ToAngleAxis(out angle, out axis);
            angle *= Time.deltaTime;

            if (localSync)
            {
                this.m_transform.localRotation = Quaternion.AngleAxis(angle, axis) * this.m_transform.localRotation;
            }
            else
                this.m_transform.rotation = Quaternion.AngleAxis(angle, axis) * this.m_transform.rotation;
        }

        public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
        {
            //ROTATION     
            Vector3 val;

            if (localSync)
            {
                val = this.m_transform.localRotation.eulerAngles;
            }
            else
                val = this.m_transform.rotation.eulerAngles;

            GetVector3(rotationSynchronizationMode, ref val, Quaternion.Slerp(Quaternion.Euler(((TransformRotationState)lhs).m_rotation), Quaternion.Euler(((TransformRotationState)rhs).m_rotation), t).eulerAngles);


            if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
            {
                if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                {
                    if (localSync)
                    {
                        rotationError = this.m_transform.localRotation * Quaternion.Inverse(Quaternion.Euler(val));
                    }
                    else
                    {
                        rotationError = this.m_transform.rotation * Quaternion.Inverse(Quaternion.Euler(val));
                    }
                }
            }
            else
                rotationError = Quaternion.identity;

            if (localSync)
            {
                this.m_transform.localRotation = rotationError * Quaternion.Euler(val);
            }
            else
                this.m_transform.rotation = rotationError * Quaternion.Euler(val);
        }


        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();

            lastRotation = this.m_transform.rotation;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) ||
                 Quaternion.Angle(this.m_transform.rotation, lastRotation) < rotationThreshold)
                return false;

            return true;
        }


        public override void GetCurrentState(NetworkingWriter networkWriter)
        {
            if (localSync)
            {
                lastRotation = this.m_transform.localRotation;
            }
            else
                lastRotation = this.m_transform.rotation;

            SerializeVector3(rotationSynchronizationMode, localSync ? this.m_transform.localEulerAngles : this.m_transform.rotation.eulerAngles, networkWriter, compressionRotationMode, minRotationValue, maxRotationValue);
        }


        public override void ReceiveCurrentState(float relativeTime, NetworkingReader networkReader)
        {
            TransformRotationState newState = new TransformRotationState(this.m_transform, relativeTime, localSync);
            UnserializeVector3(rotationSynchronizationMode, ref newState.m_rotation, networkReader, compressionRotationMode, minRotationValue, maxRotationValue);

            AddState(newState);
        }

        public override void ReceiveSync(NetworkingReader networkReader)
        {
            Vector3 val = Vector3.zero;

            UnserializeVector3(rotationSynchronizationMode, ref val, networkReader, compressionRotationMode, minRotationValue, maxRotationValue);

            if (localSync)
                this.m_transform.localRotation = Quaternion.Euler(val);
            else
                this.m_transform.rotation = Quaternion.Euler(val);

            ResetStatesBuffer();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (compressionRotationMode)
            {
                case COMPRESS_MODE.USHORT:
                    returnedValueOnCurrentRotation.x = Math.Decompress(Math.CompressToShort(this.m_transform.rotation.eulerAngles.x, minRotationValue.x, maxRotationValue.x, out precisionAfterCompression.x), minRotationValue.x, maxRotationValue.x);
                    returnedValueOnCurrentRotation.y = Math.Decompress(Math.CompressToShort(this.m_transform.rotation.eulerAngles.y, minRotationValue.y, maxRotationValue.y, out precisionAfterCompression.y), minRotationValue.y, maxRotationValue.y);
                    returnedValueOnCurrentRotation.z = Math.Decompress(Math.CompressToShort(this.m_transform.rotation.eulerAngles.z, minRotationValue.z, maxRotationValue.z, out precisionAfterCompression.z), minRotationValue.z, maxRotationValue.z);
                    break;

                case COMPRESS_MODE.NONE:
                    returnedValueOnCurrentRotation.x = this.m_transform.rotation.eulerAngles.x;
                    returnedValueOnCurrentRotation.y = this.m_transform.rotation.eulerAngles.y;
                    returnedValueOnCurrentRotation.z = this.m_transform.rotation.eulerAngles.z;
                    break;
            }
        }
#endif
    }
}
