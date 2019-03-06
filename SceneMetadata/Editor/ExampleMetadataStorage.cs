using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    public class ExampleMetadataStorage : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        List<ExampleMetadata> m_MetadataList = new List<ExampleMetadata>();

        Dictionary<string, ExampleMetadata> m_MetadataByGUID = new Dictionary<string, ExampleMetadata>();

        public void UpdateSceneMetadata(Scene scene)
        {
            var sceneGUID = AssetDatabase.AssetPathToGUID(scene.path);
            ExampleMetadata metadata;
            if (!m_MetadataByGUID.TryGetValue(sceneGUID, out metadata))
            {
                metadata = new ExampleMetadata(scene.name, sceneGUID);
                m_MetadataByGUID[sceneGUID] = metadata;
            }

            metadata.UpdateFromScene(scene);
        }

        public void RemoveSceneMetadata(string scenePath)
        {
            var sceneGUID = AssetDatabase.AssetPathToGUID(scenePath);
            m_MetadataByGUID.Remove(sceneGUID);
        }

        public void OnBeforeSerialize()
        {
            m_MetadataList.Clear();
            m_MetadataList.AddRange(m_MetadataByGUID.Values);
        }

        public void OnAfterDeserialize()
        {
            m_MetadataByGUID.Clear();
            foreach (var metadata in m_MetadataList)
            {
                m_MetadataByGUID[metadata.sceneGUID] = metadata;
            }
        }
    }
}
