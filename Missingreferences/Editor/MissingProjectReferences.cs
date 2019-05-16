using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using EditorGUI = UnityEditor.EditorGUI;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans the project for serialized references to missing (deleted) assets and displays the results in an EditorWindow
    /// </summary>
    sealed class MissingProjectReferences : MissingReferencesWindow
    {
        Vector2 m_ScrollPosition;
        bool m_ShowPrefabs;
        bool m_ShowAssets;
        readonly List<GameObject> m_Prefabs = new List<GameObject>();
        readonly List<UnityObject> m_Assets = new List<UnityObject>();

        [MenuItem("Window/SuperScience/MissingProjectReferences")]
        static void OnMenuItem() { GetWindow<MissingProjectReferences>("MissingProjectReferences"); }

        /// <summary>
        /// Load all assets in the AssetDatabase and check them for missing serialized references
        /// </summary>
        protected override void Scan()
        {
            m_Prefabs.Clear();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (Path.IsPathRooted(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && CheckForMissingRefs(prefab))
                {
                    m_Prefabs.Add(prefab);
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
                if (asset != null && CheckForMissingRefs(asset))
                    m_Assets.Add(asset);
            }
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;

                m_ShowPrefabs = EditorGUILayout.Foldout(m_ShowPrefabs, "Prefabs");
                if (m_ShowPrefabs)
                {
                    foreach (var prefab in m_Prefabs)
                    {
                        EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawObject(prefab);
                        }

                        EditorGUILayout.Separator();
                    }
                }

                m_ShowAssets = EditorGUILayout.Foldout(m_ShowAssets, "Assets");
                if (m_ShowAssets)
                {
                    foreach (var asset in m_Assets)
                    {
                        EditorGUILayout.ObjectField(asset, typeof(GameObject), false);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawObject(asset);
                        }

                        EditorGUILayout.Separator();
                    }
                }
            }
        }
    }
}
