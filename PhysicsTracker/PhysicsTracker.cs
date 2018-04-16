using System;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Object that can estimate a smoothed velocity and acceleration (linear and angular)
    /// from discrete pose (position and rotation) values
    /// </summary>
    [System.Serializable]
    public class PhysicsTracker
    {
        /// <summary>
        /// The time period that the physics values are averaged over
        /// </summary>
        const float k_Period = 0.125f;
        
        /// <summary>
        /// The number of discrete steps to store physics samples in
        /// </summary>
        const int k_Steps = 4;

        /// <summary>
        /// The time period stored within a single step sample
        /// </summary>
        const float k_SamplePeriod = k_Period/k_Steps;

        /// <summary>
        /// Weight to use for the most recent physics sample, when doing prediction
        /// </summary>
        const float k_NewSampleWeight = 2.0f;
        const float k_AdditiveWeight = k_NewSampleWeight - 1.0f;

        /// <summary>
        /// If we are doing prediction, the time period we average over is stretched out
        /// to simulate having more data than we've actually recorded
        /// </summary>
        const float k_PredictedPeriod = k_Period + k_SamplePeriod*k_AdditiveWeight;

        /// <summary>
        /// We need to keep one extra sample in our sample buffer to have a smooth transition
        /// when dropping one sample for another
        /// </summary>
        const int k_SampleLength = k_Steps + 1;

        /// <summary>
        /// Stores one sample of offset data, for calculating smooth speeds
        /// </summary>
        struct OffsetSample
        {
            public float distance;
            public float angle;
            public Vector3 offset;
            public Vector3 axisOffset;

            /// <summary>
            /// Helper function used to combine all the tracked samples up
            /// </summary>
            /// <param name="other">A sample to combine with</param>
            /// <param name="scalar">How much to scale the other sample's values</param>
            /// <param name="directionAnchor">A direction that informs which way the offset values should be pointing</param>
            /// <param name="axisAnchor">A direction that informs which way the axis values should be pointing</param>
            public void Accumulate(OffsetSample other, float scalar, Vector3 directionAnchor, Vector3 axisAnchor)
            {
                distance += other.distance * scalar;
                angle += other.angle * scalar;
                offset += other.offset * Vector3.Dot(directionAnchor, other.offset);
                axisOffset += other.axisOffset * Vector3.Dot(axisAnchor, other.axisOffset);
            }
        }

        /// <summary>
        /// Stores one sample of speed data, for calculating smooth acceleration
        /// </summary>
        struct SpeedSample
        {
            public float speed;
            public float angularSpeed;
        }

        // We store the time elapsed in the current sample, so we can determine
        // when enough data has been accumulated, and how much to fade out the 
        // last sample in the list for smooth transitions
        float m_SampleTime = 0.0f;
        OffsetSample[] m_Samples = new OffsetSample[k_SampleLength];
        SpeedSample[] m_Speeds = new SpeedSample[k_SampleLength];

        // Previous-frame history for integrating velocity
        Vector3 m_LastPosition = Vector3.zero;
        Quaternion m_LastRotation = Quaternion.identity;

        // Output data
        public float Speed { get; set; }
        public float AccelerationMagnitude { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 Acceleration { get; set; }

        public float AngularSpeed { get; set; }
        public Vector3 AngularAxis { get; set; }
        public Vector3 AngularVelocity { get; set; }
        public float AngularAccelerationMagnitude { get; set; }
        public Vector3 AngularAcceleration { get; set; }

        /// <summary>
        /// Sets the PhysicsTracker to a 'known' linear state
        /// </summary>
        /// <param name="currentPosition">The position in space the PhysicsTracker should start sampling from</param>
        /// <param name="currentRotation">The rotation in space the PhysicsTracker should start sampling from</param>
        /// <param name="currentVelocity">Any predefined motion the PhysicsTracker should take into account for smoothing</param>
        /// <param name="currentAngularVelocity">Any predefined rotation the PhysicsTracker should take into account for smoothing</param>
        public void Reset(Vector3 currentPosition, Quaternion currentRotation, Vector3 currentVelocity, Vector3 currentAngularVelocity)
        {
            // Reset history values
            m_LastPosition = currentPosition;
            m_LastRotation = currentRotation;

            // Then get new 'current' values based on this new history
            Speed = currentVelocity.magnitude;
            Direction = currentVelocity.normalized;
            Velocity = currentVelocity;
            AccelerationMagnitude = 0.0f;
            Acceleration = Vector3.zero;

            AngularSpeed = currentAngularVelocity.magnitude * Mathf.Rad2Deg;
            AngularAxis = currentAngularVelocity.normalized;
            AngularVelocity = currentAngularVelocity;

            var constantOffset = currentVelocity * k_SamplePeriod;
            var constantAxis = AngularAxis * k_SamplePeriod;
            var constantDistance = Speed * k_SamplePeriod;
            var constantAngle = AngularSpeed * k_SamplePeriod;

            // Reset the chunks to match this history as well
            for (var i = 0; i < k_Steps; i++)
            {
                m_Samples[i] = new OffsetSample { angle = constantAngle, distance = constantDistance, offset = constantOffset, axisOffset = constantAxis };
                m_Speeds[i] = new SpeedSample { speed = Speed, angularSpeed = AngularSpeed };
            }

            // The last sample is reset to 0, as it will pull in new values from our input sources
            m_SampleTime = 0.0f;
            m_Samples[k_Steps] = new OffsetSample { angle = 0, distance = 0 };
            m_Speeds[k_Steps] = new SpeedSample { speed = Speed, angularSpeed = AngularSpeed };
        }

        /// <summary>
        /// Takes in a new pose to determine new physics values
        /// </summary>
        /// <param name="newPosition">The up to date position of the physics tracker</param>
        /// <param name="newRotation">The up to date orientation of the physics tracker</param>
        /// <param name="timeSlice">How much time has passed since the last pose update</param>
        public void Update(Vector3 newPosition, Quaternion newRotation, float timeSlice)
        {
            if (timeSlice <= 0.0f)
            {
                return;
            }

            // First get single-frame offset data that we will then feed into our smoothing and prediction steps
            var currentOffset = newPosition - m_LastPosition;

            // We use different techniques that are well suited for direction and 'speed', and then recombine to velocity later
            var currentDistance = currentOffset.magnitude;
            var activeDirection = currentOffset.normalized;
            m_LastPosition = newPosition;

            // Update angular data in the same fashion
            var rotationOffset = newRotation * Quaternion.Inverse(m_LastRotation);
            float currentAngle;
            var activeAxis = Vector3.zero;
            rotationOffset.ToAngleAxis(out currentAngle, out activeAxis);
            m_LastRotation = newRotation;
        
            // We don't need to handle motion over a larger period of time than our full sampling period
            if (timeSlice > k_Period)
            {
                var timeSliceAdjustment = k_Period / timeSlice;
                timeSlice = k_Period;
                currentOffset *= timeSliceAdjustment;
                currentAngle *= timeSliceAdjustment;
            }

            // As new motion comes in, we need to free up space in our sampling buffer for make room for it
            // Annother option would be a circular array, but it makes the smoothing/prediction steps more expensive
            var shiftAmount = Mathf.FloorToInt((m_SampleTime + timeSlice) / k_SamplePeriod);
            var sampleIndex = k_Steps - shiftAmount;
            
            if (shiftAmount > 0)
            {
                var shiftIndex = shiftAmount;
                while (shiftIndex < k_SampleLength)
                {
                    m_Samples[shiftIndex - shiftAmount] = m_Samples[shiftIndex];
                    m_Speeds[shiftIndex - shiftAmount] = m_Speeds[shiftIndex];
                    shiftIndex++;
                }
            }

            // Fill up all the samples we freed up with our single-frame offset data
            var activeTimeSlice = timeSlice;
            while (activeTimeSlice > 0)
            {
                var timeToAdd = Mathf.Min(activeTimeSlice, k_SamplePeriod - m_SampleTime);
                var timePercent = (timeToAdd / timeSlice);
                activeTimeSlice -= timeToAdd;

                m_Samples[sampleIndex].distance += currentDistance * timePercent;
                m_Samples[sampleIndex].angle += currentAngle * timePercent;
                m_Samples[sampleIndex].offset += currentOffset * timePercent;
                m_Samples[sampleIndex].axisOffset += activeAxis * timePercent;

                // If we filled up the sample, prepare the next sample for writing
                if (activeTimeSlice > 0)
                {
                    sampleIndex++;
                    m_Samples[sampleIndex] = new OffsetSample();
                    m_Speeds[sampleIndex] = new SpeedSample { speed = Speed, angularSpeed = AngularSpeed };
                    m_SampleTime = 0;
                }
                else
                {
                    m_SampleTime += timeToAdd;
                }
            }

            // Generate new physics values
            // Speed and angle comes from an average of all the motion magnitude
            // Direction comes from the current frame offset, shifted by the total offset the tracker
            // has experienced over the sampling period.  The more perpendicular the motion is, the less impact it has

            // The oldest sample is faded out as the new data is coming in
            var edgeBlend = (m_SampleTime / k_SamplePeriod);
            var invEdgeBlend = 1.0f - edgeBlend;

            var comboSample = new OffsetSample();
            comboSample.Accumulate(m_Samples[0], invEdgeBlend, activeDirection, activeAxis);

            // Inner samples follow regular averaging rules
            for (var i = 1; i < k_Steps - 1; i++)
            {
                comboSample.Accumulate(m_Samples[i], 1.0f, activeDirection, activeAxis);
            }

            // The second newest sample loses its predictive weighting as the newest sample fills up
            // Thew newest sample is fully predictive
            comboSample.Accumulate(m_Samples[k_Steps - 1], 1.0f + (invEdgeBlend * k_AdditiveWeight), activeDirection, activeAxis);
            comboSample.Accumulate(m_Samples[k_Steps], k_NewSampleWeight, activeDirection, activeAxis);

            Speed = comboSample.distance / k_PredictedPeriod;
            Direction = comboSample.offset.normalized;
            Velocity = Direction * Speed;

            AngularSpeed = comboSample.angle / k_PredictedPeriod;
            AngularAxis = comboSample.axisOffset.normalized;
            AngularVelocity = AngularAxis * AngularSpeed * Mathf.Deg2Rad;

            // We compare the newest and oldest velocity samples to get the new acceleration
            var speedDelta = Mathf.Lerp(m_Speeds[k_Steps - 1].speed - m_Speeds[0].speed, m_Speeds[k_Steps].speed - m_Speeds[1].speed, edgeBlend);
            var angularSpeedDelta = Mathf.Lerp(m_Speeds[k_Steps - 1].angularSpeed - m_Speeds[0].angularSpeed, m_Speeds[k_Steps].angularSpeed - m_Speeds[1].angularSpeed, edgeBlend);

            AccelerationMagnitude = speedDelta / k_Period;
            Acceleration = AccelerationMagnitude * Direction;

            AngularAccelerationMagnitude = angularSpeedDelta / k_Period;
            AngularAcceleration = AngularAxis * AngularAccelerationMagnitude * Mathf.Deg2Rad;
        }
    }
}
