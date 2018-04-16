using UnityEngine;

namespace Unity.Labs.SuperScience.Example
{
    /// <summary>
    /// Example component that uses a physics tracker in a variety of ways
    /// </summary>
    public class DrawPhysicsData : MonoBehaviour
    {
        const float k_RayScale = 0.5f;
        const float k_RayEndcap = 0.05f;
        const float k_AngularAxisLength = 0.25f;
        const float k_AngularWedgeSize = 0.1f;
        const float k_MinAngularSpeed = 0.05f;

        [SerializeField]
        Transform m_ToTrack;

        [SerializeField]
        bool m_DrawSmoothSpeed = true;

        [SerializeField]
        bool m_DrawSmoothAcceleration = true;

        [SerializeField]
        bool m_DrawAngularVelocity = true;

        [SerializeField]
        bool m_DrawDirectSpeed = true;

        [SerializeField]
        bool m_UseDirection = true;

        PhysicsTracker m_MotionData = new PhysicsTracker();

        Vector3 m_LastPosition;

	    // Use this for initialization
	    void Start ()
        {
            m_MotionData.Reset(m_ToTrack.position, m_ToTrack.rotation, Vector3.zero, Vector3.zero);
            m_LastPosition = m_ToTrack.position;
	    }
	
	    // Update is called once per frame
	    void Update ()
        {
            m_MotionData.Update(m_ToTrack.position, m_ToTrack.rotation, Time.deltaTime);
            if (m_DrawSmoothSpeed)
            {
                if (m_UseDirection)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, m_MotionData.Direction, Color.blue, 1.0f, m_MotionData.Speed*k_RayScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + m_MotionData.Velocity*k_RayScale, k_RayEndcap, Color.blue);
                }
                else
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, transform.forward, Color.blue, 1.0f, m_MotionData.Speed*k_RayScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + transform.forward*m_MotionData.Speed*k_RayScale, k_RayEndcap, Color.blue);
                }
            }
            
            if (m_DrawSmoothAcceleration)
            {
                if (m_UseDirection)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, m_MotionData.Direction, Color.green, 1.0f, m_MotionData.AccelerationMagnitude*k_RayScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + m_MotionData.Acceleration*k_RayScale, k_RayEndcap, Color.green);
                }
                else
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, transform.forward, Color.green, 1.0f, m_MotionData.AccelerationMagnitude*k_RayScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + transform.forward*m_MotionData.AccelerationMagnitude*k_RayScale, k_RayEndcap, Color.green);
                }
            }

            if (m_DrawAngularVelocity)
            {
                if (m_UseDirection && m_MotionData.AngularSpeed > k_MinAngularSpeed)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, m_MotionData.AngularAxis, Color.white, 1.0f, k_AngularAxisLength);
                    GizmoModule.instance.DrawWedge(m_ToTrack.position + m_MotionData.AngularAxis*k_AngularAxisLength, m_MotionData.AngularAxis, m_ToTrack.up, k_AngularWedgeSize, m_MotionData.AngularSpeed, Color.white);
                }

                if (!m_UseDirection)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, -transform.right, Color.white, 1.0f, k_AngularAxisLength);
                    GizmoModule.instance.DrawWedge(m_ToTrack.position - transform.right*k_AngularAxisLength, -transform.right, transform.up, k_AngularWedgeSize, m_MotionData.AngularSpeed, Color.white);
                }
            }
            // Also draw pure single-frame integration
            if (m_DrawDirectSpeed)
            {
                var directOffset = (m_ToTrack.position - m_LastPosition);
                var deltaDistance = directOffset.magnitude/Time.deltaTime;
                var directDirection = directOffset.normalized;

                if (m_UseDirection)
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, directDirection, Color.red, 1.0f, deltaDistance*k_RayScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + directDirection*deltaDistance*k_RayScale, k_RayEndcap, Color.red);
                }
                else
                {
                    GizmoModule.instance.DrawRay(m_ToTrack.position, transform.forward, Color.red, 1.0f, deltaDistance*k_RayScale);
                    GizmoModule.instance.DrawSphere(m_ToTrack.position + transform.forward*deltaDistance*k_RayScale, k_RayEndcap, Color.red);
                }
            }

            m_LastPosition = m_ToTrack.position;

	    }
    }
}
