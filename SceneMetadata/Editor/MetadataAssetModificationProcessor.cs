using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Hooks into saving of Assets to update and save metadata along with Scenes
    /// </summary>
    class MetadataAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        const string k_MetadataExampleLabel = "MetadataExample";
        const string k_MetadataSuffix = "Metadata.asset";

        static readonly List<string> k_MetadataPaths = new List<string>();

        static string[] OnWillSaveAssets(string[] paths)
        {
            k_MetadataPaths.Clear();
            foreach (var path in paths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid())
                {
                    // We check Asset labels to contain metadata management to just Scenes involved in this example,
                    // but depending on your use case you may have a different qualifying check or even none at all
                    // if you want every Scene to have metadata.
                    var sceneLabels = AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
                    if (sceneLabels.Contains(k_MetadataExampleLabel))
                    {
                        var metadataPath = path.Substring(0, path.Length - 6) + k_MetadataSuffix;
                        var metadata = AssetDatabase.LoadAssetAtPath<ExampleSceneMetadata>(metadataPath);
                        if (metadata == null)
                        {
                            // Create metadata if it doesn't exist
                            metadata = ScriptableObject.CreateInstance<ExampleSceneMetadata>();
                            AssetDatabase.CreateAsset(metadata, metadataPath);
                        }

                        metadata.UpdateFromScene(scene);

                        // To make sure the metadata Asset gets saved, we need to both dirty the Asset and
                        // include its path in the string array returned from OnWillSaveAssets.
                        EditorUtility.SetDirty(metadata);
                        k_MetadataPaths.Add(metadataPath);
                    }
                }
            }

            if (k_MetadataPaths.Count == 0)
                return paths;

            var pathsToSave = new string[paths.Length + k_MetadataPaths.Count];
            paths.CopyTo(pathsToSave, 0);
            k_MetadataPaths.CopyTo(pathsToSave, paths.Length);
            return pathsToSave;
        }
    }
}
