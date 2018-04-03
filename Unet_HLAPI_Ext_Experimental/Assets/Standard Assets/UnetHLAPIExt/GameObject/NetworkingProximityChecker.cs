using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BC_Solution.UnetNetwork
{
    /// <summary>
    /// Check if a connection is near the transform.
    /// Modify networking listener.
    /// Only work on server
    /// </summary>
    public class NetworkingProximityChecker : MonoBehaviour
    {
#if SERVER
        public enum MODE { _2D, _3D }

        [SerializeField]
        NetworkingIdentity networkingIdentity;

        [SerializeField]
        Transform m_checkTransform;

        public float m_range = 10f;
        public float m_checkRate = 0.5f;
        public int m_maxNearConnection = 20;

        public MODE m_mode;

        Collider2D[] colliders2D;
        Collider[] colliders;

        protected void Awake()
        {
            colliders2D = new Collider2D[m_maxNearConnection];
            colliders = new Collider[m_maxNearConnection];
        }

        private float m_timer = -1;
        private void Update()
        {
            if (!networkingIdentity.isServer)
                return;

            if (Time.time > m_timer)
            {
                m_timer = Time.time + m_checkRate;

                switch (m_mode)
                {
                    case MODE._2D: Check2D(); break;
                    case MODE._3D: Check3D(); break;
                }
            }
        }

        public void Check2D()
        {
            foreach (NetworkingBehaviour networkingBehaviour in networkingIdentity.NetworkingBehaviours)
            {
                networkingBehaviour.m_serverConnectionListeners.Clear();

                int count = Physics2D.OverlapCircleNonAlloc(m_checkTransform.position, m_range, colliders2D);

                for (int i = 0; i < count; i++)
                {
                    NetworkingIdentity netIdentity = colliders2D[i].GetComponentInParent<NetworkingIdentity>();
                    if (netIdentity 
                        && netIdentity.m_serverConnection != null                    
                        && networkingIdentity.m_serverAwareConnections.Contains(netIdentity.m_serverConnection))
                    {
                        networkingBehaviour.m_serverConnectionListeners.Add(netIdentity.m_serverConnection);
                    }
                }
            }
        }

        public void Check3D()
        {
            foreach (NetworkingBehaviour networkingBehaviour in networkingIdentity.NetworkingBehaviours)
            {
                networkingBehaviour.m_serverConnectionListeners.Clear();

                int count = Physics.OverlapSphereNonAlloc(m_checkTransform.position, m_range, colliders);

                for (int i = 0; i < count; i++)
                {
                    NetworkingIdentity netIdentity = colliders[i].GetComponentInParent<NetworkingIdentity>();
                    if (netIdentity 
                        && netIdentity.m_serverConnection != null
                        && networkingIdentity.m_serverAwareConnections.Contains(netIdentity.m_serverConnection))
                    {
                        networkingBehaviour.m_serverConnectionListeners.Add(netIdentity.m_serverConnection);
                    }
                }
            }

        }

        private void OnDrawGizmosSelected()
        {
            if (m_checkTransform)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(m_checkTransform.position, m_range);
            }
        }
#endif
    }
}
