#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    public class GizmoModule : MonoBehaviour
    {
        public static GizmoModule instance;

        public const float rayLength = 100f;
        const float k_RayWidth = 0.001f;

        readonly List<Renderer> m_Rays = new List<Renderer>();
        int m_RayCount;

        readonly List<Renderer> m_Spheres = new List<Renderer>();
        int m_SphereCount;

        readonly List<Renderer> m_Cubes = new List<Renderer>();
        int m_CubeCount;

        public Material gizmoMaterial
        {
            get { return m_GizmoMaterial; }
        }

        [SerializeField]
        Material m_GizmoMaterial;

        void Awake()
        {
            instance = this;
        }

        void LateUpdate()
        {
            for (var i = m_RayCount; i < m_Rays.Count; i++)
            {
                m_Rays[i].gameObject.SetActive(false);
            }

            for (var i = m_SphereCount; i < m_Spheres.Count; i++)
            {
                m_Spheres[i].gameObject.SetActive(false);
            }

            for (var i = m_CubeCount; i < m_Cubes.Count; i++)
            {
                m_Cubes[i].gameObject.SetActive(false);
            }

            m_SphereCount = 0;
            m_RayCount = 0;
            m_CubeCount = 0;
        }

        public void DrawRay(Vector3 origin, Vector3 direction, Color color, float viewerScale = 1f, float rayLength = rayLength)
        {
            Renderer ray;
            if (m_Rays.Count > m_RayCount)
            {
                ray = m_Rays[m_RayCount];
            }
            else
            {
                ray = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Renderer>();
                Destroy(ray.GetComponent<Collider>());
                ray.transform.parent = transform;
                ray.sharedMaterial = Instantiate(m_GizmoMaterial);
                m_Rays.Add(ray);
            }

            ray.gameObject.SetActive(true);
            ray.sharedMaterial.color = color;
            var rayTransform = ray.transform;
            var rayWidth = k_RayWidth * viewerScale;
            rayTransform.localScale = new Vector3(rayWidth, rayWidth, rayLength);
            direction.Normalize();
            rayTransform.position = origin + direction * rayLength * 0.5f;
            rayTransform.rotation = Quaternion.LookRotation(direction);

            m_RayCount++;
        }

        public void DrawSphere(Vector3 center, float radius, Color color)
        {
            Renderer sphere;
            if (m_Spheres.Count > m_SphereCount)
            {
                sphere = m_Spheres[m_SphereCount];
            }
            else
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<Renderer>();
                Destroy(sphere.GetComponent<Collider>());
                sphere.transform.parent = transform;
                sphere.sharedMaterial = Instantiate(m_GizmoMaterial);
                m_Spheres.Add(sphere);
            }

            sphere.gameObject.SetActive(true);
            sphere.sharedMaterial.color = color;
            var sphereTransform = sphere.transform;
            sphereTransform.localScale = Vector3.one * radius;
            sphereTransform.position = center;

            m_SphereCount++;
        }

        public void DrawCube(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            Renderer cube;
            if (m_Cubes.Count > m_CubeCount)
            {
                cube = m_Cubes[m_CubeCount];
            }
            else
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Renderer>();
                Destroy(cube.GetComponent<Collider>());
                cube.transform.parent = transform;
                cube.sharedMaterial = Instantiate(m_GizmoMaterial);
                m_Cubes.Add(cube);
            }

            cube.gameObject.SetActive(true);
            cube.sharedMaterial.color = color;
            var cubeTransform = cube.transform;
            cubeTransform.localScale = scale;
            cubeTransform.position = position;
            cubeTransform.rotation = rotation;

            m_CubeCount++;
        }

        void OnDestroy()
        {
            foreach (var ray in m_Rays)
            {
                Destroy(ray.GetComponent<Renderer>().sharedMaterial);
            }

            foreach (var sphere in m_Spheres)
            {
                Destroy(sphere.GetComponent<Renderer>().sharedMaterial);
            }
        }

        void Destroy(UnityObject o, float t = 0f, bool withUndo = false)
        {
            if (Application.isPlaying)
            {
                UnityObject.Destroy(o, t);
            }
#if UNITY_EDITOR
            else
            {
                if (Mathf.Approximately(t, 0f))
                {
                    if (withUndo)
                        Undo.DestroyObjectImmediate(o);
                    else
                        DestroyImmediate(o);
                }
                else
                {
                    StartCoroutine(DestroyInSeconds(o, t));
                }
            }
#endif
        }

        static IEnumerator DestroyInSeconds(UnityObject o, float t, bool withUndo = false)
        {
            var startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup <= startTime + t)
                yield return null;

            if (withUndo)
                Undo.DestroyObjectImmediate(o);
            else
                DestroyImmediate(o);
        }
    }
}
#endif
