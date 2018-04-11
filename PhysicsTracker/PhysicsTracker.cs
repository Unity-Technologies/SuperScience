using System;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Object that can estimate a smoothed velocity and acceleration (linear and angular)
    /// from position alone, or from 'real' values from input or physics
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
        const int k_SampleLength = k_Steps + 1;
        /// <summary>
        /// The time period stored within a simple step sample
        /// </summary>
        const float k_SamplePeriod = k_Period/k_Steps;

        /// <summary>
        /// Weight to use for the most recent physics sample, when doing prediction
        /// </summary>
        const float k_NewSampleWeight = 2.0f;
        const float k_AdditiveWeight = k_NewSampleWeight - 1.0f;

        /// <summary>
        /// Stores one sample of physics data
        /// </summary>
        struct Sample
        {
            public float distance;
            public float angle;
        }

        // We store the time elapsed in the current sample, so we can determine
        // when enough data has been accumulated, and how much to fade out the 
        // last sample in the list for smooth transitions
        float m_SampleTime = 0.0f;
        Sample[] m_Samples = new Sample[k_Steps + 1];
        // We treat the sample list as a circular array because it makes
        // the algorithm both faster and simpler - we need to access all this data
        // all the time, and also shift out the old data as new data comes in
        int m_SampleIndex = 0;

        // Previous-frame history for integrating velocity
        Vector3 m_LastPosition = Vector3.zero;
        Quaternion m_LastRotation = Quaternion.identity;

        // Output data
        public float Speed { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 Velocity { get; set; }

        public float AngularSpeed { get; set; }
        public Vector3 AngularAxis { get; set; }
        public Vector3 AngularVelocity { get; set; }
 
        public void Reset(Vector3 currentPosition, Quaternion currentRotation, 
                            Vector3 currentVelocity, Vector3 currentAngularVelocity)
        {
            // Reset history values
            m_LastPosition = currentPosition;
            m_LastRotation = currentRotation;

            // Then get new 'current' values based on this new history
            Speed = currentVelocity.magnitude;
            Direction = currentVelocity.normalized;
            Velocity = currentVelocity;

            AngularSpeed = currentAngularVelocity.magnitude * Mathf.Rad2Deg;
            AngularAxis = currentAngularVelocity.normalized;
            AngularVelocity = currentAngularVelocity;
 
            var constantDistance = Speed * k_SamplePeriod;
            var constantAngle = AngularSpeed * k_SamplePeriod;

            // Reset the chunks to match this history as well
            for (var i = 0; i < k_Steps; i++)
            {
                m_Samples[i] = new Sample { angle = constantAngle, distance = constantDistance };
            }

            // The last sample is reset to 0, as it will pull in new values from our input sources
            m_SampleTime = 0.0f;
            m_Samples[k_Steps] = new Sample { angle = 0, distance = 0 };
            m_SampleIndex = k_Steps;
        }

        public void Update(Vector3 newPosition, Quaternion newRotation, float timeSlice)
        {
            if (timeSlice <= 0.0f)
            {
                return;
            }

            // Update linear data
            var currentOffset = newPosition - m_LastPosition;

            // Smoothed direction does not feel correct, so we calculate it from the most recent delta value
            Direction = currentOffset.normalized;
            var currentDistance = currentOffset.magnitude;
            m_LastPosition = newPosition;

            // Update angular data
            var rotationOffset = newRotation * Quaternion.Inverse(m_LastRotation);
            float currentAngle;
            var tempAxis = Vector3.zero;
            rotationOffset.ToAngleAxis(out currentAngle, out tempAxis);
            AngularAxis = tempAxis;
            m_LastRotation = newRotation;
        
            // If the time slice passed in is bigger than the amount we hold in our buffers,
            // we don't bother trying to smooth and do a reset instead
            if (timeSlice > k_Period)
            {
                var fullVelocityTime = 1.0f / timeSlice;
                var newVelocity = currentOffset * fullVelocityTime;
                var angularVelocity = AngularAxis * (currentAngle * fullVelocityTime);
                Reset(newPosition, newRotation, newVelocity, angularVelocity);
                return;
            }

            // Otherwise, fill up as many time samples as needed
            while (timeSlice > 0)
            {
                 var timeToAdd = Mathf.Min(timeSlice, k_SamplePeriod - m_SampleTime);
                timeSlice -= timeToAdd;
                var timePercent = (timeToAdd / timeSlice);

                m_Samples[m_SampleIndex].distance += currentDistance * timePercent;
                m_Samples[m_SampleIndex].angle += currentAngle * timePercent;

                // Did we fill up the sample?
                if (timeSlice > 0)
                {
                    // If so, throw out the oldest sample and store our data in that now instead
                    m_SampleIndex = (m_SampleIndex + 1) % k_SampleLength;
                    m_Samples[m_SampleIndex] = new Sample { angle = 0, distance = 0 };
                    m_SampleTime = 0;
                }
                else
                {
                    m_SampleTime += timeToAdd;
                }
            }

            // Generate the average sample
            // Accumulate the total magnitude of distance that has been moved over our sample period
            var activeIndex = (m_SampleIndex + 1) % k_SampleLength;
            var edgeBlend = (m_SampleTime / k_SamplePeriod);
            var invEdgeBlend = 1.0f - edgeBlend;

            // The last sample gets faded out as a new sample becomes available
            var totalDistance = m_Samples[activeIndex].distance * invEdgeBlend;
            var totalAngle = m_Samples[activeIndex].distance * invEdgeBlend;
            activeIndex = (activeIndex + 1) % k_SampleLength;

            // All intermediate samples get added in full
            for (var i = 1; i < (k_Steps - 1); i++)
            {
                totalDistance += m_Samples[activeIndex].distance;
                totalAngle += m_Samples[activeIndex].angle;

                activeIndex = (activeIndex + 1) % k_SampleLength;
            }
            // The second to last sample get scaled by predictive weighting
            totalDistance += m_Samples[activeIndex].distance * invEdgeBlend * k_AdditiveWeight;
            totalDistance += m_Samples[activeIndex].distance * invEdgeBlend * k_AdditiveWeight;
            activeIndex = (activeIndex + 1) % k_SampleLength;

            // The last sample gets fully predictve weighted
            totalDistance += m_Samples[activeIndex].distance * edgeBlend * k_NewSampleWeight;
            totalDistance += m_Samples[activeIndex].distance * edgeBlend * k_NewSampleWeight;
            
            Speed = totalDistance / k_Period;
            Velocity = Direction * Speed;

            AngularSpeed = totalAngle / k_Period;
            AngularVelocity = AngularAxis * AngularSpeed * Mathf.Deg2Rad;
        }
    }
}
