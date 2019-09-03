using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    sealed class MissingSceneReferences : MissingReferencesWindow
    {
        const string k_Instructions = "Click the Refresh button to scan the active scene for missing references. WARNING: " +
            "For large scenes, this may take a long time and/or crash the Editor.";

        const string k_NoMissingReferences = "No missing references in active scene";

        // Bool fields will be serialized to maintain state between domain reloads, but our list of GameObjects will not
        [NonSerialized]
        bool m_Scanned;

        Vector2 m_ScrollPosition;
        bool m_ShowGameObjects;
        readonly List<GameObject> m_GameObjects = new List<GameObject>();

        [MenuItem("Window/SuperScience/Missing Scene References")]
        static void OnMenuItem() { GetWindow<MissingSceneReferences>("MissingSceneReferences"); }

        protected override void Scan()
        {
            m_Scanned = true;
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            m_GameObjects.Clear();
            foreach (var gameObject in activeScene.GetRootGameObjects())
            {
                ScanGameObject(gameObject);
            }
        }

        void ScanGameObject(GameObject gameObject)
        {
            if (CheckForMissingRefs(gameObject))
                m_GameObjects.Add(gameObject);

            foreach (Transform child in gameObject.transform)
            {
                ScanGameObject(child.gameObject);
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

            if (m_GameObjects.Count == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
                GUIUtility.ExitGUI();
            }

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;

                m_ShowGameObjects = EditorGUILayout.Foldout(m_ShowGameObjects, string.Format("GameObjects: {0}", m_GameObjects.Count));
                if (m_ShowGameObjects)
                {
                    foreach (var gameObject in m_GameObjects)
                    {
                        EditorGUILayout.ObjectField(gameObject, typeof(GameObject), true);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawObject(gameObject);
                        }

                        EditorGUILayout.Separator();
                    }
                }
            }
        }
    }
}
