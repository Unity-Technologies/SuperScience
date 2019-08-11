using UnityEngine;
using UnityEngine.XR;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Helper class to map device position and rotation to the gameobject holding this component
    /// </summary>
    public class HandPose : MonoBehaviour
    {
        [SerializeField]
        bool m_LeftHand = true;

        void Update()
        {
            var targetNode = m_LeftHand ? XRNode.LeftHand : XRNode.RightHand;

            var node = InputDevices.GetDeviceAtXRNode(targetNode);
            if (!node.isValid)
            {
                return;
            }

            if (node.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
                node.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}