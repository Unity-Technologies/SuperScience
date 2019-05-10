using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    public class MissingSceneReferences : EditorWindow
    {
        const float k_Indent = 15f;
        Vector2 m_ScrollPosition;
        GUIStyle m_MissingScriptStyle;

        bool m_ShowPrefabs;
        bool m_ShowAssets;

        readonly List<GameObject> m_GameObjects = new List<GameObject>();
        readonly Dictionary<UnityObject, SerializedObject> m_SerializedObjects =
            new Dictionary<UnityObject, SerializedObject>();

        [MenuItem("Window/SuperScience/Missing Scene References")]
        static void OnMenuItem() { GetWindow<MissingSceneReferences>("MissingSceneReferences"); }

        void OnEnable()
        {
            m_MissingScriptStyle = new GUIStyle { richText = true };

            ScanProject();
        }

        void ScanProject()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            m_GameObjects.Clear();
            foreach (var gameObject in activeScene.GetRootGameObjects())
            {
                ScanGameObject(gameObject);
            }
        }

        void ScanGameObject(GameObject gameObject)
        {
            if (CheckForMissingRefs(gameObject))
                m_GameObjects.Add(gameObject);

            foreach (Transform child in gameObject.transform)
            {
                ScanGameObject(child.gameObject);
            }
        }

        void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
                ScanProject();

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;

                m_ShowPrefabs = EditorGUILayout.Foldout(m_ShowPrefabs, "GameObjects");
                if (m_ShowPrefabs)
                {
                    foreach (var gameObject in m_GameObjects)
                    {
                        EditorGUILayout.ObjectField(gameObject, typeof(GameObject), false);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(k_Indent);
                            using (new EditorGUILayout.VerticalScope())
                            {
                                DrawObject(gameObject);
                            }
                        }

                        EditorGUILayout.Separator();
                    }
                }
            }
        }

        bool CheckForMissingRefs(GameObject obj)
        {
            foreach (var component in obj.GetComponents<Component>())
            {
                if (CheckForMissingRefs(component))
                    return true;
            }

            return false;
        }

        bool CheckForMissingRefs(UnityObject component)
        {
            if (component == null)
                return true;

            var so = GetSerializedObjectForUnityObject(component);

            var property = so.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    return true;
            }

            return false;
        }

        SerializedObject GetSerializedObjectForUnityObject(UnityObject component)
        {
            SerializedObject so;
            if (!m_SerializedObjects.TryGetValue(component, out so))
            {
                so = new SerializedObject(component);
                m_SerializedObjects[component] = so;
            }

            return so;
        }

        void DrawObject(GameObject gameObject)
        {
            if (!gameObject)
                return;

            foreach (var component in gameObject.GetComponents<Component>())
            {
                DrawComponent(gameObject, component);
            }
        }

        void DrawComponent(GameObject gameObject, Component component)
        {
            if (component == null)
            {
                GUILayout.Label("<color=red>Missing Script!</color>", m_MissingScriptStyle);
                DrawComponentContext(gameObject, component);
                return;
            }

            var so = GetSerializedObjectForUnityObject(component);

            var property = so.GetIterator();
            var hasMissing = false;
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                {
                    hasMissing = true;
                    EditorGUILayout.PropertyField(property);
                }
            }

            if (!hasMissing)
                return;

            DrawComponentContext(gameObject, component);
        }

        static void DrawComponentContext(GameObject gameObject, Component component)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.TextField(GetTransformPath(gameObject.transform));
                EditorGUILayout.ObjectField(component, typeof(Component), false, GUILayout.Width(200));
            }
        }

        static string GetTransformPath(Transform t, string childPath = "")
        {
            while (true)
            {
                childPath = "/" + t.name + childPath;
                if (t.parent == null) return childPath;

                t = t.parent;
            }
        }
    }
}
