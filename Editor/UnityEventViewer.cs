using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class UnityEventViewer : EditorWindow
    {
        class EventRow
        {
            public SerializedProperty Property;
            public Component Component;
        }

        bool m_ShowEventsInAssets;
        Vector2 m_ScrollPosition;
        readonly List<EventRow> m_EventRows = new List<EventRow>();
        int m_EventCount;

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

            EditorGUILayout.LabelField("Total Event Count", m_EventCount.ToString());
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollScope.scrollPosition;
                foreach (var row in m_EventRows)
                {
                    EditorGUILayout.ObjectField(row.Component, typeof(Component), true);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(row.Property);
                    }
                }
            }
        }

        void Scan()
        {
            m_EventRows.Clear();
            m_EventCount = 0;
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

                    var arraySize = iterator.FindPropertyRelative("m_Calls.Array.size");
                    if (arraySize == null)
                    {
                        Debug.LogWarning("Couldn't find calls array size");
                        continue;
                    }

                    var size = arraySize.intValue;
                    m_EventCount += size;
                    if (size > 0)
                        m_EventRows.Add(new EventRow{ Component = component, Property = lastProperty});

                    enterChildren = false;
                    lastProperty = iterator.Copy();
                }
            }
        }
    }
}
