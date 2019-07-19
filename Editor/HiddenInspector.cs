using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class HiddenInspector : EditorWindow
    {
        SerializedObject m_SerializedObject;
        SerializedProperty m_ComponentProperty;

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
        }

        // Destroy method from https://answers.unity.com/questions/15225/how-do-i-remove-null-components-ie-missingmono-scr.html
        void OnGUI()
        {
            if (m_SerializedObject == null)
                return;

            var target = (GameObject)m_SerializedObject.targetObject;

            GUILayout.Label(target.name, EditorStyles.boldLabel);
            var components = target.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var component = components[i];
                    GUILayout.Label(component ? component.GetType().ToString() : "Missing Component");

                    if (GUILayout.Button("Destroy", GUILayout.Width(80)))
                    {
                        m_ComponentProperty.DeleteArrayElementAtIndex(i);

                        m_SerializedObject.ApplyModifiedProperties();
                        EditorSceneManager.MarkAllScenesDirty();
                        break;
                    }
                }
            }
        }
    }
}
