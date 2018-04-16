#if UNITY_EDITOR
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class GizmoModule : MonoBehaviour
    {
        public static GizmoModule instance;

        public const float RayLength = 100f;
        const float k_RayWidth = 0.001f;
        const int k_MaxWedgePoints = 16;

        [SerializeField]
        [Tooltip("Default sphere mesh used for drawing gizmo spheres.")]
        Mesh m_SphereMesh;

        [SerializeField]
        [Tooltip("Default cube mesh used for drawing gizmo boxes and rays.")]
        Mesh m_CubeMesh;

        MaterialPropertyBlock m_GizmoProperties;
        
        public Material gizmoMaterial
        {
            get { return m_GizmoMaterial; }
        }

        [SerializeField]
        Material m_GizmoMaterial;

        void Awake()
        {
            instance = this;
            m_GizmoProperties = new MaterialPropertyBlock();
        }
        
        /// <summary>
        /// Draws a ray for a single frame in all camera views
        /// </summary>
        /// <param name="origin">Where the ray should begin, in world space</param>
        /// <param name="direction">Which direction the ray should point, in world space</param>
        /// <param name="color">What color to draw the ray with</param>
        /// <param name="viewerScale">Optional global scale to apply to match a scaled user</param>
        /// <param name="rayLength">How long the ray should extend</param>
        public void DrawRay(Vector3 origin, Vector3 direction, Color color, float viewerScale = 1f, float rayLength = RayLength)
        {
            if (direction == Vector3.zero)
                return;

            direction.Normalize();
            m_GizmoProperties.SetColor("_Color", color);
            
            var rayPosition = origin + direction * rayLength * 0.5f;
            var rayRotation = Quaternion.LookRotation(direction);
            var rayWidth = k_RayWidth * viewerScale;
            var rayScale = new Vector3(rayWidth, rayWidth, rayLength);
            var rayMatrix = Matrix4x4.TRS(rayPosition, rayRotation, rayScale);

            Graphics.DrawMesh(m_CubeMesh, rayMatrix, m_GizmoMaterial, 0, null, 0, m_GizmoProperties);
        }

        /// <summary>
        /// Draws a sphere for a single frame in all camera views
        /// </summary>
        /// <param name="center">The center of the sphere, in world space</param>
        /// <param name="radius">The radius of the sphere, in meters</param>
        /// <param name="color">What color to draw the sphere with</param>
        public void DrawSphere(Vector3 center, float radius, Color color)
        {
            m_GizmoProperties.SetColor("_Color", color);

            var sphereMatrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * radius);
            Graphics.DrawMesh(m_SphereMesh, sphereMatrix, m_GizmoMaterial, 0, null, 0, m_GizmoProperties);
        }

        /// <summary>
        /// Draws a cube for a single frame in all camera views
        /// </summary>
        /// <param name="position">The center of the cube, in world space</param>
        /// <param name="rotation">The orientation of the cube, in world space</param>
        /// <param name="scale">The scale of the cube</param>
        /// <param name="color">What color to draw the cube with</param>
        public void DrawCube(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            m_GizmoProperties.SetColor("_Color", color);

            var cubeMatrix = Matrix4x4.TRS(position, rotation, scale);
            Graphics.DrawMesh(m_CubeMesh, cubeMatrix, m_GizmoMaterial, 0, null, 0, m_GizmoProperties);
        }

        /// <summary>
        /// Draws a wedge for a single frame in all camera views
        /// </summary>
        /// <param name="center">The center of the wedge, in world space</param>
        /// <param name="forward">The forward axis to draw the wedge around</param>
        /// <param name="up">The starting direction of the wedge endpoint</param>
        /// <param name="radius">How big to make the wedge</param>
        /// <param name="angle">How big of an arc the wedge should cover</param>
        /// <param name="color">What color to draw the wedge with</param>
        /// <param name="viewerScale">Optional global scale to apply to match a scaled user</param>
        public void DrawWedge(Vector3 center, Vector3 forward, Vector3 up, float radius, float angle, Color color, float viewerScale = 1f)
        {
            if (forward == Vector3.zero || up == Vector3.zero)
                return;

            angle = Mathf.Min(Mathf.Abs(angle), 360.0f);

            forward.Normalize();
            up.Normalize();
            m_GizmoProperties.SetColor("_Color", color);

            // Draw a ray from the origin to the start of the wedge
            var rayOrigin = center + up * radius * 0.5f;
            var rayWidth = k_RayWidth * viewerScale;
            var rayScale = new Vector3(rayWidth, rayWidth, radius);
            var rayRotation = Quaternion.LookRotation(up);
            var rayMatrix = Matrix4x4.TRS(rayOrigin, rayRotation, rayScale);

            Graphics.DrawMesh(m_CubeMesh, rayMatrix, m_GizmoMaterial, 0, null, 0, m_GizmoProperties);

            // Draw a ray from the origin to the end of the wedge
            var endRotation = Quaternion.AngleAxis(angle, forward);
            rayOrigin = center + endRotation * up  * radius * 0.5f;
            rayRotation = endRotation * rayRotation;
            rayMatrix = Matrix4x4.TRS(rayOrigin, rayRotation, rayScale);
            Graphics.DrawMesh(m_CubeMesh, rayMatrix, m_GizmoMaterial, 0, null, 0, m_GizmoProperties);

            // Draw an arc from the start to the end of the wedge
            var arcPoints = k_MaxWedgePoints * Mathf.CeilToInt(angle / 360.0f);

        }
    }
}
#endif
