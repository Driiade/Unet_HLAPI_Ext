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
    public class RigidbodyPositionSynchronizer : MovementSynchronizer
    {

        class RigidbodyPositionState : State
        {
            public Vector3 m_position;
            public Vector3 m_velocity = Vector3.zero;

            public RigidbodyPositionState(Rigidbody r, float relativeTime)
            {
                this.m_relativeTime = relativeTime;
                m_position = r.position;
                m_velocity = r.velocity;
            }
        }

        [SerializeField]
        new Rigidbody rigidbody;

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE positionSynchronizationMode;

        public INTERPOLATION_MODE positionInterpolationMode = INTERPOLATION_MODE.CATMULL_ROM;


        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE velocitySynchronizationMode = SYNCHRONISATION_MODE.CALCUL;


        [Space(20)]
        public float positionThreshold = 0.01f;
        public float snapThreshold = 10f;

        private Vector3 lastPosition = Vector3.zero;

        private Vector3 positionError = Vector3.zero;


        public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
        {
            Vector3 acceleration = Vector3.zero;

             acceleration = (((RigidbodyPositionState)extrapolatingState).m_velocity - ((RigidbodyPositionState)statesBuffer[1]).m_velocity) / ((extrapolatingState.m_relativeTime - statesBuffer[1].m_relativeTime));
             acceleration += this.rigidbody.useGravity ? (Physics.gravity) * timeSinceInterpolation : Vector3.zero;
      
            this.rigidbody.velocity = ((RigidbodyPositionState)extrapolatingState).m_velocity + acceleration*timeSinceInterpolation;
        }

        public override void OnEndExtrapolation(State rhs)
        {
            this.rigidbody.MovePosition(Vector3.Lerp(this.rigidbody.position, ((RigidbodyPositionState)statesBuffer[0]).m_position, Time.deltaTime / interpolationErrorTime));
            this.rigidbody.velocity = Vector3.Lerp(this.rigidbody.velocity, ((RigidbodyPositionState)statesBuffer[0]).m_velocity, Time.deltaTime / interpolationErrorTime);
        }

        public override void OnErrorCorrection()
        {
            this.rigidbody.MovePosition(this.rigidbody.position + positionError);
            positionError = Vector3.Lerp(positionError, Vector3.zero, Time.deltaTime / interpolationErrorTime); // smooth error compensation
            this.rigidbody.MovePosition(this.rigidbody.position - positionError);
        }

        public override void OnExtrapolation()
        {
            //Nothing to do, just run Physics
        }

        public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
        {
            //POSITION
           Vector3 val = this.rigidbody.position;
            if (Vector3.SqrMagnitude(((RigidbodyPositionState)rhs).m_position - ((RigidbodyPositionState)lhs).m_position) > (snapThreshold * snapThreshold))
            {
                GetVector3(positionSynchronizationMode, ref val, ((RigidbodyPositionState)rhs).m_position);
            }
            else
            {
                INTERPOLATION_MODE interpolationMode = GetCurrentInterpolationMode(positionInterpolationMode, lhsIndex, ((RigidbodyPositionState)rhs).m_position, ((RigidbodyPositionState)lhs).m_position);

                switch (interpolationMode)
                {
                    case INTERPOLATION_MODE.LINEAR:
                        GetVector3(positionSynchronizationMode, ref val, Vector3.Lerp(((RigidbodyPositionState)lhs).m_position, ((RigidbodyPositionState)rhs).m_position, t)); break;
                    case INTERPOLATION_MODE.CATMULL_ROM:
                        GetVector3(positionSynchronizationMode, ref val, Math.CatmullRomInterpolation(((RigidbodyPositionState)statesBuffer[lhsIndex + 1]).m_position, ((RigidbodyPositionState)lhs).m_position, ((RigidbodyPositionState)rhs).m_position, ((RigidbodyPositionState)statesBuffer[lhsIndex - 2]).m_position,
                                                                         statesBuffer[lhsIndex + 1].m_relativeTime, lhs.m_relativeTime, rhs.m_relativeTime, statesBuffer[lhsIndex - 2].m_relativeTime, (1f - t) * lhs.m_relativeTime + t * rhs.m_relativeTime));
#if DEVEVELOPMENT
                            ExtendedMath.DrawCatmullRomInterpolation(statesBuffer[lhsIndex + 1].position, lhs.position, rhs.position, statesBuffer[lhsIndex - 2].position,
                                                                             statesBuffer[lhsIndex + 1].timestamp, lhs.timestamp, rhs.timestamp, statesBuffer[lhsIndex - 2].timestamp);
#endif
                        break;
                }
            }

            if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
            {
                if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                    positionError = val - this.rigidbody.position;
            }
            else
                positionError = Vector3.zero;

            this.rigidbody.MovePosition(val - positionError);

            //VELOCITY
            this.rigidbody.velocity = Vector3.Lerp(((RigidbodyPositionState)lhs).m_velocity, ((RigidbodyPositionState)rhs).m_velocity, t);
        }



        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();
            lastPosition = this.rigidbody.position;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) ||
                 (Vector3.SqrMagnitude(this.rigidbody.position - lastPosition) < positionThreshold * positionThreshold))
                return false;

            return true;
        }

        public override void GetCurrentState(NetworkWriter networkWriter)
        {
            lastPosition = this.rigidbody.position;

            SerializeVector3(positionSynchronizationMode, this.rigidbody.position, networkWriter);
            SerializeVector3(velocitySynchronizationMode, this.rigidbody.velocity, networkWriter);
        }

        public override void ReceiveCurrentState(float relativeTime, NetworkReader networkReader)
        {
            RigidbodyPositionState newState = new RigidbodyPositionState(this.rigidbody, relativeTime);

            UnserializeVector3(positionSynchronizationMode, ref newState.m_position, networkReader);
            UnserializeVector3(velocitySynchronizationMode, ref newState.m_velocity, networkReader);


            int place = AddState(newState);

            //If calcul are needed for velocity
            if (place != -1 && place < currentStatesIndex - 1)
            {
                if (velocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
                    newState.m_velocity = (((RigidbodyPositionState)statesBuffer[place]).m_position - ((RigidbodyPositionState)statesBuffer[place + 1]).m_position) / ((statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime));
            }
        }

        public override void ReceiveSync(NetworkReader networkReader)
        {
            Vector3 val = Vector3.zero;
            UnserializeVector3(positionSynchronizationMode, ref val, networkReader);
            this.rigidbody.position = val;

            val = Vector3.zero;
            UnserializeVector3(velocitySynchronizationMode, ref val, networkReader);
            this.rigidbody.velocity = val;

            ResetStatesBuffer();
        }
    }
}
