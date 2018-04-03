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
using System;
using UnityEngine.Networking;

namespace BC_Solution.UnetNetwork
{
    public abstract class MovementSynchronizer : NetworkingBehaviour
    {
        public enum SYNCHRONISATION_MODE { NONE, X, Y, Z, XY, XZ, YZ, XYZ, CALCUL };
        public enum SNAP_MODE { NONE, RESET, CALCUL };
        public enum INTERPOLATION_MODE { LINEAR, CATMULL_ROM };
        public enum COMPRESS_MODE { NONE, USHORT }


        public class State
        {
            public float m_relativeTime = -1;
            public int m_timestamp;
            public bool m_isLastState; //true if it's the final state of the object
        }

#if UNITY_EDITOR
        public bool debug = true;
#endif

        [SerializeField, Space(10)]
        int maxBufferSize = 60;

        internal State[] m_statesBuffer;
        internal int m_currentStatesIndex = -1;


        [Space(20)]
        public bool useExtrapolation = true;
        public float extrapolationTime = 0.1f;

        [Space(20)]
        public float interpolationErrorTime = 0.01f;

        internal float m_lastInterpolationUpdateTimer = -1;

#if CLIENT
        internal bool m_clientSentEndInterpolation = false;
#endif

#if SERVER
        internal Dictionary<NetworkingConnection, float> m_lastServerInterpolationUpdateTimers = new Dictionary<NetworkingConnection, float>();
        internal Dictionary<NetworkingConnection, bool> m_serverSentEndInterpolation = new Dictionary<NetworkingConnection, bool>();
        internal Dictionary<NetworkingConnection, State> m_serverLastestStateUpdated = new Dictionary<NetworkingConnection, State>();
#endif


#if CLIENT || SERVER
        private NetworkMovementSynchronization m_networkMovementSynchronization;
#endif

        public float m_extrapolationTimer { get; private set; }

        private bool m_isActive = true;
        public bool IsActive
        {
            get { return m_isActive; }
            set
            {
                if(value != m_isActive)
                {
                    if (value)
                        OnActive();
                    else
                        OnInactive();
                }

                m_isActive = value;
            }
        }

        /// <summary>
        /// All new state with a timestamp < loackStateTimestamp will be ignored
        /// </summary>
        protected float m_lockStateTime = -1;

        protected State extrapolatingState;

        public abstract void GetCurrentState(NetworkingWriter networkWriter);
        public abstract void GetLastState(NetworkingWriter networkWriter);

        public abstract void ReceiveCurrentState(int timestamp, float relativeTime,bool isLastState, NetworkingReader networkReader);

        public abstract void AddCurrentStateToBuffer();

        public abstract void ReceiveSync(NetworkingReader networkReader);
        public abstract void OnInterpolation(State rhs, State lhs, int lhsIndex, float t);
        public abstract void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation);

        public abstract void OnExtrapolation();
        public abstract void OnEndExtrapolation(State rhs);

        public abstract void OnErrorCorrection();

#if UNITY_EDITOR
        public virtual void OnInspectorGUI() { }
#endif

        public virtual bool NeedToUpdate()
        {
            return m_isActive;
        }



#if SERVER
        public void RegisterLastestState(NetworkingConnection connection)
        {
            m_serverLastestStateUpdated[connection] = m_statesBuffer[0];
        }

        public virtual bool ServerNeedUpdateLastestStateTo(NetworkingConnection connection)
        {
            if (m_currentStatesIndex < 0)
                return false;

            State lastestStateUpdated = m_serverLastestStateUpdated[connection];

            if (lastestStateUpdated == null && m_statesBuffer[0] != null)
                return true;

             if (lastestStateUpdated != null && lastestStateUpdated.m_timestamp < m_statesBuffer[0].m_timestamp)
                return true;
            else
                return false;
        }


        public override void OnServerDisconnect(NetworkingConnection conn)
        {
            m_serverLastestStateUpdated.Remove(conn);
            m_serverSentEndInterpolation.Remove(conn);
            m_lastServerInterpolationUpdateTimers.Remove(conn);
        }

        public override void OnStopServer()
        {
            ResetStatesBuffer();
        }
#endif


        internal void Init(NetworkMovementSynchronization movSynchronization)
        {
            m_extrapolationTimer = -1;
            m_statesBuffer = new State[maxBufferSize];
#if CLIENT || SERVER
            this.m_networkMovementSynchronization = movSynchronization;
#endif
        }

        public bool m_isExtrapolating { get; private set; }
        public bool m_isInterpolating { get; private set; }

        /// <summary>
        /// Called by NEtworkingMovementSyncronization
        /// </summary>
        protected internal void MovementUpdate()
        {
            if (m_currentStatesIndex < 0)
                return;

#if SERVER
           if (isServer && this.serverConnection == null)
                return;
#endif

#if CLIENT
            if(this.isLocalClient)
                return;
#endif
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
                if (m_currentStatesIndex >= 0)
                {
                    if (!m_statesBuffer[0].m_isLastState) //Don't extrapolate after the final state
                    {
                        if (extrapolatingState == null)    // we are not yet extrapolating
                        {
                            m_isExtrapolating = true;
                            m_isInterpolating = false;
                            extrapolatingState = m_statesBuffer[0];

                            OnBeginExtrapolation(extrapolatingState, Time.realtimeSinceStartup - m_statesBuffer[0].m_relativeTime - m_networkMovementSynchronization.CurrentSynchronizationBackTime);
                            m_extrapolationTimer = Time.realtimeSinceStartup + extrapolationTime;
                        }
                        else if (extrapolatingState != null && Time.realtimeSinceStartup > m_extrapolationTimer)
                        {
                            m_isExtrapolating = true;
                            m_isInterpolating = false;
                            OnEndExtrapolation(m_statesBuffer[0]);
                        }
                        else if (extrapolatingState != null && Time.realtimeSinceStartup < m_extrapolationTimer)
                        {
                            m_isExtrapolating = true;
                            m_isInterpolating = false;
                            OnExtrapolation();
                        }
                    }
                    else
                    {
                        m_isExtrapolating = false;
                        m_isInterpolating = false;
                        extrapolatingState = null;
                        m_extrapolationTimer = -1;
                    }
                }
            }
            else if ((lhs != null && rhs != null))
            {
                m_isInterpolating = true;
                m_isExtrapolating = false;

                OnInterpolation(rhs, lhs, lhsIndex, t);

                extrapolatingState = null;
                m_extrapolationTimer = -1;
            }

            OnErrorCorrection();
        }


        void OnActive()
        {
           ResetStatesBuffer();
        }

        void OnInactive()
        {
           // ResetStatesBuffer();
        }

#if SERVER

        public override void OnServerAddListener(NetworkingConnection conn)
        {
            m_lastServerInterpolationUpdateTimers.Add(conn, -1);
            m_serverSentEndInterpolation.Add(conn, false);
            m_serverLastestStateUpdated.Add(conn, null);
        }


#endif

        /// <summary>
        /// Reset the states buffer by putting the currentStatesIndex et it initial value
        /// </summary>
        public virtual void ResetStatesBuffer()
        {
            m_currentStatesIndex = -1;
        }

        public int AddState(State s)
        {
            int place = -1;

            if(s.m_relativeTime < m_lockStateTime)
            {
                //Locked, do nothing
            }
            //If no States are present, put in first slot.
            else if (m_currentStatesIndex < 0)
            {
                m_statesBuffer[0] = s;
                place = 0;
                m_currentStatesIndex = Mathf.Min(m_currentStatesIndex + 1, maxBufferSize - 1);
            }
            else
            {
                //First find proper place in buffer. If no place is found, state can be dropped (newState is too old)
                for (int i = 0; i <= m_currentStatesIndex; i++)
                {
                    //If the state in slot i is older than our new state, we found our slot.  
                    if (m_statesBuffer[i].m_relativeTime < s.m_relativeTime || m_statesBuffer[i] == null)
                    {
                        // Shift the buffer sideways, to make room in slot i. possibly deleting last state
                        for (int k = maxBufferSize - 1; k > i; k--)
                        {
                            m_statesBuffer[k] = m_statesBuffer[k - 1];
                        }
                        //insert state
                        m_statesBuffer[i] = s;
                        place = i;

                        m_currentStatesIndex = Mathf.Min(m_currentStatesIndex + 1, maxBufferSize - 1);
                        //We are done, exit loop
                        break;
                    }
                    else if (m_statesBuffer[i].m_relativeTime == s.m_relativeTime)
                    {
                        //Replace state
                        m_statesBuffer[i] = s;
                        place = i;

                        break;
                    }
                }
            }


            return place;
        }

        public void GetBestPlayBackState(out float firstStateDelay,out int lhsIndex, out State lhs, out State rhs, out float t)
        {

            lhs = null;
            rhs = null;
            t = -1;
            lhsIndex = 0;

            float currentTime = Time.realtimeSinceStartup;
            firstStateDelay = currentTime - m_statesBuffer[0].m_relativeTime;

            float synchronisationBackTime = m_networkMovementSynchronization.CurrentSynchronizationBackTime;


           // Debug.Log("First delay : " + firstStateDelay);
          // Debug.Log("lastStateDelay : " + ((NetworkingSystem.Instance.ServerTimestamp - statesBuffer[currentStatesIndex-1].timestamp) / 1000f));
          //  Debug.Log("RTT : " + NetworkingSystem.Instance.client.GetRTT());

            // Use interpolation if the playback time is present in the buffer (it's obligatly less than the firstStateDelay(which is the younger)
            if (firstStateDelay < synchronisationBackTime)
            {
                // Go through buffer and find correct state to play back
                for (int i = 0; i < m_currentStatesIndex; i++)
                {
                    // Search the best playback state (closest to 100 ms old (default time))
                    lhs = m_statesBuffer[i];

                    float lhsDelayMS;

                    lhsDelayMS = currentTime - lhs.m_relativeTime;

                    //We find a state to synchronise or it's the last state
                    if (lhsDelayMS >= synchronisationBackTime || i == m_currentStatesIndex - 1)
                    {
                        // The state one slot newer (<100ms (default time)) than the best playback state
                        //The goal is to linearly lerp all the time we can !
                        rhs = m_statesBuffer[Mathf.Max(i - 1, 0)];

                        float rhsDelayMS;

                        rhsDelayMS = currentTime - rhs.m_relativeTime;

                        lhsIndex = i;
                        t = Mathf.InverseLerp(lhsDelayMS, rhsDelayMS, synchronisationBackTime);
                        break;
                    }
                }
            }
        }

        public void SerializeVector2(SYNCHRONISATION_MODE mode, Vector3 value, NetworkingWriter networkWriter, COMPRESS_MODE compressionMode, Vector3 minValue, Vector3 maxValue)
        {
            SerializeVector3(mode, value, networkWriter, compressionMode, minValue, maxValue);
        }

        public void SerializeVector3(SYNCHRONISATION_MODE mode, Vector3 value, NetworkingWriter networkWriter, COMPRESS_MODE compressionMode, Vector3 minValue, Vector3 maxValue)
        {
            float precision;

            switch (mode)
            {
                case SYNCHRONISATION_MODE.X:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            networkWriter.Write(value.x);
                            break;
                        case COMPRESS_MODE.USHORT:
                            networkWriter.Write(Math.CompressToShort(value.x, minValue.x, maxValue.x, out precision));
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.Y:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            networkWriter.Write(value.y);
                            break;
                        case COMPRESS_MODE.USHORT:
                            networkWriter.Write(Math.CompressToShort(value.y, minValue.y, maxValue.y, out precision));
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.Z:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            networkWriter.Write(value.z);
                            break;
                        case COMPRESS_MODE.USHORT:
                            networkWriter.Write(Math.CompressToShort(value.z, minValue.z, maxValue.z, out precision));
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.XY:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            networkWriter.Write(value.x);
                            networkWriter.Write(value.y);
                            break;
                        case COMPRESS_MODE.USHORT:
                            networkWriter.Write(Math.CompressToShort(value.x, minValue.x, maxValue.x, out precision));
                            networkWriter.Write(Math.CompressToShort(value.y, minValue.y, maxValue.y, out precision));
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.XZ:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            networkWriter.Write(value.x);
                            networkWriter.Write(value.z);
                            break;
                        case COMPRESS_MODE.USHORT:
                            networkWriter.Write(Math.CompressToShort(value.x, minValue.x, maxValue.x, out precision));
                            networkWriter.Write(Math.CompressToShort(value.z, minValue.z, maxValue.z, out precision));
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.YZ:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            networkWriter.Write(value.y);
                            networkWriter.Write(value.z);
                            break;
                        case COMPRESS_MODE.USHORT:
                            networkWriter.Write(Math.CompressToShort(value.y, minValue.y, maxValue.y, out precision));
                            networkWriter.Write(Math.CompressToShort(value.z, minValue.z, maxValue.z, out precision));
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.XYZ:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            networkWriter.Write(value.x);
                            networkWriter.Write(value.y);
                            networkWriter.Write(value.z);
                            break;
                        case COMPRESS_MODE.USHORT:
                            networkWriter.Write(Math.CompressToShort(value.x, minValue.x, maxValue.x, out precision));
                            networkWriter.Write(Math.CompressToShort(value.y, minValue.y, maxValue.y, out precision));
                            networkWriter.Write(Math.CompressToShort(value.z, minValue.z, maxValue.z, out precision));
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.NONE: break;
                case SYNCHRONISATION_MODE.CALCUL: break;
            }
        }

        public void UnserializeVector2(SYNCHRONISATION_MODE mode, ref Vector2 value, NetworkingReader networkReader, COMPRESS_MODE compressionMode, Vector3 minValue, Vector3 maxValue)
        {
            Vector3 value3 = value;
            UnserializeVector3(mode, ref value3, networkReader, compressionMode, minValue,  maxValue);
            value.x = value3.x;
            value.y = value3.y;
        }


        public void UnserializeVector3(SYNCHRONISATION_MODE mode, ref Vector3 value, NetworkingReader networkReader, COMPRESS_MODE compressionMode, Vector3 minValue, Vector3 maxValue)
        {
            switch (mode)
            {
                case SYNCHRONISATION_MODE.X:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            value.x = networkReader.ReadSingle();
                            break;
                        case COMPRESS_MODE.USHORT:
                            value.x = Math.Decompress(networkReader.ReadUInt16(), minValue.x, maxValue.x);
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.Y:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            value.y = networkReader.ReadSingle();
                            break;
                        case COMPRESS_MODE.USHORT:
                            value.y = Math.Decompress(networkReader.ReadUInt16(), minValue.y, maxValue.y);
                            break;
                    }
                    break;
                case SYNCHRONISATION_MODE.Z:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            value.z = networkReader.ReadSingle();
                            break;
                        case COMPRESS_MODE.USHORT:
                            value.z = Math.Decompress(networkReader.ReadUInt16(), minValue.z, maxValue.z);
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.XY:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            value.x = networkReader.ReadSingle();
                            value.y = networkReader.ReadSingle();
                            break;
                        case COMPRESS_MODE.USHORT:
                            value.x = Math.Decompress(networkReader.ReadUInt16(), minValue.x, maxValue.x);
                            value.y = Math.Decompress(networkReader.ReadUInt16(), minValue.y, maxValue.y);
                            break;
                    }
                    break; 

                case SYNCHRONISATION_MODE.XZ:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            value.x = networkReader.ReadSingle();
                            value.z = networkReader.ReadSingle();
                            break;
                        case COMPRESS_MODE.USHORT:
                            value.x = Math.Decompress(networkReader.ReadUInt16(), minValue.x, maxValue.x);
                            value.z = Math.Decompress(networkReader.ReadUInt16(), minValue.z, maxValue.z);
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.YZ:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            value.y = networkReader.ReadSingle();
                            value.z = networkReader.ReadSingle();
                            break;
                        case COMPRESS_MODE.USHORT:
                            value.y = Math.Decompress(networkReader.ReadUInt16(), minValue.y, maxValue.y);
                            value.z = Math.Decompress(networkReader.ReadUInt16(), minValue.z, maxValue.z);
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.XYZ:
                    switch (compressionMode)
                    {
                        case COMPRESS_MODE.NONE:
                            value.x = networkReader.ReadSingle();
                            value.y = networkReader.ReadSingle();
                            value.z = networkReader.ReadSingle();
                            break;
                        case COMPRESS_MODE.USHORT:
                            value.x = Math.Decompress(networkReader.ReadUInt16(), minValue.x, maxValue.x);
                            value.y = Math.Decompress(networkReader.ReadUInt16(), minValue.y, maxValue.y);
                            value.z = Math.Decompress(networkReader.ReadUInt16(), minValue.z, maxValue.z);
                            break;
                    }
                    break;

                case SYNCHRONISATION_MODE.NONE: break;
                case SYNCHRONISATION_MODE.CALCUL: break;
            }
        }


        public void GetVector2(SYNCHRONISATION_MODE mode, ref Vector2 value, Vector3 target)
        {
            Vector3 value3 = value;
            GetVector3(mode, ref value3, target);
            value.x = value3.x;
            value.y = value3.y;
        }

        public void GetVector3(SYNCHRONISATION_MODE mode, ref Vector3 value, Vector3 target)
        {
            switch (mode)
            {
                case SYNCHRONISATION_MODE.X:
                    value.x = target.x; break;

                case SYNCHRONISATION_MODE.Y:
                    value.y = target.y; break;

                case SYNCHRONISATION_MODE.Z:
                    value.z = target.z; break;

                case SYNCHRONISATION_MODE.XY:
                    value.x = target.x;
                    value.y = target.y; break;

                case SYNCHRONISATION_MODE.XZ:
                    value.x = target.x;
                    value.z = target.z; break;

                case SYNCHRONISATION_MODE.YZ:
                    value.y = target.y;
                    value.z = target.z; break;

                case SYNCHRONISATION_MODE.XYZ:
                    value.x = target.x;
                    value.y = target.y;
                    value.z = target.z;
                    break;

                case SYNCHRONISATION_MODE.NONE: break;
                case SYNCHRONISATION_MODE.CALCUL: break;
            }
        }


        /// <summary>
        /// Get the current interpolation Mode to use, based on the baseMode you want to use.
        /// Some interpolation need the respect of different rule to be available
        /// </summary>
        /// <param name="baseMode"></param>
        /// <param name="currentLhsIndex"></param>
        /// <returns></returns>
        protected INTERPOLATION_MODE GetCurrentInterpolationMode(INTERPOLATION_MODE baseMode, int currentLhsIndex, Vector3 currentPosition, Vector3 lastPosition, float minDistance, float minTime)
        {
            switch (baseMode)
            {
                case INTERPOLATION_MODE.CATMULL_ROM:
                    if (m_currentStatesIndex < 3 || currentLhsIndex == 0 || currentLhsIndex + 1 > m_currentStatesIndex || currentLhsIndex - 2 < 0 
                        || (m_statesBuffer[currentLhsIndex].m_relativeTime - m_statesBuffer[currentLhsIndex + 1].m_relativeTime) > minTime // superior to 100ms the catmull-Rom interpolation can be very wrong.
                        || (currentPosition - lastPosition).sqrMagnitude < minDistance) // 0.01 position threshold.
                    {
                        return INTERPOLATION_MODE.LINEAR;
                    }
                    break;
            }
            return baseMode;
        }
    }
}
