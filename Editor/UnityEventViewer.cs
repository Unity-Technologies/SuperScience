using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class UnityEventViewer : EditorWindow
    {
        class EventRow
        {
            public string TransformPath;
            public Component Component;
            public SerializedProperty EventProperty;
            public SerializedProperty CallsProperty;
            public int CallCount;
        }

        bool m_ShowEventsInAssets;
        Vector2 m_ScrollPosition;
        readonly List<EventRow> m_EventRows = new List<EventRow>();

        [NonSerialized]
        int m_CallCount;

        [MenuItem("Window/SuperScience/UnityEventViewer")]
        static void OnMenuItem()
        {
            GetWindow<UnityEventViewer>("UnityEventViewer");
        }

        void OnGUI()
        {
            m_ShowEventsInAssets = EditorGUILayout.Toggle("Show Events In Assets", m_ShowEventsInAssets);
            if (GUILayout.Button("Scan"))
                Scan();

            using (new EditorGUI.DisabledScope(m_CallCount == 0))
            {
                if (GUILayout.Button("Print"))
                    Print();
            }

            EditorGUILayout.LabelField("Total Call Count", m_CallCount.ToString());
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollScope.scrollPosition;
                foreach (var row in m_EventRows)
                {
                    EditorGUILayout.ObjectField(row.Component, typeof(Component), true);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(row.EventProperty);
                    }
                }
            }
        }

        void Scan()
        {
            m_EventRows.Clear();
            m_CallCount = 0;
            var components = Resources.FindObjectsOfTypeAll<Component>();
            foreach (var component in components)
            {
                if (!m_ShowEventsInAssets && !component.gameObject.scene.IsValid())
                    continue;

                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                var enterChildren = true;
                var lastProperty = iterator.Copy();
                while (iterator.Next(enterChildren))
                {
                    if (iterator.propertyType != SerializedPropertyType.Generic || !iterator.propertyPath.EndsWith("m_PersistentCalls"))
                    {
                        lastProperty = iterator.Copy();
                        enterChildren = true;
                        continue;
                    }

                    var callsProperty = iterator.FindPropertyRelative("m_Calls");
                    var arraySizeProperty = callsProperty.FindPropertyRelative("Array.size");
                    if (arraySizeProperty == null)
                    {
                        Debug.LogWarning("Couldn't find calls array size");
                        continue;
                    }

                    var size = arraySizeProperty.intValue;
                    m_CallCount += size;
                    if (size > 0)
                        m_EventRows.Add(new EventRow
                        {
                            TransformPath = GetTransformPath(null, component.transform),
                            Component = component,
                            EventProperty = lastProperty,
                            CallCount = size,
                            CallsProperty = callsProperty
                        });

                    enterChildren = false;
                    lastProperty = iterator.Copy();
                }
            }

            m_EventRows.Sort((a, b) =>
            {
                var compare = a.TransformPath.CompareTo(b.TransformPath);
                if (compare != 0)
                    return compare;

                return a.Component.ToString().CompareTo(b.Component.ToString());
            });
        }

        void Print()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Total Call Count {m_CallCount}");
            foreach (var eventRow in m_EventRows)
            {
                stringBuilder.AppendLine(eventRow.TransformPath);
                stringBuilder.AppendLine(eventRow.Component.ToString());
                var callsProperty = eventRow.CallsProperty;
                var callCount = eventRow.CallCount;
                for (var i = 0; i < callCount; i++)
                {
                    stringBuilder.AppendLine($"    Call {i}");
                    var callProperty = callsProperty.GetArrayElementAtIndex(i);
                    var targetProperty = callProperty.FindPropertyRelative(MissingReferencesWindow.TargetPropertyName);
                    var methodProperty = callProperty.FindPropertyRelative(MissingReferencesWindow.MethodNamePropertyName);
                    // TODO: arguments
                    stringBuilder.AppendLine($"        {targetProperty.objectReferenceValue}");
                    stringBuilder.AppendLine($"        {methodProperty.stringValue}");
                }
            }

            var output = stringBuilder.ToString();
            Debug.Log(output);
            var path = EditorUtility.SaveFilePanel("Save Event Report", string.Empty, "UnityEvents", "txt");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, output);
        }

        static string GetTransformPath(Transform root, Transform target)
        {
            // TODO: Log error on failure
            var path = string.Empty;
            while (target != null && root != target)
            {
                var name = target.name;
                if (name.Contains("/"))
                {
                    name = name.Replace('/', '_');
                    Debug.LogWarning("Encountered GameObject with name that contains '/'. This may cause issues when deserializing prefab overrides");
                }

                path = string.IsNullOrEmpty(path) ? name : $"{name}/{path}";

                target = target.parent;
            }

            return path;
        }
    }
}
