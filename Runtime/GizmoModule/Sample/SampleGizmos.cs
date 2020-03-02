using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class SampleGizmos : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        Transform m_RightHand;
#pragma warning restore 649

        void Update()
        {
            var position = transform.position;
            var otherPosition = m_RightHand != null ? m_RightHand.position : Vector3.zero;

            GizmoModule.instance.DrawSphere(position, 0.05f, Color.red);
            GizmoModule.instance.DrawSphere(otherPosition, 0.05f, Color.red);
            var handToHand = otherPosition - position;
            GizmoModule.instance.DrawRay(position, handToHand, Color.green, 1f, handToHand.magnitude);
        }
    }
}
