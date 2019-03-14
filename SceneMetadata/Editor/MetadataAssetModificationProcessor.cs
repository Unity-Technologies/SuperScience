using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    class MetadataAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        const string k_ExampleScenePath = "SuperScience/SceneMetadata/ExampleScene.unity";
        const string k_MetadataSuffix = "Metadata.asset";

        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var path in paths)
            {
                if (path.EndsWith(k_ExampleScenePath))
                {
                    var metadataPath = path.Substring(0, path.Length - 6) + k_MetadataSuffix;
                    var metadata = AssetDatabase.LoadAssetAtPath<ExampleSceneMetadata>(metadataPath);
                    if (metadata == null)
                    {
                        // Create metadata if it doesn't exist
                        metadata = ScriptableObject.CreateInstance<ExampleSceneMetadata>();
                        AssetDatabase.CreateAsset(metadata, metadataPath);
                    }

                    var scene = SceneManager.GetSceneByPath(path);
                    metadata.UpdateFromScene(scene);

                    // To make sure the metadata Asset gets saved, we need to both dirty the Asset and
                    // include its path in the string array returned from OnWillSaveAssets.
                    EditorUtility.SetDirty(metadata);
                    var pathsToSave = new string[paths.Length + 1];
                    paths.CopyTo(pathsToSave, 0);
                    pathsToSave[pathsToSave.Length - 1] = metadataPath;
                    return pathsToSave;
                }
            }

            return paths;
        }
    }
}
