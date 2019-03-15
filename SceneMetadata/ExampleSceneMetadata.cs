using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// A ScriptableObject containing simple metadata about a Scene.
    /// MetadataAssetModificationProcessor ensures that this metadata is created and updated along with the Scene.
    /// </summary>
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
