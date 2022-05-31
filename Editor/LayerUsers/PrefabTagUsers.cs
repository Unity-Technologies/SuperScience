using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans the project for prefabs which use tags.
    /// Use this utility to track down where a certain tag is used.
    /// </summary>
    public class PrefabTagUsers : EditorWindow
    {
        /// <summary>
        /// Tree structure for folder scan results.
        /// This is the root object for the project scan, and represents the results in a hierarchy that matches the
        /// project's folder structure for an easy to read presentation of tag users.
        /// When the Scan method encounters a tag user, we search the parent folder for one of these using the asset path to file it where it belongs.
        /// </summary>
        class Folder
        {
            // TODO: Share code between this window and others that display a folder structure
            const int k_IndentAmount = 15;
            const int k_SeparatorLineHeight = 1;

            readonly SortedDictionary<string, Folder> m_Subfolders = new SortedDictionary<string, Folder>();
            readonly List<PrefabRow> m_Prefabs = new List<PrefabRow>();
            readonly SortedDictionary<string, int> m_CountPerTag = new SortedDictionary<string, int>();
            int m_TotalCount;
            bool m_Expanded;

            /// <summary>
            /// Clear the contents of this container.
            /// </summary>
            public void Clear()
            {
                m_Subfolders.Clear();
                m_Prefabs.Clear();
                m_CountPerTag.Clear();
                m_TotalCount = 0;
            }

            /// <summary>
            /// Add a prefab to this folder at a given path.
            /// </summary>
            public void AddPrefab(PrefabRow prefabRow)
            {
                var folder = GetOrCreateFolderForAssetPath(prefabRow);
                folder.m_Prefabs.Add(prefabRow);
            }

            /// <summary>
            /// Get the Folder object which corresponds to the path of a given <see cref="PrefabRow"/>.
            /// If this is the first asset encountered for a given folder, create a chain of folder objects
            /// rooted with this one and return the folder at the end of that chain.
            /// Every time a folder is accessed, its Count property is incremented to indicate that it contains one more tag user.
            /// </summary>
            /// <param name="prefabRow">A <see cref="PrefabRow"/> object containing the prefab asset reference and its metadata</param>
            /// <returns>The folder object corresponding to the folder containing the tag users at the given path.</returns>
            Folder GetOrCreateFolderForAssetPath(PrefabRow prefabRow)
            {
                var directories = prefabRow.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var folder = this;
                folder.AggregateCount(prefabRow.Tags);
                var length = directories.Length - 1;
                for (var i = 0; i < length; i++)
                {
                    var directory = directories[i];
                    var subfolders = folder.m_Subfolders;
                    if (!subfolders.TryGetValue(directory, out var subfolder))
                    {
                        subfolder = new Folder();
                        subfolders[directory] = subfolder;
                    }

                    folder = subfolder;
                    folder.AggregateCount(prefabRow.Tags);
                }

                return folder;
            }

            /// <summary>
            /// Draw GUI for this Folder.
            /// </summary>
            /// <param name="name">The name of the folder.</param>
            /// <param name="tagFilter">(Optional) Tag used to filter results.</param>
            public void Draw(string name, string tagFilter = null)
            {
                var wasExpanded = m_Expanded;
                var tagList = GetTagList(m_CountPerTag.Keys, tagFilter);
                var label = $"{name}: {m_TotalCount} {{{tagList}}}";
                m_Expanded = EditorGUILayout.Foldout(m_Expanded, label, true);

                DrawLineSeparator();

                // Hold alt to apply expanded state to all children (recursively)
                if (m_Expanded != wasExpanded && Event.current.alt)
                    SetExpandedRecursively(m_Expanded);

                if (!m_Expanded)
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var kvp in m_Subfolders)
                    {
                        var folder = kvp.Value;
                        if (folder.GetCount(tagFilter) == 0)
                            continue;

                        folder.Draw(kvp.Key, tagFilter);
                    }

                    var showedPrefab = false;
                    foreach (var prefabRow in m_Prefabs)
                    {
                        var tags = prefabRow.Tags;
                        if (string.IsNullOrEmpty(tagFilter) || tags.Contains(tagFilter))
                        {
                            prefabRow.Draw(tagFilter);
                            showedPrefab = true;
                        }
                    }

                    if (showedPrefab)
                        DrawLineSeparator();
                }
            }

            /// <summary>
            /// Draw a separator line.
            /// </summary>
            static void DrawLineSeparator()
            {
                EditorGUILayout.Separator();
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * k_IndentAmount);
                    GUILayout.Box(GUIContent.none, Styles.LineStyle, GUILayout.Height(k_SeparatorLineHeight), GUILayout.ExpandWidth(true));
                }

                EditorGUILayout.Separator();
            }

            /// <summary>
            /// Set the expanded state of this folder, its contents and their children and all of its subfolders and their contents and children.
            /// </summary>
            /// <param name="expanded">Whether this object should be expanded in the GUI.</param>
            void SetExpandedRecursively(bool expanded)
            {
                m_Expanded = expanded;
                foreach (var kvp in m_Subfolders)
                {
                    kvp.Value.SetExpandedRecursively(expanded);
                }
            }

            /// <summary>
            /// Sort the contents of this folder and all subfolders by name.
            /// </summary>
            public void SortContentsRecursively()
            {
                m_Prefabs.Sort((a, b) => a.PrefabAsset.name.CompareTo(b.PrefabAsset.name));
                foreach (var kvp in m_Subfolders)
                {
                    kvp.Value.SortContentsRecursively();
                }
            }

            public int GetCount(string tagFilter = null)
            {
                if (string.IsNullOrEmpty(tagFilter))
                    return m_TotalCount;

                m_CountPerTag.TryGetValue(tagFilter, out var count);
                return count;
            }

            void AggregateCount(SortedSet<string> tags)
            {
                m_TotalCount++;
                foreach (var tag in tags)
                {
                    m_CountPerTag.TryGetValue(tag, out var count);
                    count++;
                    m_CountPerTag[tag] = count;
                }
            }
        }

        class PrefabRow
        {
            public string Path;
            public GameObject PrefabAsset;
            public SortedSet<string> Tags;
            public List<GameObjectRow> TagUsers;

            bool m_Expanded;

            public void Draw(string tagFilter = null)
            {
                var tagList = GetTagList(Tags, tagFilter);
                var label = $"{PrefabAsset.name} {{{tagList}}}";
                if (TagUsers.Count > 0)
                {
                    m_Expanded = EditorGUILayout.Foldout(m_Expanded, label, true);
                    EditorGUILayout.ObjectField(PrefabAsset, typeof(GameObject), false);
                }
                else
                {
                    EditorGUILayout.ObjectField(label, PrefabAsset, typeof(GameObject), false);
                }

                if (!m_Expanded)
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var user in TagUsers)
                    {
                        if (!string.IsNullOrEmpty(tagFilter) && !user.GameObject.CompareTag(tagFilter))
                            continue;

                        user.Draw();
                    }
                }
            }
        }

        struct GameObjectRow
        {
            public string TransformPath;
            public GameObject GameObject;

            public void Draw()
            {
                EditorGUILayout.ObjectField($"{TransformPath} - Tag: {GameObject.tag}", GameObject, typeof(GameObject), true);
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

            internal static readonly GUIStyle LineStyle = new GUIStyle
            {
                normal = new GUIStyleState
                {
#if UNITY_2019_4_OR_NEWER
                    background = Texture2D.grayTexture
#else
                    background = Texture2D.whiteTexture
#endif
                }
            };
        }

        const string k_MenuItemName = "Window/SuperScience/Prefab Tag Users";
        const string k_WindowTitle = "Prefab Tag Users";
        const string k_NoTagUsers = "No prefabs using any tags";
        const string k_ProjectFolderName = "Project";
        const int k_FilterPanelWidth = 180;
        const int k_ObjectFieldWidth = 150;
        const string k_Instructions = "Click the Scan button to scan your project for tag users. WARNING: " +
            "This will load every prefab in your project. For large projects, this may take a long time and/or crash the Editor.";
        const string k_ScanFilter = "t:Prefab";
        const int k_ProgressBarHeight = 15;
        const int k_MaxScanUpdateTimeMilliseconds = 50;
        const string k_UntaggedString = "Untagged";

        static readonly GUIContent k_ScanGUIContent = new GUIContent("Scan", "Scan the project for tag users");
        static readonly GUIContent k_CancelGUIContent = new GUIContent("Cancel", "Cancel the current scan");
        static readonly GUILayoutOption k_FilterPanelWidthOption = GUILayout.Width(k_FilterPanelWidth);
        static readonly Vector2 k_MinSize = new Vector2(400, 200);

        static readonly Stopwatch k_StopWatch = new Stopwatch();

        Vector2 m_FilterListScrollPosition;
        Vector2 m_FolderTreeScrollPosition;
        readonly Folder m_ParentFolder = new Folder();
        readonly SortedDictionary<string, HashSet<GameObject>> m_FilterRows = new SortedDictionary<string, HashSet<GameObject>>();
        static readonly string[] k_ScanFolders = { "Assets", "Packages" };
        int m_ScanCount;
        int m_ScanProgress;
        IEnumerator m_ScanEnumerator;
        bool m_Scanned;

        [SerializeField]
        string m_TagFilter;

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly StringBuilder k_StringBuilder = new StringBuilder(4096);
        static readonly Stack<string> k_TransformPathStack = new Stack<string>();

        /// <summary>
        /// Initialize the window
        /// </summary>
        [MenuItem(k_MenuItemName)]
        static void Init()
        {
            GetWindow<PrefabTagUsers>(k_WindowTitle).Show();
        }

        void OnEnable()
        {
            minSize = k_MinSize;
            m_ScanCount = 0;
            m_ScanProgress = 0;
            m_Scanned = false;
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

                if (m_ParentFolder.GetCount(m_TagFilter) == 0)
                {
                    GUILayout.Label(k_NoTagUsers);
                }
                else
                {
                    using (var scrollView = new GUILayout.ScrollViewScope(m_FolderTreeScrollPosition))
                    {
                        m_FolderTreeScrollPosition = scrollView.scrollPosition;
                        m_ParentFolder.Draw(k_ProjectFolderName, m_TagFilter);
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
            var count = m_ParentFolder.GetCount();
            var style = string.IsNullOrEmpty(m_TagFilter) ? Styles.ActiveFilterButton : Styles.InactiveFilterButton;
            if (GUILayout.Button($"All ({count})", style))
                m_TagFilter = null;

            using (var scrollView = new GUILayout.ScrollViewScope(m_FilterListScrollPosition))
            {
                m_FilterListScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in m_FilterRows)
                {
                    var tag =  kvp.Key;

                    count = 0;
                    if (m_FilterRows.TryGetValue(tag, out var filterRow))
                        count = filterRow.Count;

                    style = m_TagFilter == tag ? Styles.ActiveFilterButton : Styles.InactiveFilterButton;
                    if (GUILayout.Button($"{tag} ({count})", style))
                        m_TagFilter = tag;
                }
            }
        }

        /// <summary>
        /// Update the current scan coroutine.
        /// </summary>
        void UpdateScan()
        {
            if (m_ScanEnumerator == null)
                return;

            k_StopWatch.Reset();
            k_StopWatch.Start();

            // Process as many steps as possible within a given time frame
            while (m_ScanEnumerator.MoveNext())
            {
                // Process for a maximum amount of time and early-out to keep the UI responsive
                if (k_StopWatch.ElapsedMilliseconds > k_MaxScanUpdateTimeMilliseconds)
                    break;
            }

            m_ParentFolder.SortContentsRecursively();
            Repaint();
        }

        /// <summary>
        /// Coroutine for processing scan results.
        /// </summary>
        /// <param name="prefabAssets">Prefab assets to scan.</param>
        /// <returns>IEnumerator used to run the coroutine.</returns>
        IEnumerator ProcessScan(List<(string, GameObject)> prefabAssets)
        {
            m_Scanned = true;
            m_ScanCount = prefabAssets.Count;
            m_ScanProgress = 0;
            foreach (var (path, prefabAsset) in prefabAssets)
            {
                FindTagUsersInPrefab(path, prefabAsset);
                m_ScanProgress++;
                yield return null;
            }

            m_ScanEnumerator = null;
            EditorApplication.update -= UpdateScan;
        }

        void FindTagUsersInPrefab(string path, GameObject prefabAsset)
        {
            var tags = new SortedSet<string>();
            var tagUsers = new List<GameObjectRow>();

            var prefabRoot = prefabAsset;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.assetPath == path)
                prefabRoot = prefabStage.prefabContentsRoot;

            FindTagUsersRecursively(prefabRoot, tags, tagUsers);
            if (tags.Count > 0)
            {
                var prefabRow = new PrefabRow
                {
                    Path = path,
                    PrefabAsset = prefabAsset,
                    Tags = tags,
                    TagUsers = tagUsers
                };

                m_ParentFolder.AddPrefab(prefabRow);
                foreach (var tag in tags)
                {
                    var prefabs = GetOrCreatePrefabHashSetForTag(tag);
                    prefabs.Add(prefabAsset);
                }
            }
        }

        static void FindTagUsersRecursively(GameObject gameObject, SortedSet<string> tags, List<GameObjectRow> tagUsers)
        {
            var tag = gameObject.tag;
            if (!gameObject.CompareTag(k_UntaggedString))
            {
                tags.Add(tag);
                tagUsers.Add(new GameObjectRow
                {
                    TransformPath = GetTransformPath(gameObject.transform),
                    GameObject = gameObject
                });
            }

            foreach (Transform child in gameObject.transform)
            {
                FindTagUsersRecursively(child.gameObject, tags, tagUsers);
            }
        }

        /// <summary>
        /// Scan the project for tag users and populate the data structures for UI.
        /// </summary>
        void Scan()
        {
            var guids = AssetDatabase.FindAssets(k_ScanFilter, k_ScanFolders);
            if (guids == null || guids.Length == 0)
                return;

            m_FilterRows.Clear();
            m_ParentFolder.Clear();
            var prefabAssets = new List<(string, GameObject)>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.Log($"Could not convert {guid} to path");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"Could not load prefab at {path}");
                    continue;
                }

                prefabAssets.Add((path, prefab));
            }

            m_ScanEnumerator = ProcessScan(prefabAssets);
            EditorApplication.update += UpdateScan;
        }

        /// <summary>
        /// Get or create a HashSet&lt;GameObject&gt; for a given tag.
        /// </summary>
        /// <param name="tag">The tag to use for this row.</param>
        /// <returns>The row for the tag.</returns>
        HashSet<GameObject> GetOrCreatePrefabHashSetForTag(string tag)
        {
            if (m_FilterRows.TryGetValue(tag, out var filterRow))
                return filterRow;

            filterRow = new HashSet<GameObject>();
            m_FilterRows[tag] = filterRow;
            return filterRow;
        }

        static string GetTagList(IEnumerable<string> tags, string tagFilter = null)
        {
            if (!string.IsNullOrEmpty(tagFilter))
                return tagFilter;

            k_StringBuilder.Length = 0;
            foreach (var tag in tags)
            {
                k_StringBuilder.Append($"{tag}, ");
            }

            // Remove the last ", ". If we didn't add any tags, the StringBuilder will be empty so skip this step
            if (k_StringBuilder.Length >= 2)
                k_StringBuilder.Length -= 2;

            return k_StringBuilder.ToString();
        }

        static string GetTransformPath(Transform transform)
        {
            if (transform.parent == null)
                return transform.name;

            k_TransformPathStack.Clear();
            while (transform != null)
            {
                k_TransformPathStack.Push(transform.name);
                transform = transform.parent;
            }

            k_StringBuilder.Length = 0;
            while (k_TransformPathStack.Count > 0)
            {
                k_StringBuilder.Append(k_TransformPathStack.Pop());
                k_StringBuilder.Append("/");
            }

            k_StringBuilder.Length -= 1;
            return k_StringBuilder.ToString();
        }
    }
}
