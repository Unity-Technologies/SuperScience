using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class HiddenInspector : EditorWindow
    {
        SerializedObject m_SerializedObject;
        SerializedProperty m_ComponentProperty;
        readonly Dictionary<Component, bool> m_ComponentsExpanded = new Dictionary<Component, bool>();
        readonly Dictionary<Component, SerializedObject> m_ComponentSerializedObjects = new Dictionary<Component, SerializedObject>();
        Vector2 m_ScrollPosition;
        bool m_ShowHiddenProperties;

        [MenuItem("Window/SuperScience/HiddenInspector")]
        static void OnMenuItem()
        {
            GetWindow<HiddenInspector>("HiddenInspector");
        }

        void OnEnable()
        {
            Selection.selectionChanged += Repaint;
            Selection.selectionChanged += SelectionChanged;
            SelectionChanged();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
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
            m_ComponentProperty = m_SerializedObject.FindProperty("m_Component");
            m_ComponentSerializedObjects.Clear();
            m_ComponentsExpanded.Clear();
        }

        // Destroy method from https://answers.unity.com/questions/15225/how-do-i-remove-null-components-ie-missingmono-scr.html
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
            m_ShowHiddenProperties = EditorGUILayout.Toggle("Show Hidden Properties", m_ShowHiddenProperties);
            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;
                var components = target.GetComponents<Component>();
                for (var i = 0; i < components.Length; i++)
                {
                    var expanded = false;
                    var component = components[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (component != null)
                        {
                            if (!m_ComponentsExpanded.TryGetValue(component, out expanded))
                                m_ComponentsExpanded[component] = false;

                            var wasExpanded = expanded;
                            expanded = EditorGUILayout.Foldout(expanded, component.GetType().ToString(), true);
                            if (wasExpanded != expanded)
                                m_ComponentsExpanded[component] = expanded;
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Missing Component");
                        }

                        if (GUILayout.Button("Destroy", GUILayout.Width(80)))
                        {
                            m_ComponentProperty.DeleteArrayElementAtIndex(i);

                            m_SerializedObject.ApplyModifiedProperties();
                            EditorSceneManager.MarkAllScenesDirty();
                            break;
                        }
                    }

                    if (expanded)
                    {
                        SerializedObject componentSerializedObject;
                        if (!m_ComponentSerializedObjects.TryGetValue(component, out componentSerializedObject))
                        {
                            componentSerializedObject = new SerializedObject(component);
                            m_ComponentSerializedObjects[component] = componentSerializedObject;
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
    }
}
