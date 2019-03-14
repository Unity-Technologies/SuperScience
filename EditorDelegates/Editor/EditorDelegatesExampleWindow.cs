using UnityEditor;

namespace Unity.Labs.SuperScience
{
    public class EditorDelegatesExampleWindow : EditorWindow
    {
        [MenuItem("Window/SuperScience/Editor Delegates Example")]
        static void Init()
        {
            var window = GetWindow<EditorDelegatesExampleWindow>("Editor Delegates Example");
            window.Show();
        }

        void OnEnable()
        {
            EditorDelegates.IsMouseOverExampleWindow = IsMouseOverWindow;
        }

        void OnDisable()
        {
            EditorDelegates.IsMouseOverExampleWindow = null;
        }

        void OnFocus()
        {
            EditorDelegates.OnExampleWindowFocus();
        }

        void OnLostFocus()
        {
            EditorDelegates.OnExampleWindowLostFocus();
        }

        bool IsMouseOverWindow()
        {
            return mouseOverWindow == this;
        }
    }
}
