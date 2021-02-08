using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans all loaded scenes for references to missing (deleted) assets and other types of missing references and displays the results in an EditorWindow
    /// </summary>
    sealed class MissingSceneReferences : MissingReferencesWindow
    {
        const string k_Instructions = "Click the Scan button to scan the active scene for missing references. " +
            "WARNING: For large scenes, this may take a long time and/or crash the Editor.";

        const string k_NoMissingReferences = "No missing references in active scene";
        const string k_UntitledSceneName = "Untitled";

        // Bool fields will be serialized to maintain state between domain reloads, but our list of GameObjects will not
        [NonSerialized]
        bool m_Scanned;

        Vector2 m_ScrollPosition;
        readonly List<KeyValuePair<string, GameObjectContainer>> m_SceneRoots = new List<KeyValuePair<string, GameObjectContainer>>();

        ILookup<int, GameObjectContainer> m_AllMissingReferences;

        [MenuItem("Window/SuperScience/Missing Scene References")]
        static void OnMenuItem() { GetWindow<MissingSceneReferences>("Missing Scene References"); }

        /// <summary>
        /// Scan all assets in the active scene for missing serialized references
        /// </summary>
        /// <param name="options">User-configurable options for this view</param>
        protected override void Scan(Options options)
        {
            m_Scanned = true;
            m_SceneRoots.Clear();

            // If we are in prefab isolation mode, scan the prefab stage instead of the active scene
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                ScanScene(prefabStage.scene, options);
                return;
            }

            var loadedSceneCount = SceneManager.sceneCount;
            for (var i = 0; i < loadedSceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                ScanScene(scene, options);
            }

            var allMissingReferencesContainers = new List<GameObjectContainer>();

            void AddToList(List<GameObjectContainer> list, GameObjectContainer container)
            {
                list.AddRange(container.Children.Where(x => x.HasMissingReferences));
                foreach (var child in container.Children)
                {
                    AddToList(list, child);
                }
            }

            foreach (var kvp in m_SceneRoots)
            {
                AddToList(allMissingReferencesContainers, kvp.Value);
            }

            m_AllMissingReferences = allMissingReferencesContainers.ToLookup(x => x.Object.GetInstanceID());

            foreach (var reference in allMissingReferencesContainers)
            {
                EditorGUIUtility.PingObject(reference.Object);
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        void ScanScene(Scene scene, Options options)
        {
            var rootObjectContainer = new GameObjectContainer();
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                rootObjectContainer.AddChild(gameObject, options);
            }

            if (rootObjectContainer.Count > 0)
            {
                var sceneName = scene.name;
                if (string.IsNullOrEmpty(sceneName))
                    sceneName = k_UntitledSceneName;

                m_SceneRoots.Add(new KeyValuePair<string, GameObjectContainer>(sceneName, rootObjectContainer));
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

            if (m_SceneRoots.Count == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
                {
                    m_ScrollPosition = scrollView.scrollPosition;
                    foreach (var kvp in m_SceneRoots)
                    {
                        kvp.Value.Draw();
                    }
                }
            }
        }

        void OnEnable()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
            EditorApplication.RepaintHierarchyWindow();
        }

        void OnDisable()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowItemOnGUI;
            EditorApplication.RepaintHierarchyWindow();
        }

        void HierarchyWindowItemOnGUI(int instanceId, Rect selectionRect)
        {
            if (m_AllMissingReferences == null)
                return;

            if (!m_AllMissingReferences.Contains(instanceId))
                return;

            DrawItem(selectionRect);
        }
    }
}
