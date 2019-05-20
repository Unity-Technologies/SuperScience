using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class GizmoModule : MonoBehaviour
    {
        public static GizmoModule instance;

        public const float RayLength = 100f;
        const float k_RayWidth = 0.001f;

#pragma warning disable 649
        [SerializeField]
        [Tooltip("Default sphere mesh used for drawing gizmo spheres.")]
        Mesh m_SphereMesh;

        [SerializeField]
        [Tooltip("Default cube mesh used for drawing gizmo boxes and rays.")]
        Mesh m_CubeMesh;

        [SerializeField]
        [Tooltip("Default quad mesh used for drawing gizmo wedges.")]
        Mesh m_QuadMesh;

        [SerializeField]
        Material m_GizmoMaterial;

        [SerializeField]
        Material m_GizmoCutoffMaterial;
#pragma warning restore 649

        MaterialPropertyBlock m_GizmoProperties;

        public Material gizmoMaterial
        {
            get { return m_GizmoMaterial; }
        }

        public Material gizmoCutoffMaterial
        {
            get { return m_GizmoCutoffMaterial; }
        }

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
        ///  Draws a wedge for a single frame in all camera views
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="radius"></param>
        /// <param name="angle"></param>
        /// <param name="color"></param>
        public void DrawWedge(Vector3 position, Quaternion rotation, float radius, float angle, Color color)
        {
            m_GizmoProperties.SetColor("_Color", color);
            m_GizmoProperties.SetFloat("_Edge", 1.0f -  (angle / 360.0f));

            var wedgeMatrix = Matrix4x4.TRS(position, rotation, Vector3.one * radius);
            Graphics.DrawMesh(m_QuadMesh, wedgeMatrix, m_GizmoCutoffMaterial, 0, null, 0, m_GizmoProperties);
        }
    }
}
