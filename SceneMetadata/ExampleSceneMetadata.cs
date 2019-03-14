using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    public class ExampleSceneMetadata : ScriptableObject
    {
        [SerializeField]
        int m_TotalComponents;

        static readonly List<GameObject> k_RootGameObjects = new List<GameObject>();
        static readonly List<Component> k_Components = new List<Component>();

        public void UpdateFromScene(Scene scene)
        {
            m_TotalComponents = 0;
            scene.GetRootGameObjects(k_RootGameObjects);
            foreach (var gameObject in k_RootGameObjects)
            {
                gameObject.GetComponentsInChildren(k_Components);
                m_TotalComponents += k_Components.Count;
            }
        }
    }
}
