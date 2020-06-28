using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    public class HiddenInspector : EditorWindow
    {
        const string k_ShowHiddenPropertiesLabel = "Show Hidden Properties";
#if !UNITY_2019_1_OR_NEWER
        SerializedProperty m_ComponentProperty;
#endif

        SerializedObject m_SerializedObject;
        readonly Dictionary<UnityObject, bool> m_ObjectsExpanded = new Dictionary<UnityObject, bool>();
        readonly Dictionary<UnityObject, SerializedObject> m_SerializedObjects = new Dictionary<UnityObject, SerializedObject>();
        Vector2 m_ScrollPosition;
        bool m_ShowHiddenProperties;

        [MenuItem("Window/SuperScience/HiddenInspector")]
        static void OnMenuItem()
        {
            GetWindow<HiddenInspector>("HiddenInspector");
        }

        void OnEnable()
        {
            autoRepaintOnSceneChange = true;
            Selection.selectionChanged += SelectionChanged;
            SelectionChanged();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= SelectionChanged;
        }

        void SelectionChanged()
        {
            var activeObject = Selection.activeObject;
            if (!activeObject)
                return;

            var activeGameObject = activeObject as GameObject;
            if (!activeGameObject)
                return;

            m_SerializedObject = new SerializedObject(activeGameObject);
            m_SerializedObjects.Clear();
            m_ObjectsExpanded.Clear();

#if !UNITY_2019_1_OR_NEWER
            m_ComponentProperty = m_SerializedObject.FindProperty("m_Component");
#endif
        }

        // Destroy method (obsolete in 2019.1+) from https://answers.unity.com/questions/15225/how-do-i-remove-null-components-ie-missingmono-scr.html
        void OnGUI()
        {
            if (m_SerializedObject == null)
                return;

            var targetObject = m_SerializedObject.targetObject;
            if (targetObject == null)
                return;

            m_SerializedObject.Update();
            var target = (GameObject)targetObject;
            EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
            m_ShowHiddenProperties = EditorGUILayout.Toggle(k_ShowHiddenPropertiesLabel, m_ShowHiddenProperties);
            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
            {
                DrawSerializedObject(target, target);

                // Bail on this GUI pass if we've just destroyed the object
                if (target == null)
                    GUIUtility.ExitGUI();

                m_ScrollPosition = scrollView.scrollPosition;
                var components = target.GetComponents<Component>();
                for (var i = 0; i < components.Length; i++)
                {
                    DrawSerializedObject(components[i], target);
                }
            }
        }

        void DrawSerializedObject(UnityObject drawTarget, GameObject inspectorTarget)
        {
            var expanded = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                var isTransform = false;
                if (drawTarget == null)
                {
                    EditorGUILayout.LabelField("Missing Component");
                }
                else
                {
                    if (!m_ObjectsExpanded.TryGetValue(drawTarget, out expanded))
                    {
                        expanded = true;
                        m_ObjectsExpanded[drawTarget] = true;
                    }

                    var wasExpanded = expanded;
                    var componentType = drawTarget.GetType();
                    isTransform = componentType == typeof(Transform);
                    expanded = EditorGUILayout.Foldout(expanded, componentType.ToString(), true);
                    if (wasExpanded != expanded)
                        m_ObjectsExpanded[drawTarget] = expanded;
                }

                using (new EditorGUI.DisabledScope(isTransform))
                {
                    if (GUILayout.Button("Destroy", GUILayout.Width(80)))
                    {
#if UNITY_2019_1_OR_NEWER
                        // Unity 2019.1 introduced a special method for removing missing scripts
                        if (drawTarget == null)
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(inspectorTarget);
                        else
                            Undo.DestroyObjectImmediate(drawTarget);
#else
                                // In older versions of Unity, this method will remove missing scripts and regular components
                                m_ComponentProperty.DeleteArrayElementAtIndex(i);

                                m_SerializedObject.ApplyModifiedProperties();
                                EditorSceneManager.MarkAllScenesDirty();
#endif

                        // Early-out so that we don't try to draw this component
                        return;
                    }
                }
            }

            if (expanded)
            {
                SerializedObject componentSerializedObject;
                if (!m_SerializedObjects.TryGetValue(drawTarget, out componentSerializedObject))
                {
                    componentSerializedObject = new SerializedObject(drawTarget);
                    m_SerializedObjects[drawTarget] = componentSerializedObject;
                }

                componentSerializedObject.Update();

                using (new EditorGUI.IndentLevelScope())
                {
                    var property = componentSerializedObject.GetIterator();
                    property.Next(true);
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        if (m_ShowHiddenProperties)
                        {
                            drawTarget.hideFlags = (HideFlags)EditorGUILayout.EnumFlagsField("Hide Flags", drawTarget.hideFlags);
                            while (property.Next(false))
                            {
                                EditorGUILayout.PropertyField(property, true);
                            }
                        }
                        else
                        {
                            while (property.NextVisible(false))
                            {
                                EditorGUILayout.PropertyField(property, true);
                            }
                        }

                        if (check.changed)
                            componentSerializedObject.ApplyModifiedProperties();
                    }
                }

                EditorGUILayout.Separator();
            }
        }
    }
}
