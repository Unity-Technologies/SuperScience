using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans all loaded scenes for GameObjects which use custom tags and displays the results in an EditorWindow.
    /// </summary>
    public class SceneTagUsers : EditorWindow
    {
        class SceneContainer
        {
            const string k_UntitledSceneName = "Untitled";

            readonly string m_SceneName;
            readonly List<GameObjectContainer> m_Roots;
            readonly int m_TotalCount;
            readonly SortedDictionary<string, int> m_CountPerTag;
            readonly string[] m_Usages;

            bool m_Expanded;

            SceneContainer(string sceneName, List<GameObjectContainer> roots)
            {
                m_SceneName = sceneName;
                m_Roots = roots;
                m_CountPerTag = new SortedDictionary<string, int>();
                foreach (var container in roots)
                {
                    var gameObject = container.GameObject;
                    var tag = gameObject.tag;
                    if (!gameObject.CompareTag(k_UntaggedString))
                    {
                        m_TotalCount++;
                        IncrementCountForTag(tag, m_CountPerTag);
                    }

                    m_TotalCount += container.TotalUsagesInChildren;
                    AggregateCountPerTag(container.UsagesInChildrenPerTag, m_CountPerTag);
                }

                m_Usages = m_CountPerTag.Keys.ToArray();
            }

            public void Draw(string tagFilter)
            {
                var count = GetCount(tagFilter);
                if (count == 0)
                    return;

                k_StringBuilder.Length = 0;
                k_StringBuilder.Append(m_SceneName);
                k_StringBuilder.Append(" (");
                k_StringBuilder.Append(count.ToString());
                k_StringBuilder.Append(") {");
                AppendTagNameList(k_StringBuilder, m_Usages, tagFilter);
                k_StringBuilder.Append("}");
                var label = k_StringBuilder.ToString();
                var wasExpanded = m_Expanded;
                m_Expanded = EditorGUILayout.Foldout(m_Expanded, label, true);

                // Hold alt to apply expanded state to all children (recursively)
                if (m_Expanded != wasExpanded && Event.current.alt)
                {
                    foreach (var gameObjectContainer in m_Roots)
                    {
                        gameObjectContainer.SetExpandedRecursively(m_Expanded);
                    }
                }

                if (!m_Expanded)
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var gameObjectContainer in m_Roots)
                    {
                        gameObjectContainer.Draw(tagFilter);
                    }
                }
            }

            public static SceneContainer CreateIfNecessary(Scene scene, SortedDictionary<string, HashSet<GameObject>> filterRows)
            {
                var sceneName = scene.name;
                if (string.IsNullOrEmpty(sceneName))
                    sceneName = k_UntitledSceneName;

                List<GameObjectContainer> roots = null;
                foreach (var gameObject in scene.GetRootGameObjects())
                {
                    var rootContainer = GameObjectContainer.CreateIfNecessary(gameObject, filterRows);
                    if (rootContainer != null)
                    {
                        roots ??= new List<GameObjectContainer>();
                        roots.Add(rootContainer);
                    }
                }

                return roots != null ? new SceneContainer(sceneName, roots) : null;
            }

            public int GetCount(string tagFilter = null)
            {
                var unfiltered = string.IsNullOrEmpty(tagFilter);
                if (unfiltered)
                    return m_TotalCount;

                m_CountPerTag.TryGetValue(tagFilter, out var count);
                return count;
            }
        }

        /// <summary>
        /// Tree structure for GameObject scan results
        /// When the Scan method encounters a GameObject in a scene or a prefab in the project, we initialize one of
        /// these using the GameObject as an argument. This scans the object and its components/children, retaining
        /// the results for display in the GUI. The window calls into these helper objects to draw them, as well.
        /// </summary>
        class GameObjectContainer
        {
            readonly GameObject m_GameObject;
            readonly List<GameObjectContainer> m_Children;

            //TODO: Rename Users -> Usages
            readonly int m_TotalUsagesInChildren;
            readonly SortedDictionary<string, int> m_UsagesInChildrenPerTag;

            readonly string[] m_Usages;

            bool m_Expanded;

            public GameObject GameObject { get { return m_GameObject; } }
            public int TotalUsagesInChildren => m_TotalUsagesInChildren;
            public SortedDictionary<string, int> UsagesInChildrenPerTag => m_UsagesInChildrenPerTag;

            // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
            static readonly SortedSet<string> k_Usages = new SortedSet<string>();

            GameObjectContainer(GameObject gameObject, List<GameObjectContainer> children)
            {
                m_GameObject = gameObject;
                m_Children = children;
                k_Usages.Clear();

                var tag = gameObject.tag;
                if (!gameObject.CompareTag(k_UntaggedString))
                    k_Usages.Add(tag);

                if (children != null)
                {
                    m_UsagesInChildrenPerTag = new SortedDictionary<string, int>();
                    foreach (var container in children)
                    {
                        AggregateCount(container, ref m_TotalUsagesInChildren);
                    }

                    k_Usages.UnionWith(m_UsagesInChildrenPerTag.Keys);
                }

                m_Usages = k_Usages.ToArray();
            }

            /// <summary>
            /// Initialize a GameObjectContainer to represent the given GameObject
            /// This will scan the component for missing references and retain the information for display in
            /// the given window.
            /// </summary>
            /// <param name="gameObject">The GameObject to scan for missing references</param>
            /// <param name="filterRows">Dictionary of HashSet&gt;GameObject&lt; objects for counting usages per tag.</param>
            public static GameObjectContainer CreateIfNecessary(GameObject gameObject, SortedDictionary<string, HashSet<GameObject>> filterRows)
            {
                List<GameObjectContainer> children = null;
                foreach (Transform child in gameObject.transform)
                {
                    var childContainer = CreateIfNecessary(child.gameObject, filterRows);
                    if (childContainer != null)
                    {
                        children ??= new List<GameObjectContainer>();
                        children.Add(childContainer);
                    }
                }

                var tag = gameObject.tag;
                var isTagUser = !gameObject.CompareTag(k_UntaggedString);
                if (isTagUser || children != null)
                {
                    if (isTagUser)
                    {
                        var hashSet = GetOrCreateHashSetForTag(filterRows, tag);
                        hashSet.Add(gameObject);
                    }

                    return new GameObjectContainer(gameObject, children);
                }

                return null;
            }

            /// <summary>
            /// Draw tag usage information for this GameObjectContainer
            /// </summary>
            public void Draw(string tagFilter = null)
            {
                var tag = m_GameObject.tag;
                var isTagUser = string.IsNullOrEmpty(tagFilter) ? !m_GameObject.CompareTag(k_UntaggedString) : m_GameObject.CompareTag(tagFilter);

                var childCount = GetChildCount(tagFilter);
                var hasChildren = childCount > 0;
                if (!(isTagUser || hasChildren))
                    return;

                var label = GetLabel(tagFilter, childCount, isTagUser, tag);
                var wasExpanded = m_Expanded;
                if (hasChildren)
                    m_Expanded = EditorGUILayout.Foldout(m_Expanded, label, true);
                else
                    EditorGUILayout.LabelField(label);


                // Hold alt to apply expanded state to all children (recursively)
                if (m_Expanded != wasExpanded && Event.current.alt)
                {
                    if (m_Children != null)
                    {
                        foreach (var gameObjectContainer in m_Children)
                        {
                            gameObjectContainer.SetExpandedRecursively(m_Expanded);
                        }
                    }
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    if (isTagUser)
                        EditorGUILayout.ObjectField(m_GameObject, typeof(GameObject), true);

                    if (!m_Expanded)
                        return;

                    if (hasChildren && m_Children != null)
                    {
                        foreach (var child in m_Children)
                        {
                            child.Draw(tagFilter);
                        }
                    }
                }
            }

            string GetLabel(string tagFilter, int count, bool isTagUser, string tag)
            {
                k_StringBuilder.Length = 0;
                k_StringBuilder.Append(m_GameObject.name);
                k_StringBuilder.Append(" (");
                k_StringBuilder.Append(count.ToString());
                if (isTagUser)
                {
                    k_StringBuilder.Append(") - Tag: ");
                    k_StringBuilder.Append(tag);
                    k_StringBuilder.Append(" {");
                }
                else
                {
                    k_StringBuilder.Append(") {");
                }

                AppendTagNameList(k_StringBuilder, m_Usages, tagFilter);
                k_StringBuilder.Append("}");

                return k_StringBuilder.ToString();
            }

            /// <summary>
            /// Set the expanded state of this object and all of its children
            /// </summary>
            /// <param name="expanded">Whether this object should be expanded in the GUI</param>
            public void SetExpandedRecursively(bool expanded)
            {
                m_Expanded = expanded;
                if (m_Children != null)
                {
                    foreach (var child in m_Children)
                    {
                        child.SetExpandedRecursively(expanded);
                    }
                }
            }

            int GetChildCount(string tagFilter = null)
            {
                if (m_Children == null)
                    return 0;

                var unfiltered = string.IsNullOrEmpty(tagFilter);
                if (unfiltered)
                    return m_TotalUsagesInChildren;

                m_UsagesInChildrenPerTag.TryGetValue(tagFilter, out var totalCount);
                return totalCount;
            }

            void AggregateCount(GameObjectContainer child, ref int usagesInChildren)
            {
                var gameObject = child.m_GameObject;
                var tag = gameObject.tag;
                if (!gameObject.CompareTag(k_UntaggedString))
                {
                    usagesInChildren++;
                    IncrementCountForTag(tag, m_UsagesInChildrenPerTag);
                }

                usagesInChildren += child.m_TotalUsagesInChildren;
                AggregateCountPerTag(child.m_UsagesInChildrenPerTag, m_UsagesInChildrenPerTag);
            }
        }

        static class Styles
        {
            internal static readonly GUIStyle ActiveFilterButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };

            internal static readonly GUIStyle InactiveFilterButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft
            };
        }

        const string k_MenuItemName = "Window/SuperScience/Scene Tag Users";
        const string k_WindowTitle = "Scene Tag Users";
        const string k_NoUsages = "No scene objects using any custom tags";
        const int k_FilterPanelWidth = 180;
        const int k_ObjectFieldWidth = 150;
        const string k_Instructions = "Click the Scan button to scan your project for users of custom tags. WARNING: " +
            "This will load every prefab in your project. For large projects, this may take a long time and/or crash the Editor.";
        const int k_ProgressBarHeight = 15;
        const string k_UntaggedString = "Untagged";

        static readonly GUIContent k_ScanGUIContent = new GUIContent("Scan", "Scan the loaded scenes for usages of custom tags");
        static readonly GUIContent k_CancelGUIContent = new GUIContent("Cancel", "Cancel the current scan");
        static readonly GUILayoutOption k_FilterPanelWidthOption = GUILayout.Width(k_FilterPanelWidth);
        static readonly Vector2 k_MinSize = new Vector2(400, 200);
        static readonly HashSet<string> k_BuiltInTags = new HashSet<string>
        {
            k_UntaggedString,
            "Respawn",
            "Finish",
            "EditorOnly",
            "MainCamera",
            "Player",
            "GameController",
        };

        Vector2 m_ColorListScrollPosition;
        Vector2 m_FolderTreeScrollPosition;
        readonly List<SceneContainer> m_SceneContainers = new List<SceneContainer>();
        readonly SortedDictionary<string, HashSet<GameObject>> m_FilterRows = new SortedDictionary<string, HashSet<GameObject>>();
        int m_ScanCount;
        int m_ScanProgress;
        IEnumerator m_ScanEnumerator;

        [NonSerialized]
        bool m_Scanned;

        [SerializeField]
        string m_TagFilter;

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly StringBuilder k_StringBuilder = new StringBuilder(4096);

        /// <summary>
        /// Initialize the window
        /// </summary>
        [MenuItem(k_MenuItemName)]
        static void Init()
        {
            GetWindow<SceneTagUsers>(k_WindowTitle).Show();
        }

        void OnEnable()
        {
            minSize = k_MinSize;
            m_ScanCount = 0;
            m_ScanProgress = 0;
        }

        void OnDisable()
        {
            m_ScanEnumerator = null;
        }

        void OnGUI()
        {
            EditorGUIUtility.labelWidth = position.width - k_FilterPanelWidth - k_ObjectFieldWidth;

            if (m_ScanEnumerator == null)
            {
                if (GUILayout.Button(k_ScanGUIContent))
                    Scan();
            }
            else
            {
                if (GUILayout.Button(k_CancelGUIContent))
                    m_ScanEnumerator = null;
            }

            if (!m_Scanned)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(k_FilterPanelWidthOption))
                {
                    DrawFilters();
                }

                using (new GUILayout.VerticalScope())
                {
                    var count = 0;
                    foreach (var container in m_SceneContainers)
                    {
                        count += container.GetCount(m_TagFilter);
                    }

                    if (count == 0)
                    {
                        GUILayout.Label(k_NoUsages);
                    }
                    else
                    {
                        using (var scrollView = new GUILayout.ScrollViewScope(m_FolderTreeScrollPosition))
                        {
                            m_FolderTreeScrollPosition = scrollView.scrollPosition;
                            foreach (var container in m_SceneContainers)
                            {
                                container.Draw(m_TagFilter);
                            }
                        }
                    }
                }
            }

            if (m_ScanCount > 0 && m_ScanCount - m_ScanProgress > 0)
            {
                var rect = GUILayoutUtility.GetRect(0, float.PositiveInfinity, k_ProgressBarHeight, k_ProgressBarHeight);
                EditorGUI.ProgressBar(rect, (float) m_ScanProgress / m_ScanCount, $"{m_ScanProgress} / {m_ScanCount}");
            }
        }

        /// <summary>
        /// Draw a list buttons for filtering based on tag.
        /// </summary>
        void DrawFilters()
        {
            var count = 0;
            foreach (var container in m_SceneContainers)
            {
                count += container.GetCount();
            }

            var style = string.IsNullOrEmpty(m_TagFilter) ? Styles.ActiveFilterButton : Styles.InactiveFilterButton;
            if (GUILayout.Button($"All ({count})", style))
                m_TagFilter = null;

            using (var scrollView = new GUILayout.ScrollViewScope(m_ColorListScrollPosition))
            {
                m_ColorListScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in m_FilterRows)
                {
                    var tag = kvp.Key;
                    count = 0;
                    if (m_FilterRows.TryGetValue(tag, out var hashSet))
                        count = hashSet.Count;

                    style = m_TagFilter == tag ? Styles.ActiveFilterButton : Styles.InactiveFilterButton;
                    if (GUILayout.Button($"{tag}: ({count})", style))
                        m_TagFilter = tag;
                }
            }
        }

        /// <summary>
        /// Scan the project for tag usages and populate the data structures for UI.
        /// </summary>
        void Scan()
        {
            m_Scanned = true;
            m_SceneContainers.Clear();
            m_FilterRows.Clear();

            // Add all tags to FilterRows to include tags with no users
            foreach (var tag in InternalEditorUtility.tags)
            {
                if (k_BuiltInTags.Contains(tag))
                    continue;

                m_FilterRows[tag] = new HashSet<GameObject>();
            }

            // If we are in prefab isolation mode, scan the prefab stage instead of the active scene
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                ScanScene(prefabStage.scene);
                return;
            }

            var loadedSceneCount = SceneManager.sceneCount;
            for (var i = 0; i < loadedSceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                ScanScene(scene);
            }
        }

        void ScanScene(Scene scene)
        {
            var sceneContainer = SceneContainer.CreateIfNecessary(scene, m_FilterRows);
            if (sceneContainer != null)
                m_SceneContainers.Add(sceneContainer);
        }

        /// <summary>
        /// Get or create a HashSet&gt;GameObject&lt; for a given tag.
        /// </summary>
        /// <param name="filterRows">Dictionary of HashSet&gt;GameObject&lt; objects for counting usages per tag.</param>
        /// <param name="tag">The tag to use for this row.</param>
        /// <returns>The HashSet&gt;GameObject&lt; for the tag.</returns>
        static HashSet<GameObject> GetOrCreateHashSetForTag(SortedDictionary<string, HashSet<GameObject>> filterRows, string tag)
        {
            if (filterRows.TryGetValue(tag, out var filterRow))
                return filterRow;

            filterRow = new HashSet<GameObject>();
            filterRows[tag] = filterRow;
            return filterRow;
        }

        static void AppendTagNameList(StringBuilder stringBuilder, string[] tags, string tagFilter = null)
        {
            if (!string.IsNullOrEmpty(tagFilter))
            {
                stringBuilder.Append(tagFilter);
                return;
            }

            var length = tags.Length;
            if (length == 0)
                return;

            var lengthMinusOne = length - 1;
            for (var i = 0; i < lengthMinusOne; i++)
            {
                stringBuilder.Append(tags[i]);
                stringBuilder.Append(", ");
            }

            stringBuilder.Append(tags[lengthMinusOne]);
        }

        static void IncrementCountForTag(string tag, SortedDictionary<string, int> countPerTag)
        {
            countPerTag.TryGetValue(tag, out var count);
            count++;
            countPerTag[tag] = count;
        }

        static void AggregateCountPerTag(SortedDictionary<string, int> source, SortedDictionary<string, int> destination)
        {
            if (source == null)
                return;

            foreach (var kvp in source)
            {
                var tag = kvp.Key;
                destination.TryGetValue(tag, out var count);
                count += kvp.Value;
                destination[tag] = count;
            }
        }
    }
}
