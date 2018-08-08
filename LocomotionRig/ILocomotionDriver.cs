namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Describes how a locomotion driver's active movement should be handled if the Locomotion Rig needs to cancel it mid-motion
    /// </summary>
    public enum LocomotionClearMode
    {
        Cancel = 0,         // Undo any locomotion that has been applied from the running driver
        Stop,               // Stop locomotion wherever it is currently running
        Finish,             // Allow the locomotion animation to play to the end, but no additional motion can be queued up
        FinishImmediate,    // Skip to the end of desired locomotion
    }

    /// <summary>
    /// Classes that implement ILocomotionDriver are capable of requesting exclusive access to the 
    /// Locomotion Rig's controls.  This lets them do things like play a locomotion animation or transition.
    /// In return they must handle the situation where these animations need to be stopped in a variety of ways
    /// </summary>
    public interface ILocomotionDriver
    {
        /// <summary>
        /// If this locomotion driver has exclusive access and an external source wants to stop the locomotion
        /// now, that is handled here.
        /// </summary>
        /// <param name="clearMode">Where the locomotion rig is requesting the motion to stop at.</param>
        void Clear(LocomotionClearMode clearMode);
    }
}
