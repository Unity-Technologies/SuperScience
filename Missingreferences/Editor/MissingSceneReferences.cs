using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    sealed class MissingSceneReferences : MissingReferencesWindow
    {
        Vector2 m_ScrollPosition;
        bool m_ShowGameObjects;
        readonly List<GameObject> m_GameObjects = new List<GameObject>();

        [MenuItem("Window/SuperScience/Missing Scene References")]
        static void OnMenuItem() { GetWindow<MissingSceneReferences>("MissingSceneReferences"); }

        protected override void Scan()
        {
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

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;

                m_ShowGameObjects = EditorGUILayout.Foldout(m_ShowGameObjects, "GameObjects");
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
