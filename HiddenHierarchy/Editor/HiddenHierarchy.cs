using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    public class HiddenHierarchy : EditorWindow
    {
        const float k_Indent = 15f;
        const float k_FoldoutArrowSize = 12f;
        static readonly Dictionary<GameObject, bool> k_FoldoutStates = new Dictionary<GameObject, bool>();

        Vector2 m_ScrollPosition;

        [MenuItem("Window/SuperScience/HiddenHierarchy")]
        static void OnMenuItem()
        {
            GetWindow<HiddenHierarchy>("HiddenHierarchy");
        }

        void OnEnable()
        {
            Selection.selectionChanged += Repaint;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
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

        static void DrawObject(GameObject go, float indent)
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

                    if (GUILayout.Button("Select", GUILayout.Width(50)))
                        Selection.activeObject = go;
                }
                else
                {
                    GUILayout.Space(k_FoldoutArrowSize);
                    if (GUILayout.Button(go.name, GUIStyle.none))
                        Selection.activeObject = go;
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
