using UnityEditor;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class ModificationResponseExampleWindow : EditorWindow
    {
        const double k_TimeToResponse = 0.3;

        double m_TimeOfLastChange = -1;
        Color m_AverageColor;

        [MenuItem("Window/Modification Response Example")]
        static void Init()
        {
            var window = GetWindow<ModificationResponseExampleWindow>("Modification Response Example");
            window.Show();
        }

        void OnEnable()
        {
            UpdateAverageColor();
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        void OnDisable()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(Vector2.zero, position.size), m_AverageColor);
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
            if (Selection.activeGameObject.GetComponent<ColorContributor>())
                m_TimeOfLastChange = EditorApplication.timeSinceStartup;
        }

        void UpdateAverageColor()
        {
            m_AverageColor = Color.clear;
            var colorContributors = FindObjectsOfType<ColorContributor>();
            foreach (var colorContributor in colorContributors)
            {
                m_AverageColor += colorContributor.color;
            }

            m_AverageColor /= colorContributors.Length;
            Repaint();
        }
    }
}
