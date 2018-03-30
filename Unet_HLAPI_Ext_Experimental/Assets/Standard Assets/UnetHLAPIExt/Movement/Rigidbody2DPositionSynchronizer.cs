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
using BC_Solution;
using System;

namespace BC_Solution.UnetNetwork
{
    public class Rigidbody2DPositionSynchronizer : MovementSynchronizer
    {

        class Rigidbody2DPositionState : State
        {
            public Vector2 m_position;
            public Vector2 m_velocity;

            public Rigidbody2DPositionState(Rigidbody2D r,int timestamp, float relativeTime,bool isLastState)
            {
                this.m_timestamp = timestamp;
                this.m_relativeTime = relativeTime;
                this.m_isLastState = isLastState;

                m_position = r.position;
                m_velocity = r.velocity;
            }
        }

        [SerializeField]
        Rigidbody2D m_rigidbody2D;

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE positionSynchronizationMode;

        public INTERPOLATION_MODE positionInterpolationMode = INTERPOLATION_MODE.CATMULL_ROM;
        [Tooltip("Min distance to use CatmUllRom interpolation instead of Linear")]
        public float minCatmullRomDistance = 0.1f;
        [Tooltip("Min time between 2 states to use CatmUllRom interpolation instead of Linear")]
        public float minCatmullRomTime = 0.1f;

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE velocitySynchronizationMode = SYNCHRONISATION_MODE.CALCUL;


        [Space(20)]
        public float positionThreshold = 0.01f;
        public float snapThreshold = 10f;

        [Header("Compression")]
        public COMPRESS_MODE compressionPositionMode = COMPRESS_MODE.NONE;
        public Vector2 minPositionValue;
        public Vector2 maxPositionValue;

        [Space(5)]
        public COMPRESS_MODE compressionVelocityMode = COMPRESS_MODE.NONE;
        public Vector2 minVelocityValue;
        public Vector2 maxVelocityValue;


        private Vector2 lastPosition = Vector3.zero;
        private Vector2 positionError = Vector3.zero;


        public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
        {
            Vector2 acceleration = Vector3.zero;

            if (m_currentStatesIndex >= 1)
            {
                acceleration = (((Rigidbody2DPositionState)extrapolatingState).m_velocity - ((Rigidbody2DPositionState)m_statesBuffer[1]).m_velocity) / ((extrapolatingState.m_relativeTime - m_statesBuffer[1].m_relativeTime));
                acceleration += this.m_rigidbody2D.gravityScale * (Physics2D.gravity) * timeSinceInterpolation;
            }

            this.m_rigidbody2D.velocity = ((Rigidbody2DPositionState)extrapolatingState).m_velocity + acceleration * timeSinceInterpolation;
        }

        public override void OnEndExtrapolation(State rhs)
        {
            this.m_rigidbody2D.position = (Vector3.Lerp(this.m_rigidbody2D.position, ((Rigidbody2DPositionState)m_statesBuffer[0]).m_position, Time.deltaTime / interpolationErrorTime));
            this.m_rigidbody2D.velocity = Vector3.zero;
        }

        public override void OnErrorCorrection()
        {
            this.m_rigidbody2D.position = (this.m_rigidbody2D.position - positionError);
            positionError = Vector3.Lerp(positionError, Vector3.zero, Time.deltaTime / interpolationErrorTime); // smooth error compensation
            this.m_rigidbody2D.position = (this.m_rigidbody2D.position + positionError);
        }

        public override void OnExtrapolation()
        {
            //Nothing to do, just run Physics
        }

        public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
        {
            //POSITION
            Vector2 val = this.m_rigidbody2D.position;
            if (Vector3.SqrMagnitude(((Rigidbody2DPositionState)rhs).m_position - ((Rigidbody2DPositionState)lhs).m_position) > (snapThreshold * snapThreshold))
            {
                GetVector2(positionSynchronizationMode, ref val, ((Rigidbody2DPositionState)rhs).m_position);
            }
            else
            {
                INTERPOLATION_MODE interpolationMode = GetCurrentInterpolationMode(positionInterpolationMode, lhsIndex, ((Rigidbody2DPositionState)rhs).m_position, ((Rigidbody2DPositionState)lhs).m_position, minCatmullRomDistance, minCatmullRomTime);

                switch (interpolationMode)
                {
                    case INTERPOLATION_MODE.LINEAR:
                        GetVector2(positionSynchronizationMode, ref val, Vector3.Lerp(((Rigidbody2DPositionState)lhs).m_position, ((Rigidbody2DPositionState)rhs).m_position, t)); break;
                    case INTERPOLATION_MODE.CATMULL_ROM:
                        GetVector2(positionSynchronizationMode, ref val, Math.CatmullRomInterpolation(((Rigidbody2DPositionState)m_statesBuffer[lhsIndex + 1]).m_position, ((Rigidbody2DPositionState)lhs).m_position, ((Rigidbody2DPositionState)rhs).m_position, ((Rigidbody2DPositionState)m_statesBuffer[lhsIndex - 2]).m_position,
                                                                         m_statesBuffer[lhsIndex + 1].m_relativeTime, lhs.m_relativeTime, rhs.m_relativeTime, m_statesBuffer[lhsIndex - 2].m_relativeTime, (1f - t) * lhs.m_relativeTime + t * rhs.m_relativeTime));
#if DEVELOPMENT
                            Math.DrawCatmullRomInterpolation(((Rigidbody2DPositionState)m_statesBuffer[lhsIndex + 1]).m_position, ((Rigidbody2DPositionState)lhs).m_position, ((Rigidbody2DPositionState)rhs).m_position, ((Rigidbody2DPositionState)m_statesBuffer[lhsIndex - 2]).m_position,
                                                                             m_statesBuffer[lhsIndex + 1].m_relativeTime, lhs.m_relativeTime, rhs.m_relativeTime, m_statesBuffer[lhsIndex - 2].m_relativeTime);
#endif
                        break;
                }
            }

            if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
            {
                if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                    positionError = this.m_rigidbody2D.position - val;
            }
            else
                positionError = Vector2.zero;

            this.m_rigidbody2D.position = (val + positionError);

            //VELOCITY
            this.m_rigidbody2D.velocity = Vector3.Lerp(((Rigidbody2DPositionState)lhs).m_velocity, ((Rigidbody2DPositionState)rhs).m_velocity, t);
        }



        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();
            lastPosition = this.m_rigidbody2D.position;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) ||
                 (Vector3.SqrMagnitude(this.m_rigidbody2D.position - lastPosition) < positionThreshold * positionThreshold))
                return false;

            return true;
        }

        public override void GetCurrentState(NetworkingWriter networkWriter)
        {
            lastPosition = this.m_rigidbody2D.position;

            SerializeVector2(positionSynchronizationMode, this.m_rigidbody2D.position, networkWriter, compressionPositionMode, minPositionValue, maxPositionValue);
            SerializeVector2(velocitySynchronizationMode, this.m_rigidbody2D.velocity, networkWriter, compressionVelocityMode, minVelocityValue, maxVelocityValue);
        }

        public override void GetLastState(NetworkingWriter networkWriter)
        {
            SerializeVector2(positionSynchronizationMode, ((Rigidbody2DPositionState)m_statesBuffer[0]).m_position, networkWriter, compressionPositionMode, minPositionValue, maxPositionValue);
            SerializeVector2(velocitySynchronizationMode, ((Rigidbody2DPositionState)m_statesBuffer[0]).m_velocity, networkWriter, compressionVelocityMode, minVelocityValue, maxVelocityValue);
        }

        public override void AddCurrentStateToBuffer()
        {
            AddState(new Rigidbody2DPositionState(this.m_rigidbody2D, NetworkTransport.GetNetworkTimestamp(), Time.realtimeSinceStartup, true));
        }


        public override void ReceiveCurrentState(int timestamp, float relativeTime,bool lastState, NetworkingReader networkReader)
        {
            Rigidbody2DPositionState newState = new Rigidbody2DPositionState(this.m_rigidbody2D, timestamp, relativeTime, lastState);

            UnserializeVector2(positionSynchronizationMode, ref newState.m_position, networkReader, compressionPositionMode, minPositionValue, maxPositionValue);
            UnserializeVector2(velocitySynchronizationMode, ref newState.m_velocity, networkReader, compressionVelocityMode, minVelocityValue, maxVelocityValue);

            int place = AddState(newState);

            //If calcul are needed for velocity
            if (place != -1 && place < m_currentStatesIndex - 1)
            {
                if (velocitySynchronizationMode == SYNCHRONISATION_MODE.CALCUL)
                    newState.m_velocity = (((Rigidbody2DPositionState)m_statesBuffer[place]).m_position - ((Rigidbody2DPositionState)m_statesBuffer[place + 1]).m_position) / ((m_statesBuffer[place].m_relativeTime - m_statesBuffer[place + 1].m_relativeTime));
            }
        }

        public override void ReceiveSync(NetworkingReader networkReader)
        {
            Vector2 val = Vector2.zero;
            UnserializeVector2(positionSynchronizationMode, ref val, networkReader, compressionPositionMode, minPositionValue, maxPositionValue);
            this.m_rigidbody2D.position = val;

            val = Vector2.zero;
            UnserializeVector2(velocitySynchronizationMode, ref val, networkReader, compressionVelocityMode, minVelocityValue, maxVelocityValue);
            this.m_rigidbody2D.velocity = val;

            ResetStatesBuffer();
        }

#if UNITY_EDITOR
        public override void OnInspectorGUI()
        {

             Vector2 precisionAfterCompression = Vector2.zero;
             Vector2 returnedValueOnCurrentPosition = Vector2.zero;

            switch (compressionPositionMode)
            {
                case COMPRESS_MODE.USHORT:
                    returnedValueOnCurrentPosition.x = Math.Decompress(Math.CompressToShort(this.m_rigidbody2D.position.x, minPositionValue.x, maxPositionValue.x, out precisionAfterCompression.x), minPositionValue.x, maxPositionValue.x);
                    returnedValueOnCurrentPosition.y = Math.Decompress(Math.CompressToShort(this.m_rigidbody2D.position.y, minPositionValue.y, maxPositionValue.y, out precisionAfterCompression.y), minPositionValue.y, maxPositionValue.y);
                    break;

                case COMPRESS_MODE.NONE:
                    returnedValueOnCurrentPosition.x = this.m_rigidbody2D.position.x;
                    returnedValueOnCurrentPosition.y = this.m_rigidbody2D.position.y;
                    break;
            }
            GUILayout.Space(10);
            GUILayout.Label("Precision : \n" + "(" + precisionAfterCompression.x + ", " + precisionAfterCompression.y + ")");
            GUILayout.Label("Returned value after compression : \n" + "(" + returnedValueOnCurrentPosition.x + ", " + returnedValueOnCurrentPosition.y + ")");
        }
#endif
    }
}
