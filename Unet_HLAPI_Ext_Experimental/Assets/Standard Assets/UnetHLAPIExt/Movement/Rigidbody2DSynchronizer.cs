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

/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

namespace BC_Solution.UnetNetwork
{
    public class Rigidbody2DSynchronizer : MovementSynchronizer
    {

        class Rigidbody2DState : State
        {
            public float rotation = 0;
            public Vector3 velocity = Vector3.zero;
            public float angularVelocity = 0;
            public sbyte rotationOrientation = 0;

            public Rigidbody2DState(Rigidbody2D r, float relativeTime)
            {
                position = r.position;
                rotation = r.rotation;
                velocity = r.velocity;
                angularVelocity = r.angularVelocity;
                this.m_relativeTime = relativeTime;
            }
        }

        [SerializeField]
        new Rigidbody2D rigidbody2D;

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE positionSynchronizationMode;

        public INTERPOLATION_MODE positionInterpolationMode;


        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE velocitySynchronizationMode;

        [SerializeField][Tooltip("Don't do that if you always synchronise velocity")]
        bool calculVelocity = false;

        [Space(10)]
        [SerializeField]
        [Tooltip("It's recommanded to synchronise the angularVelocity if hight speed rotation synchronisation is needed")]
        bool rotationSynchronization;

        [SerializeField]
        bool angularVelocitySynchronization;
        public float rotationThreshold = 0.1f;

        [Space(20)]
        public float positionThreshold = 0.01f;
        public float snapThreshold = 10f;
        public SNAP_MODE velocitySnapMode = SNAP_MODE.CALCUL;


        private Vector2 lastPosition = Vector2.zero;
        private Vector2 lastVelocity = Vector2.zero;
        private float lastRotation = 0;
        private bool isExtrapolating = false;

        protected override void Awake()
        {
            base.Awake();
        }


        void FixedUpdate()
        {
            IsActive = !this.rigidbody2D.isKinematic;

            if (currentStatesIndex < 0 || networkIdentity.hasAuthority)
                return;

            State lhs;
            int lhsIndex;
            State rhs;
            float t;
            float firstStateDelay;
            Vector3 val = Vector3.zero;

            GetBestPlayBackState(out firstStateDelay,out lhsIndex, out lhs, out rhs, out t);


            //Extrapolation
            if (useExtrapolation && (lhs == null || rhs == null))
            {
                if (isExtrapolating == true)
                {
                    if (t >= extrapolationTime)
                    {
                        this.rigidbody2D.position = lastPosition;
                        this.rigidbody2D.rotation = lastRotation;
                        this.rigidbody2D.velocity = Vector3.zero;
                        return;
                    }
                    else
                    {
                        lastPosition = this.rigidbody2D.position;
                        lastRotation = this.rigidbody2D.rotation;
                        lastVelocity = this.rigidbody2D.velocity;
                        return;
                    }
                }
                else if(currentStatesIndex > 0)
                {
                    isExtrapolating = true;

                    if (!useAdaptativeSynchronizationBackTime)
                        t = Mathf.Clamp(firstStateDelay - networkMovementSynchronization.nonAdaptativeBacktime, 0, extrapolationTime);
                    else
                        t = Mathf.Clamp(firstStateDelay - networkMovementSynchronization.AdaptativeSynchronizationBackTime(), 0, extrapolationTime);

                    Vector2 acceleration = Vector2.zero;
                    float angularAcceleration = 0;

                    if (currentStatesIndex > 1)
                    {
                        acceleration = ((Rigidbody2DState)statesBuffer[0]).velocity - ((Rigidbody2DState)statesBuffer[1]).velocity;
                        angularAcceleration = ((Rigidbody2DState)statesBuffer[0]).rotation - ((Rigidbody2DState)statesBuffer[1]).rotation;
                    }

                    this.rigidbody2D.velocity = ((Vector2)((Rigidbody2DState)statesBuffer[0]).velocity);

                    this.rigidbody2D.MovePosition(((Rigidbody2DState)statesBuffer[0]).position
                                                                        + (Vector3)((Rigidbody2DState)statesBuffer[0]).velocity * t
                                                                        + 0.5f * ((Vector3)acceleration + (Vector3)(this.rigidbody2D.gravityScale*Physics2D.gravity)) * t * t);

                    this.rigidbody2D.velocity += acceleration * t;


                    if (rotationSynchronization)
                        this.rigidbody2D.MoveRotation(((Rigidbody2DState)statesBuffer[0]).rotation + ((Rigidbody2DState)statesBuffer[0]).angularVelocity * t + 0.5f * angularAcceleration * t * t);

                    if (angularVelocitySynchronization)
                        this.rigidbody2D.angularVelocity = ((Rigidbody2DState)statesBuffer[0]).angularVelocity + angularAcceleration * t;

                    lastPosition = this.rigidbody2D.position;
                    lastRotation = this.rigidbody2D.rotation;
                    lastVelocity = this.rigidbody2D.velocity;
                }
            }
            else if ((lhs != null && rhs != null))
            {
                isExtrapolating = false;

                //POSITION
                bool snapPosition = false;
                lastPosition = this.rigidbody2D.position;

                val = this.rigidbody2D.position;
                if (Vector3.SqrMagnitude(((Rigidbody2DState)rhs).position - ((Rigidbody2DState)lhs).position) > (snapThreshold * snapThreshold))
                {
                    GetVector3(positionSynchronizationMode, ref val, ((Rigidbody2DState)rhs).position);
                    snapPosition = true;
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
#if EQUILIBREGAMES_DEBUG
                            ExtendedMath.DrawCatmullRomInterpolation(statesBuffer[lhsIndex + 1].position, lhs.position, rhs.position, statesBuffer[lhsIndex - 2].position,
                                                                             statesBuffer[lhsIndex + 1].timestamp, lhs.timestamp, rhs.timestamp, statesBuffer[lhsIndex - 2].timestamp);
#endif
                            break;
                    }
                }
                this.rigidbody2D.MovePosition(val);

                //ROTATION
                if (rotationSynchronization)
                {
                    if (angularVelocitySynchronization)
                    {
                        this.rigidbody2D.angularVelocity = Mathf.Lerp(((Rigidbody2DState)lhs).angularVelocity, ((Rigidbody2DState)rhs).angularVelocity, t);
                        this.rigidbody2D.rotation = ((Rigidbody2DState)lhs).rotation + this.rigidbody2D.angularVelocity * (t * (rhs.m_relativeTime - lhs.m_relativeTime));
                    }
                    else
                    {
                        if (((Rigidbody2DState)lhs).rotationOrientation >= 0)
                        {
                            if (((Rigidbody2DState)lhs).rotation <= ((Rigidbody2DState)rhs).rotation)
                                this.rigidbody2D.MoveRotation(Mathf.Lerp(((Rigidbody2DState)lhs).rotation, ((Rigidbody2DState)rhs).rotation, t));
                            else
                                this.rigidbody2D.MoveRotation(Mathf.Lerp(((Rigidbody2DState)lhs).rotation, 360f + ((Rigidbody2DState)rhs).rotation, t));
                        }
                        else
                        {
                            if (((Rigidbody2DState)lhs).rotation >= ((Rigidbody2DState)rhs).rotation)
                                this.rigidbody2D.MoveRotation(Mathf.Lerp(((Rigidbody2DState)lhs).rotation, ((Rigidbody2DState)rhs).rotation, t));
                            else
                                this.rigidbody2D.MoveRotation(Mathf.Lerp(((Rigidbody2DState)lhs).rotation, ((Rigidbody2DState)rhs).rotation, t) - 360f);
                        }
                    }
                }

                if (!calculVelocity)
                    GetVector3(velocitySynchronizationMode, ref val, (Vector2.Lerp(((Rigidbody2DState)lhs).velocity, ((Rigidbody2DState)rhs).velocity, t)));
                else if (currentStatesIndex > 1)
                {
                    if (snapPosition)
                    {
                        switch (velocitySnapMode)
                        {
                            case SNAP_MODE.CALCUL: val = (((Rigidbody2DState)rhs).position - ((Rigidbody2DState)lhs).position) / ((rhs.m_relativeTime - lhs.m_relativeTime)); break;
                            case SNAP_MODE.RESET: val = Vector3.zero; break;
                            default: break;
                        }
                    }
                    else
                        val = (((Rigidbody2DState)rhs).position - ((Rigidbody2DState)lhs).position) / ((rhs.m_relativeTime - lhs.m_relativeTime));
                }


                this.rigidbody2D.velocity = val;
            }
            else if (IsActive)
            {
                this.rigidbody2D.position = lastPosition;
                this.rigidbody2D.velocity = lastVelocity;
                this.rigidbody2D.rotation = lastRotation;
            }

            lastPosition = this.rigidbody2D.position;
            lastRotation = this.rigidbody2D.rotation;
            lastVelocity = this.rigidbody2D.velocity;
        }


        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();

            lastPosition = this.rigidbody2D.position;
            lastRotation = this.rigidbody2D.rotation;
            lastVelocity = this.rigidbody2D.velocity;
        }

        public override bool NeedToUpdate()
        {
            if  ( (!base.NeedToUpdate()) || 
                 ((Vector2.SqrMagnitude(this.rigidbody2D.position - lastPosition) < positionThreshold * positionThreshold) && Mathf.Abs(lastRotation - this.rigidbody2D.rotation) < rotationThreshold) || 
                (this.rigidbody2D.isKinematic) )
            return false; 

            return true;
        }

        public override void GetCurrentState(NetworkWriter networkWriter)
        {
            lastPosition = this.rigidbody2D.position;
            lastRotation = this.rigidbody2D.rotation;

            SerializeVector3(positionSynchronizationMode, this.rigidbody2D.position, networkWriter);

            if (rotationSynchronization)
                networkWriter.Write(this.rigidbody2D.rotation);

            SerializeVector3(velocitySynchronizationMode, this.rigidbody2D.velocity, networkWriter);

            if (angularVelocitySynchronization)
                networkWriter.Write(this.rigidbody2D.angularVelocity);
            else
                networkWriter.Write((sbyte)(this.rigidbody2D.angularVelocity >= 0 ? 1 : -1));
        }

        public override void ReceiveCurrentState(float relativeTime, NetworkReader networkReader)
        {
            Rigidbody2DState newState = new Rigidbody2DState(this.rigidbody2D, relativeTime);

            UnserializeVector3(positionSynchronizationMode, ref newState.position, networkReader);

            if (rotationSynchronization)
                newState.rotation =  networkReader.ReadSingle();

            UnserializeVector3(velocitySynchronizationMode, ref newState.velocity, networkReader);

            if (angularVelocitySynchronization)
                newState.angularVelocity = networkReader.ReadSingle();
            else
                newState.rotationOrientation = networkReader.ReadSByte();

            int place = AddState(newState);

            if (calculVelocity && place != -1 && place < currentStatesIndex-1)
                newState.velocity = (statesBuffer[place].position - statesBuffer[place + 1].position) / ((statesBuffer[place].m_relativeTime - statesBuffer[place + 1].m_relativeTime));
        }

        public override void ReceiveSync(NetworkReader networkReader)
        {
            Vector3 val = Vector3.zero;

            UnserializeVector3(positionSynchronizationMode, ref val, networkReader);
            this.rigidbody2D.position = val;

            if (rotationSynchronization)
                this.rigidbody2D.rotation = networkReader.ReadSingle();

            val = Vector3.zero;
            UnserializeVector3(velocitySynchronizationMode, ref val, networkReader);
            this.rigidbody2D.velocity = val;

            if (angularVelocitySynchronization)
                this.rigidbody2D.angularVelocity = networkReader.ReadSingle();
        }
    }
} */

