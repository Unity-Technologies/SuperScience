﻿using System;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    sealed class MissingSceneReferences : MissingReferencesWindow
    {
        const string k_Instructions = "Click the Scan button to scan the active scene for missing references. " +
            "WARNING: For large scenes, this may take a long time and/or crash the Editor.";

        const string k_NoMissingReferences = "No missing references in active scene";

        // Bool fields will be serialized to maintain state between domain reloads, but our list of GameObjects will not
        [NonSerialized]
        bool m_Scanned;

        Vector2 m_ScrollPosition;
        bool m_ShowGameObjects;
        readonly GameObjectContainer m_ParentGameObjectContainer = new GameObjectContainer();

        [MenuItem("Window/SuperScience/Missing Scene References")]
        static void OnMenuItem() { GetWindow<MissingSceneReferences>("Missing Scene References"); }

        protected override void Clear()
        {
            m_ParentGameObjectContainer.Clear();
        }

        /// <summary>
        /// Scan all assets in the active scene for missing serialized references
        /// </summary>
        /// <param name="options">User-configurable options for this view</param>
        protected override void Scan(Options options)
        {
            m_Scanned = true;

            // If we are in prefab isolation mode, scan the prefab stage instead of the active scene
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                ScanScene(prefabStage.scene, options);
                return;
            }

            // TODO: Scan all loaded scenes
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            ScanScene(activeScene, options);
        }

        void ScanScene(Scene scene, Options options)
        {
            m_ParentGameObjectContainer.Clear();
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                m_ParentGameObjectContainer.AddChild(gameObject, options);
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

            if (m_ParentGameObjectContainer.Count == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
                {
                    m_ScrollPosition = scrollView.scrollPosition;
                    m_ParentGameObjectContainer.Draw();
                }
            }
        }
    }
}