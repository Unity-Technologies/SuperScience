using UnityEngine;
using UnityEngine.UI;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// This is an example of a runtime class that uses functionality from the Editor assembly.
    /// It drives UI text based on states of the Editor Delegates Example Window.
    /// </summary>
    public class EditorDelegatesUser : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        Text m_MouseOverText;

        [SerializeField]
        Text m_FocusedText;
#pragma warning restore 649

        void Awake()
        {
#if UNITY_EDITOR
            EditorDelegates.onExampleWindowFocus += ShowFocusedText;
            EditorDelegates.onExampleWindowLostFocus += ShowUnfocusedText;
            if (EditorDelegates.ShowExampleWindow != null)
                EditorDelegates.ShowExampleWindow();
#endif
        }

        void OnDestroy()
        {
#if UNITY_EDITOR
            EditorDelegates.onExampleWindowFocus -= ShowFocusedText;
            EditorDelegates.onExampleWindowLostFocus -= ShowUnfocusedText;
#endif
        }

        void Update()
        {
            var mouseOver = false;
#if UNITY_EDITOR
            if (EditorDelegates.IsMouseOverExampleWindow != null)
                mouseOver = EditorDelegates.IsMouseOverExampleWindow();
#endif
            m_MouseOverText.text = mouseOver ? "Mouse Over" : "Mouse Not Over";
        }

        void ShowFocusedText()
        {
            m_FocusedText.text = "Focused";
        }

        void ShowUnfocusedText()
        {
            m_FocusedText.text = "Unfocused";
        }
    }
}
