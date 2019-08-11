using System.Collections.Generic;
using System.Linq;
using Unity.Labs.SuperScience;
using UnityEngine;

namespace Unity.Labs.SuperScience.Example
{
    /// <summary>
    /// Example gameobjects being tossed using the physics tracker (or XrPhysics for comparison)
    /// Not the same as, but similiar to the toss setup shown in https://youtu.be/P55i2cJKrFk
    /// </summary>
    public class TestPhysicsTrackerToss : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        [Tooltip("When true, uses physics tracker. Otherwise uses XRNodeState.")]
        bool m_UsePhysicsTracker;

        [SerializeField]
        [Tooltip("GameObject to be tossed.")]
        Rigidbody m_ToToss = null;

        [SerializeField]
        [Tooltip("What to toss the object with.")]
        UnityEngine.XR.XRNode m_TossedWith = UnityEngine.XR.XRNode.RightHand;

        [SerializeField]
        [Tooltip("Hold this button to grab the object. release to toss.")]
        string m_GrabButton = "Fire1";

        [SerializeField]
        [Header("Values to help with Debug")]
        bool m_IsButtonHeld;
#pragma warning restore 649

        // We have a physicsTracker for getting the smooth data, and hold the last position for doing direct integration
        PhysicsTracker m_MotionData = new PhysicsTracker();
        Vector3 m_XrOriginPosition, m_XrNodePosition;
        Quaternion m_XrOriginRotation, m_XrNodeRotation;

        private void Awake()
        {
            // get an offset to add to UnityEngine.XR.InputTracking.GetLocalPosition
            m_XrOriginPosition = Camera.main.transform.position;
            m_XrOriginRotation = Camera.main.transform.rotation;
        }

        /// <summary>
        /// Sends updated data to the physicsTracker, and then draws the calculated data
        /// </summary>
        void Update()
        {
            // update the GameObject to show it is being held
            if (m_IsButtonHeld)
            {
                m_XrNodePosition = m_XrOriginPosition + UnityEngine.XR.InputTracking.GetLocalPosition(m_TossedWith);

                if (m_UsePhysicsTracker)
                {
                    m_XrNodeRotation = m_XrOriginRotation * UnityEngine.XR.InputTracking.GetLocalRotation(m_TossedWith);
                    m_MotionData.Update(m_XrNodePosition, m_XrNodeRotation, Time.smoothDeltaTime);
                }
                m_ToToss.transform.position = m_XrNodePosition;
            }

            // check if GrabButton state has changed
            if (Input.GetButtonDown(m_GrabButton))
            {
                grab();
            }
            else if (Input.GetButtonUp(m_GrabButton))
            {
                release();
            }
        }

        /// <summary>
        /// attach the object to m_TossedWith 
        /// </summary>
        private void grab()
        {
            m_IsButtonHeld = true;
            if (m_UsePhysicsTracker)
            {
                m_MotionData.Reset(m_XrNodePosition, m_XrNodeRotation, Vector3.zero, Vector3.zero);
            }
        }

        /// <summary>
        /// detach the object from m_TossedWith
        /// </summary>
        private void release()
        {
            m_IsButtonHeld = false;
            if (m_UsePhysicsTracker)
            {
                m_ToToss.velocity = m_MotionData.Velocity;
            }
            else
            {
                m_ToToss.velocity = getVelocityOf(m_TossedWith);
            }
        }

        /// <summary>
        /// Gets the current velocity of an XrNode source
        /// </summary>
        private Vector3 getVelocityOf(UnityEngine.XR.XRNode source)
        {
            List<UnityEngine.XR.XRNodeState> nodeStates = new List<UnityEngine.XR.XRNodeState>();
            UnityEngine.XR.InputTracking.GetNodeStates(nodeStates);
            var sourceInfo = nodeStates.SingleOrDefault(s => s.nodeType == source);

            // handle the source node not existing
            if (sourceInfo.Equals(default(UnityEngine.XR.XRNodeState)))
                return Vector3.zero;

            Vector3 velocity;
            if (sourceInfo.TryGetVelocity(out velocity))
            {
                return velocity;
            }
            else
            {
                Debug.Log("TryGetVelocity() failed on " + source.ToString());
                return Vector3.zero;
            }
        }
    }
}
