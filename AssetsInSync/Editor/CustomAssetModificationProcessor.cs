using System.Collections.Generic;
using UnityEditor;

namespace Unity.Labs.SuperScience
{
    class CustomAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        static readonly List<string> k_OtherPathsToSave = new List<string>();

        static string[] OnWillSaveAssets(string[] paths)
        {
            k_OtherPathsToSave.Clear();
            foreach (var path in paths)
            {
                var exampleObjectA = AssetDatabase.LoadAssetAtPath<ExampleObjectA>(path);
                if (exampleObjectA != null)
                {
                    var otherObject = exampleObjectA.otherObject;
                    if (otherObject != null)
                    {
                        // To make sure the other Asset gets saved, we need to both dirty the Asset and
                        // include its path in the string array returned from OnWillSaveAssets.
                        otherObject.UpdateFromObjectA(exampleObjectA);
                        EditorUtility.SetDirty(otherObject);
                        var otherObjectPath = AssetDatabase.GetAssetPath(otherObject);
                        k_OtherPathsToSave.Add(otherObjectPath);
                    }
                }
            }

            var pathsToSave = new string[paths.Length + k_OtherPathsToSave.Count];
            paths.CopyTo(pathsToSave, 0);
            k_OtherPathsToSave.CopyTo(pathsToSave, paths.Length);
            return pathsToSave;
        }
    }
}
