using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    public class HiddenHierarchy : EditorWindow
    {
        static class Styles
        {
            const float k_RowHeight = 16f;
            public static readonly GUIStyle SceneFoldout;
            public static readonly GUIStyle ClickableFoldout;
            public static readonly GUIStyle ClickableFoldoutButton;
            public static readonly GUIStyle ClickableLabel;

            static Styles()
            {
                SceneFoldout = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };

                ClickableFoldoutButton = new GUIStyle(EditorStyles.label)
                {
                    padding = new RectOffset(-41, 1, 2, -2),
                    fixedHeight = k_RowHeight
                };

                ClickableFoldout = new GUIStyle(EditorStyles.foldout)
                {
                    stretchWidth = false,
                    fixedHeight = k_RowHeight
                };

                ClickableLabel = new GUIStyle(EditorStyles.label)
                {
                    fixedHeight = k_RowHeight
                };
            }
        }

        const float k_Indent = 15f;
        const float k_FoldoutArrowSize = 12f;
        const string k_FreeGameobjectsLabel = "Free GameObjects";

        // Fields of SerializedObjects are serialized even if not marked with [SerializedField]
        Vector2 m_ScrollPosition;
        bool m_AutoUpdate = true;
        List<bool> m_SceneFoldoutStates = new List<bool>();
        bool m_FreeGameObjectsFoldout = true;

        // Non-serialized state stored here for non-auto-refresh mode
        readonly List<KeyValuePair<string, List<GameObject>>> m_RootObjectLists = new List<KeyValuePair<string, List<GameObject>>>();
        readonly List<GameObject> m_FreeGameObjects = new List<GameObject>();
        readonly Dictionary<GameObject, bool> m_GameObjectFoldoutStates = new Dictionary<GameObject, bool>();

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
                var rootListCount = m_RootObjectLists.Count;

                // Ensure m_SceneFoldoutStates has enough values; Default to expanded
                while (m_SceneFoldoutStates.Count < rootListCount)
                    m_SceneFoldoutStates.Add(true);

                for (var i = 0; i < rootListCount; i++)
                {
                    var kvp = m_RootObjectLists[i];
                    var sceneName = kvp.Key;

                    var foldout = m_SceneFoldoutStates[i];
                    foldout = EditorGUILayout.Foldout(foldout, sceneName, true, Styles.SceneFoldout);
                    m_SceneFoldoutStates[i] = foldout;

                    if (!foldout)
                        continue;

                    var rootObjects = kvp.Value;
                    foreach (var go in rootObjects)
                    {
                        DrawObject(go, k_Indent);
                    }
                }

                m_FreeGameObjectsFoldout = EditorGUILayout.Foldout(m_FreeGameObjectsFoldout, k_FreeGameobjectsLabel, true, Styles.SceneFoldout);
                if (!m_FreeGameObjectsFoldout)
                    return;

                foreach (var gameObject in m_FreeGameObjects)
                {
                    DrawObject(gameObject, k_Indent);
                }
            }
        }

        void DrawObject(GameObject go, float indent)
        {
            if (go == null)
                return;

            var transform = go.transform;

            var foldout = false;
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(indent);
                GUIStyle labelStyle;
                if (transform.childCount > 0)
                {
                    m_GameObjectFoldoutStates.TryGetValue(go, out foldout);
                    foldout = EditorGUILayout.Foldout(foldout, string.Empty, false, Styles.ClickableFoldout);
                    m_GameObjectFoldoutStates[go] = foldout;

                    labelStyle = Styles.ClickableFoldoutButton;
                }
                else
                {
                    GUILayout.Space(k_FoldoutArrowSize);
                    labelStyle = Styles.ClickableLabel;
                }

                var content = new GUIContent(go.name, AssetPreview.GetMiniThumbnail(go));
                if (GUILayout.Button(content, labelStyle))
                    Selection.activeObject = go;
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
