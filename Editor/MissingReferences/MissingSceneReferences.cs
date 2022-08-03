using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

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

        // Bool fields will be serialized to maintain state between domain reloads, but our list of GameObjects will not
        [NonSerialized]
        bool m_Scanned;

        Vector2 m_ScrollPosition;
        readonly List<SceneContainer> m_SceneContainers = new List<SceneContainer>();

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
            m_SceneContainers.Clear();

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
                var children = container.Children;
                if (children == null)
                    return;

                list.AddRange(children.Where(x => x.HasMissingReferences));
                foreach (var child in children)
                {
                    AddToList(list, child);
                }
            }

            foreach (var container in m_SceneContainers)
            {
                foreach (var gameObjectContainer in container.Roots)
                {
                    AddToList(allMissingReferencesContainers, gameObjectContainer);
                }
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
            var sceneContainer = SceneContainer.CreateIfNecessary(scene, options);
            if (sceneContainer != null)
                m_SceneContainers.Add(sceneContainer);
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!m_Scanned)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
            }

            if (m_SceneContainers.Count == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
                {
                    m_ScrollPosition = scrollView.scrollPosition;
                    foreach (var container in m_SceneContainers)
                    {
                        container.Draw();
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
