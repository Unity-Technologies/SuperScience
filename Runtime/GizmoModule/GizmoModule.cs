#if INCLUDE_MODULE_LOADER
using Unity.XRTools.ModuleLoader;
#endif

using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class GizmoModule : MonoBehaviour
#if INCLUDE_MODULE_LOADER
    , IModule
#endif
    {
        public static GizmoModule instance;

        public const float RayLength = 100f;
        const float k_RayWidth = 0.001f;

        static readonly int k_Color = Shader.PropertyToID("_Color");
        static readonly int k_Edge = Shader.PropertyToID("_Edge");

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

        public Material gizmoMaterial => m_GizmoMaterial;

        public Material gizmoCutoffMaterial => m_GizmoCutoffMaterial;

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
            m_GizmoProperties.SetColor(k_Color, color);

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
            m_GizmoProperties.SetColor(k_Color, color);

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
            m_GizmoProperties.SetColor(k_Color, color);

            var cubeMatrix = Matrix4x4.TRS(position, rotation, scale);
            Graphics.DrawMesh(m_CubeMesh, cubeMatrix, m_GizmoMaterial, 0, null, 0, m_GizmoProperties);
        }

        /// <summary>
        ///  Draws a wedge for a single frame in all camera views
        /// </summary>
        /// <param name="position">The position where the wedge should be drawn.</param>
        /// <param name="rotation">The rotation at which the wedge should be drawn.</param>
        /// <param name="radius">The radius of the wedge.</param>
        /// <param name="angle">The angle of the wedge "slice."</param>
        /// <param name="color">The color of the wedge</param>
        public void DrawWedge(Vector3 position, Quaternion rotation, float radius, float angle, Color color)
        {
            m_GizmoProperties.SetColor(k_Color, color);
            m_GizmoProperties.SetFloat(k_Edge, 1.0f -  angle / 360.0f);

            var wedgeMatrix = Matrix4x4.TRS(position, rotation, Vector3.one * radius);
            Graphics.DrawMesh(m_QuadMesh, wedgeMatrix, m_GizmoCutoffMaterial, 0, null, 0, m_GizmoProperties);
        }

#if INCLUDE_MODULE_LOADER
        void IModule.LoadModule()
        {
            instance = this;
        }

        void IModule.UnloadModule() { }
#endif
    }
}
