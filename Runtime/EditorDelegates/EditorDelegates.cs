#if UNITY_EDITOR
using System;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// This class is used for routing functionality from the Editor assembly to the runtime assembly
    /// </summary>
    public static class EditorDelegates
    {
        public static Action ShowExampleWindow { get; set; }
        public static Func<bool> IsMouseOverExampleWindow { get; set; }

        public static event Action onExampleWindowFocus;
        public static event Action onExampleWindowLostFocus;

        public static void OnExampleWindowFocus()
        {
            if (onExampleWindowFocus != null)
                onExampleWindowFocus();
        }

        public static void OnExampleWindowLostFocus()
        {
            if (onExampleWindowLostFocus != null)
                onExampleWindowLostFocus();
        }
    }
}
#endif
