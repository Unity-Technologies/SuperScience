using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    [Serializable]
    public class ExampleMetadata
    {
        static readonly List<GameObject> k_RootGameObjects = new List<GameObject>();
        static readonly List<Component> k_Components = new List<Component>();
        static readonly List<Renderer> k_Renderers = new List<Renderer>();

        public string sceneName;
        public string sceneGUID;
        public int totalComponents;
        public Bounds totalRendererBounds;

        public ExampleMetadata(string sceneName, string sceneGUID)
        {
            this.sceneName = sceneName;
            this.sceneGUID = sceneGUID;
        }

        public void UpdateFromScene(Scene scene)
        {
            scene.GetRootGameObjects(k_RootGameObjects);
            totalComponents = 0;
            k_Renderers.Clear();
            foreach (var gameObject in k_RootGameObjects)
            {
                gameObject.GetComponentsInChildren(k_Components);
                totalComponents += k_Components.Count;
                k_Renderers.AddRange(gameObject.GetComponentsInChildren<Renderer>());
            }

            if (k_Renderers.Count > 0)
            {
                totalRendererBounds = new Bounds(k_Renderers[0].transform.position, Vector3.zero);
                foreach (var renderer in k_Renderers)
                {
                    if (renderer.bounds.size != Vector3.zero)
                        totalRendererBounds.Encapsulate(renderer.bounds);
                }
            }
            else
            {
                totalRendererBounds = default(Bounds);
            }
        }
    }
}
