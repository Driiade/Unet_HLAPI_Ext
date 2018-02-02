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
    public abstract class MovementSynchronizer : MonoBehaviour
    {
        public enum SYNCHRONISATION_MODE { NONE, X, Y, Z, XY, XZ, YZ, XYZ, CALCUL };
        public enum SNAP_MODE { NONE, RESET, CALCUL};
        public enum INTERPOLATION_MODE { LINEAR, CATMULL_ROM};
        public enum COMPRESS_MODE {NONE, USHORT}

        [System.Serializable]
        public class Config
        {
            [Tooltip("Update x per second")]
            public float updateRate = 20f;
        }

        public class State
        {
            public float m_relativeTime = -1;
        }

        [SerializeField]
        [Tooltip("If this is != null, no one can set the velocity except who has authority on the object")]
        protected NetworkingIdentity networkingIdentity;

        public Config onlineConfig;
        public Config lanConfig;
        public Config defaultConfig;

        [SerializeField]
        int maxBufferSize = 60;
        public bool useAdaptativeSynchronizationBackTime = true;

        protected State[] statesBuffer;
        protected int currentStatesIndex = -1;

        /// <summary>
        /// Used when real Update is needed for interpolation
        /// </summary>
        public float lastInterpolationUpdateTimer = -1;

        /// <summary>
        /// Used to compensate extrapolation side effect
        /// </summary>
        public float lastExtrapolationUpdateTimer = -1;

        [Space(20)]
        public bool useExtrapolation = false;
        public float extrapolationTime = 0.25f;

        [Space(20)]
        public float interpolationErrorTime = 0.1f;


        private NetworkMovementSynchronization networkMovementSynchronization;

        protected float extrapolationTimer = -1;

        private bool isActive = true;
        public bool IsActive
        {
            get { return isActive; }
            set
            {
                if(value != isActive)
                {
                    if (value)
                        OnActive();
                    else
                        OnInactive();
                }

                isActive = value;
            }
        }

        protected State extrapolatingState;

        public abstract void GetCurrentState(NetworkingWriter networkWriter);
        public abstract void ReceiveCurrentState(float relativeTime, NetworkingReader networkReader);
        public abstract void ReceiveSync(NetworkingReader networkReader);
        public abstract void OnInterpolation(State rhs, State lhs, int lhsIndex, float t);
        public abstract void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation);

        public abstract void OnExtrapolation();
        public abstract void OnEndExtrapolation(State rhs);

        public abstract void OnErrorCorrection();

        public virtual bool NeedToUpdate()
        {
            return isActive && enabled;
        }


        internal void Init(NetworkMovementSynchronization movSynchronization)
        {
            statesBuffer = new State[maxBufferSize];
            this.networkMovementSynchronization = movSynchronization;
        }



        protected void Update()
        {
           if (currentStatesIndex < 0 || networkingIdentity.hasAuthority)
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
                        OnEndExtrapolation(statesBuffer[0]);
                    }
                    else if (extrapolatingState == null || extrapolatingState != statesBuffer[0])    // we are not yet extrapolating
                    {
                       OnBeginExtrapolation(extrapolatingState, Time.realtimeSinceStartup - statesBuffer[0].m_relativeTime);

                        if (extrapolatingState == null)
                            extrapolationTimer = Time.realtimeSinceStartup + extrapolationTime;

                        extrapolatingState = statesBuffer[0];
                    }
                    else
                    {
                      OnExtrapolation();
                    }
                }
            }
            else if ((lhs != null && rhs != null))
            {
                OnInterpolation(rhs, lhs, lhsIndex, t);

                extrapolatingState = null;
                extrapolationTimer = -1;
            }

            OnErrorCorrection();
        }

        public float UpdateRate()
        {
            if (MatchmakingSystem.IsOnOnlineMatch)
                return onlineConfig.updateRate;
            else if (MatchmakingSystem.IsOnLanMatch)
                return lanConfig.updateRate;
            else
                return defaultConfig.updateRate;
        }


        void OnActive()
        {
           ResetStatesBuffer();
        }

        void OnInactive()
        {
           // ResetStatesBuffer();
        }

        /// <summary>
        /// Reset the states buffer by putting the currentStatesIndex et it initial value
        /// </summary>
        public virtual void ResetStatesBuffer()
        {
            currentStatesIndex = -1;
        }

        public int AddState(State s)
        {
            int place = -1;

            //If no States are present, put in first slot.
            if (currentStatesIndex < 0)
            {
                statesBuffer[0] = s;
                place = 0;
            }
            else
            {
                //First find proper place in buffer. If no place is found, state can be dropped (newState is too old)
                for (int i = 0; i <= currentStatesIndex; i++)
                {
                    //If the state in slot i is older than our new state, we found our slot.  
                    if (statesBuffer[i].m_relativeTime < s.m_relativeTime)
                    {
                        // Shift the buffer sideways, to make room in slot i. possibly deleting last state
                        for (int k = maxBufferSize - 1; k > i; k--)
                        {
                            statesBuffer[k] = statesBuffer[k - 1];
                        }
                        //insert state
                        statesBuffer[i] = s;
                        place = i;

                        //We are done, exit loop
                        break;
                    }
                }
            }

            currentStatesIndex = Mathf.Min(currentStatesIndex + 1, maxBufferSize-1);
            return place;
        }

        public void GetBestPlayBackState(out float firstStateDelay,out int lhsIndex, out State lhs, out State rhs, out float t)
        {

            lhs = null;
            rhs = null;
            t = -1;
            lhsIndex = 0;

            float currentTime = Time.realtimeSinceStartup;
            firstStateDelay = currentTime - statesBuffer[0].m_relativeTime;

            float synchronisationBackTime;

            if (!useAdaptativeSynchronizationBackTime)
                synchronisationBackTime = networkMovementSynchronization.m_nonAdaptativeBacktime;
            else
                synchronisationBackTime =  networkMovementSynchronization.m_adaptativeSynchronizationBackTime;

           // Debug.Log("First delay : " + firstStateDelay);
          // Debug.Log("lastStateDelay : " + ((NetworkingSystem.Instance.ServerTimestamp - statesBuffer[currentStatesIndex-1].timestamp) / 1000f));
          //  Debug.Log("RTT : " + NetworkingSystem.Instance.client.GetRTT());

            // Use interpolation if the playback time is present in the buffer (it's obligatly less than the firstStateDelay(which is the younger)
            if (firstStateDelay < synchronisationBackTime)
            {
                // Go through buffer and find correct state to play back
                for (int i = 0; i < currentStatesIndex; i++)
                {
                    // Search the best playback state (closest to 100 ms old (default time))
                    lhs = statesBuffer[i];

                    float lhsDelayMS;

                    lhsDelayMS = currentTime - lhs.m_relativeTime;

                    //We find a state to synchronise or it's the last state
                    if (lhsDelayMS >= synchronisationBackTime || i == currentStatesIndex - 1)
                    {
                        // The state one slot newer (<100ms (default time)) than the best playback state
                        //The goal is to linearly lerp all the time we can !
                        rhs = statesBuffer[Mathf.Max(i - 1, 0)];

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
        protected INTERPOLATION_MODE GetCurrentInterpolationMode(INTERPOLATION_MODE baseMode, int currentLhsIndex, Vector3 currentPosition, Vector3 lastPosition)
        {
            switch (baseMode)
            {
                case INTERPOLATION_MODE.CATMULL_ROM:
                    if (currentStatesIndex < 3 || currentLhsIndex == 0 || currentLhsIndex + 1 > currentStatesIndex || currentLhsIndex - 2 < 0 
                        || (statesBuffer[currentLhsIndex].m_relativeTime - statesBuffer[currentLhsIndex + 1].m_relativeTime) > 0.1f // superior to 100ms the catmull-Rom interpolation can be very wrong.
                        || (currentPosition - lastPosition).sqrMagnitude < 0.01) // 0.01 position threshold.
                    {
                        return INTERPOLATION_MODE.LINEAR;
                    }
                    break;
            }
            return baseMode;
        }

    }
}
