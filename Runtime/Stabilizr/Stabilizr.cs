using UnityEngine;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Example component using two-point object stabilization
    /// Stabilizes orientation to match previous orientation, or to a target point
    /// </summary>
    public class Stabilizr : MonoBehaviour
    {
        /// <summary>
        /// 4 degrees seems to be the sweet spot for stabilization range
        /// Anything higher seems laggy and anything lower makes motion feel quantized.
        /// </summary>
        const float k_AngleStabilization = 4.0f;
        const float k_90FPS = 1.0f/90.0f;

#pragma warning disable 649
        [SerializeField]
        [Tooltip("The transform to match position and orientation - ie. a tracke controller")]
        Transform m_FollowSource;

        [SerializeField]
        [Tooltip("The transform that contains the point to stabilize against - like the end of a broom for example")]
        Transform m_StabilizationPoint;

        [SerializeField]
        [Tooltip("When enabled, the object's previous orientation will be considered for stabilization")]
        bool m_UsePreviousOrientation = true;

        [SerializeField]
        [Tooltip("When enabled, the object's endpoint will be considered for stabilization")]
        bool m_UseEndPoint = true;
#pragma warning restore 649

        void LateUpdate ()
        {
            var targetPosition = m_FollowSource.position;
            var targetRotation = m_FollowSource.rotation;

            // Determine the angular difference between the current rotation and new 'follow' rotation
            // This is for maintaining a steady orientation for an object while moving the controller around
            var oldRotation = transform.rotation;
            var steadyAngleDif = 180.0f;
            if (m_UsePreviousOrientation)
            {
                steadyAngleDif = Quaternion.Angle(oldRotation, targetRotation);
            }

            // Determine the optimal orientation this object would have if it was keeping the endpoint stable
            // Then get the angular difference between that rotation and the new 'follow' rotation
            var toEndPoint = (m_StabilizationPoint.position - targetPosition).normalized;
            var endPointRotation = Quaternion.LookRotation(toEndPoint, transform.up);
            var endPointAngleDif = 180.0f;
            if (m_UseEndPoint)
            {
                endPointAngleDif = Quaternion.Angle(endPointRotation, targetRotation);
            }

            // Whichever angular difference is less is the one we stabilize against
            if (endPointAngleDif < steadyAngleDif)
            {
                var lerpFactor = CalculateStabilizedLerp(endPointAngleDif, Time.deltaTime);
                targetRotation = Quaternion.Slerp(endPointRotation, targetRotation, lerpFactor);
            }
            else
            {
                var lerpFactor = CalculateStabilizedLerp(steadyAngleDif, Time.deltaTime);
                targetRotation = Quaternion.Slerp(oldRotation, targetRotation, lerpFactor);
            }

            transform.rotation = targetRotation;
            transform.position = targetPosition;
        }

        float CalculateStabilizedLerp(float distance, float timeSlice)
        {
            // The original angle stabilization code just calculated distance/maxAngle
            // This feels great in VR but is frame-dependent on experiences running at 90fps
            //return Mathf.Clamp01(distance / k_AngleStabilization);

            // We can estimate a time-independent analog
            var originalLerp = (distance / k_AngleStabilization);
            if (originalLerp >= 1.0f)
            {
                return 1.0f;
            }
            if (originalLerp <= 0.0f)
            {
                return 0.0f;
            }

            // For fps higher than 90 fps, we scale this value
            // For fps lower than 90fps, we take advantage of the fact that each time this algorithm
            // runs with the same values, the remaining lerp distance squares itself
            // We estimate this up to 3 timeslices.  At that point the numbers just get too small to be useful
            // (and any VR experience running at 30 fps is going to be pretty rough, even with reprojection)
            var doubleFrameLerp = originalLerp - (originalLerp*originalLerp);
            var tripleFrameLerp = doubleFrameLerp * doubleFrameLerp;

            var firstSlice = Mathf.Clamp01(timeSlice / k_90FPS);
            var secondSlice = Mathf.Clamp01((timeSlice - k_90FPS) / k_90FPS);
            var thirdSlice = Mathf.Clamp01((timeSlice - (2.0f * k_90FPS)) / k_90FPS);

            return originalLerp*firstSlice + doubleFrameLerp*secondSlice + tripleFrameLerp*thirdSlice;
        }
    }
}
