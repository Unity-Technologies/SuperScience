using UnityEngine;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// The atomic unit of locomotion.  Allows full transformation of the Locomotion Rig in a single step.
    /// </summary>
    public class LocomotionUnit
    {
        /// <summary>
        /// Describes how a piece of locomotion data should be applied in reference to the world
        /// </summary>
        public enum Mode
        {
            None = 0,       // Do not apply this locomotion property
            Relative,       // Apply any locomotion properties as offsets
            Velocity,       // Apply as offsets, but scaled by delta time
            AbsoluteRoom,   // Set the room/tracking root's transformation
            AbsolutePlayer, // Set the player's transformation (and shift the room to match)
            AbsoluteHMD,    // Set the HMD's transformation directly (and shift the room to match).  You usually want AbsolutePlayer instead
        }

        /// <summary>
        /// Acts as either a target or offset to move the Locomotion Rig
        /// </summary>
        public Vector3 position;
        public Mode positionMode;

        /// <summary>
        /// Acts as either a target or offset to rotate the Locomotion Rig
        /// </summary>
        public Quaternion rotation;
        public Mode rotationMode;
        public float rotationVelocityScale;   // Helper to set rotational velocities greater than 180 degrees

        /// <summary>
        /// Acts as either a target or offset to uniform scale the Locomotion Rig
        /// </summary>
        public float scale;
        public Mode scaleMode;
        public bool customScaleCenter;
        public Vector3 scaleCenter;

        /// <summary>
        /// Is this part of smooth motion or a discrete change in position?
        /// </summary>
        public bool blink;

        /// <summary>
        /// The time step to use when locomoting with velocity.
        /// </summary>
        public float deltaTime;

        /// <summary>
        /// The property block to an uninitialized (no effect on locomotion) state
        /// </summary>
        public void Reset()
        {
            position = Vector3.zero;
            positionMode = Mode.None;
            rotation = Quaternion.identity;
            rotationMode = Mode.None;
            rotationVelocityScale = 1.0f;
            scale = 1.0f;
            scaleMode = Mode.None;
            customScaleCenter = false;
            scaleCenter = Vector3.zero;
            blink = false;
            deltaTime = Time.deltaTime;
        }
    }
}
