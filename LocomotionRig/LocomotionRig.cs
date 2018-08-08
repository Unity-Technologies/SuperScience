using System;
using UnityEngine;
using UnityEngine.XR;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// This class is the base layer of VR Locomotion.  Put this on your VR Rig - The Gameobject that contains your VR tracked camera and controllers.
    /// The LocomotionRig provides a few services
    /// 1. A centralized channel for locomotion techniques to work through, allowing multiple methods (that are *not* aware of one another)
    ///    to work together simulatenously.  This includes abilities for Locomotion techniques to 'lock' the rig if needed.
    /// 2. Callbacks for adjusting the motion before it is applied, and after it takes effect
    /// 3. Ways to define *every* type of motion, from simple position setting, to adjusting a user's scale with velocity, in a clear, easy manner.
    /// </summary>
    public class LocomotionRig : MonoBehaviour
    {
        const float k_MinimumScaleVelocity = 0.01f;
        const float k_MinimumLocomotionScale = 0.001f;

        /// <summary>
        /// We store what kind of transform data a given locomotion technique will result in rather than apply it right away.
        /// This allows it to be analyzed and modified by interested parties, before being applied.
        /// </summary>
        public class LocomotionResult
        {
            public Vector3 position;
            public Quaternion rotation;
            public float scale;
        }

        [SerializeField]
        [Tooltip("The transform that corresponds to the head center - this is usually the transform with the main VR camera.")]
        Transform m_HeadNode;

        /// <summary>
        /// Callback that occurs before locomotion is finalized - the LocomotionResult can be modified to alter it
        /// </summary>
        public Action<LocomotionUnit, LocomotionResult> PreLocomotion;

        /// <summary>
        /// Callback that occurs after locomotion is finished
        /// </summary>
        public Action<LocomotionUnit> OnLocomotion;

        /// <summary>
        /// How much scale this VR Rig is experiencing from locomotion 
        /// </summary>
        float m_ScaleFromLocomotion = 1.0f;

        /// <summary>
        /// The current 'owner' of the locomotion - if unset, locomotion commands from every source are obeyed
        /// </summary>
        ILocomotionDriver m_LocomotionOwner;

        /// <summary>
        /// Cached helper locomotion unit, used in the simple 'set position' function
        /// </summary>
        LocomotionUnit m_SimpleMovement = new LocomotionUnit();

        /// <summary>
        /// Cached Locomotion Result
        /// </summary>
        LocomotionResult m_LocomotionResult = new LocomotionResult();

        /// <summary>
        /// How much scale this VR Rig is experiencing from locomotion 
        /// </summary>
        public float ScaleFromLocomotion { get { return m_ScaleFromLocomotion; } }

        /// <summary>
        /// Ensures the head node of the rig is set - we need this to calculate many of the offset and rotation types
        /// </summary>
        void Awake()
        {
            if (m_HeadNode == null)
            {
                Debug.LogError("The locomotion rig requires a head node set before it can be used.");
                enabled = false;
            }
        }

        /// <summary>
        /// Simple/easy mode function to set the position of the VR Player
        /// </summary>
        /// <param name="position">A desired offset or destination for the VR Rig in-engine</param>
        /// <param name="mode">How this position value is aopplied.  By default it sets the approximated player position to (position)</param>
        /// <param name="blink">Is this a smooth motion or teleportation?</param>
        /// <returns>True if the locomotion was successfully performed, false if an ILocomotionDriver has the Rig locked</returns>
        public bool SetLocomotionPosition(Vector3 position, LocomotionUnit.Mode mode = LocomotionUnit.Mode.AbsolutePlayer, bool blink = false)
        {
            m_SimpleMovement.position = position;
            m_SimpleMovement.positionMode = mode;
            m_SimpleMovement.blink = blink;
            return SetLocomotionUnit(m_SimpleMovement);
        }

        /// <summary>
        /// Applies a set of locomotion operations to the user and their VR Tracked area
        /// </summary>
        /// <param name="movement">What motion to apply to the VR Rig</param>
        /// <param name="driver">Optional - Class that is requesting this motion.</param>
        /// <returns>True if the locomotion was successfully performed, false if another ILocomotionDriver has the Rig locked</returns>
        public bool SetLocomotionUnit(LocomotionUnit movement, ILocomotionDriver driver = null)
        {
            if (!LocomotionReady(driver))
            {
                return false;
            }

            // Pre-calculate the proper delta time for any velocity being applied
            float deltaTime = movement.deltaTime;

            // Reset the locomotion result as we are about to recalculate it
            // Any unset operations will just default to the existing transform
            m_LocomotionResult.position = transform.position;
            m_LocomotionResult.rotation = transform.rotation;
            m_LocomotionResult.scale = m_ScaleFromLocomotion;
            var appliedScale = 1.0f;

            // In room scale tracking we estimate the 'player' location by projecting their XZ coordinates to the 
            // base of the tracking area.  In Stationary scale we assume the tracking 'center' is the player location instead, because
            // they should not be moving around.
            if (XRDevice.GetTrackingSpaceType() == TrackingSpaceType.Stationary)
            {
                if (movement.positionMode == LocomotionUnit.Mode.AbsolutePlayer)
                {
                    movement.positionMode = LocomotionUnit.Mode.AbsoluteRoom;
                }
                if (movement.rotationMode == LocomotionUnit.Mode.AbsolutePlayer)
                {
                    movement.rotationMode = LocomotionUnit.Mode.AbsoluteRoom;
                }
                if (movement.scaleMode == LocomotionUnit.Mode.AbsolutePlayer)
                {
                    movement.scaleMode = LocomotionUnit.Mode.AbsoluteRoom;
                }
            }

            // To properly mix all the operations at once, we apply scale, then rotation, and finally translation
            if (movement.scaleMode != LocomotionUnit.Mode.None)
            {
                // Estimate 'player location' to scale around - we estimate that as the head position xz with the tracking root Y
                var scaleCenter = m_HeadNode.position;
                scaleCenter.y = transform.position.y;

                // For relative or velocity style scaling the user can override the scale center - 
                // when scaling about the user, HMD, or room a custom scaling center does not make sense.
                switch (movement.scaleMode)
                {
                    case LocomotionUnit.Mode.Relative:
                        if (movement.customScaleCenter)
                        {
                            scaleCenter = movement.scaleCenter;
                        }
                        m_LocomotionResult.scale *= movement.scale;
                        break;
                    case LocomotionUnit.Mode.Velocity:
                        if (movement.customScaleCenter)
                        {
                            scaleCenter = movement.scaleCenter;
                        }
                        // We don't bother adjusting our scale, if the velocity is so low it is not doing anything
                        if (Mathf.Abs(1.0f - movement.scale) > k_MinimumScaleVelocity)
                        {
                            // It is indeed possible to scale with a 'velocity'
                            // We pull out the scaling curve so we can scale properly over time in a way that feels linear
                            // Our scale velocity is always done with a 'power' curve.
                            // We calculate an initial value that makes the power curve map to the current scale
                            // and then shift that vlaue foward in time.
                            // Finally, we re-apply it to the power curve to get the properly time-adjusted scale value
                            var scaleDistance = Mathf.Log(m_LocomotionResult.scale, movement.scale);
                            scaleDistance += deltaTime;
                            m_LocomotionResult.scale = Mathf.Pow(movement.scale, scaleDistance);
                        }    
                        break;
                    case LocomotionUnit.Mode.AbsoluteRoom:
                        scaleCenter = transform.position;
                        m_LocomotionResult.scale = movement.scale;
                        break;
                    case LocomotionUnit.Mode.AbsolutePlayer:
                        m_LocomotionResult.scale = movement.scale;
                        break;
                    case LocomotionUnit.Mode.AbsoluteHMD:
                        scaleCenter = m_HeadNode.position;
                        m_LocomotionResult.scale = movement.scale;
                        break;
                }

                // Don't allow negative or 0 scales 
                m_LocomotionResult.scale = Mathf.Max(m_LocomotionResult.scale, k_MinimumLocomotionScale);

                // The scale we apply to our transformation data also undoes the previous locomotion scale
                appliedScale = (m_LocomotionResult.scale / m_ScaleFromLocomotion);

                // Adjust the final position based on the scale center
                m_LocomotionResult.position = m_LocomotionResult.position + (scaleCenter - transform.position)*(1.0f - appliedScale);
            }

            if (movement.rotationMode != LocomotionUnit.Mode.None)
            {
                switch (movement.rotationMode)
                {
                    case LocomotionUnit.Mode.Relative:
                        m_LocomotionResult.rotation *= movement.rotation;
                        break;
                    case LocomotionUnit.Mode.Velocity:
                        var target = m_LocomotionResult.rotation * movement.rotation;
                        var rotationTimeScale = deltaTime * movement.rotationVelocityScale;
                        m_LocomotionResult.rotation = Quaternion.SlerpUnclamped(m_LocomotionResult.rotation, target, rotationTimeScale);
                        break;
                    case LocomotionUnit.Mode.AbsoluteRoom:
                        m_LocomotionResult.rotation = movement.rotation;
                        break;
                    case LocomotionUnit.Mode.AbsolutePlayer:
                        // Get the forward rotation delta between the player and the room
                        // Apply the rotation to the room, then minus the delta
                        var hmdForward = XZForward(m_HeadNode);
                        var forwardAnglePlayer = Mathf.Atan2(hmdForward.z, hmdForward.x) * Mathf.Rad2Deg;
                        var forwardAngleRoom = Mathf.Atan2(transform.forward.z, transform.forward.x) * Mathf.Rad2Deg;
                        var angleDelta = ShortestAngleDistance(forwardAngleRoom, forwardAnglePlayer);

                        m_LocomotionResult.rotation = movement.rotation * Quaternion.Euler(0, -angleDelta, 0);
                        break;
                    case LocomotionUnit.Mode.AbsoluteHMD:
                        var deltaRotation = Quaternion.Inverse(m_LocomotionResult.rotation) * m_HeadNode.rotation;
                        m_LocomotionResult.rotation = movement.rotation * deltaRotation;
                        break;
                }
            }

            if (movement.positionMode != LocomotionUnit.Mode.None)
            {
                switch (movement.positionMode)
                {
                    case LocomotionUnit.Mode.Relative:
                        m_LocomotionResult.position += movement.position;
                        break;
                    case LocomotionUnit.Mode.Velocity:
                        m_LocomotionResult.position += (movement.position*deltaTime);
                        break;
                    case LocomotionUnit.Mode.AbsoluteRoom:
                        m_LocomotionResult.position = movement.position;
                        break;
                    case LocomotionUnit.Mode.AbsolutePlayer:
                    {
                        // We get the head's offset on the XZ plane to calculate where the player is relative to the play space
                        var roomToPlayer = (m_HeadNode.position - transform.position);
                        roomToPlayer.y = (transform.position.y - m_LocomotionResult.position.y);    // This handles the situation where we have scaled about an anchor point
                                                                                                    // We still want to move the head in relation to that virtual anchor point
                        m_LocomotionResult.position = movement.position - (roomToPlayer*appliedScale);
                        break;
                    }
                    case LocomotionUnit.Mode.AbsoluteHMD:
                    {
                        // In this case, we want to set the position of the head itself during a given locomotion operation.
                        // This means getting the offset from head to tracking center, and then applying the inverse of that along with the locomotion
                        var roomToHMD = (m_HeadNode.position - transform.position);
                        m_LocomotionResult.position = movement.position - (roomToHMD*appliedScale);
                        break;
                    }
                }
            }
  
            // Detect a change that is big enough to be noticable/worth applying
            var transformDelta = transform.position - m_LocomotionResult.position;
            var rotationDelta = Quaternion.Angle(transform.rotation, m_LocomotionResult.rotation);
            var scaleDelta = Mathf.Abs(1.0f - appliedScale);

            var maxChange = Mathf.Max(Mathf.Max(transformDelta.sqrMagnitude, scaleDelta), rotationDelta);
            if (maxChange < Mathf.Epsilon*3.0f)
            {
                // Locomotion was not blocked, so we still return true here, but don't activate the callbacks
                return true;
            }

            // Let other systems adjust the motion before we apply it
            if (PreLocomotion != null)
            {
                PreLocomotion(movement, m_LocomotionResult);
            }

            // Finally apply the motion to the transform
            transform.position = m_LocomotionResult.position;
            transform.rotation = m_LocomotionResult.rotation;
            appliedScale = (m_LocomotionResult.scale / m_ScaleFromLocomotion);

            transform.localScale *= appliedScale;
            m_ScaleFromLocomotion = m_LocomotionResult.scale;

            // Then let any interested systems know just what locomotion occurred
            if (OnLocomotion != null)
            {
                OnLocomotion(movement);
            }
            return true;
        }

        /// <summary>
        /// Fills a locomotion unit with data that will reset the tracking area to the state it is currently in
        /// </summary>
        /// <param name="movement">The LocomotionUnit that will get the reset data</param>
        public void GetLocomotionPropertyBlock(ref LocomotionUnit movement)
        {
            if (movement == null)
            {
                movement = new LocomotionUnit();
            }
            else
            {
                movement.Reset();
            }

            movement.position = transform.position;
            movement.positionMode = LocomotionUnit.Mode.AbsoluteRoom;

            movement.rotation = transform.rotation;
            movement.rotationMode =  LocomotionUnit.Mode.AbsoluteRoom;

            movement.scale = m_ScaleFromLocomotion;
            movement.scaleMode =  LocomotionUnit.Mode.AbsoluteRoom;

            movement.blink = true;
        }

        /// <summary>
        /// Stops any exclusive mode locomotion that is currently playing
        /// </summary>
        /// <param name="clearMode">How the currently playing locomotion should stop</param>
        public void Clear(LocomotionClearMode clearMode)
        {
            if (m_LocomotionOwner != null)
            {
                m_LocomotionOwner.Clear(clearMode);
            }
            if (clearMode != LocomotionClearMode.Finish)
            {
                ReleaseLocomotion(m_LocomotionOwner);
            }
        }

        /// <summary>
        /// Allows a locomotion system to see if it is allowed to apply locomotion to the VR Rig
        /// this takes system availability and locking into account
        /// </summary>
        /// <param name="driver">The locomotion system that wants to run</param>
        /// <returns>True if the locomotion system can apply changes, false if it is not available</returns>
        public bool LocomotionReady(ILocomotionDriver driver)
        {
            if (m_LocomotionOwner != null)
            {
                return m_LocomotionOwner == driver;
            }

            return isActiveAndEnabled;
        }

        /// <summary>
        /// Allows a locomotion system, if desired, to request 'exclusive access' to the VR Rig for
        /// locomotion purposes.  This should be used for short bursts, like a dash attack or teleport zip
        /// </summary>
        /// <param name="requester">The locomotion driver that wants exclusive control</param>
        /// <returns>True if the driver gained control, false if the system was already locked by something else or is currently disabled</returns>
        public bool LockLocomotion(ILocomotionDriver requester)
        {
            if (!LocomotionReady(requester))
            {
                return false;    
            }
            m_LocomotionOwner = requester;
            return true;
        }

        /// <summary>
        /// If a locomotion system has exclusive access, this will release that control
        /// </summary>
        /// <param name="releaser">The locomotion system that no longer needs exclusive access</param>
        public void ReleaseLocomotion(ILocomotionDriver releaser)
        {
            if (m_LocomotionOwner == releaser)
            {
                m_LocomotionOwner = null;
            }
        }

        /// <summary>
        /// Helper function that does a fairly accurate estimate of a body's facing direction on the XZ plane
        /// given their head's facing direction.
        /// Useful for general purpose aiming or motion hints.  Takes into account the player being upside-down
        /// or bending backwards.  The corresponding up vector to these results should always be (0,1,0)
        /// </summary>
        /// <param name="target">The transform we are calculating the planar forward from.</param>
        /// <returns>A 3d vector with no Y component that corresponds to a player forward direction</returns>
        public static Vector3 XZForward(Transform target)
        {
            var forward = target.forward;

            // If the transform is facing more up or down, use the up vector for more numerical data
            if ((forward.y*forward.y) >= 0.5f)
            {
                // This ensures the vector we use is always facing forward
                // Head tilted up gets us a backwards facing up vector, but set negative (so now faces forward), and has a positive y value
                // So that remains unchanged
                // Head tilted down gets us a forward facing up vector, set negative (so then facing backward), but the y value is negative, 
                // so it is once again flipped to positive
                forward = -target.up*Mathf.Sign(forward.y);
            }
            else
            {
                // The only time the up vector will be < 0 is limbo or looking between our legs, at least while wearing an HMD in an area with
                // gravity.  In those situations, we just flip the forward vector so the player is still facing forward
                if (target.up.y < 0.0f)
                {
                    forward = -forward;
                }
            }
            forward.y = 0.0f;
            return forward.normalized;
        }

        /// <summary>
        /// Returns the shortest angular distance between two angles
        /// </summary>
        /// <param name="startAngle">The angle we want to use as an anchor (degrees)</param>
        /// <param name="endAngle">The angle we will calculate the offset from (degrees)</param>
        /// <returns>The amount of degrees to add to startAngle to make it equivalent to endAngle.  This value will always be between -180 and 180.</returns>
        public static float ShortestAngleDistance(float startAngle, float endAngle)
        {
            var angleDelta = (endAngle - startAngle);
            var resultSign = Mathf.Sign(angleDelta);
            angleDelta = Mathf.Abs(angleDelta) % 360.0f;
            if (angleDelta > 180.0f)
            {
                angleDelta = -(360.0f - angleDelta);
            }
             return angleDelta * resultSign;
        }
    }
}
