using UnityEditor;

namespace Unity.Labs.SuperScience
{
    public class EditorDelegatesExampleWindow : EditorWindow
    {
        const string k_HelpMessage = "This window shows an example of an Editor assembly class assigning delegates and " +
            "firing callbacks that exist in a runtime assembly. The MonoBehaviour EditorDelegatesUser is able to respond " +
            "to interaction with this window by hooking into delegates assigned by this window.";

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorDelegates.ShowExampleWindow = ShowWindow;
        }

        [MenuItem("Window/SuperScience/Editor Delegates Example")]
        static void ShowWindow()
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

        void OnGUI()
        {
            EditorGUILayout.HelpBox(k_HelpMessage, MessageType.Info);
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
