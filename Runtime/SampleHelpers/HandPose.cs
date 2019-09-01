using System;
using UnityEngine;
using UnityEngine.XR;

namespace Unity.Labs.SuperScience.Sample
{
    /// <summary>
    /// Helper class to map device position and rotation to the gameobject holding this component
    /// Also exposes events for common hand events
    /// </summary>
    public class HandPose : MonoBehaviour
    {
#if UNITY_2019_1_OR_NEWER
        [SerializeField]
        bool m_LeftHand = true;

        /// <summary>
        /// Raw velocity value from the corresponding XR node
        /// </summary>
        public Vector3 Velocity { get; private set; }

        Action<bool> m_OnGrip;
        bool m_LastGrip = false;

        Action<bool> m_OnTrigger;
        bool m_LastTrigger = false;

        /// <summary>
        /// Adds a callback to occur when the given XR Node/hand grips
        /// </summary>
        public void AddGripAction(Action<bool> toAdd)
        {
            m_OnGrip += toAdd;
        }

        /// <summary>
        /// Removes an existing grip callback 
        /// </summary>
        public void RemoveGripAction(Action<bool> toRemove)
        {
            m_OnGrip -= toRemove;
        }

        /// <summary>
        /// Adds a callback to occur when the given XR Node/hand activates a trigger button
        /// </summary>
        public void AddTriggerAction(Action<bool> toAdd)
        {
            m_OnTrigger += toAdd;
        }

        /// <summary>
        /// Removes an existing trigger callback
        /// </summary>
        public void RemoveTriggerAction(Action<bool> toRemove)
        {
            m_OnTrigger -= toRemove;
        }

        /// <summary>
        /// Updates input state into the transform of this component and calls any available event functions
        /// </summary>
        void Update()
        {
            var targetNode = m_LeftHand ? XRNode.LeftHand : XRNode.RightHand;

            var device = InputDevices.GetDeviceAtXRNode(targetNode);
            if (!device.isValid)
            {
                return;
            }

            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.SetPositionAndRotation(position, rotation);
            }
            if (device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity))
            {
                Velocity = velocity;
            }

            DoInputEvent(device, CommonUsages.gripButton, ref m_LastGrip, m_OnGrip);
            DoInputEvent(device, CommonUsages.triggerButton, ref m_LastTrigger, m_OnTrigger);
        }

        void DoInputEvent(InputDevice device, InputFeatureUsage<bool> inputFeature, ref bool cachedValue, Action<bool> onInput)
        {
            if (onInput == null)
            {
                return;
            }

            if (device.TryGetFeatureValue(inputFeature, out bool currentInput))
            {
                if (currentInput && !cachedValue)
                {
                    onInput.Invoke(true);
                }
                if (!currentInput && cachedValue)
                {
                    onInput.Invoke(false);
                }
                cachedValue = currentInput;
            }
        }
#endif
    }
}
