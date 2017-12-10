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

/*namespace BC_Solution.UnetNetwork
{
    public class RigidbodySynchronizer : MovementSynchronizer
    {

        class RigidbodyState : State
        {
            public Vector3 rotation;
            public Vector3 velocity = Vector3.zero;
            public Vector3 angularVelocity = Vector3.zero;
            public sbyte rotationOrientation = 0;

            public RigidbodyState(Rigidbody r, float relativeTime)
            {
                position = r.position;
                rotation = r.rotation.eulerAngles;
                velocity = r.velocity;
                angularVelocity = r.angularVelocity;

                this.m_relativeTime = relativeTime;
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

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE rotationSynchronizationMode = SYNCHRONISATION_MODE.CALCUL;

        [SerializeField]
        public float rotationThreshold = 0.1f;

        [Space(20)]
        public float positionThreshold = 0.01f;
        public float snapThreshold = 10f;



        [Space(20)]
        public float interpolationErrorTime = 0.1f;


        private Vector3 lastPosition = Vector3.zero;
        private Quaternion lastRotation = Quaternion.identity;
        private Vector3 positionError = Vector3.zero;
        private Quaternion rotationError = Quaternion.identity;

        private State extrapolatingState;

        protected override void Awake()
        {
            base.Awake();
        }

        void Update()
        {
            if (currentStatesIndex < 0 || networkIdentity.hasAuthority)
                return;

            State lhs;
            int lhsIndex;
            State rhs;
            float t;
            float firstStateDelay;
            Vector3 val = Vector3.zero;

            GetBestPlayBackState(out firstStateDelay, out lhsIndex, out lhs, out rhs, out t);

            //Extrapolation
            if (useExtrapolation && (lhs == null || rhs == null))
            {
                if (currentStatesIndex > 0)
                {
                    if (Time.realtimeSinceStartup > extrapolationTimer)
                    {
                        this.rigidbody.MovePosition( Vector3.Lerp(this.rigidbody.position,((RigidbodyState)statesBuffer[0]).position, Time.deltaTime/ interpolationErrorTime));
                        this.rigidbody.velocity = Vector3.Lerp(this.rigidbody.velocity, ((RigidbodyState)statesBuffer[0]).velocity, Time.deltaTime / interpolationErrorTime);
                        this.rigidbody.MoveRotation(Quaternion.Slerp(this.rigidbody.rotation, Quaternion.Euler(((RigidbodyState)statesBuffer[0]).rotation), Time.deltaTime / interpolationErrorTime));
                        this.rigidbody.angularVelocity = Vector3.Lerp(this.rigidbody.angularVelocity,((RigidbodyState)statesBuffer[0]).angularVelocity, Time.deltaTime / interpolationErrorTime);
                    }
                    else if (extrapolatingState == null)    // we are not yet extrapolating
                    {

                        Vector3 acceleration = Vector3.zero;

                       // acceleration = (((RigidbodyState)extrapolatingState).velocity - ((RigidbodyState)statesBuffer[1]).velocity) / ((extrapolatingState.timestamp - statesBuffer[1].timestamp) / 1000f);
                        // acceleration += this.rigidbody.useGravity ? (Physics.gravity) * t : Vector3.zero;
                        // this.rigidbody.MovePosition(((RigidbodyState)extrapolatingState).position + this.rigidbody.velocity * t + 0.5f*acceleration*t*t);
                        this.rigidbody.velocity = ((RigidbodyState)extrapolatingState).velocity;

                        //this.rigidbody.MoveRotation(Quaternion.Euler(((RigidbodyState)extrapolatingState).rotation)* Quaternion.Euler(angularVelocity*t));
                        this.rigidbody.angularVelocity = ((RigidbodyState)extrapolatingState).angularVelocity;

                        if (extrapolatingState == null)
                            extrapolationTimer = Time.realtimeSinceStartup + extrapolationTime;

                        extrapolatingState = statesBuffer[0];
                    }
                }
            }
            else if (lhs != null && rhs != null)
            {
                //POSITION
                val = this.rigidbody.position;
                if (Vector3.SqrMagnitude(((RigidbodyState)rhs).position - ((RigidbodyState)lhs).position) > (snapThreshold * snapThreshold))
                {
                    GetVector3(positionSynchronizationMode, ref val, ((RigidbodyState)rhs).position);
                }
                else
                {
                    INTERPOLATION_MODE interpolationMode = GetCurrentInterpolationMode(positionInterpolationMode, lhsIndex);

                    switch (interpolationMode)
                    {
                        case INTERPOLATION_MODE.LINEAR:
                            GetVector3(positionSynchronizationMode, ref val, Vector3.Lerp(lhs.position, rhs.position, t)); break;
                        case INTERPOLATION_MODE.CATMULL_ROM:
                            GetVector3(positionSynchronizationMode, ref val, Math.CatmullRomInterpolation(statesBuffer[lhsIndex + 1].position, lhs.position, rhs.position, statesBuffer[lhsIndex - 2].position,
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


                //ROTATION
                val = this.transform.rotation.eulerAngles;
                GetVector3(rotationSynchronizationMode, ref val, Quaternion.Slerp(Quaternion.Euler(((RigidbodyState)lhs).rotation), Quaternion.Euler(((RigidbodyState)rhs).rotation), t).eulerAngles);


                if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
                {
                    if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                        rotationError = this.rigidbody.rotation * Quaternion.Inverse(Quaternion.Euler(val));
                }
                else
                    rotationError = Quaternion.identity;


                this.rigidbody.MoveRotation(rotationError * Quaternion.Euler(val));


                //ANGULAR VELOCITY
                //For the moment, no angular velocity sync, always calcul
                this.rigidbody.angularVelocity = Vector3.Lerp(((RigidbodyState)lhs).angularVelocity, ((RigidbodyState)rhs).angularVelocity, t);

                //VELOCITY
                val = this.rigidbody.velocity;

                if (velocitySynchronizationMode != SYNCHRONISATION_MODE.CALCUL)
                    GetVector3(velocitySynchronizationMode, ref val, (Vector3.Lerp(((RigidbodyState)lhs).velocity, ((RigidbodyState)rhs).velocity, t)));
                else
                {
                    val = Vector3.Lerp(((RigidbodyState)lhs).velocity, ((RigidbodyState)rhs).velocity, t);
                }
                this.rigidbody.velocity = val;

                extrapolatingState = null; //Not extrapolating anymore
                extrapolationTimer = -1;
            }
            else if (IsActive)
            {
                this.rigidbody.position = ((RigidbodyState)statesBuffer[0]).position;
                this.rigidbody.velocity = ((RigidbodyState)statesBuffer[0]).velocity;
                this.rigidbody.rotation = Quaternion.Euler(((RigidbodyState)statesBuffer[0]).rotation);
                this.rigidbody.angularVelocity = ((RigidbodyState)statesBuffer[0]).angularVelocity;
            }

            this.rigidbody.MoveRotation(Quaternion.Inverse(rotationError) * this.rigidbody.rotation);
            rotationError = Quaternion.Slerp(rotationError, Quaternion.identity, Time.deltaTime / interpolationErrorTime);
            this.rigidbody.MoveRotation(rotationError * this.rigidbody.rotation);

            this.rigidbody.MovePosition(this.rigidbody.position + positionError);
            positionError = Vector3.Lerp(positionError, Vector3.zero, Time.deltaTime / interpolationErrorTime); // smooth error compensation
            this.rigidbody.MovePosition(this.rigidbody.position - positionError);

            lastPosition = this.rigidbody.position;
            lastRotation = this.rigidbody.rotation;
        }


        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();

            lastPosition = this.rigidbody.position;
            lastRotation = this.rigidbody.rotation;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) ||
                 ((Vector3.SqrMagnitude(this.rigidbody.position - lastPosition) < positionThreshold * positionThreshold) 
                 && Vector3.SqrMagnitude(lastRotation.eulerAngles - this.rigidbody.rotation.eulerAngles) < rotationThreshold * rotationThreshold))
                return false;

            return true;
        }

        public override void GetCurrentState(NetworkWriter networkWriter)
        {
            lastPosition = this.rigidbody.position;
            lastRotation = this.rigidbody.rotation;

            SerializeVector3(positionSynchronizationMode, this.rigidbody.position, networkWriter);
            SerializeVector3(velocitySynchronizationMode, this.rigidbody.velocity, networkWriter);
            SerializeVector3(rotationSynchronizationMode, this.rigidbody.rotation.eulerAngles, networkWriter);
        }

        public override void ReceiveCurrentState(float relativeTime, NetworkReader networkReader)
        {
            RigidbodyState newState = new RigidbodyState(this.rigidbody, relativeTime);

            UnserializeVector3(positionSynchronizationMode, ref newState.position, networkReader);
            UnserializeVector3(velocitySynchronizationMode, ref newState.velocity, networkReader);
            UnserializeVector3(rotationSynchronizationMode, ref newState.rotation, networkReader);


            int place = AddState(newState);

            //If calcul are needed for velocity
            if (place != -1 && place < currentStatesIndex - 1)
            {
               if(velocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
                    newState.velocity = (statesBuffer[place].position - statesBuffer[place + 1].position) / ((statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime));


                Quaternion diffRotation = (Quaternion.Euler(((RigidbodyState)statesBuffer[place]).rotation) * Quaternion.Inverse(Quaternion.Euler(((RigidbodyState)statesBuffer[place + 1]).rotation)));
                float rotationAngle;
                Vector3 rotationAxis;
                diffRotation.ToAngleAxis(out rotationAngle, out rotationAxis);
                
                newState.angularVelocity = rotationAxis * rotationAngle * Mathf.Deg2Rad / ((statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime) );
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

            val = Vector3.zero;
            UnserializeVector3(rotationSynchronizationMode, ref val, networkReader);
            this.rigidbody.rotation = Quaternion.Euler(val);

            ResetStatesBuffer();
        }
    }
}*/

