using UnityEngine;

namespace Unity.Labs.SuperScience.Example
{
    /// <summary>
    /// Example component that draws data from the physics tracker (or raw integration for comparison)
    /// </summary>
    public class DrawPhysicsData : MonoBehaviour
    {
        /// <summary>
        /// Drawing constants we pass to the gizmo functions
        /// </summary>
        const float k_RayScale = 0.5f;
        const float k_RayEndcap = 0.05f;
        const float k_AngularAxisLength = 0.25f;
        const float k_AngularWedgeSize = 0.1f;
        const float k_MinAngularSpeed = 0.05f;

        // These control the lengths of the drawn velocity, and acceleration vectors
        // Acceleration values tend to be pretty high, so we lower them down considerably to stay readable
        const float k_VelocityScale = 1.0f*k_RayScale;
        const float k_AccelerationScale = 0.125f*k_RayScale;

        static readonly Color k_SmoothVelColor = Color.blue;
        static readonly Color k_SmoothAccColor = Color.green;
        static readonly Color k_AngularVelocityColor = Color.white;
        static readonly Color k_DirectIntegrationColor = Color.red;

#pragma warning disable 649
        [SerializeField]
        [Tooltip("The object to track in space and report physics data on.")]
        Transform m_ToTrack;

        [SerializeField]
        [Tooltip("Should we draw the PhysicsTracker's reported smooth speed for the object?")]
        bool m_DrawSmoothSpeed = true;

        [SerializeField]
        [Tooltip("Should we draw the PhysicsTracker's reported smooth acceleration for the object?")]
        bool m_DrawSmoothAcceleration = true;

        [SerializeField]
        [Tooltip("Should we draw the PhysicsTracker's reported angular velocity for the object?")]
        bool m_DrawAngularVelocity = true;

        [SerializeField]
        [Tooltip("Should we draw a direct frame-by-frame integrated speed for the object?")]
        bool m_DrawDirectSpeed = true;

        [SerializeField]
        [Tooltip("Should we use the PhysicsTracker's reported direction for physics data, or just report magnitudes?")]
        bool m_UseDirection = true;
#pragma warning restore 649

        // We have a physicsTracker for getting the smooth data, and hold the last position for doing direct integration
        PhysicsTracker m_MotionData = new PhysicsTracker();
        Vector3 m_LastPosition;

        void Start ()
        {
            m_MotionData.Reset(m_ToTrack.position, m_ToTrack.rotation, Vector3.zero, Vector3.zero);
            m_LastPosition = m_ToTrack.position;
        }

        /// <summary>
        /// Sends updated data to the physicsTracker, and then draws the calculated data
        /// </summary>
        void Update ()
        {
            m_MotionData.Update(m_ToTrack.position, m_ToTrack.rotation, Time.smoothDeltaTime);
            if (m_DrawSmoothSpeed)
            {
                if (m_UseDirection)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, m_MotionData.Direction, k_SmoothVelColor, 1.0f, m_MotionData.Speed*k_VelocityScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + m_MotionData.Velocity*k_VelocityScale, k_RayEndcap, k_SmoothVelColor);
                }
                else
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, transform.forward, k_SmoothVelColor, 1.0f, m_MotionData.Speed*k_VelocityScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + transform.forward*m_MotionData.Speed*k_VelocityScale, k_RayEndcap, k_SmoothVelColor);
                }
            }
            
            if (m_DrawSmoothAcceleration)
            {
                if (m_UseDirection)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, m_MotionData.Direction, k_SmoothAccColor, 1.0f, m_MotionData.AccelerationStrength*k_AccelerationScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + m_MotionData.Acceleration*k_AccelerationScale, k_RayEndcap, k_SmoothAccColor);
                }
                else
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, transform.forward, k_SmoothAccColor, 1.0f, m_MotionData.AccelerationStrength*k_AccelerationScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + transform.forward*m_MotionData.AccelerationStrength*k_AccelerationScale, k_RayEndcap, k_SmoothAccColor);
                }
            }

            if (m_DrawAngularVelocity)
            {
                // Angular velocity axis changes to rapidly to follow data, so we always draw the rotation off a fixed axis
                GizmoModule.instance.DrawRay(m_ToTrack.position, -transform.right, k_AngularVelocityColor, 1.0f, k_AngularAxisLength);
                GizmoModule.instance.DrawWedge(m_ToTrack.position - transform.right*k_AngularAxisLength, Quaternion.LookRotation(-transform.right), k_AngularWedgeSize, m_MotionData.AngularSpeed, k_AngularVelocityColor);

                // If someone wants to see the active axis, draw that as an additional ray
                if (m_UseDirection)
                {
                    // We draw the axis centered - rotating one way or another can flip the axis and it just adds visual noise.
                    GizmoModule.instance.DrawRay(m_ToTrack.position - m_MotionData.AngularAxis*k_AngularAxisLength*0.5f, m_MotionData.AngularAxis, k_AngularVelocityColor, 1.0f, k_AngularAxisLength);
                }
            }

            // Also draw pure single-frame integration
            if (m_DrawDirectSpeed)
            {
                var directOffset = (m_ToTrack.position - m_LastPosition);
                var deltaDistance = directOffset.magnitude/Mathf.Max(Time.smoothDeltaTime, 0.00001f);
                var directDirection = directOffset.normalized;

                if (m_UseDirection)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, directDirection, k_DirectIntegrationColor, 1.0f, deltaDistance*k_VelocityScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + directDirection*deltaDistance*k_VelocityScale, k_RayEndcap, k_DirectIntegrationColor);
                }
                else
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, transform.forward, k_DirectIntegrationColor, 1.0f, deltaDistance*k_RayScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + transform.forward*deltaDistance*k_VelocityScale, k_RayEndcap, k_DirectIntegrationColor);
                }
            }

            // Store the last position so we can integrate it again next frame
            m_LastPosition = m_ToTrack.position;

        }
    }
}
