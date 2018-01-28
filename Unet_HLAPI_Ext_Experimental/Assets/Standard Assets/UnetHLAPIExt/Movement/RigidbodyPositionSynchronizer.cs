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
namespace BC_Solution.UnetNetwork
{
    public class RigidbodyPositionSynchronizer : MovementSynchronizer
    {

        class RigidbodyPositionState : State
        {
            public Vector3 m_position;
            public Vector3 m_velocity;

            public RigidbodyPositionState(Rigidbody r, float relativeTime)
            {
                this.m_relativeTime = relativeTime;
                m_position = r.position;
                m_velocity = r.velocity;
            }
        }

        [SerializeField]
        Rigidbody m_rigidbody;

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

        [Header("Compression")]
        public COMPRESS_MODE compressionPositionMode = COMPRESS_MODE.NONE;
        public Vector3 minPositionValue;
        public Vector3 maxPositionValue;

        [Space(5)]
        public COMPRESS_MODE compressionVelocityMode = COMPRESS_MODE.NONE;
        public Vector3 minVelocityValue;
        public Vector3 maxVelocityValue;

#if UNITY_EDITOR
        [Space(10)]
        [SerializeField]
        Vector3 precisionAfterCompression;
        [SerializeField]
        Vector3 returnedValueOnCurrentPosition;
# endif


        private Vector3 lastPosition = Vector3.zero;
        private Vector3 positionError = Vector3.zero;


        public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
        {
            Vector3 acceleration = Vector3.zero;

             acceleration = (((RigidbodyPositionState)extrapolatingState).m_velocity - ((RigidbodyPositionState)statesBuffer[1]).m_velocity) / ((extrapolatingState.m_relativeTime - statesBuffer[1].m_relativeTime));
             acceleration += this.m_rigidbody.useGravity ? (Physics.gravity) * timeSinceInterpolation : Vector3.zero;
      
            this.m_rigidbody.velocity = ((RigidbodyPositionState)extrapolatingState).m_velocity + acceleration*timeSinceInterpolation;
        }

        public override void OnEndExtrapolation(State rhs)
        {
            this.m_rigidbody.MovePosition(Vector3.Lerp(this.m_rigidbody.position, ((RigidbodyPositionState)statesBuffer[0]).m_position, Time.deltaTime / interpolationErrorTime));
            this.m_rigidbody.velocity = Vector3.Lerp(this.m_rigidbody.velocity, ((RigidbodyPositionState)statesBuffer[0]).m_velocity, Time.deltaTime / interpolationErrorTime);
        }

        public override void OnErrorCorrection()
        {
            this.m_rigidbody.MovePosition(this.m_rigidbody.position + positionError);
            positionError = Vector3.Lerp(positionError, Vector3.zero, Time.deltaTime / interpolationErrorTime); // smooth error compensation
            this.m_rigidbody.MovePosition(this.m_rigidbody.position - positionError);
        }

        public override void OnExtrapolation()
        {
            //Nothing to do, just run Physics
        }

        public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
        {
            //POSITION
           Vector3 val = this.m_rigidbody.position;
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
#if DEVELOPMENT
                        Math.DrawCatmullRomInterpolation(((RigidbodyPositionState)statesBuffer[lhsIndex + 1]).m_position, ((RigidbodyPositionState)lhs).m_position, ((RigidbodyPositionState)rhs).m_position, ((RigidbodyPositionState)statesBuffer[lhsIndex - 2]).m_position,
                                                                         statesBuffer[lhsIndex + 1].m_relativeTime, lhs.m_relativeTime, rhs.m_relativeTime, statesBuffer[lhsIndex - 2].m_relativeTime);
#endif
                        break;
                }
            }

            if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
            {
                if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                    positionError = val - this.m_rigidbody.position;
            }
            else
                positionError = Vector3.zero;

            this.m_rigidbody.MovePosition(val - positionError);

            //VELOCITY
            this.m_rigidbody.velocity = Vector3.Lerp(((RigidbodyPositionState)lhs).m_velocity, ((RigidbodyPositionState)rhs).m_velocity, t);
        }



        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();
            lastPosition = this.m_rigidbody.position;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) ||
                 (Vector3.SqrMagnitude(this.m_rigidbody.position - lastPosition) < positionThreshold * positionThreshold))
                return false;

            return true;
        }

        public override void GetCurrentState(NetworkWriter networkWriter)
        {
            lastPosition = this.m_rigidbody.position;

            SerializeVector3(positionSynchronizationMode, this.m_rigidbody.position, networkWriter, compressionPositionMode, minPositionValue, maxPositionValue);
            SerializeVector3(velocitySynchronizationMode, this.m_rigidbody.velocity, networkWriter, compressionVelocityMode, minVelocityValue, maxVelocityValue);
        }

        public override void ReceiveCurrentState(float relativeTime, NetworkReader networkReader)
        {
            RigidbodyPositionState newState = new RigidbodyPositionState(this.m_rigidbody, relativeTime);

            UnserializeVector3(positionSynchronizationMode, ref newState.m_position, networkReader, compressionPositionMode, minPositionValue, maxPositionValue);
            UnserializeVector3(velocitySynchronizationMode, ref newState.m_velocity, networkReader, compressionVelocityMode, minVelocityValue, maxVelocityValue);


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
            UnserializeVector3(positionSynchronizationMode, ref val, networkReader, compressionPositionMode, minPositionValue, maxPositionValue);
            this.m_rigidbody.position = val;

            val = Vector3.zero;
            UnserializeVector3(velocitySynchronizationMode, ref val, networkReader, compressionVelocityMode, minVelocityValue, maxVelocityValue);
            this.m_rigidbody.velocity = val;

            ResetStatesBuffer();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (compressionPositionMode)
            {
                case COMPRESS_MODE.USHORT:
                    returnedValueOnCurrentPosition.x = Math.Decompress(Math.CompressToShort(this.m_rigidbody.position.x, minPositionValue.x, maxPositionValue.x, out precisionAfterCompression.x), minPositionValue.x, maxPositionValue.x);
                    returnedValueOnCurrentPosition.y = Math.Decompress(Math.CompressToShort(this.m_rigidbody.position.y, minPositionValue.y, maxPositionValue.y, out precisionAfterCompression.y), minPositionValue.y, maxPositionValue.y);
                    returnedValueOnCurrentPosition.z = Math.Decompress(Math.CompressToShort(this.m_rigidbody.position.z, minPositionValue.z, maxPositionValue.z, out precisionAfterCompression.z), minPositionValue.z, maxPositionValue.z);
                    break;

                case COMPRESS_MODE.NONE:
                    returnedValueOnCurrentPosition.x = this.m_rigidbody.position.x;
                    returnedValueOnCurrentPosition.y = this.m_rigidbody.position.y;
                    returnedValueOnCurrentPosition.z = this.m_rigidbody.position.z;
                    break;
            }
        }
#endif
    }
}
