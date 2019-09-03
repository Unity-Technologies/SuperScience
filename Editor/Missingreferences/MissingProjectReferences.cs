using System;
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
        const string k_Instructions = "Click the Refresh button to scan your project for missing references. WARNING: " +
            "This will load every asset in your project. For large projects, this may take a long time and/or crash the Editor.";

        const string k_NoMissingReferences = "No missing references in project";
        const string k_NoMissingPrefabReferences = "No missing references in prefabs";
        const string k_NoMissingAssetReferences = "No missing references in asssets";

        // Bool fields will be serialized to maintain state between domain reloads, but our list of GameObjects will not
        [NonSerialized]
        bool m_Scanned;

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
            m_Scanned = true;
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

            if (!m_Scanned)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
            }

            var prefabsCount = m_Prefabs.Count;
            var assetsCount = m_Assets.Count;
            var noPrefabs = prefabsCount == 0;
            var noAssets = assetsCount == 0;
            if (noPrefabs && noAssets)
            {
                GUILayout.Label(k_NoMissingReferences);
                GUIUtility.ExitGUI();
            }

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;

                if (noPrefabs)
                {
                    GUILayout.Label(k_NoMissingPrefabReferences);
                }
                else
                {
                    m_ShowPrefabs = EditorGUILayout.Foldout(m_ShowPrefabs, string.Format("Prefabs: {0}", prefabsCount));
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
                }

                if (noAssets)
                {
                    GUILayout.Label(k_NoMissingAssetReferences);
                }
                else
                {
                    m_ShowAssets = EditorGUILayout.Foldout(m_ShowAssets, string.Format("Assets: {0}", assetsCount));
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
}
