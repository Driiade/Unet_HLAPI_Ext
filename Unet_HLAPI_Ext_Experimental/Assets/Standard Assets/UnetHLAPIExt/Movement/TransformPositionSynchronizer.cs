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
    public class TransformPositionSynchronizer : MovementSynchronizer
    {
        class TransformPositionState : State
        {
            public Vector3 m_position;

            public TransformPositionState(Transform t, float relativeTime)
            {
                m_relativeTime = relativeTime;
                m_position = t.position;
            }
        }

        [SerializeField]
        Transform m_transform;

        [Space(10)]
        [SerializeField]
        SYNCHRONISATION_MODE positionSynchronizationMode;

        public INTERPOLATION_MODE positionInterpolationMode;

        [Space(20)]
        public float positionThreshold = 0.01f;
        public float snapThreshold = 10f;

        private Vector3 lastPosition = Vector3.zero;
        private Vector3 extrapolationVelocity;
        private Vector3 positionError = Vector3.zero;

        [Header("Compression")]
        public COMPRESS_MODE compressionMode = COMPRESS_MODE.NONE;
        public Vector3 minPositionValue;
        public Vector3 maxPositionValue;

#if UNITY_EDITOR
        [Space(10)]
        [SerializeField]
        Vector3 precisionAfterCompression;
        [SerializeField]
        Vector3 returnedValueOnCurrentPosition;
# endif

        public override void OnEndExtrapolation(State rhs)
        {
            this.m_transform.position = Vector3.Lerp(this.m_transform.position, ((TransformPositionState)rhs).m_position, Time.deltaTime / interpolationErrorTime);
        }

        public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
        {
            extrapolationVelocity = (((TransformPositionState)m_statesBuffer[0]).m_position - ((TransformPositionState)m_statesBuffer[1]).m_position) / ((m_statesBuffer[0].m_relativeTime - m_statesBuffer[1].m_relativeTime));

            this.m_transform.position += extrapolationVelocity * Time.deltaTime;
        }

        public override void OnExtrapolation()
        {
            this.m_transform.position += extrapolationVelocity * Time.deltaTime;
        }

        public override void OnErrorCorrection()
        {
            this.m_transform.position += positionError;
            positionError = Vector3.Lerp(positionError, Vector3.zero, Time.deltaTime / interpolationErrorTime); // smooth error compensation
            this.m_transform.position -= positionError;
        }

        public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
        {
            Vector3 val = this.m_transform.position;
            if (Vector3.SqrMagnitude(((TransformPositionState)rhs).m_position - ((TransformPositionState)lhs).m_position) > (snapThreshold * snapThreshold))
            {
                GetVector3(positionSynchronizationMode, ref val, ((TransformPositionState)rhs).m_position);
            }
            else
            {
                INTERPOLATION_MODE interpolationMode = GetCurrentInterpolationMode(positionInterpolationMode, lhsIndex, ((TransformPositionState)rhs).m_position, ((TransformPositionState)lhs).m_position);

                switch (interpolationMode)
                {
                    case INTERPOLATION_MODE.LINEAR:
                        GetVector3(positionSynchronizationMode, ref val, Vector3.Lerp(((TransformPositionState)lhs).m_position, ((TransformPositionState)rhs).m_position, t)); break;
                    case INTERPOLATION_MODE.CATMULL_ROM:
                        GetVector3(positionSynchronizationMode, ref val, Math.CatmullRomInterpolation(((TransformPositionState)m_statesBuffer[lhsIndex + 1]).m_position, ((TransformPositionState)lhs).m_position, ((TransformPositionState)rhs).m_position, ((TransformPositionState)m_statesBuffer[lhsIndex - 2]).m_position,
                                                                         m_statesBuffer[lhsIndex + 1].m_relativeTime, lhs.m_relativeTime, rhs.m_relativeTime, m_statesBuffer[lhsIndex - 2].m_relativeTime, (1f - t) * lhs.m_relativeTime + t * rhs.m_relativeTime));
#if EQUILIBREGAMES_DEBUG
                            ExtendedMath.DrawCatmullRomInterpolation(statesBuffer[lhsIndex + 1].position, lhs.position, rhs.position, statesBuffer[lhsIndex - 2].position,
                                                                             statesBuffer[lhsIndex + 1].timestamp, lhs.timestamp, rhs.timestamp, statesBuffer[lhsIndex - 2].timestamp);
#endif
                        break;
                }
            }

            if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
            {
                if (extrapolatingState != null)                     // We were extrapolating
                    positionError = this.m_transform.position - val;
            }
            else
                positionError = Vector3.zero;

            this.m_transform.position = val + positionError;
        }


        public override void ResetStatesBuffer()
        {
            base.ResetStatesBuffer();
            lastPosition = this.m_transform.position;
        }

        public override bool NeedToUpdate()
        {
            if ((!base.NeedToUpdate()) ||
                 ((Vector3.SqrMagnitude(this.m_transform.position - lastPosition) < positionThreshold * positionThreshold)))
                return false;

            return true;
        }


        public override void GetCurrentState(NetworkingWriter networkWriter)
        {
            lastPosition = this.m_transform.position;
            SerializeVector3(positionSynchronizationMode, this.m_transform.position, networkWriter, compressionMode, minPositionValue, maxPositionValue);
        }


        public override void ReceiveCurrentState(float relativeTime, NetworkingReader networkReader)
        {
            TransformPositionState newState = new TransformPositionState(this.m_transform, relativeTime);
            UnserializeVector3(positionSynchronizationMode, ref newState.m_position, networkReader, compressionMode, minPositionValue, maxPositionValue);
            AddState(newState);
        }

        public override void ReceiveSync(NetworkingReader networkReader)
        {
            Vector3 val = Vector3.zero;

            UnserializeVector3(positionSynchronizationMode, ref val, networkReader, compressionMode, minPositionValue, maxPositionValue);
            this.m_transform.position = val;

            ResetStatesBuffer();
        }

        public void Teleport(Vector3 position)
        {
            LocalTeleport(position);

            if (isServer)
            {
                foreach (NetworkingConnection conn in this.server.connections)
                {
                    if(conn != null && conn == this.connection)
                        SendToConnection(conn,"RpcTeleport", NetworkingChannel.DefaultReliable, position);
                }
            }
            else if (isClient)
                SendToServer("CmdTeleport", NetworkingChannel.DefaultReliable, position);

        }

        [NetworkedFunction]
        void CmdTeleport(Vector3 position)
        {
            LocalTeleport(position);

            foreach (NetworkingConnection conn in this.server.connections)
            {
                if (conn != null && conn == this.connection)
                    SendToConnection(conn, "RpcTeleport", NetworkingChannel.DefaultReliable, position);
            }
        }

        [NetworkedFunction]
        void RpcTeleport(Vector3 position)
        {
            if(!isServer)
                LocalTeleport(position);
        }

        public void LocalTeleport(Vector3 position)
        {
            m_transform.position = position;
            this.ResetStatesBuffer();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (compressionMode)
            {
                case COMPRESS_MODE.USHORT:
                    returnedValueOnCurrentPosition.x = Math.Decompress(Math.CompressToShort(this.m_transform.position.x, minPositionValue.x, maxPositionValue.x, out precisionAfterCompression.x), minPositionValue.x, maxPositionValue.x);
                    returnedValueOnCurrentPosition.y = Math.Decompress(Math.CompressToShort(this.m_transform.position.y, minPositionValue.y, maxPositionValue.y, out precisionAfterCompression.y), minPositionValue.y, maxPositionValue.y);
                    returnedValueOnCurrentPosition.z = Math.Decompress(Math.CompressToShort(this.m_transform.position.z, minPositionValue.z, maxPositionValue.z, out precisionAfterCompression.z), minPositionValue.z, maxPositionValue.z);
                    break;

                case COMPRESS_MODE.NONE:
                    returnedValueOnCurrentPosition.x = this.m_transform.position.x;
                    returnedValueOnCurrentPosition.y = this.m_transform.position.y;
                    returnedValueOnCurrentPosition.z = this.m_transform.position.z;
                    break;
            }
        }
#endif
    }
}
