using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RunInEditHelper : EditorWindow
{
    GUIStyle m_ClickableLabel;
    Vector2 m_ScrollPosition;

    static bool s_Updating;

    static readonly GUIContent k_RunSelection = new GUIContent("Run Selection", "Set runInEditMode to true on all" +
        " MonoBehaviour components attached to currently selected objects (excluding children, if not selected)");
    static readonly GUIContent k_StopSelection = new GUIContent("Stop Selection", "Set runInEditMode to false on all" +
        " MonoBehaviour components attached to currently selected objects (excluding children, if not selected)");

    static readonly GUIContent k_RunPlayerLoop = new GUIContent("Run Player Loop", "Queue Player Loop Updates continuously");
    static readonly GUIContent k_StopPlayerLoop = new GUIContent("Stop Player Loop", "Stop Queueing Player Loop Updates");

    static readonly List<MonoBehaviour> k_RunningBehaviors = new List<MonoBehaviour>();

    [MenuItem("Window/RunInEditHelper")]
    static void OnMenuItem()
    {
        GetWindow<RunInEditHelper>("RunInEditHelper");
    }

    void OnEnable()
    {
        m_ClickableLabel = new GUIStyle { margin = new RectOffset(5, 5, 5, 5) };
    }

    void OnGUI()
    {
        using (new EditorGUI.DisabledScope(s_Updating))
        {
            if (GUILayout.Button(k_RunPlayerLoop))
            {
                s_Updating = true;
                EditorApplication.update += EditorUpdate;
            }
        }

        using (new EditorGUI.DisabledScope(!s_Updating))
        {
            if (GUILayout.Button(k_StopPlayerLoop))
            {
                s_Updating = false;
                EditorApplication.update -= EditorUpdate;
            }
        }

        if (GUILayout.Button(k_RunSelection))
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                foreach (var behaviour in gameObject.GetComponents<MonoBehaviour>())
                {
                    behaviour.runInEditMode = true;
                }
            }
        }

        if (GUILayout.Button(k_StopSelection))
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                foreach (var behaviour in gameObject.GetComponents<MonoBehaviour>())
                {
                    var wasEnabled = behaviour.enabled;
                    if (wasEnabled)
                        behaviour.enabled = false;

                    behaviour.runInEditMode = false;

                    if (wasEnabled)
                        behaviour.enabled = true;
                }
            }
        }

        k_RunningBehaviors.Clear();
        var behaviors = FindObjectsOfType<MonoBehaviour>();
        foreach (var behavior in behaviors)
        {
            if (behavior.runInEditMode)
                k_RunningBehaviors.Add(behavior);
        }

        GUILayout.Label(string.Format("Objects currently running in edit mode: {0}", k_RunningBehaviors.Count));
        using (var scrollScope = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
        {
            m_ScrollPosition = scrollScope.scrollPosition;
            foreach (var behavior in k_RunningBehaviors)
            {
                if (GUILayout.Button(behavior.name, m_ClickableLabel))
                    EditorGUIUtility.PingObject(behavior.gameObject);
            }
        }
    }

    static void EditorUpdate()
    {
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
