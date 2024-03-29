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
    public class SolidColorTextures : EditorWindow
    {
        /// <summary>
        /// Tree structure for folder scan results.
        /// This is the root object for the project scan, and represents the results in a hierarchy that matches the
        /// project's folder structure for an easy to read presentation of solid color textures.
        /// When the Scan method encounters a texture, we initialize one of these using the asset path to determine where it belongs.
        /// </summary>
        class Folder
        {
            // TODO: Share code between this window and others that display a folder structure
            const int k_ShrunkTextureSize = 32;
            const int k_IndentAmount = 15;
            const int k_SeparatorLineHeight = 1;
            static readonly GUIContent k_ShrinkGUIContent = new GUIContent("Shrink", "Apply import settings to reduce this texture to the shrink size (32x32)");
            static readonly GUIContent k_ShrinkAllGUIContent = new GUIContent("Shrink All", "Apply import settings to all solid color textures in this directory and its subdirectories shrink them to the minimum size (32x32)");
            static readonly GUILayoutOption k_ShrinkAllWidth = GUILayout.Width(100);

            readonly SortedDictionary<string, Folder> m_Subfolders = new SortedDictionary<string, Folder>();
            readonly List<(string, Texture2D)> m_Textures = new List<(string, Texture2D)>();
            bool m_Visible;

            /// <summary>
            /// The number of solid color textures in this folder.
            /// </summary>
            public int Count { get; private set; }

            /// <summary>
            /// Clear the contents of this container.
            /// </summary>
            public void Clear()
            {
                m_Subfolders.Clear();
                m_Textures.Clear();
                Count = 0;
            }

            /// <summary>
            /// Add a texture to this folder at a given path.
            /// </summary>
            /// <param name="path">The path of the texture.</param>
            /// <param name="texture">The texture to add.</param>
            public void AddTextureAtPath(string path, Texture2D texture)
            {
                var folder = GetOrCreateFolderForAssetPath(path);
                folder.m_Textures.Add((path, texture));
            }

            /// <summary>
            /// Get the Folder object which corresponds to the given path.
            /// If this is the first asset encountered for a given folder, create a chain of folder objects
            /// rooted with this one and return the folder at the end of that chain.
            /// Every time a folder is accessed, its Count property is incremented to indicate that it contains one
            /// more solid color texture.
            /// </summary>
            /// <param name="path">Path to a solid color texture relative to this folder.</param>
            /// <returns>The folder object corresponding to the folder containing the texture at the given path.</returns>
            Folder GetOrCreateFolderForAssetPath(string path)
            {
                var directories = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var folder = this;
                folder.Count++;
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
                    folder.Count++;
                }

                return folder;
            }

            /// <summary>
            /// Draw GUI for this Folder.
            /// </summary>
            /// <param name="name">The name of the folder.</param>
            public void Draw(string name)
            {
                var wasVisible = m_Visible;
                using (new GUILayout.HorizontalScope())
                {
                    m_Visible = EditorGUILayout.Foldout(m_Visible, $"{name}: {Count}", true);
                    if (GUILayout.Button(k_ShrinkAllGUIContent, k_ShrinkAllWidth))
                        ShrinkAndFinalize();
                }

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
                        kvp.Value.Draw(kvp.Key);
                    }

                    foreach (var (_, texture) in m_Textures)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField(texture.name, texture, typeof(Texture2D), false);
                            if (GUILayout.Button(k_ShrinkGUIContent))
                                ShrinkAndFinalize();
                        }
                    }

                    if (m_Textures.Count > 0)
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
            /// Shrink all textures in this folder and subfolders, and refresh the AssetDatabase on completion.
            /// </summary>
            void ShrinkAndFinalize()
            {
                try
                {
                    AssetDatabase.StartAssetEditing();
                    ShrinkAllTexturesRecursively();
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                }
            }

            /// <summary>
            /// Shrink all textures in this folder and subfolders.
            /// </summary>
            void ShrinkAllTexturesRecursively()
            {
                foreach (var (path, _) in m_Textures)
                {
                    var importer = AssetImporter.GetAtPath(path);
                    if (importer == null)
                    {
                        Debug.LogWarning($"Could not get asset importer for {path}");
                        continue;
                    }

                    if (!(importer is TextureImporter textureImporter))
                        continue;

                    textureImporter.maxTextureSize = k_ShrunkTextureSize;
                    textureImporter.SaveAndReimport();
                }

                foreach (var kvp in m_Subfolders)
                {
                    kvp.Value.ShrinkAllTexturesRecursively();
                }
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
                m_Textures.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                foreach (var kvp in m_Subfolders)
                {
                    kvp.Value.SortContentsRecursively();
                }
            }
        }

        /// <summary>
        /// Container for unique color rows.
        /// </summary>
        class ColorRow
        {
            public bool expanded;
            public readonly List<Texture2D> textures = new List<Texture2D>();
        }

        static class Styles
        {
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

        const string k_MenuItemName = "Window/SuperScience/Solid Color Textures";
        const string k_WindowTitle = "Solid Color Textures";
        const string k_NoMissingReferences = "No solid color textures";
        const string k_ProjectFolderName = "Project";
        const int k_TextureColumnWidth = 150;
        const int k_ColorPanelWidth = 150;
        const string k_Instructions = "Click the Scan button to scan your project for solid color textures. WARNING: " +
            "This will load every texture in your project. For large projects, this may take a long time and/or crash the Editor.";
        const string k_ScanFilter = "t:Texture2D";
        const int k_ProgressBarHeight = 15;
        const int k_MaxScanUpdateTimeMilliseconds = 50;

        static readonly GUIContent k_ScanGUIContent = new GUIContent("Scan", "Scan the project for solid color textures");
        static readonly GUIContent k_CancelGUIContent = new GUIContent("Cancel", "Cancel the current scan");

        static readonly GUILayoutOption k_ColorPanelWidthOption = GUILayout.Width(k_ColorPanelWidth);
        static readonly GUILayoutOption k_ColorSwatchWidthOption = GUILayout.Width(30);

        static readonly Vector2 k_MinSize = new Vector2(400, 200);

        static readonly Stopwatch k_StopWatch = new Stopwatch();

        Vector2 m_ColorListScrollPosition;
        Vector2 m_FolderTreeScrollPosition;
        readonly Folder m_ParentFolder = new Folder();
        readonly SortedDictionary<int, ColorRow> m_TexturesByColor = new SortedDictionary<int, ColorRow>();
        static readonly string[] k_ScanFolders = {"Assets", "Packages"};
        int m_ScanCount;
        int m_ScanProgress;
        IEnumerator m_ScanEnumerator;

        /// <summary>
        /// Initialize the window
        /// </summary>
        [MenuItem(k_MenuItemName)]
        static void Init() { GetWindow<SolidColorTextures>(k_WindowTitle).Show(); }

        void OnEnable()
        {
            minSize = k_MinSize;
            m_ScanCount = 0;
            m_ScanProgress = 0;
        }

        void OnDisable() { m_ScanEnumerator = null; }

        void OnGUI()
        {
            EditorGUIUtility.labelWidth = position.width - k_TextureColumnWidth - k_ColorPanelWidth;

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

            if (m_ParentFolder.Count == 0)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
                return;
            }

            if (m_ParentFolder.Count == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope(k_ColorPanelWidthOption))
                    {
                        DrawColors();
                    }

                    using (var scrollView = new GUILayout.ScrollViewScope(m_FolderTreeScrollPosition))
                    {
                        m_FolderTreeScrollPosition = scrollView.scrollPosition;
                        m_ParentFolder.Draw(k_ProjectFolderName);
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
        /// Draw a list of unique colors.
        /// </summary>
        void DrawColors()
        {
            GUILayout.Label($"{m_TexturesByColor.Count} Unique Colors");
            using (var scrollView = new GUILayout.ScrollViewScope(m_ColorListScrollPosition))
            {
                m_ColorListScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in m_TexturesByColor)
                {
                    var color = Color32ToInt.Convert(kvp.Key);
                    var row = kvp.Value;
                    var textures = row.textures;

                    using (new GUILayout.HorizontalScope())
                    {
                        row.expanded = EditorGUILayout.Foldout(row.expanded, $"{textures.Count} Texture(s)", true);
                        EditorGUILayout.ColorField(new GUIContent(), color, false, true, false, k_ColorSwatchWidthOption);
                    }

                    if (row.expanded)
                    {
                        foreach (var texture in row.textures)
                        {
                            EditorGUILayout.ObjectField(texture, typeof(Texture2D), true);
                        }
                    }
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
        /// <param name="textureAssets">Texture assets to scan.</param>
        /// <returns>IEnumerator used to run the coroutine.</returns>
        IEnumerator ProcessScan(Dictionary<string, Texture2D> textureAssets)
        {
            m_ScanCount = textureAssets.Count;
            m_ScanProgress = 0;

            // We will have to repeat the scan multiple times because the preview utility works asynchronously
            while (m_ScanProgress < m_ScanCount)
            {
                var remainingTextureAssets = new Dictionary<string, Texture2D>(textureAssets);
                foreach (var kvp in remainingTextureAssets)
                {
                    var path = kvp.Key;
                    var textureAsset = kvp.Value;
                    var texture = textureAsset;

                    // For non-readable textures, get a preview texture which is readable
                    // TODO: get a full-size preview; AssetPreview.GetAssetPreview returns a 128x128 texture
                    if (!textureAsset.isReadable)
                    {
                        texture = AssetPreview.GetAssetPreview(textureAsset);
                        if (texture == null)
                            continue;
                    }

                    CheckForSolidColorTexture(path, texture, textureAsset);
                    m_ScanProgress++;
                    textureAssets.Remove(path);
                    yield return null;
                }
            }

            m_ScanEnumerator = null;
            EditorApplication.update -= UpdateScan;
        }

        /// <summary>
        /// Scan the project for solid color textures and populate the data structures for UI.
        /// </summary>
        void Scan()
        {
            var guids = AssetDatabase.FindAssets(k_ScanFilter, k_ScanFolders);
            if (guids == null || guids.Length == 0)
                return;

            m_TexturesByColor.Clear();
            m_ParentFolder.Clear();

            var textureAssets = new Dictionary<string, Texture2D>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.Log($"Could not convert {guid} to path");
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null)
                {
                    Debug.LogWarning($"Could not load texture at {path}");
                    continue;
                }

                // Skip non-2D textures (which don't support GetPixels)
                if (!(texture is Texture2D texture2D))
                    continue;

                // Skip textures which are child assets (fonts, embedded textures, etc.)
                if (!AssetDatabase.IsMainAsset(texture))
                    continue;

                textureAssets.Add(path, texture2D);
            }

            m_ScanEnumerator = ProcessScan(textureAssets);
            EditorApplication.update += UpdateScan;
        }

        /// <summary>
        /// Add a texture to UI data structures if is comprised of a single solid color.
        /// </summary>
        /// <param name="path">The path to the texture asset.</param>
        /// <param name="texture">The texture to test. This is different from <paramref name="textureAsset"/> because the original asset may not be readable.</param>
        /// <param name="textureAsset">The texture asset loaded from the AssetDatabase.</param>
        void CheckForSolidColorTexture(string path, Texture2D texture, Texture2D textureAsset)
        {
            if (IsSolidColorTexture(texture, out var colorValue))
            {
                m_ParentFolder.AddTextureAtPath(path, textureAsset);
                GetOrCreateRowForColor(colorValue).textures.Add(textureAsset);
            }
        }

        /// <summary>
        /// Check if a texture is comprised of a single solid color.
        /// </summary>
        /// <param name="texture">The texture to check.</param>
        /// <param name="colorValue">The color of the texture, converted to an int.</param>
        /// <returns>True if the texture is a single solid color.</returns>
        static bool IsSolidColorTexture(Texture2D texture, out int colorValue)
        {
            // Skip "degenerate" textures like font atlases
            if (texture.width == 0 || texture.height == 0)
            {
                colorValue = default;
                return false;
            }

            var pixels = texture.GetPixels();

            // It is unlikely to get a null pixels array, but we should check just in case
            if (pixels == null)
            {
                Debug.LogWarning($"Could not read {texture}");
                colorValue = default;
                return false;
            }

            // It is unlikely, but possible that we got this far and there are no pixels.
            var pixelCount = pixels.Length;
            if (pixelCount == 0)
            {
                Debug.LogWarning($"No pixels in {texture}");
                colorValue = default;
                return false;
            }

            // Convert to int for faster comparison
            colorValue = Color32ToInt.Convert(pixels[0]);
            var isSolidColor = true;
            for (var i = 1; i < pixelCount; i++)
            {
                var pixel = Color32ToInt.Convert(pixels[i]);
                if (pixel != colorValue)
                {
                    isSolidColor = false;
                    break;
                }
            }

            return isSolidColor;
        }

        /// <summary>
        /// Get or create a <see cref="ColorRow"/> for a given color value.
        /// </summary>
        /// <param name="colorValue">The color value to use for this row.</param>
        /// <returns>The color row for the color value.</returns>
        ColorRow GetOrCreateRowForColor(int colorValue)
        {
            if (m_TexturesByColor.TryGetValue(colorValue, out var row))
                return row;

            row = new ColorRow();
            m_TexturesByColor[colorValue] = row;
            return row;
        }
    }
}
