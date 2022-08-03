using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans all loaded scenes for GameObjects which use non-default layers and displays the results in an EditorWindow.
    /// </summary>
    public class SceneLayerUsers : EditorWindow
    {
        class SceneContainer
        {
            const string k_UntitledSceneName = "Untitled";

            readonly string m_SceneName;
            readonly List<GameObjectContainer> m_Roots;
            readonly int m_TotalCount;
            readonly int m_TotalWithoutLayerMasks;
            readonly SortedDictionary<int, int> m_CountPerLayer;
            readonly SortedDictionary<int, int> m_CountWithoutLayerMasksPerLayer;
            readonly int[] m_Usages;
            readonly int[] m_UsagesWithoutLayerMasks;

            bool m_Expanded;

            SceneContainer(string sceneName, List<GameObjectContainer> roots)
            {
                m_SceneName = sceneName;
                m_Roots = roots;
                m_CountPerLayer = new SortedDictionary<int, int>();
                m_CountWithoutLayerMasksPerLayer = new SortedDictionary<int, int>();
                foreach (var container in roots)
                {
                    var layer = container.GameObject.layer;
                    if (layer != 0)
                    {
                        m_TotalCount++;
                        IncrementCountForLayer(layer, m_CountPerLayer);
                        IncrementCountForLayer(layer, m_CountWithoutLayerMasksPerLayer);
                    }

                    m_TotalCount += container.TotalUsagesInChildren;
                    m_TotalCount += container.TotalUsagesInComponents;
                    m_TotalWithoutLayerMasks += container.TotalUsagesInChildrenWithoutLayerMasks;
                    AggregateCountPerLayer(container.UsagesInChildrenPerLayer, m_CountPerLayer);
                    AggregateCountPerLayer(container.UsagesInComponentsPerLayer, m_CountPerLayer);
                    AggregateCountPerLayer(container.UsagesInChildrenWithoutLayerMasksPerLayer, m_CountWithoutLayerMasksPerLayer);
                }

                m_Usages = m_CountPerLayer.Keys.ToArray();
                m_UsagesWithoutLayerMasks = m_CountPerLayer.Keys.ToArray();
            }

            public void Draw(Dictionary<int, string> layerToName, int layerFilter, bool includeLayerMaskFields)
            {
                var count = GetCount(layerFilter, includeLayerMaskFields);
                if (count == 0)
                    return;

                k_StringBuilder.Length = 0;
                k_StringBuilder.Append(m_SceneName);
                k_StringBuilder.Append(" (");
                k_StringBuilder.Append(count.ToString());
                k_StringBuilder.Append(") {");
                AppendLayerNameList(k_StringBuilder, m_Usages, m_UsagesWithoutLayerMasks, layerToName, layerFilter, includeLayerMaskFields);
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
                        gameObjectContainer.Draw(layerToName, layerFilter, includeLayerMaskFields);
                    }
                }
            }

            public static SceneContainer CreateIfNecessary(Scene scene, SortedDictionary<int, FilterRow> filterRows,
                Dictionary<int, string> layerToName, int layerWithNoName)
            {
                var sceneName = scene.name;
                if (string.IsNullOrEmpty(sceneName))
                    sceneName = k_UntitledSceneName;

                List<GameObjectContainer> roots = null;
                foreach (var gameObject in scene.GetRootGameObjects())
                {
                    var rootContainer = GameObjectContainer.CreateIfNecessary(gameObject, filterRows, layerToName, layerWithNoName);
                    if (rootContainer != null)
                    {
                        roots ??= new List<GameObjectContainer>();
                        roots.Add(rootContainer);
                    }
                }

                return roots != null ? new SceneContainer(sceneName, roots) : null;
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
                    m_CountPerLayer.TryGetValue(layerFilter, out var totalCount);
                    return totalCount;
                }

                m_CountWithoutLayerMasksPerLayer.TryGetValue(layerFilter, out var totalWithoutLayerMasks);
                return totalWithoutLayerMasks;
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
            /// <summary>
            /// Container for component scan results. Just as with GameObjectContainer, we initialize one of these
            /// using a component to scan it for missing references and retain the results
            /// </summary>
            internal class ComponentContainer
            {
                readonly Component m_Component;
                readonly List<SerializedProperty> m_LayerMaskFields;
                readonly SortedDictionary<int, int> m_UsagesPerLayer;
                readonly int[] m_Usages;

                bool m_Expanded;
                public int Count => m_LayerMaskFields.Count;
                public SortedDictionary<int, int> UsagesPerLayer => m_UsagesPerLayer;
                public bool Expanded { set { m_Expanded = value; } }

                ComponentContainer(Component component, SortedDictionary<int, int> usagesPerLayer, List<SerializedProperty> layerMaskFields)
                {
                    m_Component = component;
                    m_UsagesPerLayer = usagesPerLayer;
                    m_LayerMaskFields = layerMaskFields;
                    m_Usages = m_UsagesPerLayer.Keys.ToArray();
                }

                /// <summary>
                /// Draw the missing references UI for this component
                /// </summary>
                public void Draw(Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer)
                {
                    if (layerFilter != k_InvalidLayer && !m_UsagesPerLayer.ContainsKey(layerFilter))
                        return;

                    // Because we can potentially draw a lot of rows, the efficiency using a StringBuilder is worth the messy code
                    k_StringBuilder.Length = 0;
                    k_StringBuilder.Append(m_Component.GetType().Name);
                    k_StringBuilder.Append(" {");
                    k_StringBuilder.Append(m_LayerMaskFields.Count.ToString());
                    k_StringBuilder.Append(") {");
                    AppendLayerNameList(k_StringBuilder, m_Usages, layerToName, layerFilter);
                    k_StringBuilder.Append("}");
                    var label = k_StringBuilder.ToString();
                    m_Expanded = EditorGUILayout.Foldout(m_Expanded, label, true);
                    if (!m_Expanded)
                        return;

                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.ObjectField(m_Component, typeof(Component), true);
                        foreach (var property in m_LayerMaskFields)
                        {
                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                EditorGUILayout.PropertyField(property);
                                if (check.changed)
                                    property.serializedObject.ApplyModifiedProperties();
                            }
                        }
                    }
                }

                /// <summary>
                /// Initialize a ComponentContainer to represent the given Component
                /// This will scan the component for missing references and retain the information for display in
                /// the given window.
                /// </summary>
                /// <param name="component">The Component to scan for missing references</param>
                /// <param name="layerToName">Layer to Name dictionary for fast lookup of layer names.</param>
                /// <param name="layerWithNoName">An unnamed layer to use for layer mask checks.</param>
                public static ComponentContainer CreateIfNecessary(Component component, Dictionary<int, string> layerToName, int layerWithNoName)
                {
                    var serializedObject = new SerializedObject(component);
                    var iterator = serializedObject.GetIterator();
                    SortedDictionary<int, int> usagesPerLayer = null;
                    List<SerializedProperty> layerMaskFields = null;
                    while (iterator.Next(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.LayerMask)
                            continue;

                        var layerMask = iterator.intValue;
                        usagesPerLayer = GetLayersFromLayerMask(usagesPerLayer, layerToName, layerWithNoName, layerMask);
                        layerMaskFields ??= new List<SerializedProperty>();
                        layerMaskFields.Add(iterator.Copy());
                    }

                    return usagesPerLayer != null ? new ComponentContainer(component, usagesPerLayer, layerMaskFields) : null;
                }

                static SortedDictionary<int, int> GetLayersFromLayerMask(SortedDictionary<int, int> usagesPerLayer,
                    Dictionary<int, string> layerToName, int layerWithNoName, int layerMask)
                {
                    // If all layers are named, it is not possible to infer whether layer mask fields "use" a layer
                    if (layerWithNoName == k_InvalidLayer)
                        return null;

                    // Exclude the special cases where every layer is included or excluded
                    if (layerMask == k_InvalidLayer || layerMask == 0)
                        return null;

                    // Depending on whether or not the mask started out as "Everything" or "Nothing", a layer will count as "used" when the user toggles its state.
                    // We use the layer without a name to check whether or not the starting point is "Everything" or "Nothing." If this layer's bit is 0, we assume
                    // the mask started with "Nothing." Otherwise, if its bit is 1, we assume the mask started with "Everything."
                    var defaultValue = (layerMask & 1 << layerWithNoName) != 0;

                    foreach (var kvp in layerToName)
                    {
                        var layer = kvp.Key;

                        // Skip layer 0 since we only want non-default layers
                        if (layer == 0)
                            continue;

                        // We compare (using xor) this layer's bit value with the default value. If they are different, the layer counts as "used."
                        if ((layerMask & (1 << layer)) != 0 ^ defaultValue)
                        {
                            usagesPerLayer ??= new SortedDictionary<int, int>();
                            IncrementCountForLayer(layer, usagesPerLayer);
                        }
                    }

                    return usagesPerLayer;
                }
            }

            readonly GameObject m_GameObject;
            readonly List<GameObjectContainer> m_Children;
            readonly List<ComponentContainer> m_Components;

            readonly int m_TotalUsagesInComponents;
            readonly SortedDictionary<int, int> m_UsagesInComponentsPerLayer;

            //TODO: Rename Users -> Usages
            readonly int m_TotalUsagesInChildren;
            readonly int m_TotalUsagesInChildrenWithoutLayerMasks;
            readonly SortedDictionary<int, int> m_UsagesInChildrenPerLayer;
            readonly SortedDictionary<int, int> m_UsagesInChildrenWithoutLayerMasksPerLayer;

            readonly int[] m_Usages;
            readonly int[] m_UsagesWithoutLayerMasks;
            readonly int[] m_UsagesInComponents;
            readonly int[] m_UsagesInChildren;
            readonly int[] m_UsagesInChildrenWithoutLayerMasks;

            bool m_Expanded;
            bool m_ShowComponents;
            bool m_ShowChildren;

            public GameObject GameObject { get { return m_GameObject; } }
            public int TotalUsagesInComponents => m_TotalUsagesInComponents;
            public SortedDictionary<int, int> UsagesInComponentsPerLayer => m_UsagesInComponentsPerLayer;
            public int TotalUsagesInChildren => m_TotalUsagesInChildren;
            public int TotalUsagesInChildrenWithoutLayerMasks => m_TotalUsagesInChildrenWithoutLayerMasks;
            public SortedDictionary<int, int> UsagesInChildrenPerLayer => m_UsagesInChildrenPerLayer;
            public SortedDictionary<int, int> UsagesInChildrenWithoutLayerMasksPerLayer => m_UsagesInChildrenWithoutLayerMasksPerLayer;

            // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
            static readonly SortedSet<int> k_Usages = new SortedSet<int>();
            static readonly SortedSet<int> k_UsagesWithoutLayerMasks = new SortedSet<int>();

            GameObjectContainer(GameObject gameObject, List<ComponentContainer> components, List<GameObjectContainer> children)
            {
                m_GameObject = gameObject;
                m_Components = components;
                m_Children = children;
                k_Usages.Clear();
                k_UsagesWithoutLayerMasks.Clear();

                var layer = gameObject.layer;
                if (layer != 0)
                {
                    k_Usages.Add(layer);
                    k_UsagesWithoutLayerMasks.Add(layer);
                }

                if (components != null)
                {
                    m_UsagesInComponentsPerLayer = new SortedDictionary<int, int>();
                    foreach (var container in components)
                    {
                        AggregateCount(container, ref m_TotalUsagesInComponents);
                    }

                    m_UsagesInComponents = m_UsagesInComponentsPerLayer.Keys.ToArray();
                    k_Usages.UnionWith(m_UsagesInComponents);
                }

                if (children != null)
                {
                    m_UsagesInChildrenPerLayer = new SortedDictionary<int, int>();
                    m_UsagesInChildrenWithoutLayerMasksPerLayer = new SortedDictionary<int, int>();
                    foreach (var container in children)
                    {
                        var componentUsages = container.m_UsagesInComponentsPerLayer;
                        if (componentUsages != null)
                            m_UsagesInComponentsPerLayer ??= new SortedDictionary<int, int>();

                        AggregateCount(container, ref m_TotalUsagesInChildren, ref m_TotalUsagesInChildrenWithoutLayerMasks);
                    }

                    m_UsagesInChildren = m_UsagesInChildrenPerLayer.Keys.ToArray();
                    m_UsagesInChildrenWithoutLayerMasks = m_UsagesInChildrenWithoutLayerMasksPerLayer.Keys.ToArray();
                    k_Usages.UnionWith(m_UsagesInChildren);
                    k_UsagesWithoutLayerMasks.UnionWith(m_UsagesInChildrenWithoutLayerMasks);
                }

                m_Usages = k_Usages.ToArray();
                m_UsagesWithoutLayerMasks = k_UsagesWithoutLayerMasks.ToArray();
            }

            /// <summary>
            /// Initialize a GameObjectContainer to represent the given GameObject
            /// This will scan the component for missing references and retain the information for display in
            /// the given window.
            /// </summary>
            /// <param name="gameObject">The GameObject to scan for missing references</param>
            /// <param name="filterRows">Dictionary of FilterRow objects for counting usages per layer.</param>
            /// <param name="layerToName">Layer to Name dictionary for fast lookup of layer names.</param>
            /// <param name="layerWithNoName">An unnamed layer to use for layer mask checks.</param>
            public static GameObjectContainer CreateIfNecessary(GameObject gameObject, SortedDictionary<int, FilterRow> filterRows,
                Dictionary<int, string> layerToName, int layerWithNoName)
            {
                // GetComponents will clear the list, so we don't havWe to
                gameObject.GetComponents(k_Components);
                List<ComponentContainer> components = null;
                foreach (var component in k_Components)
                {
                    var container = ComponentContainer.CreateIfNecessary(component, layerToName, layerWithNoName);
                    if (container != null)
                    {
                        components ??= new List<ComponentContainer>();
                        components.Add(container);
                    }
                }

                // Clear the list after we're done to avoid lingering references
                k_Components.Clear();

                List<GameObjectContainer> children = null;
                foreach (Transform child in gameObject.transform)
                {
                    var childContainer = CreateIfNecessary(child.gameObject, filterRows, layerToName, layerWithNoName);
                    if (childContainer != null)
                    {
                        children ??= new List<GameObjectContainer>();
                        children.Add(childContainer);
                    }
                }

                var layer = gameObject.layer;
                var isLayerUser = layer != 0;
                if (isLayerUser || components != null || children != null)
                {
                    if (isLayerUser)
                    {
                        var filterRow = GetOrCreateFilterRowForLayer(filterRows, layer);
                        filterRow.UsersWithoutLayerMasks.Add(gameObject);
                        filterRow.AllUsers.Add(gameObject);
                    }

                    var newContainer = new GameObjectContainer(gameObject, components, children);
                    if (components != null)
                    {
                        foreach (var kvp in newContainer.m_UsagesInComponentsPerLayer)
                        {
                            var filterRow = GetOrCreateFilterRowForLayer(filterRows, kvp.Key);
                            filterRow.AllUsers.Add(gameObject);
                        }
                    }

                    return newContainer;
                }

                return null;
            }

            /// <summary>
            /// Draw layer user information for this GameObjectContainer
            /// </summary>
            public void Draw(Dictionary<int, string> layerToName, int layerFilter, bool includeLayerMaskFields)
            {
                var layer = m_GameObject.layer;
                var isLayerUser = layerFilter == k_InvalidLayer ? layer != 0 : layer == layerFilter;

                var componentCount = 0;
                var hasComponents = false;
                if (includeLayerMaskFields)
                {
                    componentCount = GetComponentCount(layerFilter);
                    hasComponents = componentCount > 0;
                }

                var childCount = GetChildCount(layerFilter, includeLayerMaskFields);
                var hasChildren = childCount > 0;
                if (!(isLayerUser || hasChildren || hasComponents))
                    return;

                var count = componentCount + childCount;
                var label = GetLabel(layerToName, layerFilter, includeLayerMaskFields, count, isLayerUser, layer);
                var wasExpanded = m_Expanded;
                if (hasChildren || hasComponents)
                    m_Expanded = EditorGUILayout.Foldout(m_Expanded, label, true);
                else
                    EditorGUILayout.LabelField(label);


                // Hold alt to apply expanded state to all children (recursively)
                if (m_Expanded != wasExpanded && Event.current.alt)
                {
                    if (m_Components != null)
                    {
                        foreach (var gameObjectContainer in m_Components)
                        {
                            gameObjectContainer.Expanded = m_Expanded;
                        }
                    }

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
                    if (isLayerUser)
                        EditorGUILayout.ObjectField(m_GameObject, typeof(GameObject), true);

                    if (!m_Expanded)
                        return;

                    if (hasComponents && m_Components != null)
                        DrawComponents(layerToName, layerFilter, componentCount);

                    if (hasChildren && m_Children != null)
                        DrawChildren(layerToName, layerFilter, includeLayerMaskFields, childCount);
                }
            }

            string GetLabel(Dictionary<int, string> layerToName, int layerFilter, bool includeLayerMaskFields, int count, bool isLayerUser, int layer)
            {
                k_StringBuilder.Length = 0;
                k_StringBuilder.Append(m_GameObject.name);
                k_StringBuilder.Append(" (");
                k_StringBuilder.Append(count.ToString());
                if (isLayerUser)
                {
                    var layerName = GetLayerNameString(layerToName, layer);
                    k_StringBuilder.Append(") - Layer: ");
                    k_StringBuilder.Append(layerName);
                    k_StringBuilder.Append(" {");
                }
                else
                {
                    k_StringBuilder.Append(") {");
                }

                AppendLayerNameList(k_StringBuilder, m_Usages, m_UsagesWithoutLayerMasks, layerToName, layerFilter, includeLayerMaskFields);
                k_StringBuilder.Append("}");

                return k_StringBuilder.ToString();
            }

            void DrawComponents(Dictionary<int, string> layerToName, int layerFilter, int componentCount)
            {
                k_StringBuilder.Length = 0;
                k_StringBuilder.Append("Components (");
                k_StringBuilder.Append(componentCount.ToString());
                k_StringBuilder.Append(") {");
                AppendLayerNameList(k_StringBuilder, m_UsagesInComponents, layerToName, layerFilter);
                k_StringBuilder.Append("}");
                var label = k_StringBuilder.ToString();
                m_ShowComponents = EditorGUILayout.Foldout(m_ShowComponents, label, true);
                if (m_ShowComponents)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var component in m_Components)
                        {
                            component.Draw(layerToName, layerFilter);
                        }
                    }
                }
            }

            void DrawChildren(Dictionary<int, string> layerToName, int layerFilter, bool includeLayerMaskFields, int childCount)
            {
                k_StringBuilder.Length = 0;
                k_StringBuilder.Append("Children (");
                k_StringBuilder.Append(childCount.ToString());
                k_StringBuilder.Append(") {");
                AppendLayerNameList(k_StringBuilder, m_UsagesInChildren, m_UsagesInChildrenWithoutLayerMasks, layerToName, layerFilter, includeLayerMaskFields);
                k_StringBuilder.Append("}");
                var label = k_StringBuilder.ToString();
                m_ShowChildren = EditorGUILayout.Foldout(m_ShowChildren, label, true);
                if (m_ShowChildren)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var child in m_Children)
                        {
                            child.Draw(layerToName, layerFilter, includeLayerMaskFields);
                        }
                    }
                }
            }

            /// <summary>
            /// Set the expanded state of this object and all of its children
            /// </summary>
            /// <param name="expanded">Whether this object should be expanded in the GUI</param>
            public void SetExpandedRecursively(bool expanded)
            {
                m_Expanded = expanded;
                m_ShowComponents = expanded;
                m_ShowChildren = expanded;

                if (m_Components != null)
                {
                    foreach (var component in m_Components)
                    {
                        component.Expanded = expanded;
                    }
                }

                if (m_Children != null)
                {
                    foreach (var child in m_Children)
                    {
                        child.SetExpandedRecursively(expanded);
                    }
                }
            }

            int GetComponentCount(int layerFilter = k_InvalidLayer)
            {
                if (m_Components == null)
                    return 0;

                if (layerFilter == k_InvalidLayer)
                    return m_TotalUsagesInComponents;

                m_UsagesInComponentsPerLayer.TryGetValue(layerFilter, out var count);
                return count;
            }

            int GetChildCount(int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
            {
                if (m_Children == null)
                    return 0;

                var unfiltered = layerFilter == k_InvalidLayer;
                if (unfiltered && includeLayerMaskFields)
                    return m_TotalUsagesInChildren;

                if (unfiltered)
                    return m_TotalUsagesInChildrenWithoutLayerMasks;

                if (includeLayerMaskFields)
                {
                    m_UsagesInChildrenPerLayer.TryGetValue(layerFilter, out var totalCount);
                    return totalCount;
                }

                m_UsagesInChildrenWithoutLayerMasksPerLayer.TryGetValue(layerFilter, out var totalWithoutLayerMasks);
                return totalWithoutLayerMasks;
            }

            void AggregateCount(GameObjectContainer child, ref int usagesInChildren,
                ref int usagesInChildrenWithoutLayerMasks)
            {
                var layer = child.GameObject.layer;
                if (layer != 0)
                {
                    usagesInChildren++;
                    usagesInChildrenWithoutLayerMasks++;
                    IncrementCountForLayer(layer, m_UsagesInChildrenPerLayer);
                    IncrementCountForLayer(layer, m_UsagesInChildrenWithoutLayerMasksPerLayer);
                }

                usagesInChildren += child.m_TotalUsagesInChildren;
                usagesInChildrenWithoutLayerMasks += child.m_TotalUsagesInChildrenWithoutLayerMasks;
                AggregateCountPerLayer(child.m_UsagesInChildrenPerLayer, m_UsagesInChildrenPerLayer);
                AggregateCountPerLayer(child.m_UsagesInChildrenWithoutLayerMasksPerLayer, m_UsagesInChildrenWithoutLayerMasksPerLayer);

                usagesInChildren += child.m_TotalUsagesInComponents;
                AggregateCountPerLayer(child.m_UsagesInComponentsPerLayer, m_UsagesInChildrenPerLayer);
            }

            void AggregateCount(ComponentContainer container, ref int layerUsagesInComponents)
            {
                layerUsagesInComponents += container.Count;
                AggregateCountPerLayer(container.UsagesPerLayer, m_UsagesInComponentsPerLayer);
            }
        }

        class FilterRow
        {
            public readonly HashSet<GameObject> AllUsers = new HashSet<GameObject>();
            public readonly HashSet<GameObject> UsersWithoutLayerMasks = new HashSet<GameObject>();
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

        const string k_MenuItemName = "Window/SuperScience/Scene Layer Users";
        const string k_WindowTitle = "Scene Layer Users";
        const string k_NoUsages = "No scene objects using a non-default layer";
        const int k_FilterPanelWidth = 180;
        const int k_ObjectFieldWidth = 150;
        const string k_Instructions = "Click the Scan button to scan your project for users of non-default layers. WARNING: " +
            "This will load every prefab in your project. For large projects, this may take a long time and/or crash the Editor.";
        const int k_ProgressBarHeight = 15;
        const int k_InvalidLayer = -1;

        static readonly GUIContent k_IncludeLayerMaskFieldsGUIContent = new GUIContent("Include LayerMask Fields",
            "Include layers from layer mask fields in the results. This is only possible if there is at least one layer without a name.");

        static readonly GUIContent k_ScanGUIContent = new GUIContent("Scan", "Scan the project for users of non-default layers");
        static readonly GUIContent k_CancelGUIContent = new GUIContent("Cancel", "Cancel the current scan");
        static readonly GUILayoutOption k_FilterPanelWidthOption = GUILayout.Width(k_FilterPanelWidth);
        static readonly Vector2 k_MinSize = new Vector2(400, 200);

        Vector2 m_ColorListScrollPosition;
        Vector2 m_FolderTreeScrollPosition;
        readonly List<SceneContainer> m_SceneContainers = new List<SceneContainer>();
        readonly SortedDictionary<int, FilterRow> m_FilterRows = new SortedDictionary<int, FilterRow>();
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

        /// <summary>
        /// Initialize the window
        /// </summary>
        [MenuItem(k_MenuItemName)]
        static void Init()
        {
            GetWindow<SceneLayerUsers>(k_WindowTitle).Show();
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

            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(k_FilterPanelWidthOption))
                {
                    DrawFilters();
                }

                using (new GUILayout.VerticalScope())
                {
                    using (new EditorGUI.DisabledScope(m_LayerWithNoName == k_InvalidLayer))
                    {
                        m_IncludeLayerMaskFields = EditorGUILayout.Toggle(k_IncludeLayerMaskFieldsGUIContent, m_IncludeLayerMaskFields);
                    }

                    var count = 0;
                    foreach (var container in m_SceneContainers)
                    {
                        count += container.GetCount(m_LayerFilter, m_IncludeLayerMaskFields);
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
                                container.Draw(m_LayerToName, m_LayerFilter, m_IncludeLayerMaskFields);
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
        /// Draw a list buttons for filtering based on layer.
        /// </summary>
        void DrawFilters()
        {
            var count = 0;
            foreach (var container in m_SceneContainers)
            {
                count += container.GetCount(includeLayerMaskFields: m_IncludeLayerMaskFields);
            }

            var style = m_LayerFilter == k_InvalidLayer ? Styles.ActiveFilterButton : Styles.InactiveFilterButton;
            if (GUILayout.Button($"All ({count})", style))
                m_LayerFilter = k_InvalidLayer;

            using (var scrollView = new GUILayout.ScrollViewScope(m_ColorListScrollPosition))
            {
                m_ColorListScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in m_FilterRows)
                {
                    var layer = kvp.Key;

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
        /// Scan the project for layer users and populate the data structures for UI.
        /// </summary>
        void Scan()
        {
            m_LayerWithNoName = k_InvalidLayer;
            m_LayerToName.Clear();
            m_FilterRows.Clear();
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

            m_SceneContainers.Clear();

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
            var sceneContainer = SceneContainer.CreateIfNecessary(scene, m_FilterRows, m_LayerToName, m_LayerWithNoName);
            if (sceneContainer != null)
                m_SceneContainers.Add(sceneContainer);
        }

        /// <summary>
        /// Get or create a <see cref="FilterRow"/> for a given layer value.
        /// </summary>
        /// <param name="filterRows">Dictionary of FilterRow objects for counting usages per layer.</param>
        /// <param name="layer">The layer value to use for this row.</param>
        /// <returns>The row for the layer value.</returns>
        static FilterRow GetOrCreateFilterRowForLayer(SortedDictionary<int, FilterRow> filterRows, int layer)
        {
            if (filterRows.TryGetValue(layer, out var filterRow))
                return filterRow;

            filterRow = new FilterRow();
            filterRows[layer] = filterRow;
            return filterRow;
        }

        static void AppendLayerNameList(StringBuilder stringBuilder, int[] usages, int[] usagesWithoutLayerMasks,
            Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer, bool includeLayerMaskFields = true)
        {
            if (layerFilter >= 0)
            {
                stringBuilder.Append(GetLayerNameString(layerToName, layerFilter));
                return;
            }

            AppendLayerNameList(stringBuilder, includeLayerMaskFields ? usages : usagesWithoutLayerMasks, layerToName, layerFilter);
        }

        static void AppendLayerNameList(StringBuilder stringBuilder, int[] layers, Dictionary<int, string> layerToName, int layerFilter = k_InvalidLayer)
        {
            if (layerFilter >= 0)
            {
                stringBuilder.Append(GetLayerNameString(layerToName, layerFilter));
                return;
            }

            var length = layers.Length;
            if (length == 0)
                return;

            var lengthMinusOne = length - 1;
            for (var i = 0; i < lengthMinusOne; i++)
            {
                stringBuilder.Append(GetLayerNameString(layerToName, layers[i]));
                stringBuilder.Append(", ");
            }

            stringBuilder.Append(GetLayerNameString(layerToName, layers[lengthMinusOne]));
        }

        static string GetLayerNameString(Dictionary<int, string> layerToName, int layer)
        {
            layerToName.TryGetValue(layer, out var layerName);
            if (string.IsNullOrEmpty(layerName))
                layerName = layer.ToString();

            return layerName;
        }

        static void IncrementCountForLayer(int layer, SortedDictionary<int, int> countPerLayer)
        {
            countPerLayer.TryGetValue(layer, out var count);
            count++;
            countPerLayer[layer] = count;
        }

        static void AggregateCountPerLayer(SortedDictionary<int, int> source, SortedDictionary<int, int> destination)
        {
            if (source == null)
                return;

            foreach (var kvp in source)
            {
                var layer = kvp.Key;
                destination.TryGetValue(layer, out var count);
                count += kvp.Value;
                destination[layer] = count;
            }
        }
    }
}
