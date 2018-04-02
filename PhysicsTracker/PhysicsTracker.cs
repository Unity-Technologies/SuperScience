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

        /// <summary>
        /// The time period stored within a simple step sample
        /// </summary>
        const float k_ChunkPeriod = k_Period/k_Steps;

        /// <summary>
        /// Weight to use for the most recent physics sample, when doing prediction
        /// </summary>
        const float k_NewChunkWeight = 2.0f;
        
        /// <summary>
        /// Stores one sample of physics data
        /// Time is used to measure how much more frame samples should be accumulated here
        /// </summary>
        struct Chunk
        {
            float time;
            float distance;
            float angle;
        }

        int m_ChunkIndex = 0;
        Chunk[] m_Chunks = new Chunk[k_Steps];

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
        
        // public Vector3 currentVelocity = current direction * speed

        // Angular Motion
        //public Vector3 currentAngularVelocity = rotationAxis * currentSpeed * deg2rad
        
        public void Reset(Vector3 currentPosition, Quaternion currentRotation, 
                            Vector3 currentVelocity, Vector3 currentAngularVelocity)
        {
            Velocity = currentVelocity;
            AngularVelocity = currentAngularVelocity;
            m_ChunkIndex = 0;
        }
        // Initializes this tracker to a safe starting point    
        public void Initialize(Vector3 startPoint, Quaternion startRotation)
        {
            // Since this is not a monobehavior we don't have OnValidate - which means we need to do input validation here
            period = Mathf.Max(period, .01f);
            steps = Mathf.Max(steps, 1);

            m_ChunkPeriod = period / steps;

            m_DistanceChunks = new float[steps];
            m_AngleChunks = new float[steps];
            m_TimeChunks = new float[steps];

            ForceChangePosition(startPoint);
            ForceChangeRotation(startRotation);

            m_Initialized = true;
        }

        public void Update(Vector3 newPosition, Quaternion newRotation, float timeSlice)
        {
            if (!m_Initialized)
                Initialize(newPosition, newRotation);

            // Update the stored time and distance values
            if (m_TimeChunks[m_ChunkIndex] > m_ChunkPeriod)
            {
                m_ChunkIndex = (m_ChunkIndex + 1) % steps;
                m_DistanceChunks[m_ChunkIndex] = 0;
                m_AngleChunks[m_ChunkIndex] = 0;
                m_TimeChunks[m_ChunkIndex] = 0;
            }
            m_TimeChunks[m_ChunkIndex] += timeSlice;

            // Update positions and distance value
            var currentOffset = newPosition - m_LastPosition;
            m_CurrentDirection = currentOffset.normalized;
            var newDistance = currentOffset.magnitude;
            m_LastPosition = newPosition;
            m_DistanceChunks[m_ChunkIndex] += newDistance;

            // Update angles and rotation value
            var rotationOffset = newRotation * Quaternion.Inverse(m_LastRotation);
            float currentAngle;
            rotationOffset.ToAngleAxis(out currentAngle, out m_CurrentRotationAxis);
            m_LastRotation = newRotation;
            m_AngleChunks[m_ChunkIndex] += currentAngle;

            // Update the average velocity
            var totalDistance = 0.0f;
            var totalRotation = 0.0f;
            var totalTime = 0.0f;
            var chunkCounter = 0;
            while (chunkCounter < steps)
            {
                if (chunkCounter == m_ChunkIndex)
                {
                    totalDistance += m_DistanceChunks[chunkCounter] * newChunkWeight;
                    totalRotation += m_AngleChunks[chunkCounter] * newChunkWeight;
                    totalTime += m_TimeChunks[chunkCounter] * newChunkWeight;
                }
                else
                {
                    totalDistance += m_DistanceChunks[chunkCounter];
                    totalRotation += m_AngleChunks[chunkCounter];
                    totalTime += m_TimeChunks[chunkCounter];
                }
                chunkCounter++;
            }

            if (totalTime > 0.0f)
            {
                currentSpeed = totalDistance / totalTime;
                m_CurrentRotationSpeed = totalRotation / totalTime;
            }
        }

        /// <summary>
        /// Clear the buffer and set a new position
        /// </summary>
        /// <param name="newPosition">The new position</param>
        public void ForceChangePosition(Vector3 newPosition)
        {
            m_LastPosition = newPosition;
            ClearChunks();
        }

        /// <summary>
        /// Clear the buffer and set a new rotation
        /// </summary>
        /// <param name="newRotation">The new rotation</param>
        public void ForceChangeRotation(Quaternion newRotation)
        {
            m_LastRotation = newRotation;
            ClearChunks();
        }

        void ClearChunks()
        {
            Array.Clear(m_DistanceChunks, 0, m_DistanceChunks.Length);
            Array.Clear(m_AngleChunks, 0, m_AngleChunks.Length);
            Array.Clear(m_TimeChunks, 0, m_TimeChunks.Length);
        }
    }
}
