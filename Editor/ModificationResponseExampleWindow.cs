using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Labs.SuperScience
{
    public class ModificationResponseExampleWindow : EditorWindow
    {
        const double k_TimeToResponse = 0.3;
        const float k_ColorRectPadding = 4f;
        const string k_HelpMessage = "This window shows the average color from all ColorContributors in the Scene. " +
            "It is updated on a delayed response to property modifications to ColorContributors. " +
            "We use averaging colors here for the sake of simplicity - in practice this delayed response pattern " +
            "is most useful for operations with a lot of overhead.";

        double m_TimeOfLastChange = -1;
        Color m_AverageColor;

        [MenuItem("Window/SuperScience/Modification Response Example")]
        static void Init()
        {
            var window = GetWindow<ModificationResponseExampleWindow>("Modification Response Example");
            window.Show();
        }

        void OnEnable()
        {
            UpdateAverageColor();
            EditorSceneManager.sceneOpened += OnSceneOpened;
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(k_HelpMessage, MessageType.Info);

            var lastRect = GUILayoutUtility.GetLastRect();
            var colorRectY = lastRect.yMax + k_ColorRectPadding;
            var colorRect = new Rect(0, colorRectY, position.width, position.height - colorRectY);
            EditorGUI.DrawRect(colorRect, m_AverageColor);
        }

        void Update()
        {
            if (m_TimeOfLastChange >= 0 && EditorApplication.timeSinceStartup - m_TimeOfLastChange >= k_TimeToResponse)
            {
                m_TimeOfLastChange = -1;
                UpdateAverageColor();
            }
        }

        UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            foreach (var modification in modifications)
            {
                if (modification.currentValue.target is ColorContributor)
                {
                    m_TimeOfLastChange = EditorApplication.timeSinceStartup;
                    break;
                }
            }

            return modifications;
        }

        void OnUndoRedoPerformed()
        {
            // When undoing/redoing a modification made through the Inspector, the modified object will be the active selection.
            // This is not guaranteed to handle all cases though, since it is possible for a modification to happen from
            // some arbitrary user code, for example through SerializedProperty.
            var selectedGameObject = Selection.activeGameObject;
            if (selectedGameObject != null && selectedGameObject.GetComponent<ColorContributor>())
                m_TimeOfLastChange = EditorApplication.timeSinceStartup;
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            UpdateAverageColor();
        }

        void UpdateAverageColor()
        {
            m_AverageColor = Color.clear;

            var colorContributors = FindObjectsOfType<ColorContributor>();
            if (colorContributors.Length > 0)
            {
                foreach (var colorContributor in colorContributors)
                {
                    m_AverageColor += colorContributor.color;
                }

                m_AverageColor /= colorContributors.Length;
            }

            Repaint();
        }
    }
}
