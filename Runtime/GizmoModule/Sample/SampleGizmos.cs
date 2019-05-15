using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR;

namespace Unity.Labs.SuperScience
{
    public class SampleGizmos : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        Transform m_OtherHand;
#pragma warning restore 649

        void Start()
        {
            if (XRDevice.isPresent)
            {
                GetComponent<TrackedPoseDriver>().enabled = true;
                m_OtherHand.GetComponent<TrackedPoseDriver>().enabled = true;
            }
        }

        void Update()
        {
            var position = transform.position;
            var otherPosition = m_OtherHand.position;
            GizmoModule.instance.DrawSphere(position, 0.05f, Color.red);
            GizmoModule.instance.DrawSphere(otherPosition, 0.05f, Color.red);
            var handToHand = otherPosition - position;
            GizmoModule.instance.DrawRay(position, handToHand, Color.green, 1f, handToHand.magnitude);
        }
    }
}
