using UnityEngine;

namespace Unity.Labs.SuperScience.Sample
{
    /// <summary>
    /// Allows for example gameobjects to be tossed with the physics tracker, or the native XRPhysics data for comparison
    /// </summary>
    [RequireComponent(typeof(HandPose))]
    public class TestPhysicsTrackerToss : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        [Tooltip("When true, uses physics tracker. Otherwise uses XRNodeState.")]
        bool m_UsePhysicsTracker;

        [SerializeField]
        [Tooltip("GameObject to be tossed.")]
        Rigidbody m_ToToss = null;

#pragma warning restore 649

        // The source of all hand positioning and event data
        HandPose m_HandPose;

        // We use the physics tracker for smoothed extrapolated throwing data
        PhysicsTracker m_MotionData = new PhysicsTracker();
        bool m_Grabbed;

        void OnEnable()
        {
            m_HandPose = GetComponent<HandPose>();
            if (m_HandPose == null)
            {
                Debug.LogWarning("No source of hand data - disabling component");
                enabled = false;
            }
            m_HandPose.AddGripAction(UpdateGrab);
        }

        void OnDisable()
        {
            if (m_HandPose)
            {
                m_HandPose.RemoveGripAction(UpdateGrab);
            }
        }
        /// <summary>
        /// Tracks the targeted throwable object while the user is grabbing
        /// </summary>
        void Update()
        {
            if (m_Grabbed)
            {
                if (m_UsePhysicsTracker)
                {
                    m_MotionData.Update(transform.position, transform.rotation, Time.smoothDeltaTime);
                }
                m_ToToss.transform.position = transform.position;
            }
        }

        /// <summary>
        /// Handles tracking or releasing the object based on the given input
        /// </summary>
        /// <param name="grabbed">True if the grab input is down</param>
        void UpdateGrab(bool grabbed)
        {
            m_Grabbed = grabbed;
            if (m_Grabbed)
            {
                if (m_UsePhysicsTracker)
                {
                    m_MotionData.Reset(transform.position, transform.rotation, Vector3.zero, Vector3.zero);
                }
            }
            else
            {
                if (m_UsePhysicsTracker)
                {
                    m_ToToss.velocity = m_MotionData.Velocity;
                }
                else
                {
                    m_ToToss.velocity = m_HandPose.Velocity;
                }
            }
        }
    }
}
