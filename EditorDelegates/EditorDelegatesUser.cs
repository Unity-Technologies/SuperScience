using UnityEngine;
using UnityEngine.UI;

namespace Unity.Labs.SuperScience
{
    public class EditorDelegatesUser : MonoBehaviour
    {
        [SerializeField]
        Text m_MouseOverText;

        [SerializeField]
        Text m_FocusedText;

        void Awake()
        {
#if UNITY_EDITOR
            EditorDelegates.onExampleWindowFocus += ShowFocusedText;
            EditorDelegates.onExampleWindowLostFocus += ShowUnfocusedText;
#endif

            ShowUnfocusedText();
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
