using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans the project for serialized references to missing (deleted) assets and displays the results in an EditorWindow
    /// </summary>
    abstract class MissingReferencesWindow : EditorWindow
    {
        /// <summary>
        /// Tree structure for GameObject scan results
        /// When the Scan method encounters a GameObject in a scene or a prefab in the project, we initialize one of
        /// these using the GameObject as an argument. This scans the object and its components/children, retaining
        /// the results for display in the GUI. The window calls into these helper objects to draw them, as well.
        /// </summary>
        protected class GameObjectContainer
        {
            /// <summary>
            /// Container for component scan results. Just as with GameObjectContainer, we initialize one of these
            /// using a component to scan it for missing references and retain the results
            /// </summary>
            class ComponentContainer
            {
                readonly Component m_Component;
                public readonly List<SerializedProperty> PropertiesWithMissingReferences = new List<SerializedProperty>();

                // TODO: Try and remove window argument
                /// <summary>
                /// Initialize a ComponentContainer to represent the given Component
                /// This will scan the component for missing references and retain the information for display in
                /// the given window.
                /// </summary>
                /// <param name="component">The Component to scan for missing references</param>
                /// <param name="window">The window which will display the information</param>
                public ComponentContainer(Component component, MissingReferencesWindow window)
                {
                    m_Component = component;
                    window.CheckForMissingReferences(component, PropertiesWithMissingReferences);
                }

                /// <summary>
                /// Draw the missing references UI for this component
                /// </summary>
                public void Draw()
                {
                    EditorGUILayout.ObjectField(m_Component, typeof(Component), false);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        // If the component equates to null, it is an empty scripting wrapper, indicating a missing script
                        if (m_Component == null)
                        {
                            EditorGUILayout.LabelField("<color=red>Missing Script!</color>", Styles.RichTextStyle);
                            return;
                        }

                        DrawPropertiesWithMissingReferences(PropertiesWithMissingReferences);
                    }
                }
            }

            readonly GameObject m_GameObject;
            readonly List<GameObjectContainer> m_Children = new List<GameObjectContainer>();
            readonly List<ComponentContainer> m_Components = new List<ComponentContainer>();
            bool m_IsMissingPrefab;

            bool m_Visible;
            bool m_ShowComponents;
            bool m_ShowChildren;

            public int Count { get; private set; }
            public GameObject GameObject { get { return m_GameObject; } }

            public GameObjectContainer() { }

            /// <summary>
            /// Initialize a GameObjectContainer to represent the given GameObject
            /// This will scan the component for missing references and retain the information for display in
            /// the given window.
            /// </summary>
            /// <param name="gameObject">The GameObject to scan for missing references</param>
            /// <param name="window">The window which will display the information</param>
            internal GameObjectContainer(GameObject gameObject, MissingReferencesWindow window)
            {
                m_GameObject = gameObject;

                if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
                    m_IsMissingPrefab = PrefabUtility.IsPrefabAssetMissing(gameObject);

                foreach (var component in gameObject.GetComponents<Component>())
                {
                    var container = new ComponentContainer(component, window);
                    if (component == null)
                    {
                        m_Components.Add(container);
                        Count++;
                        continue;
                    }

                    var count = container.PropertiesWithMissingReferences.Count;
                    if (count > 0)
                    {
                        m_Components.Add(container);
                        Count += count;
                    }
                }

                foreach (Transform child in gameObject.transform)
                {
                    AddChild(child.gameObject, window);
                }
            }

            public void Clear()
            {
                m_Children.Clear();
                m_Components.Clear();
                Count = 0;
            }

            /// <summary>
            /// Add a child GameObject to this GameObjectContainer
            /// </summary>
            /// <param name="gameObject">The GameObject to scan for missing references</param>
            /// <param name="window">The window which will display the information</param>
            public void AddChild(GameObject gameObject, MissingReferencesWindow window)
            {
                var container = new GameObjectContainer(gameObject, window);
                Count += container.Count;

                var isMissingPrefab = container.m_IsMissingPrefab;
                if (isMissingPrefab)
                    Count++;

                if (container.Count > 0 || isMissingPrefab)
                    m_Children.Add(container);
            }

            /// <summary>
            /// Draw missing reference information for this GameObjectContainer
            /// </summary>
            internal void Draw()
            {
                var name = "GameObjects";
                if (m_GameObject)
                    name = m_GameObject.name;

                // Missing prefabs will not have any components or children
                if (m_IsMissingPrefab)
                {
                    //TODO: use rich text to make this red
                    EditorGUILayout.LabelField(string.Format("<color=red>{0} - Missing Prefab</color>", name), Styles.RichTextStyle);
                    return;
                }

                var wasVisible = m_Visible;
                m_Visible = EditorGUILayout.Foldout(m_Visible, string.Format("{0}: {1}", name, Count), true);

                // Hold alt to apply visibility state to all children (recursively)
                if (m_Visible != wasVisible && Event.current.alt)
                    SetVisibleRecursively(m_Visible);

                if (!m_Visible)
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    // If m_GameObject is null, this is a scene
                    if (m_GameObject == null)
                    {
                        DrawChildren();
                        return;
                    }

                    var count = m_Components.Count;
                    if (count > 0)
                    {
                        EditorGUILayout.ObjectField(m_GameObject, typeof(GameObject), true);
                        m_ShowComponents = EditorGUILayout.Foldout(m_ShowComponents, string.Format("Components: {0}", count), true);
                        if (m_ShowComponents)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                foreach (var component in m_Components)
                                {
                                    component.Draw();
                                }
                            }
                        }
                    }

                    count = m_Children.Count;
                    if (count > 0)
                    {
                        m_ShowChildren = EditorGUILayout.Foldout(m_ShowChildren, string.Format("Children: {0}", count), true);
                        if (m_ShowChildren)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                DrawChildren();
                            }
                        }
                    }
                }

                void DrawChildren()
                {
                    foreach (var child in m_Children)
                    {
                        var childObject = child.m_GameObject;

                        // Check for null in case  of destroyed object
                        if (childObject)
                            child.Draw();
                    }
                }
            }

            /// <summary>
            /// Set the visibility state of this object and all of its children
            /// </summary>
            /// <param name="visible">Whether this object and its children should be visible in the GUI</param>
            public void SetVisibleRecursively(bool visible)
            {
                m_Visible = visible;
                m_ShowComponents = visible;
                m_ShowChildren = visible;
                foreach (var child in m_Children)
                {
                    child.SetVisibleRecursively(visible);
                }
            }
        }

        static class Styles
        {
            const string k_IncludeEmptyEventsLabel = "Include Empty Events";
            const string k_IncludeEmptyEventsTooltip = "While scanning properties for missing references, include serialized UnityEvent references which do not have a target object. Missing references to target objects will be included whether or not this is set.";
            const string k_IncludeMissingMethodsLabel = "Include Missing Methods";
            const string k_IncludeMissingMethodsTooltip = "While scanning properties for missing references, include serialized UnityEvent references which specify methods that do not exist";
            const string k_IncludeUnsetMethodsLabel = "Include Unset Methods";
            const string k_IncludeUnsetMethodsTooltip = "While scanning properties for missing references, include serialized UnityEvent references which do not specify a method";

            public static GUIStyle RichTextStyle;
            public static GUIContent IncludeEmptyEventsContent;
            public static GUIContent IncludeMissingMethodsContent;
            public static GUIContent IncludeUnsetMethodsContent;

            static Styles()
            {
                RichTextStyle = new GUIStyle { richText = true };
                IncludeEmptyEventsContent = new GUIContent(k_IncludeEmptyEventsLabel, k_IncludeEmptyEventsTooltip);
                IncludeMissingMethodsContent = new GUIContent(k_IncludeMissingMethodsLabel, k_IncludeMissingMethodsTooltip);
                IncludeUnsetMethodsContent = new GUIContent(k_IncludeUnsetMethodsLabel, k_IncludeUnsetMethodsTooltip);
            }
        }

        const float k_LabelWidthRatio = 0.5f;
        const string k_PersistentCallsSearchString = "m_PersistentCalls.m_Calls.Array.data[";
        const string k_TargetPropertyName = "m_Target";
        const string k_MethodNamePropertyName = "m_MethodName";

        readonly Dictionary<UnityObject, SerializedObject> m_SerializedObjects = new Dictionary<UnityObject, SerializedObject>();

        bool m_IncludeEmptyEvents = true;
        bool m_IncludeMissingMethods = true;
        bool m_IncludeUnsetMethods = true;

        void OnEnable()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChangedInEditMode;
        }

        void OnActiveSceneChangedInEditMode(Scene oldScene, Scene newScene) { Clear(); }

        void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChangedInEditMode;
        }

        /// <summary>
        /// Clear this window's cache of missing references
        /// </summary>
        protected abstract void Clear();

        /// <summary>
        /// Load all assets in the AssetDatabase and check them for missing serialized references
        /// </summary>
        protected virtual void Scan()
        {
            m_SerializedObjects.Clear();
        }

        protected virtual void OnGUI()
        {
            EditorGUIUtility.labelWidth = position.width * k_LabelWidthRatio;
            m_IncludeEmptyEvents = EditorGUILayout.Toggle(Styles.IncludeEmptyEventsContent, m_IncludeEmptyEvents);
            m_IncludeMissingMethods = EditorGUILayout.Toggle(Styles.IncludeMissingMethodsContent, m_IncludeMissingMethods);
            m_IncludeUnsetMethods = EditorGUILayout.Toggle(Styles.IncludeUnsetMethodsContent, m_IncludeUnsetMethods);
            if (GUILayout.Button("Refresh"))
                Scan();
        }

        /// <summary>
        /// Check a UnityObject for missing serialized references
        /// </summary>
        /// <param name="obj">The UnityObject to be scanned</param>
        /// <param name="properties">A list to which properties with missing references will be added</param>
        /// <returns>True if the object has any missing references</returns>
        public void CheckForMissingReferences(UnityObject obj, List<SerializedProperty> properties)
        {
            if (obj == null)
                return;

            var so = GetSerializedObjectForUnityObject(obj);

            var property = so.GetIterator();
            while (property.NextVisible(true)) // enterChildren = true to scan all properties
            {
                if (CheckForMissingReferences(property))
                    properties.Add(property.Copy()); // Use a copy of this property because we are iterating on it
            }
        }

        bool CheckForMissingReferences(SerializedProperty property)
        {
            var propertyPath = property.propertyPath;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    if (!m_IncludeEmptyEvents && !m_IncludeMissingMethods && !m_IncludeUnsetMethods)
                        return false;

                    // Property paths matching a particular pattern will contain serialized UnityEvent references
                    if (propertyPath.Contains(k_PersistentCallsSearchString))
                    {
                        // UnityEvent properties contain a target object and a method name
                        var targetProperty = property.FindPropertyRelative(k_TargetPropertyName);
                        var methodProperty = property.FindPropertyRelative(k_MethodNamePropertyName);

                        if (targetProperty != null && methodProperty != null)
                        {
                            // If the target reference is missing, we can't search for methods. If the user has chosen
                            // to include empty events, we return true, otherwise we must ignore this event.
                            if (targetProperty.objectReferenceValue == null)
                            {
                                // If the target is a missing reference it will be caught below
                                if (property.objectReferenceInstanceIDValue != 0)
                                    return false;

                                return m_IncludeEmptyEvents;
                            }

                            var methodName = methodProperty.stringValue;
                            // Include if the method name is empty and the user has chosen to include unset methods
                            if (string.IsNullOrEmpty(methodName))
                                return m_IncludeUnsetMethods;

                            if (m_IncludeMissingMethods)
                            {
                                // If the user has chosen to include missing methods, check if the target object type
                                // for public methods with the same name as the value of the method property
                                var type = targetProperty.objectReferenceValue.GetType();
                                try
                                {
                                    if (!type.GetMethods().Any(info => info.Name == methodName))
                                        return true;
                                }
                                catch (Exception e)
                                {
                                    Debug.LogException(e);

                                    // Treat reflection errors as missing methods
                                    return true;
                                }
                            }
                        }
                    }
                    break;
                case SerializedPropertyType.ObjectReference:
                    // Some references may be null, which is to be expected--not every field is set
                    // Valid asset references will have a non-null objectReferenceValue
                    // Valid asset references will have some non-zero objectReferenceInstanceIDValue value
                    // References to missing assets will have a null objectReferenceValue, but will retain
                    // their non-zero objectReferenceInstanceIDValue
                    if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        return true;

                    break;
            }

            return false;
        }

        /// <summary>
        /// For a given UnityObject, get the cached SerializedObject, or create one and cache it
        /// </summary>
        /// <param name="obj">The UnityObject</param>
        /// <returns>A cached SerializedObject wrapper for the given UnityObject</returns>
        SerializedObject GetSerializedObjectForUnityObject(UnityObject obj)
        {
            SerializedObject so;
            if (!m_SerializedObjects.TryGetValue(obj, out so))
            {
                so = new SerializedObject(obj);
                m_SerializedObjects[obj] = so;
            }

            return so;
        }

        /// <summary>
        /// Draw the missing references UI for a list of properties known to have missing references
        /// </summary>
        /// <param name="properties">A list of SerializedProperty objects known to have missing references</param>
        internal static void DrawPropertiesWithMissingReferences(List<SerializedProperty> properties)
        {
            foreach (var property in properties)
            {
                switch (property.propertyType)
                {
                    // The only way a generic property could be in the list of missing properties is if it
                    // is a serialized UnityEvent with its method missing
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
