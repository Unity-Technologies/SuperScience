using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    public class HiddenHierarchy : EditorWindow
    {
        const float k_Indent = 15f;
        static readonly Dictionary<GameObject, bool> k_FoldoutStates = new Dictionary<GameObject, bool>();

        Vector2 m_ScrollPosition;

        [MenuItem("Window/SuperScience/HiddenHierarchy")]
        static void OnMenuItem()
        {
            GetWindow<HiddenHierarchy>("HiddenHierarchy");
        }

        void OnGUI()
        {
            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;
                var sceneCount = SceneManager.sceneCount;
                for (var i = 0; i < sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid())
                        continue;

                    GUILayout.Label(scene.name, EditorStyles.boldLabel);

                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var go in rootObjects)
                    {
                        DrawObject(go, k_Indent);
                    }
                }
            }
        }

        void DrawObject(GameObject go, float indent)
        {
            var transform = go.transform;

            var foldout = false;
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(indent);
                if (transform.childCount > 0)
                {
                    k_FoldoutStates.TryGetValue(go, out foldout);
                    foldout = EditorGUILayout.Foldout(foldout, go.name);
                    k_FoldoutStates[go] = foldout;
                }
                else
                {
                    const float foldoutArrowSize = 12f;
                    GUILayout.Space(foldoutArrowSize);
                    GUILayout.Label(go.name);
                }
            }

            if (!foldout)
                return;

            foreach (Transform child in transform)
            {
                DrawObject(child.gameObject, indent + k_Indent);
            }
        }
    }
}
