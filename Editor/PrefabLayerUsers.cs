using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans the project for textures comprised of a single solid color.
    /// Use this utility to identify redundant textures, and textures which are larger than they need to be.
    /// </summary>
    public class PrefabLayerUsers : EditorWindow
    {
        /// <summary>
        /// Tree structure for folder scan results.
        /// This is the root object for the project scan, and represents the results in a hierarchy that matches the
        /// project's folder structure for an easy to read presentation of solid color textures.
        /// When the Scan method encounters a texture, we initialize one of these using the asset path to determine where it belongs.
        /// </summary>
        class Folder
        {
            // TODO: Share code between this window and MissingProjectReferences
            const int k_IndentAmount = 15;
            const int k_SeparatorLineHeight = 1;

            readonly SortedDictionary<string, Folder> m_Subfolders = new SortedDictionary<string, Folder>();
            readonly List<(string, GameObject, SortedSet<int>, SortedSet<int>)> m_Prefabs = new List<(string, GameObject, SortedSet<int>, SortedSet<int>)>();
            readonly SortedDictionary<int, int> m_TotalCountPerLayer = new SortedDictionary<int, int>();
            readonly SortedDictionary<int, int> m_TotalWithoutLayerMasksPerLayer = new SortedDictionary<int, int>();
            int m_TotalCount;
            int m_TotalWithoutLayerMasks;
            bool m_Visible;

            // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
            static readonly StringBuilder k_StringBuilder = new StringBuilder();

            /// <summary>
            /// Clear the contents of this container.
            /// </summary>
            public void Clear()
            {
                m_Subfolders.Clear();
                m_Prefabs.Clear();
                m_TotalCountPerLayer.Clear();
                m_TotalWithoutLayerMasksPerLayer.Clear();
                m_TotalCount = 0;
                m_TotalWithoutLayerMasks = 0;
            }

            /// <summary>
            /// Add a texture to this folder at a given path.
            /// </summary>
            /// <param name="path">The path of the texture.</param>
            /// <param name="prefabAsset">The prefab to add.</param>
            /// <param name="gameObjectLayers">List of layers used by GameObjects in this prefab</param>
            /// <param name="layerMaskLayers">List of layers used by LayerMask fields in this prefab</param>
            public void AddPrefabAtPath(string path, GameObject prefabAsset, SortedSet<int> gameObjectLayers, SortedSet<int> layerMaskLayers)
            {
                var folder = GetOrCreateFolderForAssetPath(path, gameObjectLayers, layerMaskLayers);
                folder.m_Prefabs.Add((path, prefabAsset, gameObjectLayers, layerMaskLayers));
            }

            /// <summary>
            /// Get the Folder object which corresponds to the given path.
            /// If this is the first asset encountered for a given folder, create a chain of folder objects
            /// rooted with this one and return the folder at the end of that chain.
            /// Every time a folder is accessed, its Count property is incremented to indicate that it contains one
            /// more solid color texture.
            /// </summary>
            /// <param name="path">Path to a solid color texture relative to this folder.</param>
            /// <param name="gameObjectLayers">The GameObject layers for the object which will be added to this folder when it is created (used to aggregate layer counts)</param>
            /// <param name="layerMaskLayers">The LayerMask layers for the object which will be added to this folder when it is created (used to aggregate layer counts)</param>
            /// <returns>The folder object corresponding to the folder containing the texture at the given path.</returns>
            Folder GetOrCreateFolderForAssetPath(string path, SortedSet<int> gameObjectLayers, SortedSet<int> layerMaskLayers)
            {
                var directories = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var folder = this;
                folder.AggregateCount(gameObjectLayers, layerMaskLayers);
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
                    folder.AggregateCount(gameObjectLayers, layerMaskLayers);
                }

                return folder;
            }

            /// <summary>
            /// Draw GUI for this Folder.
            /// </summary>
            /// <param name="name">The name of the folder.</param>
            /// <param name="layerToName">Layer to Name dictionary for fast lookup of layer names.</param>
            /// <param name="layerFilter">(Optional) Layer used to filter results.</param>
            /// <param name="includeLayerMaskFields">(Optional) Whether to include layers from LayerMask fields in the results.</param>
            public void Draw(string name, Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
            {
                var wasVisible = m_Visible;
                var layerNameList = GetLayerNameList(m_TotalCountPerLayer.Keys, m_TotalWithoutLayerMasksPerLayer.Keys, layerToName, layerFilter, includeLayerMaskFields);
                var label = $"{name}: {GetCount(layerFilter, includeLayerMaskFields)} {{{layerNameList}}}";
                m_Visible = EditorGUILayout.Foldout(m_Visible, label, true);

                DrawLineSeparator();

                // Hold alt to apply visibility state to all children (recursively)
                if (m_Visible != wasVisible && Event.current.alt)
                    SetVisibleRecursively(m_Visible);

                if (!m_Visible)
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var kvp in m_Subfolders)
                    {
                        var folder = kvp.Value;
                        if (folder.GetCount(layerFilter, includeLayerMaskFields) == 0)
                            continue;

                        folder.Draw(kvp.Key, layerToName, layerFilter, includeLayerMaskFields);
                    }

                    var showedPrefab = false;
                    foreach (var (_, prefab, gameObjectLayers, layerMaskLayers) in m_Prefabs)
                    {
                        if (layerFilter == k_InvalidLayer || gameObjectLayers.Contains(layerFilter) || layerMaskLayers.Contains(layerFilter))
                        {
                            layerNameList = GetLayerNameList(gameObjectLayers, layerMaskLayers, layerToName, layerFilter, includeLayerMaskFields);
                            EditorGUILayout.ObjectField($"{prefab.name} ({layerNameList})", prefab, typeof(GameObject), false);
                            showedPrefab = true;
                        }
                    }

                    if (showedPrefab)
                        DrawLineSeparator();
                }
            }

            static string GetLayerNameList(IEnumerable<int> gameObjectLayers, IEnumerable<int> layerMaskLayers,
                Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
            {
                if (layerFilter >= 0)
                {
                    layerToName.TryGetValue(layerFilter, out var layerName);
                    if (string.IsNullOrEmpty(layerName))
                        layerName = layerFilter.ToString();

                    return layerName;
                }

                k_StringBuilder.Length = 0;
                k_LayerUnionHashSet.Clear();
                k_LayerUnionHashSet.UnionWith(gameObjectLayers);
                if (includeLayerMaskFields)
                    k_LayerUnionHashSet.UnionWith(layerMaskLayers);

                foreach (var layer in k_LayerUnionHashSet)
                {
                    layerToName.TryGetValue(layer, out var layerName);
                    if (string.IsNullOrEmpty(layerName))
                        layerName = layer.ToString();

                    k_StringBuilder.Append($"{layerName}, ");
                }

                // Remove the last ", ". If we didn't add any layers, the StringBuilder will be empty so skip this step
                if (k_StringBuilder.Length >= 2)
                    k_StringBuilder.Length -= 2;

                return k_StringBuilder.ToString();
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
            /// Set the visibility state of this folder, its contents and their children and all of its subfolders and their contents and children.
            /// </summary>
            /// <param name="visible">Whether this object and its children should be visible in the GUI.</param>
            void SetVisibleRecursively(bool visible)
            {
                m_Visible = visible;
                foreach (var kvp in m_Subfolders)
                {
                    kvp.Value.SetVisibleRecursively(visible);
                }
            }

            /// <summary>
            /// Sort the contents of this folder and all subfolders by name.
            /// </summary>
            public void SortContentsRecursively()
            {
                m_Prefabs.Sort((a, b) => a.Item2.name.CompareTo(b.Item2.name));
                foreach (var kvp in m_Subfolders)
                {
                    kvp.Value.SortContentsRecursively();
                }
            }

            public int GetCount(int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
            {
                var unfiltered = layerFilter == k_InvalidLayer;
                if (unfiltered && includeLayerMaskFields)
                    return m_TotalCount;

                if (unfiltered)
                    return m_TotalWithoutLayerMasks;

                if (includeLayerMaskFields)
                {
                    m_TotalCountPerLayer.TryGetValue(layerFilter, out var totalCount);
                    return totalCount;
                }

                m_TotalWithoutLayerMasksPerLayer.TryGetValue(layerFilter, out var totalWithoutLayerMasks);
                return totalWithoutLayerMasks;
            }

            void AggregateCount(SortedSet<int> gameObjectLayers, SortedSet<int> layerMaskLayers)
            {
                var hasGameObjectLayers = gameObjectLayers.Count > 0;
                if (hasGameObjectLayers || layerMaskLayers.Count > 0)
                    m_TotalCount++;

                if (hasGameObjectLayers)
                    m_TotalWithoutLayerMasks++;

                k_LayerUnionHashSet.Clear();
                k_LayerUnionHashSet.UnionWith(gameObjectLayers);
                k_LayerUnionHashSet.UnionWith(layerMaskLayers);
                foreach (var layer in k_LayerUnionHashSet)
                {
                    m_TotalCountPerLayer.TryGetValue(layer, out var count);
                    count++;
                    m_TotalCountPerLayer[layer] = count;
                }

                foreach (var layer in gameObjectLayers)
                {
                    m_TotalWithoutLayerMasksPerLayer.TryGetValue(layer, out var count);
                    count++;
                    m_TotalWithoutLayerMasksPerLayer[layer] = count;
                }
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

        struct FilterRow
        {
            public HashSet<GameObject> AllUsers;
            public HashSet<GameObject> UsersWithoutLayerMasks;
        }

        const string k_NoMissingReferences = "No prefabs using a non-default layer";
        const string k_ProjectFolderName = "Project";
        const int k_FilterPanelWidth = 180;
        const string k_WindowTitle = "Prefab Layer Users";
        const string k_Instructions = "Click the Scan button to scan your project for users of non-default layers. WARNING: " +
            "This will load every prefab in your project. For large projects, this may take a long time and/or crash the Editor.";
        const string k_ScanFilter = "t:Prefab";
        const int k_ProgressBarHeight = 15;
        const int k_MaxScanUpdateTimeMilliseconds = 50;
        const int k_InvalidLayer = -1;

        static readonly GUIContent k_IncludeLayerMaskFieldsGUIContent = new GUIContent("Include LayerMask Fields",
            "Include layers from layer mask fields in the results. This is only possible if there is at least one layer without a name.");

        static readonly GUIContent k_ScanGUIContent = new GUIContent("Scan", "Scan the project for users of non-default layers");
        static readonly GUILayoutOption k_LayerPanelWidthOption = GUILayout.Width(k_FilterPanelWidth);
        static readonly Vector2 k_MinSize = new Vector2(400, 200);

        static readonly Stopwatch k_StopWatch = new Stopwatch();

        Vector2 m_ColorListScrollPosition;
        Vector2 m_FolderTreeScrollPosition;
        readonly Folder m_ParentFolder = new Folder();
        readonly SortedDictionary<int, FilterRow> m_FilterRows = new SortedDictionary<int, FilterRow>();
        static readonly string[] k_ScanFolders = { "Assets", "Packages" };
        int m_ScanCount;
        int m_ScanProgress;
        IEnumerator m_ScanEnumerator;
        readonly Dictionary<int, string> m_LayerToName = new Dictionary<int, string>();
        int m_LayerWithNoName = k_InvalidLayer;

        [SerializeField]
        bool m_IncludeLayerMaskFields = true;

        [SerializeField]
        int m_LayerFilter = k_InvalidLayer;

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_Components = new List<Component>();
        static readonly SortedSet<int> k_LayerUnionHashSet = new SortedSet<int>();

        /// <summary>
        /// Initialize the window
        /// </summary>
        [MenuItem("Window/SuperScience/Layer Users")]
        static void Init()
        {
            GetWindow<PrefabLayerUsers>(k_WindowTitle).Show();
        }

        void OnEnable()
        {
            minSize = k_MinSize;
            m_ScanCount = 0;
            m_ScanProgress = 0;
        }

        void OnGUI()
        {
            if (GUILayout.Button(k_ScanGUIContent))
                Scan();

            // If m_LayerToName hasn't been set up, we haven't scanned yet
            // This dictionary will always at least include the built-in layer names
            if (m_LayerToName.Count == 0)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
                return;
            }

            if (m_ParentFolder.GetCount(m_LayerFilter, m_IncludeLayerMaskFields) == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope(k_LayerPanelWidthOption))
                    {
                        DrawFilters();
                    }

                    using (new GUILayout.VerticalScope())
                    {
                        using (new EditorGUI.DisabledScope(m_LayerWithNoName == k_InvalidLayer))
                        {
                            m_IncludeLayerMaskFields = EditorGUILayout.Toggle(k_IncludeLayerMaskFieldsGUIContent, m_IncludeLayerMaskFields);
                        }

                        using (var scrollView = new GUILayout.ScrollViewScope(m_FolderTreeScrollPosition))
                        {
                            m_FolderTreeScrollPosition = scrollView.scrollPosition;
                            m_ParentFolder.Draw(k_ProjectFolderName, m_LayerToName, m_LayerFilter, m_IncludeLayerMaskFields);
                        }
                    }
                }
            }

            if (m_ScanCount > 0 && m_ScanCount - m_ScanProgress > 0)
            {
                var rect = GUILayoutUtility.GetRect(0, float.PositiveInfinity, k_ProgressBarHeight, k_ProgressBarHeight);
                EditorGUI.ProgressBar(rect, (float)m_ScanProgress / m_ScanCount, $"{m_ScanProgress} / {m_ScanCount}");
            }
        }

        /// <summary>
        /// Draw a list of unique layers.
        /// </summary>
        void DrawFilters()
        {
            var count = m_ParentFolder.GetCount(k_InvalidLayer, m_IncludeLayerMaskFields);
            var style = m_LayerFilter == k_InvalidLayer ? Styles.ActiveFilterButton : Styles.InactiveFilterButton;
            if (GUILayout.Button($"All ({count})", style))
                m_LayerFilter = k_InvalidLayer;

            using (var scrollView = new GUILayout.ScrollViewScope(m_ColorListScrollPosition))
            {
                m_ColorListScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in m_LayerToName)
                {
                    var layer =  kvp.Key;

                    // Skip the default layer
                    if (layer == 0)
                        continue;

                    m_LayerToName.TryGetValue(layer, out var layerName);
                    if (string.IsNullOrEmpty(layerName))
                        layerName = layer.ToString();

                    count = 0;
                    if (m_FilterRows.TryGetValue(layer, out var filterRow))
                        count = m_IncludeLayerMaskFields ? filterRow.AllUsers.Count : filterRow.UsersWithoutLayerMasks.Count;

                    style = m_LayerFilter == layer ? Styles.ActiveFilterButton : Styles.InactiveFilterButton;
                    if (GUILayout.Button($"{layer}: {layerName} ({count})", style))
                        m_LayerFilter = layer;
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
        IEnumerator ProcessScan(Dictionary<string, GameObject> prefabAssets)
        {
            m_ScanCount = prefabAssets.Count;
            m_ScanProgress = 0;
            foreach (var kvp in prefabAssets)
            {
                FindLayerUsersInPrefab(kvp.Key, kvp.Value);
                m_ScanProgress++;
                yield return null;
            }

            m_ScanEnumerator = null;
            EditorApplication.update -= UpdateScan;
        }

        void FindLayerUsersInPrefab(string path, GameObject prefabAsset)
        {
            var gameObjectLayers = new SortedSet<int>();
            var layerMaskLayers = new SortedSet<int>();
            FindLayerUsersRecursively(prefabAsset, gameObjectLayers, layerMaskLayers);
            if (gameObjectLayers.Count > 0 || layerMaskLayers.Count > 0)
            {
                m_ParentFolder.AddPrefabAtPath(path, prefabAsset, gameObjectLayers, layerMaskLayers);
                k_LayerUnionHashSet.Clear();
                k_LayerUnionHashSet.UnionWith(gameObjectLayers);
                k_LayerUnionHashSet.UnionWith(layerMaskLayers);
                foreach (var layer in k_LayerUnionHashSet)
                {
                    var filterRow = GetOrCreatePrefabHashSetForLayer(layer);
                    filterRow.AllUsers.Add(prefabAsset);
                }

                foreach (var layer in gameObjectLayers)
                {
                    var filterRow = GetOrCreatePrefabHashSetForLayer(layer);
                    filterRow.UsersWithoutLayerMasks.Add(prefabAsset);
                }
            }
        }

        void FindLayerUsersRecursively(GameObject gameObject, SortedSet<int> gameObjectLayers, SortedSet<int> layerMaskLayers)
        {
            var layer = gameObject.layer;
            if (layer != 0)
                gameObjectLayers.Add(layer);

            // GetComponents will clear the list, so we don't have to
            gameObject.GetComponents(k_Components);
            foreach (var component in k_Components)
            {
                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.LayerMask)
                        continue;

                    GetLayersFromLayerMask(layerMaskLayers, iterator.intValue);
                }
            }

            // Clear the list after we're done to avoid lingering references
            k_Components.Clear();

            foreach (Transform child in gameObject.transform)
            {
                FindLayerUsersRecursively(child.gameObject, gameObjectLayers, layerMaskLayers);
            }
        }

        void GetLayersFromLayerMask(SortedSet<int> layers, int layerMask)
        {
            // If all layers are named, it is not possible to infer whether layer mask fields "use" a layer
            if (m_LayerWithNoName == k_InvalidLayer)
                return;

            // Exclude the special cases where every layer is included or excluded
            if (layerMask == k_InvalidLayer || layerMask == 0)
                return;

            // Depending on whether or not the mask started out as "Everything" or "Nothing", a layer will count as "used" when the user toggles its state.
            // We use the layer without a name to check whether or not the starting point is "Everything" or "Nothing." If this layer's bit is 0, we assume
            // the mask started with "Nothing." Otherwise, if its bit is 1, we assume the mask started with "Everything."
            var defaultValue = (layerMask & 1 << m_LayerWithNoName) != 0;

            foreach (var kvp in m_LayerToName)
            {
                var layer = kvp.Key;

                // Skip layer 0 since we only want non-default layers
                if (layer == 0)
                    continue;

                // We compare (using xor) this layer's bit value with the default value. If they are different, the layer counts as "used."
                if ((layerMask & (1 << layer)) != 0 ^ defaultValue)
                    layers.Add(layer);
            }
        }

        /// <summary>
        /// Scan the project for solid color textures and populate the data structures for UI.
        /// </summary>
        void Scan()
        {
            var guids = AssetDatabase.FindAssets(k_ScanFilter, k_ScanFolders);
            if (guids == null || guids.Length == 0)
                return;

            m_FilterRows.Clear();
            m_ParentFolder.Clear();

            m_LayerWithNoName = k_InvalidLayer;
            m_LayerToName.Clear();
            for (var i = 0; i < 32; i++)
            {
                var layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                    m_LayerToName.Add(i, layerName);
                else
                    m_LayerWithNoName = i;
            }

            // LayerMask field scanning requires at least one layer without a name
            if (m_LayerWithNoName == k_InvalidLayer)
                m_IncludeLayerMaskFields = false;

            var prefabAssets = new Dictionary<string, GameObject>();
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

                prefabAssets.Add(path, prefab);
            }

            m_ScanEnumerator = ProcessScan(prefabAssets);
            EditorApplication.update += UpdateScan;
        }

        /// <summary>
        /// Get or create a <see cref="FilterRow"/> for a given layer value.
        /// </summary>
        /// <param name="layer">The layer value to use for this row.</param>
        /// <returns>The row for the layer value.</returns>
        FilterRow GetOrCreatePrefabHashSetForLayer(int layer)
        {
            if (m_FilterRows.TryGetValue(layer, out var filterRow))
                return filterRow;

            filterRow = new FilterRow
            {
                AllUsers = new HashSet<GameObject>(),
                UsersWithoutLayerMasks = new HashSet<GameObject>()
            };

            m_FilterRows[layer] = filterRow;
            return filterRow;
        }
    }
}
