using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans the project for serialized references to missing (deleted) assets and displays the results in an EditorWindow
    /// </summary>
    sealed class MissingProjectReferences : MissingReferencesWindow
    {
        class Folder
        {
            class AssetContainer
            {
                readonly UnityObject m_Object;
                public readonly List<SerializedProperty> PropertiesWithMissingReferences = new List<SerializedProperty>();

                public UnityObject UnityObject { get { return m_Object; } }

                public AssetContainer(UnityObject unityObject, MissingReferencesWindow window)
                {
                    m_Object = unityObject;
                    window.CheckForMissingRefs(unityObject, PropertiesWithMissingReferences);
                }

                public void Draw()
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var property in PropertiesWithMissingReferences)
                        {
                            switch (property.propertyType)
                            {
                                case SerializedPropertyType.Generic:
                                    EditorGUILayout.LabelField(string.Format("Missing Method: {0}", property.propertyPath));
                                    break;
                                case SerializedPropertyType.ObjectReference:
                                    EditorGUILayout.PropertyField(property, new GUIContent(property.propertyPath));
                                    break;
                            }
                        }
                    }
                }
            }

            readonly Dictionary<string, Folder> m_Subfolders = new Dictionary<string, Folder>();
            readonly List<GameObjectContainer> m_Prefabs = new List<GameObjectContainer>();
            readonly List<AssetContainer> m_Assets = new List<AssetContainer>();
            bool m_Visible;

            public int Count;

            public void Clear()
            {
                m_Subfolders.Clear();
                m_Prefabs.Clear();
                m_Assets.Clear();
                Count = 0;
            }

            public void Add(string path, MissingReferencesWindow window)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Model)
                        return;

                    var gameObjectContainer = new GameObjectContainer(prefab, window);
                    if (gameObjectContainer.Count > 0)
                        GetSubFolder(path).m_Prefabs.Add(gameObjectContainer);
                }
                else
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
                    var assetContainer = new AssetContainer(asset, window);
                    if (assetContainer.PropertiesWithMissingReferences.Count > 0)
                        GetSubFolder(path).m_Assets.Add(assetContainer);
                }
            }

            Folder GetSubFolder(string path)
            {
                var directories = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var folder = this;
                folder.Count++;
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
                    folder.Count++;
                }

                return folder;
            }

            public void Draw(MissingReferencesWindow window, string name)
            {
                var wasVisible = m_Visible;
                m_Visible = EditorGUILayout.Foldout(m_Visible, string.Format("{0}: {1}", name, Count));
                if (m_Visible != wasVisible && Event.current.alt)
                    SetVisibleRecursively(m_Visible);

                if (!m_Visible)
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var kvp in m_Subfolders)
                    {
                        kvp.Value.Draw(window, kvp.Key);
                    }

                    foreach (var prefab in m_Prefabs)
                    {
                        EditorGUILayout.ObjectField(prefab.GameObject, typeof(GameObject), false);
                        prefab.Draw(window, prefab.GameObject.name);
                    }

                    foreach (var asset in m_Assets)
                    {
                        EditorGUILayout.ObjectField(asset.UnityObject, typeof(UnityObject), false);
                        asset.Draw();
                    }
                }
            }

            void SetVisibleRecursively(bool visible)
            {
                m_Visible = visible;
                foreach (var prefab in m_Prefabs)
                {
                    prefab.SetVisibleRecursively(visible);
                }

                foreach (var kvp in m_Subfolders)
                {
                    kvp.Value.SetVisibleRecursively(visible);
                }
            }
        }

        const string k_Instructions = "Click the Refresh button to scan your project for missing references. WARNING: " +
            "This will load every asset in your project. For large projects, this may take a long time and/or crash the Editor.";

        const string k_NoMissingReferences = "No missing references in project";

        // Bool fields will be serialized to maintain state between domain reloads, but our list of GameObjects will not
        [NonSerialized]
        bool m_Scanned;

        Vector2 m_ScrollPosition;
        readonly Folder m_ParentFolder = new Folder();

        [MenuItem("Window/SuperScience/Missing Project References")]
        static void OnMenuItem() { GetWindow<MissingProjectReferences>("Missing Project References"); }

        protected override void Clear()
        {
            m_ParentFolder.Clear();
        }

        /// <summary>
        /// Load all assets in the AssetDatabase and check them for missing serialized references
        /// </summary>
        protected override void Scan()
        {
            base.Scan();
            m_Scanned = true;
            m_ParentFolder.Clear();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (Path.IsPathRooted(path))
                    continue;

                m_ParentFolder.Add(path, this);
            }
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!m_Scanned)
            {
                EditorGUILayout.HelpBox(k_Instructions, MessageType.Info);
                GUIUtility.ExitGUI();
            }

            if (m_ParentFolder.Count == 0)
            {
                GUILayout.Label(k_NoMissingReferences);
            }
            else
            {
                using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
                {
                    m_ScrollPosition = scrollView.scrollPosition;
                    m_ParentFolder.Draw(this, "Project");
                }
            }
        }
    }
}
