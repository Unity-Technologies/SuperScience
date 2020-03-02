using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    class OrphanedAssets : EditorWindow
    {
        class AssetGroup
        {
            readonly HashSet<string> m_Guids = new HashSet<string>();

            public string header { get; set; }
            public Type type { get; set; }
            public bool foldout { get; set; }

            public HashSet<string> guids
            {
                get { return m_Guids; }
            }
        }

        const int k_FrameTimeTicks = 50 * 10000; // 50ms in "ticks" which are 100ns

        static readonly string[] k_SearchFolders = { "Assets" };
        static readonly string[] k_ExcludePaths = { "ignored", "libs", "Resources" };

        readonly AssetGroup m_OrphanedShaders = new AssetGroup { header = "Orphaned Shaders", type = typeof(Shader) };
        readonly AssetGroup m_OrphanedMaterials = new AssetGroup { header = "Orphaned Materials", type = typeof(Material) };
        readonly AssetGroup m_OrphanedTextures = new AssetGroup { header = "Orphaned Textures", type = typeof(Texture) };
        readonly AssetGroup m_OrphanedPrefabs = new AssetGroup { header = "Orphaned Prefabs", type = typeof(GameObject) };
        readonly AssetGroup m_OrphanedScenes = new AssetGroup { header = "Orphaned Scenes", type = typeof(SceneAsset) };
        readonly AssetGroup m_OrphanedGUISkins = new AssetGroup { header = "Orphaned GUI Skins", type = typeof(GUISkin) };

        readonly HashSet<string> m_References = new HashSet<string>();
        readonly List<AssetGroup> m_AssetGroups = new List<AssetGroup>();

        Vector2 m_Scroll;
        IEnumerator m_Update;
        readonly Stopwatch m_FrameTimer = new Stopwatch();

        [MenuItem("Window/Orphaned Assets")]
        static void Init()
        {
            var window = GetWindow<OrphanedAssets>();
            window.Show();
        }

        void OnEnable()
        {
            m_AssetGroups.Clear();
            m_AssetGroups.Add(m_OrphanedShaders);
            m_AssetGroups.Add(m_OrphanedMaterials);
            m_AssetGroups.Add(m_OrphanedTextures);
            m_AssetGroups.Add(m_OrphanedPrefabs);
            m_AssetGroups.Add(m_OrphanedScenes);
            m_AssetGroups.Add(m_OrphanedGUISkins);

            RunFindOrphanedAssets();

#if UNITY_2018_1_OR_NEWER
            EditorApplication.projectChanged += RunFindOrphanedAssets;
#else
        EditorApplication.projectWindowChanged += RunFindOrphanedAssets;
#endif

            EditorApplication.update += UpdateEnumerator;
        }

        void OnDisable()
        {
#if UNITY_2018_1_OR_NEWER
            EditorApplication.projectChanged -= RunFindOrphanedAssets;
#else
        EditorApplication.projectWindowChanged -= RunFindOrphanedAssets;
#endif
            EditorApplication.update -= UpdateEnumerator;
        }

        void UpdateEnumerator()
        {
            if (m_Update != null)
            {
                var hasNext = true;
                var oldUpdate = m_Update;
                while (hasNext)
                {
                    if (m_FrameTimer.ElapsedTicks > k_FrameTimeTicks)
                        break;

                    hasNext = m_Update.MoveNext();
                }

                //Check if update is equal to the old one in case we start a new coroutine right as the last one ends
                if (!hasNext && m_Update == oldUpdate)
                    m_Update = null;

                Repaint();
            }

            m_FrameTimer.Reset();
            m_FrameTimer.Start();
        }

        void RunFindOrphanedAssets()
        {
            m_Update = FindOrphanedAssets();
        }

        /// <summary>
        /// Find orphaned assets and put them into lists which will be displayed in the GUI
        /// The approach here is to first collect all assets of a given type within the search folders, and then check each
        /// one for assets it might reference, while also excluding specified exclude paths
        /// </summary>
        IEnumerator FindOrphanedAssets()
        {
            m_References.Clear();
            var shaders = m_OrphanedShaders.guids;
            var materials = m_OrphanedMaterials.guids;
            var textures = m_OrphanedTextures.guids;
            var prefabs = m_OrphanedPrefabs.guids;
            var scenes = m_OrphanedScenes.guids;
            var guiSkins = m_OrphanedGUISkins.guids;

            shaders.Clear();
            materials.Clear();
            textures.Clear();
            prefabs.Clear();
            scenes.Clear();
            guiSkins.Clear();
            shaders.UnionWith(AssetDatabase.FindAssets("t:shader", k_SearchFolders));
            materials.UnionWith(AssetDatabase.FindAssets("t:material", k_SearchFolders));
            textures.UnionWith(AssetDatabase.FindAssets("t:texture", k_SearchFolders));
            prefabs.UnionWith(AssetDatabase.FindAssets("t:prefab", k_SearchFolders));
            scenes.UnionWith(AssetDatabase.FindAssets("t:scene", k_SearchFolders));
            guiSkins.UnionWith(AssetDatabase.FindAssets("t:guiskin", k_SearchFolders));

            foreach (var guid in materials)
            {
                var materialPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(materialPath))
                {
                    m_References.Add(guid);
                    continue;
                }

                CheckDependencies(materialPath);

                if (materialPath.Contains(".ttf") || ExcludePath(materialPath))
                    m_References.Add(guid);

                yield return null;
            }

            foreach (var guid in shaders)
            {
                var shaderPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!shaderPath.Contains(".shader"))
                {
                    m_References.Add(guid);
                    continue;
                }

                CheckDependencies(shaderPath);

                if (ExcludePath(shaderPath))
                    m_References.Add(guid);

                yield return null;
            }

            foreach (var guid in prefabs)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                CheckDependencies(prefabPath);

                if (ExcludePath(prefabPath))
                    m_References.Add(guid);

                yield return null;
            }

            var scripts = AssetDatabase.FindAssets("t:script", k_SearchFolders);
            foreach (var guid in scripts)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                CheckDependencies(scriptPath);

                yield return null;
            }

            var buildScenes = EditorBuildSettings.scenes.Select(scene => scene.guid.ToString());
            foreach (var guid in scenes)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                CheckDependencies(scenePath);

                // If the scene is included in the build, we consider it referenced, so remove it from the list
                if (buildScenes.Contains(guid))
                    m_References.Add(guid);

                yield return null;
            }

            foreach (var guid in textures)
            {
                var texturePath = AssetDatabase.GUIDToAssetPath(guid);
                CheckDependencies(texturePath);

                if (ExcludePath(texturePath))
                    m_References.Add(guid);

                yield return null;
            }

            foreach (var guid in guiSkins)
            {
                var skinPath = AssetDatabase.GUIDToAssetPath(guid);
                CheckDependencies(skinPath);

                if (ExcludePath(skinPath))
                    m_References.Add(guid);

                yield return null;
            }

            foreach (var assetGroup in m_AssetGroups)
            {
                assetGroup.guids.ExceptWith(m_References);
            }
        }

        void CheckDependencies(string assetPath)
        {
            var dependencies = AssetDatabase.GetDependencies(assetPath);
            foreach (var dependency in dependencies)
            {
                if (dependency == assetPath)
                    continue;

                var guid = AssetDatabase.AssetPathToGUID(dependency);
                m_References.Add(guid);
            }
        }

        static bool ExcludePath(string assetPath)
        {
            foreach (var path in k_ExcludePaths)
            {
                if (assetPath.Contains(path) || assetPath.Contains(path))
                    return true;
            }

            return false;
        }

        void OnGUI()
        {
            //Ctrl + w to close
            var current = Event.current;
            if (current.Equals(Event.KeyboardEvent("^w")))
            {
                Close();
                current.Use();
                GUIUtility.ExitGUI();
            }

            if (m_Update != null)
            {
                GUILayout.Label("Loading...");
                GUIUtility.ExitGUI();
                return;
            }

            if (GUILayout.Button("Refresh"))
                RunFindOrphanedAssets();

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            foreach (var assetGroup in m_AssetGroups)
            {
                OrphanedAssetGUI(assetGroup);
            }

            EditorGUILayout.EndScrollView();
        }

        static void OrphanedAssetGUI(AssetGroup group)
        {
            var header = group.header;
            var guids = group.guids;
            var foldout = group.foldout;
            foldout = EditorGUILayout.Foldout(foldout, string.Format("{0} - {1}", header, guids.Count), true);
            group.foldout = foldout;

            if (!foldout)
                return;

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(15);
                using (new GUILayout.VerticalScope())
                {
                    var type = group.type;
                    foreach (var orphanedAsset in guids)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(orphanedAsset), type);
                        EditorGUILayout.ObjectField(asset.name, asset, type, false);
                    }
                }
            }
        }
    }
}
