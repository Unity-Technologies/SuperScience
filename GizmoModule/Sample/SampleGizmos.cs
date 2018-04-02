using Unity.Labs.SuperScience;
using UnityEngine;

public class SampleGizmos : MonoBehaviour
{
    [SerializeField]
    Transform m_OtherHand;

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
