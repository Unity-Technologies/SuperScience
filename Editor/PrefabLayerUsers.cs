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
            readonly List<PrefabRow> m_Prefabs = new List<PrefabRow>();
            readonly SortedDictionary<int, int> m_TotalCountPerLayer = new SortedDictionary<int, int>();
            readonly SortedDictionary<int, int> m_TotalWithoutLayerMasksPerLayer = new SortedDictionary<int, int>();
            int m_TotalCount;
            int m_TotalWithoutLayerMasks;
            bool m_Visible;

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
            public void AddPrefab(PrefabRow prefabRow)
            {
                var folder = GetOrCreateFolderForAssetPath(prefabRow);
                folder.m_Prefabs.Add(prefabRow);
            }

            /// <summary>
            /// Get the Folder object which corresponds to the given path.
            /// If this is the first asset encountered for a given folder, create a chain of folder objects
            /// rooted with this one and return the folder at the end of that chain.
            /// Every time a folder is accessed, its Count property is incremented to indicate that it contains one
            /// more solid color texture.
            /// </summary>
            /// <param name="prefabRow">A <see cref="PrefabRow"/> struct containing the prefab asset reference and its metadata</param>
            /// <returns>The folder object corresponding to the folder containing the texture at the given path.</returns>
            Folder GetOrCreateFolderForAssetPath(PrefabRow prefabRow)
            {
                var directories = prefabRow.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var folder = this;
                var gameObjectLayers = prefabRow.GameObjectLayers;
                var layerMaskLayers = prefabRow.LayerMaskLayers;
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
                    foreach (var prefabRow in m_Prefabs)
                    {
                        var gameObjectLayers = prefabRow.GameObjectLayers;
                        var layerMaskLayers = prefabRow.LayerMaskLayers;
                        if (layerFilter == k_InvalidLayer || gameObjectLayers.Contains(layerFilter) || layerMaskLayers.Contains(layerFilter))
                        {
                            prefabRow.Draw(layerToName, layerFilter, includeLayerMaskFields);
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
                m_Prefabs.Sort((a, b) => a.PrefabAsset.name.CompareTo(b.PrefabAsset.name));
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

        class PrefabRow
        {
            public string Path;
            public GameObject PrefabAsset;
            public SortedSet<int> GameObjectLayers;
            public SortedSet<int> LayerMaskLayers;
            public List<GameObjectRow> LayerUsers;
            public int LayerUsersWithoutLayerMasks;

            bool m_Expanded;

            public void Draw(Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
            {
                var layerNameList = GetLayerNameList(GameObjectLayers, LayerMaskLayers, layerToName, layerFilter, includeLayerMaskFields);
                var label = $"{PrefabAsset.name} {{{layerNameList}}}";
                if (includeLayerMaskFields && LayerUsers.Count > 0 || LayerUsersWithoutLayerMasks > 0)
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
                    foreach (var layerUser in LayerUsers)
                    {
                        if (layerFilter != k_InvalidLayer)
                        {
                            var gameObjectLayerMatchesFilter = layerUser.PrefabGameObject.layer == layerFilter;
                            var layerMasksMatchFilter = includeLayerMaskFields && layerUser.LayerMaskLayers.Contains(layerFilter);
                            if (!(gameObjectLayerMatchesFilter || layerMasksMatchFilter))
                                continue;
                        }

                        layerUser.Draw(layerToName, layerFilter, includeLayerMaskFields);
                    }
                }
            }
        }

        class GameObjectRow
        {
            public string TransformPath;
            public GameObject PrefabGameObject;
            public List<ComponentRow> LayerMaskComponents;
            public SortedSet<int> LayerMaskLayers;

            bool m_Expanded;

            public void Draw(Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
            {
                var layer = PrefabGameObject.layer;
                var layerName = GetLayerNameString(layerToName, layer);

                k_StringBuilder.Length = 0;
                k_StringBuilder.Append($"{TransformPath} - Layer: {layerName}");
                if (includeLayerMaskFields && LayerMaskLayers.Count > 0)
                {
                    var layerNameList = GetLayerNameList(LayerMaskLayers, layerToName, layerFilter);
                    k_StringBuilder.Append($" LayerMasks:{{{layerNameList}}}");
                }

                var label = k_StringBuilder.ToString();

                if (includeLayerMaskFields && GetComponentCount(layerFilter) > 0)
                {
                    m_Expanded = EditorGUILayout.Foldout(m_Expanded, label, true);
                    EditorGUILayout.ObjectField(PrefabGameObject, typeof(GameObject), true);
                }
                else
                {
                    EditorGUILayout.ObjectField(label, PrefabGameObject, typeof(GameObject), true);
                }

                if (!m_Expanded || !includeLayerMaskFields)
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var component in LayerMaskComponents)
                    {
                        if (layerFilter != k_InvalidLayer && !component.UsedLayers.Contains(layerFilter))
                            continue;

                        component.Draw(layerToName, layerFilter);
                    }
                }
            }

            int GetComponentCount(int layerFilter = k_InvalidLayer)
            {
                if (layerFilter == k_InvalidLayer)
                    return LayerMaskComponents.Count;

                var count = 0;
                foreach (var component in LayerMaskComponents)
                {
                    if (component.UsedLayers.Contains(layerFilter))
                        count++;
                }

                return count;
            }
        }

        struct ComponentRow
        {
            public Component PrefabComponent;
            public SortedSet<int> UsedLayers;

            public void Draw(Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer)
            {
                using (new GUILayout.HorizontalScope())
                {
                    var layerNameList = GetLayerNameList(UsedLayers, layerToName, layerFilter);
                    EditorGUILayout.ObjectField($"{PrefabComponent.name} ({PrefabComponent.GetType().Name}) {{{layerNameList}}}", PrefabComponent, typeof(Component), true);
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

        class FilterRow
        {
            public readonly HashSet<GameObject> AllUsers = new HashSet<GameObject>();
            public readonly HashSet<GameObject> UsersWithoutLayerMasks = new HashSet<GameObject>();
        }

        const string k_NoMissingReferences = "No prefabs using a non-default layer";
        const string k_ProjectFolderName = "Project";
        const int k_FilterPanelWidth = 180;
        const int k_ObjectFieldWidth = 150;
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
        static readonly GUIContent k_CancelGUIContent = new GUIContent("Cancel", "Cancel the current scan");
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
        static readonly StringBuilder k_StringBuilder = new StringBuilder(4096);
        static readonly List<Component> k_Components = new List<Component>();
        static readonly SortedSet<int> k_LayerUnionHashSet = new SortedSet<int>();
        static readonly Stack<string> k_TransformPathStack = new Stack<string>();

        /// <summary>
        /// Initialize the window
        /// </summary>
        [MenuItem("Window/SuperScience/Prefab Layer Users")]
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
                foreach (var kvp in m_FilterRows)
                {
                    var layer =  kvp.Key;

                    // Skip the default layer
                    if (layer == 0)
                        continue;

                    count = 0;
                    if (m_FilterRows.TryGetValue(layer, out var filterRow))
                        count = m_IncludeLayerMaskFields ? filterRow.AllUsers.Count : filterRow.UsersWithoutLayerMasks.Count;

                    var layerName = GetLayerNameString(m_LayerToName, layer);
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
        IEnumerator ProcessScan(List<(string, GameObject)> prefabAssets)
        {
            m_ScanCount = prefabAssets.Count;
            m_ScanProgress = 0;
            foreach (var (path, prefabAsset) in prefabAssets)
            {
                FindLayerUsersInPrefab(path, prefabAsset);
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
            var layerUsers = new List<GameObjectRow>();
            var layerUsersWithoutLayerMasks = 0;

            var prefabRoot = prefabAsset;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.assetPath == path)
                prefabRoot = prefabStage.prefabContentsRoot;

            FindLayerUsersRecursively(prefabRoot, gameObjectLayers, layerMaskLayers, layerUsers, ref layerUsersWithoutLayerMasks);
            if (gameObjectLayers.Count > 0 || layerMaskLayers.Count > 0)
            {
                var prefabRow = new PrefabRow
                {
                    Path = path,
                    PrefabAsset = prefabAsset,
                    GameObjectLayers = gameObjectLayers,
                    LayerMaskLayers = layerMaskLayers,
                    LayerUsers = layerUsers,
                    LayerUsersWithoutLayerMasks = layerUsersWithoutLayerMasks
                };

                m_ParentFolder.AddPrefab(prefabRow);
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

        void FindLayerUsersRecursively(GameObject gameObject, SortedSet<int> prefabGameObjectLayers, SortedSet<int> prefabLayerMaskLayers, List<GameObjectRow> layerUsers, ref int layerUsersWithoutLayerMasks)
        {
            var isLayerUser = false;
            var layer = gameObject.layer;
            if (layer != 0)
            {
                isLayerUser = true;
                prefabGameObjectLayers.Add(layer);
                layerUsersWithoutLayerMasks++;
            }

            var layerMaskLayers = new SortedSet<int>();
            var componentRows = new List<ComponentRow>();

            // GetComponents will clear the list, so we don't have to
            gameObject.GetComponents(k_Components);
            foreach (var component in k_Components)
            {
                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                var componentUsesLayers = false;
                var usedLayers = new SortedSet<int>();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.LayerMask)
                        continue;

                    componentUsesLayers |= GetLayersFromLayerMask(usedLayers, iterator.intValue);
                }

                isLayerUser |= componentUsesLayers;

                if (componentUsesLayers)
                {
                    prefabLayerMaskLayers.UnionWith(usedLayers);
                    layerMaskLayers.UnionWith(usedLayers);
                    componentRows.Add(new ComponentRow
                    {
                        PrefabComponent = component,
                        UsedLayers = usedLayers
                    });
                }
            }

            // Clear the list after we're done to avoid lingering references
            k_Components.Clear();

            if (isLayerUser)
            {
                layerUsers.Add(new GameObjectRow
                {
                    TransformPath = GetTransformPath(gameObject.transform),
                    PrefabGameObject = gameObject,
                    LayerMaskComponents = componentRows,
                    LayerMaskLayers = layerMaskLayers
                });
            }

            foreach (Transform child in gameObject.transform)
            {
                FindLayerUsersRecursively(child.gameObject, prefabGameObjectLayers, prefabLayerMaskLayers, layerUsers, ref layerUsersWithoutLayerMasks);
            }
        }

        bool GetLayersFromLayerMask(SortedSet<int> layers, int layerMask)
        {
            // If all layers are named, it is not possible to infer whether layer mask fields "use" a layer
            if (m_LayerWithNoName == k_InvalidLayer)
                return false;

            // Exclude the special cases where every layer is included or excluded
            if (layerMask == k_InvalidLayer || layerMask == 0)
                return false;

            // Depending on whether or not the mask started out as "Everything" or "Nothing", a layer will count as "used" when the user toggles its state.
            // We use the layer without a name to check whether or not the starting point is "Everything" or "Nothing." If this layer's bit is 0, we assume
            // the mask started with "Nothing." Otherwise, if its bit is 1, we assume the mask started with "Everything."
            var defaultValue = (layerMask & 1 << m_LayerWithNoName) != 0;

            var isLayerMaskUser = false;
            foreach (var kvp in m_LayerToName)
            {
                var layer = kvp.Key;

                // Skip layer 0 since we only want non-default layers
                if (layer == 0)
                    continue;

                // We compare (using xor) this layer's bit value with the default value. If they are different, the layer counts as "used."
                if ((layerMask & (1 << layer)) != 0 ^ defaultValue)
                {
                    isLayerMaskUser = true;
                    layers.Add(layer);
                }
            }

            return isLayerMaskUser;
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
                {
                    m_LayerToName.Add(i, layerName);
                    m_FilterRows.Add(i, new FilterRow());
                }
                else
                {
                    m_LayerWithNoName = i;
                }
            }

            // LayerMask field scanning requires at least one layer without a name
            if (m_LayerWithNoName == k_InvalidLayer)
                m_IncludeLayerMaskFields = false;

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
        /// Get or create a <see cref="FilterRow"/> for a given layer value.
        /// </summary>
        /// <param name="layer">The layer value to use for this row.</param>
        /// <returns>The row for the layer value.</returns>
        FilterRow GetOrCreatePrefabHashSetForLayer(int layer)
        {
            if (m_FilterRows.TryGetValue(layer, out var filterRow))
                return filterRow;

            filterRow = new FilterRow();
            m_FilterRows[layer] = filterRow;
            return filterRow;
        }

        static string GetLayerNameList(IEnumerable<int> gameObjectLayers, IEnumerable<int> layerMaskLayers,
            Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
        {
            k_StringBuilder.Length = 0;
            k_LayerUnionHashSet.Clear();
            k_LayerUnionHashSet.UnionWith(gameObjectLayers);
            if (includeLayerMaskFields)
                k_LayerUnionHashSet.UnionWith(layerMaskLayers);

            return GetLayerNameList(k_LayerUnionHashSet, layerToName, layerFilter);
        }

        static string GetLayerNameList(IEnumerable<int> layers, Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer)
        {
            if (layerFilter >= 0)
                return GetLayerNameString(layerToName, layerFilter);

            k_StringBuilder.Length = 0;
            foreach (var layer in layers)
            {
                k_StringBuilder.Append($"{GetLayerNameString(layerToName, layer)}, ");
            }

            // Remove the last ", ". If we didn't add any layers, the StringBuilder will be empty so skip this step
            if (k_StringBuilder.Length >= 2)
                k_StringBuilder.Length -= 2;

            return k_StringBuilder.ToString();
        }

        static string GetLayerNameString(Dictionary<int, string> layerToName, int layer)
        {
            layerToName.TryGetValue(layer, out var layerName);
            if (string.IsNullOrEmpty(layerName))
                layerName = layer.ToString();
            return layerName;
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
