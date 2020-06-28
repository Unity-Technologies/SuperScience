using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    public class HiddenHierarchy : EditorWindow
    {
        static class Styles
        {
            const float k_RowHeight = 16f;
            public static readonly GUIStyle SelectedRow;
            public static readonly GUIStyle UnSelectedRow;
            public static readonly GUIStyle SceneFoldout;
            public static readonly GUIStyle FoldoutButton;
            public static readonly GUIStyle FoldoutButtonExpanded;
            public static readonly GUIStyle ClickableLabel;

            static Styles()
            {
                SelectedRow = new GUIStyle
                {
                    normal = new GUIStyleState { background = Texture2D.grayTexture },
                    fixedHeight = k_RowHeight
                };

                UnSelectedRow = new GUIStyle
                {
                    hover = new GUIStyleState { background = Texture2D.grayTexture },
                    fixedHeight = k_RowHeight
                };

                SceneFoldout = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };

                FoldoutButton = new GUIStyle
                {
                    normal = new GUIStyleState { background = EditorStyles.foldout.normal.scaledBackgrounds[0]},

                    // Margin and overflow found by trial and error
                    margin = new RectOffset(0,0, 6, 0),
                    overflow = new RectOffset(3, 0, 4, -7),
                    stretchWidth = false,
                    fixedWidth = k_FoldoutButtonWidth,
                    fixedHeight = k_RowHeight
                };

                FoldoutButtonExpanded = new GUIStyle(FoldoutButton)
                {
                    normal = new GUIStyleState { background = EditorStyles.foldout.onNormal.scaledBackgrounds[0]},
                };

                ClickableLabel = new GUIStyle(EditorStyles.label)
                {
                    fixedHeight = k_RowHeight,
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }
        }

        const float k_Indent = 15f;
        const float k_FoldoutButtonWidth = 10f;
        const string k_FreeGameobjectsLabel = "Free GameObjects";
        const string k_RefreshButtonLabel = "Refresh";
        const string k_AutoUpdateFieldLabel = "Auto Update";
        const string k_PreviewSceneLabelFormat = "(Preview) {0}";
        const string k_UntitledSceneName = "Untitled";
        const int k_RefreshButtonWidth = 50;

        // Serializable fields of SerializedObjects survive domain reload even if not marked with [SerializedField]
        Vector2 m_ScrollPosition;
        bool m_AutoUpdate = true;
        List<bool> m_SceneFoldoutStates = new List<bool>();
        bool m_FreeGameObjectsFoldout = true;

        // Dictionary cannot not be serialized, so foldout states will not survive domain reload
        readonly Dictionary<GameObject, bool> m_GameObjectExpandedStates = new Dictionary<GameObject, bool>();

        // Non-serialized state stored here for non-auto-refresh mode
        readonly List<KeyValuePair<string, List<GameObject>>> m_RootObjectLists = new List<KeyValuePair<string, List<GameObject>>>();
        readonly List<GameObject> m_FreeGameObjects = new List<GameObject>();

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly Dictionary<Scene, List<GameObject>> k_SceneGameObjectMap = new Dictionary<Scene, List<GameObject>>();

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

            // Set up a dictionary to map scene -> gameobject for faster lookup below
            k_SceneGameObjectMap.Clear();
            m_FreeGameObjects.Clear();
            foreach (var gameObject in gameObjects)
            {
                // We only care about root objects
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

                var sceneHandle = scene.handle;
                if (!k_SceneGameObjectMap.TryGetValue(scene, out var list))
                {
                    list = new List<GameObject>();
                    k_SceneGameObjectMap[scene] = list;
                }

                list.Add(gameObject);
            }

            // Sort by sibling index so that order matches actual hierarchy
            foreach (var kvp in k_SceneGameObjectMap)
            {
                kvp.Value.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            }

            // Store the objects in a list of lists to keep scene sorting order
            m_RootObjectLists.Clear();
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                var sceneHandle = scene.handle;
                if (!k_SceneGameObjectMap.TryGetValue(scene, out var list))
                    continue;

                k_SceneGameObjectMap.Remove(scene);

                var sceneName = scene.name;

                // Untitled scene will have a blank scene name; replace with Untitled to match actual hierarchy
                if (string.IsNullOrEmpty(sceneName))
                    sceneName = k_UntitledSceneName;

                m_RootObjectLists.Add(new KeyValuePair<string, List<GameObject>>(sceneName, list));
            }

            foreach (var kvp in k_SceneGameObjectMap)
            {
                var scene = kvp.Key;
                var label = scene.name;
                if (EditorSceneManager.IsPreviewScene(scene))
                    label = string.Format(k_PreviewSceneLabelFormat, label);

                m_RootObjectLists.Add(new KeyValuePair<string, List<GameObject>>(label, kvp.Value));
            }
        }

        void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                m_AutoUpdate = EditorGUILayout.Toggle(k_AutoUpdateFieldLabel, m_AutoUpdate);
                if (m_AutoUpdate)
                    Refresh();

                using (new GUILayout.HorizontalScope(GUILayout.Width(k_RefreshButtonWidth)))
                using (new EditorGUI.DisabledScope(m_AutoUpdate))
                {
                    if (GUILayout.Button(k_RefreshButtonLabel))
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

        void DrawObject(GameObject gameObject, float indent)
        {
            if (gameObject == null)
                return;

            var transform = gameObject.transform;

            var expanded = false;
            var rowStyle = Selection.activeGameObject == gameObject ? Styles.SelectedRow : Styles.UnSelectedRow;
            using (new GUILayout.VerticalScope(rowStyle))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(indent);
                    if (transform.childCount > 0)
                    {
                        m_GameObjectExpandedStates.TryGetValue(gameObject, out expanded);
                        var style = expanded ? Styles.FoldoutButtonExpanded : Styles.FoldoutButton;

                        // Foldouts without labels take up some minimum width and steal clicks from the label button
                        // We emulate a foldout with a button and custom styles so that we can use a button to display
                        // the gameobject label and set selection on click
                        if (GUILayout.Button(string.Empty, style))
                        {
                            expanded = !expanded;
                            m_GameObjectExpandedStates[gameObject] = expanded;
                            if (Event.current.alt)
                                SetExpandedRecursively(gameObject, expanded);
                        }
                    }
                    else
                    {
                        GUILayout.Space(k_FoldoutButtonWidth);
                    }

                    var content = new GUIContent(gameObject.name, AssetPreview.GetMiniThumbnail(gameObject));
                    if (GUILayout.Button(content, Styles.ClickableLabel))
                        Selection.activeObject = gameObject;
                }
            }

            if (!expanded)
                return;

            foreach (Transform child in transform)
            {
                DrawObject(child.gameObject, indent + k_Indent);
            }
        }

        void SetExpandedRecursively(GameObject gameObject, bool expanded)
        {
            m_GameObjectExpandedStates[gameObject] = expanded;
            foreach (Transform child in gameObject.transform)
            {
                SetExpandedRecursively(child.gameObject, expanded);
            }
        }
    }
}
