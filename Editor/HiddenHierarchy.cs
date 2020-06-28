using System;
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
        bool m_AutoUpdate = true;
        readonly List<KeyValuePair<string, List<GameObject>>> m_RootObjectLists = new List<KeyValuePair<string, List<GameObject>>>();
        readonly List<GameObject> m_FreeGameObjects = new List<GameObject>();

        [MenuItem("Window/SuperScience/HiddenHierarchy")]
        static void OnMenuItem()
        {
            GetWindow<HiddenHierarchy>("HiddenHierarchy");
        }

        void OnEnable()
        {
            autoRepaintOnSceneChange = m_AutoUpdate;
            if (!m_AutoUpdate)
                Refresh();
        }

        void Refresh()
        {
            // Resources.FindObjectsOfTypeAll will find hidden root objects that scene.GetRootGameObjects doesn't contain
            var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            var sceneCount = SceneManager.sceneCount;

            // Set up a dictionary to map scene path -> gameobject for faster lookup below
            var gameObjectScenePathMap = new Dictionary<string, List<GameObject>>(sceneCount);
            m_FreeGameObjects.Clear();
            foreach (var gameObject in gameObjects)
            {
                if (gameObject.transform.parent != null)
                    continue;

                var scene = gameObject.scene;
                if (!scene.IsValid())
                {
                    // If the scene is not valid, and this isn't a prefab, it is a "free floating" GameObject
                    if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
                        m_FreeGameObjects.Add(gameObject);

                    continue;
                }

                var scenePath = scene.path;
                if (!gameObjectScenePathMap.TryGetValue(scenePath, out var list))
                {
                    list = new List<GameObject>();
                    gameObjectScenePathMap[scenePath] = list;
                }

                list.Add(gameObject);
            }

            // Store the objects in a list of lists to keep scene sorting order
            m_RootObjectLists.Clear();
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                if (!gameObjectScenePathMap.TryGetValue(scene.path, out var list))
                    continue;

                list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                var sceneName = scene.name;
                if (string.IsNullOrEmpty(sceneName))
                    sceneName = "Untitled";

                m_RootObjectLists.Add(new KeyValuePair<string, List<GameObject>>(sceneName, list));
            }
        }

        void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                m_AutoUpdate = EditorGUILayout.Toggle("Auto Update", m_AutoUpdate);
                if (m_AutoUpdate)
                    Refresh();

                using (new GUILayout.HorizontalScope(GUILayout.Width(50)))
                using (new EditorGUI.DisabledScope(m_AutoUpdate))
                {
                    if (GUILayout.Button("Refresh"))
                        Refresh();
                }
            }

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in m_RootObjectLists)
                {
                    var sceneName = kvp.Key;
                    GUILayout.Label(sceneName, EditorStyles.boldLabel);

                    var rootObjects = kvp.Value;
                    foreach (var go in rootObjects)
                    {
                        DrawObject(go, k_Indent);
                    }
                }

                GUILayout.Label("Free GameObjects", EditorStyles.boldLabel);
                foreach (var gameObject in m_FreeGameObjects)
                {
                    DrawObject(gameObject, k_Indent);
                }
            }
        }

        static void DrawObject(GameObject go, float indent)
        {
            if (go == null)
                return;

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
