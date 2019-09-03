using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// Scans the project for serialized references to missing (deleted) assets and displays the results in an EditorWindow
    /// </summary>
    abstract class MissingReferencesWindow : EditorWindow
    {
        protected GUIStyle m_MissingScriptStyle;

        readonly Dictionary<UnityObject, SerializedObject> m_SerializedObjects =
            new Dictionary<UnityObject, SerializedObject>();

        void OnEnable()
        {
            m_MissingScriptStyle = new GUIStyle { richText = true };
        }

        /// <summary>
        /// Load all assets in the AssetDatabase and check them for missing serialized references
        /// </summary>
        protected abstract void Scan();

        protected virtual void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
                Scan();
        }

        /// <summary>
        /// Check a GameObject and its components for missing serialized references
        /// </summary>
        /// <param name="gameObject">The GameObject to be scanned</param>
        /// <returns>True if the object has any missing references</returns>
        protected bool CheckForMissingRefs(GameObject gameObject)
        {
            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (CheckForMissingRefs(component))
                    return true;
            }

            foreach (Transform child in gameObject.transform)
            {
                if (CheckForMissingRefs(child.gameObject))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check a UnityObject for missing serialized references
        /// </summary>
        /// <param name="obj">The UnityObject to be scanned</param>
        /// <returns>True if the object has any missing references</returns>
        protected bool CheckForMissingRefs(UnityObject obj)
        {
            if (obj == null)
                return true;

            var so = GetSerializedObjectForUnityObject(obj);

            var property = so.GetIterator();
            while (property.NextVisible(true))
            {
                // Asset references will all be UnityObject's of some kind.
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                // Some references may be null, which is to be expected--not every field is set
                // Valid asset references will have a non-null objectReferenceValue
                // Valid asset references will have some non-zero objectReferenceInstanceIDValue value
                // References to missing assets will have a null objectReferenceValue, but will retain
                // their non-zero objectReferenceInstanceIDValue
                if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    return true;
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

        protected void DrawObject(GameObject gameObject)
        {
            foreach (var component in gameObject.GetComponents<Component>())
            {
                DrawComponent(gameObject, component);
            }

            foreach (Transform child in gameObject.transform)
            {
                DrawObject(child.gameObject);
            }
        }

        protected void DrawObject(UnityObject obj)
        {
            var so = GetSerializedObjectForUnityObject(obj);

            var property = so.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    EditorGUILayout.PropertyField(property);
            }
        }

        void DrawComponent(GameObject gameObject, Component component)
        {
            if (component == null)
            {
                EditorGUILayout.LabelField("<color=red>Missing Script!</color>", m_MissingScriptStyle);

                // Though component == null, this object still exists and has an InstanceID
                // ReSharper disable once ExpressionIsAlwaysNull
                DrawComponentContext(gameObject, null);
                return;
            }

            var so = GetSerializedObjectForUnityObject(component);

            var property = so.GetIterator();
            var hasMissing = false;
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                {
                    hasMissing = true;
                    EditorGUILayout.PropertyField(property);
                }
            }

            if (!hasMissing)
                return;

            DrawComponentContext(gameObject, component);
        }

        static void DrawComponentContext(GameObject gameObject, Component component)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField(GetTransformPath(gameObject.transform));
                EditorGUILayout.ObjectField(component, typeof(Component), false, GUILayout.Width(150));
            }
        }

        /// <summary>
        /// Get a string to help users find an object within its transform hierarchy
        /// This will combine the name of the given transform and its parent recursively until it reaches the root,
        /// and return the resulting concatenated string, joined on a / character
        /// </summary>
        /// <param name="childTransform">The child transform whose name will be applied to its parent's path</param>
        /// <param name="childPath">The path suffix containing previously visited children</param>
        /// <returns>The combined transform path</returns>
        public static string GetTransformPath(Transform childTransform, string childPath = "")
        {
            while (true)
            {
                childPath = "/" + childTransform.name + childPath;
                if (childTransform.parent == null) return childPath;

                childTransform = childTransform.parent;
            }
        }
    }
}
