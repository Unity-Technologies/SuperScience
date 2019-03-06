using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    class MetadataAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        const string k_MetadataExampleLabel = "MetadataExample";
        const string k_MetadataStoragePath = "Assets/SuperScience/SceneMetadata/Editor/ExampleMetadataStorage.asset";

        static ExampleMetadataStorage s_MetadataStorageInstance;

        static string[] OnWillSaveAssets(string[] paths)
        {
            var saveMetadata = false;
            var metadataStorage = GetMetadataStorage();
            foreach (var path in paths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && IsMetadataScene(path))
                {
                    metadataStorage.UpdateSceneMetadata(scene);
                    saveMetadata = true;
                }
            }

            if (!saveMetadata)
                return paths;

            // To make sure the metadata storage Asset gets saved, we need to both dirty the Asset and
            // include its path in the string array returned from OnWillSaveAssets.
            EditorUtility.SetDirty(metadataStorage);
            var pathsToSave = new string[paths.Length + 1];
            paths.CopyTo(pathsToSave, 0);
            pathsToSave[pathsToSave.Length - 1] = k_MetadataStoragePath;
            return pathsToSave;
        }

        static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
        {
            if (IsMetadataScene(path))
            {
                var metadataStorage = GetMetadataStorage();
                metadataStorage.RemoveSceneMetadata(path);
                EditorUtility.SetDirty(metadataStorage);
            }

            return AssetDeleteResult.DidNotDelete;
        }

        static bool IsMetadataScene(string scenePath)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            var sceneLabels = AssetDatabase.GetLabels(sceneAsset);
            return sceneLabels.Contains(k_MetadataExampleLabel);
        }

        static ExampleMetadataStorage GetMetadataStorage()
        {
            if (s_MetadataStorageInstance == null)
            {
                s_MetadataStorageInstance = AssetDatabase.LoadAssetAtPath<ExampleMetadataStorage>(k_MetadataStoragePath);
                if (s_MetadataStorageInstance == null)
                {
                    s_MetadataStorageInstance = ScriptableObject.CreateInstance<ExampleMetadataStorage>();
                    AssetDatabase.CreateAsset(s_MetadataStorageInstance, k_MetadataStoragePath);
                }
            }

            return s_MetadataStorageInstance;
        }
    }
}
