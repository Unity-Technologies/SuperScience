using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        /// Container for unique color rows.
        /// </summary>
        class LayerRow
        {
            public readonly List<GameObject> prefabs = new List<GameObject>();
        }

        /// <summary>
        /// Tree structure for folder scan results.
        /// This is the root object for the project scan, and represents the results in a hierarchy that matches the
        /// project's folder structure for an easy to read presentation of solid color textures.
        /// When the Scan method encounters a texture, we initialize one of these using the asset path to determine where it belongs.
        /// </summary>
        class Folder
        {
            // TODO: Share code between this window and MissingProjectReferences
            static class Styles
            {
                internal static readonly GUIStyle ProSkinLineStyle = new GUIStyle
                {
                    normal = new GUIStyleState
                    {
                        background = Texture2D.grayTexture
                    }
                };
            }

            const string k_LabelFormat = "{0}: {1}";
            const int k_IndentAmount = 15;
            const int k_SeparatorLineHeight = 1;

            readonly SortedDictionary<string, Folder> m_Subfolders = new SortedDictionary<string, Folder>();
            readonly List<(string, GameObject, SortedSet<int>)> m_Prefabs = new List<(string, GameObject, SortedSet<int>)>();
            readonly SortedDictionary<int, int> m_CountPerLayer = new SortedDictionary<int, int>();
            int m_TotalCount;
            bool m_Visible;

            /// <summary>
            /// Clear the contents of this container.
            /// </summary>
            public void Clear()
            {
                m_Subfolders.Clear();
                m_Prefabs.Clear();
                m_CountPerLayer.Clear();
            }

            /// <summary>
            /// Add a texture to this folder at a given path.
            /// </summary>
            /// <param name="path">The path of the texture.</param>
            /// <param name="prefabAsset">The prefab to add.</param>
            /// <param name="layers">List of layers used by this prefab</param>
            public void AddPrefabAtPath(string path, GameObject prefabAsset, SortedSet<int> layers)
            {
                var folder = GetOrCreateFolderForAssetPath(path, layers);
                folder.m_Prefabs.Add((path, prefabAsset, layers));
            }

            /// <summary>
            /// Get the Folder object which corresponds to the given path.
            /// If this is the first asset encountered for a given folder, create a chain of folder objects
            /// rooted with this one and return the folder at the end of that chain.
            /// Every time a folder is accessed, its Count property is incremented to indicate that it contains one
            /// more solid color texture.
            /// </summary>
            /// <param name="path">Path to a solid color texture relative to this folder.</param>
            /// <param name="layers">The layers for the object which will be added to this folder when it is created (used to aggregate layer counts)</param>
            /// <returns>The folder object corresponding to the folder containing the texture at the given path.</returns>
            Folder GetOrCreateFolderForAssetPath(string path, SortedSet<int> layers)
            {
                var directories = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var folder = this;
                folder.AggregateCount(layers);
                var length = directories.Length - 1;
                for (var i = 0; i < length; i++)
                {
                    var directory = directories[i];
                    Folder subfolder;
                    var subfolders = folder.m_Subfolders;
                    if (!subfolders.TryGetValue(directory, out subfolder))
                    {
                        subfolder = new Folder();
                        subfolders[directory] = subfolder;
                    }

                    folder = subfolder;
                    folder.AggregateCount(layers);
                }

                return folder;
            }

            /// <summary>
            /// Draw GUI for this Folder.
            /// </summary>
            /// <param name="name">The name of the folder.</param>
            /// <param name="layerFilter">(Optional) Layer used to filter results</param>
            public void Draw(string name, int layerFilter = -1)
            {
                var wasVisible = m_Visible;
                m_Visible = EditorGUILayout.Foldout(m_Visible, string.Format(k_LabelFormat, name, GetCount(layerFilter)), true);

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
                        if (folder.GetCount(layerFilter) == 0)
                            continue;

                        folder.Draw(kvp.Key, layerFilter);
                    }

                    foreach (var (_, prefab, layers) in m_Prefabs)
                    {
                        if (layerFilter == -1 || layers.Contains(layerFilter))
                            EditorGUILayout.ObjectField($"{prefab.name} ({string.Join(", ", layers)})", prefab, typeof(GameObject), false);
                    }

                    if (m_Prefabs.Count > 0)
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
                    GUILayout.Box(GUIContent.none, Styles.ProSkinLineStyle, GUILayout.Height(k_SeparatorLineHeight), GUILayout.ExpandWidth(true));
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

            public int GetCount(int layerFilter = -1)
            {
                if (layerFilter == -1)
                    return m_TotalCount;

                m_CountPerLayer.TryGetValue(layerFilter, out var count);
                return count;
            }

            void AggregateCount(SortedSet<int> layers)
            {
                m_TotalCount++;
                foreach (var layer in layers)
                {
                    m_CountPerLayer.TryGetValue(layer, out var count);
                    count++;
                    m_CountPerLayer[layer] = count;
                }
            }
        }

        const string k_NoMissingReferences = "No prefabs using a non-default layer";
        const string k_ProjectFolderName = "Project";
        const int k_TextureColumnWidth = 150;
        const int k_ColorPanelWidth = 150;
        const string k_WindowTitle = "Layer Users";
        const string k_Instructions = "Click the Scan button to scan your project for users of non-default layers. WARNING: " +
            "This will load every prefab in your project. For large projects, this may take a long time and/or crash the Editor.";
        const string k_ScanFilter = "t:Prefab";
        const int k_ProgressBarHeight = 15;
        const int k_MaxScanUpdateTimeMilliseconds = 50;

        static readonly GUIContent k_ScanGUIContent = new GUIContent("Scan", "Scan the project for users of non-default layers");
        static readonly GUILayoutOption k_LayerPanelWidthOption = GUILayout.Width(k_ColorPanelWidth);
        static readonly Vector2 k_MinSize = new Vector2(400, 200);

        static readonly Stopwatch k_StopWatch = new Stopwatch();

        Vector2 m_ColorListScrollPosition;
        Vector2 m_FolderTreeScrollPosition;
        readonly Folder m_ParentFolder = new Folder();
        readonly SortedDictionary<int, LayerRow> m_PrefabsByLayer = new SortedDictionary<int, LayerRow>();
        static readonly string[] k_ScanFolders = new[] { "Assets", "Packages" };
        int m_ScanCount;
        int m_ScanProgress;
        IEnumerator m_ScanEnumerator;
        readonly List<int> m_LayersWithNames = new List<int>();
        int m_LayerFilter = -1;

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Component> k_Components = new List<Component>();

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
            EditorGUIUtility.labelWidth = position.width - k_TextureColumnWidth - k_ColorPanelWidth;

            var rect = GUILayoutUtility.GetRect(0, float.PositiveInfinity, k_ProgressBarHeight, k_ProgressBarHeight);
            EditorGUI.ProgressBar(rect, (float)m_ScanProgress / m_ScanCount, $"{m_ScanProgress} / {m_ScanCount}");
            if (GUILayout.Button(k_ScanGUIContent))
                Scan();

            if (m_ParentFolder.GetCount(m_LayerFilter) == 0)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
                return;
            }

            if (m_ParentFolder.GetCount(m_LayerFilter) == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                GUILayout.Label($"Layer Filter: {(m_LayerFilter == -1 ? "All" : m_LayerFilter.ToString())}");
                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope(k_LayerPanelWidthOption))
                    {
                        DrawColors();
                    }

                    using (var scrollView = new GUILayout.ScrollViewScope(m_FolderTreeScrollPosition))
                    {
                        m_FolderTreeScrollPosition = scrollView.scrollPosition;
                        m_ParentFolder.Draw(k_ProjectFolderName, m_LayerFilter);
                    }
                }
            }
        }

        /// <summary>
        /// Draw a list of unique layers.
        /// </summary>
        void DrawColors()
        {
            GUILayout.Label($"{m_PrefabsByLayer.Count} Used Layers");
            if (GUILayout.Button("All"))
                m_LayerFilter = -1;

            using (var scrollView = new GUILayout.ScrollViewScope(m_ColorListScrollPosition))
            {
                m_ColorListScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in m_PrefabsByLayer)
                {
                    var layer = kvp.Key;
                    if (GUILayout.Button($"Layer {layer} ({kvp.Value.prefabs.Count})"))
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
        }

        /// <summary>
        /// Coroutine for processing scan results.
        /// </summary>
        /// <param name="prefabAssets">Prefab assets to scan.</param>
        /// <returns>IEnumerator used to run the coroutine.</returns>
        IEnumerator ProcessScan(Dictionary<string, GameObject> prefabAssets)
        {
            m_ScanCount = prefabAssets.Count;
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
            var layers = new SortedSet<int>();
            FindLayerUsersRecursively(prefabAsset, layers);
            if (layers.Count > 0)
            {
                m_ParentFolder.AddPrefabAtPath(path, prefabAsset, layers);
                foreach (var layer in layers)
                {
                    var layerRow = GetOrCreateRowForColor(layer);
                    layerRow.prefabs.Add(prefabAsset);
                }
            }
        }

        void FindLayerUsersRecursively(GameObject gameObject, SortedSet<int> layers)
        {
            var layer = gameObject.layer;
            if (layer != 0)
                layers.Add(layer);

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

                    GetLayersFromLayerMask(layers, iterator.intValue);
                }
            }

            // Clear the list after we're done to avoid lingering references
            k_Components.Clear();

            foreach (Transform child in gameObject.transform)
            {
                FindLayerUsersRecursively(child.gameObject, layers);
            }
        }

        void GetLayersFromLayerMask(SortedSet<int> layers, int layerMask)
        {
            // If layer 0 is in the mask, assume that "on" is the default, meaning that a layer that is not the mask
            // counts as "used". Otherwise, if layer 0 is not in the mask, layers that are in the mask count as "used"
            var defaultValue = (layerMask & 1) != 0;

            // Exclude the special cases where every layer except default is included or excluded
            if (layerMask == -2 || layerMask == 1)
                return;

            // Skip layer 0 since we only want non-default layers
            var count = m_LayersWithNames.Count;
            for (var i = 1; i < count; i++)
            {
                var layer = m_LayersWithNames[i];
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

            m_PrefabsByLayer.Clear();
            m_ParentFolder.Clear();

            m_LayersWithNames.Clear();
            for (var i = 0; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                    m_LayersWithNames.Add(i);
            }

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
        /// Get or create a <see cref="LayerRow"/> for a given layer value.
        /// </summary>
        /// <param name="layer">The layer value to use for this row.</param>
        /// <returns>The layer row for the layer value.</returns>
        LayerRow GetOrCreateRowForColor(int layer)
        {
            if (m_PrefabsByLayer.TryGetValue(layer, out var row))
                return row;

            row = new LayerRow();
            m_PrefabsByLayer[layer] = row;
            return row;
        }
    }
}
