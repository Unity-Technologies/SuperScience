using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    public class MissingReferences : EditorWindow
    {
        const float k_Indent = 15f;
        Vector2 m_ScrollPosition;
        GUIStyle m_MissingScriptStyle;

        bool m_ShowPrefabs;
        bool m_ShowAssets;

        readonly List<GameObject> m_Prefabs = new List<GameObject>();
        readonly List<UnityObject> m_OtherAssets = new List<UnityObject>();
        readonly Dictionary<UnityObject, SerializedObject> m_SerializedObjects =
            new Dictionary<UnityObject, SerializedObject>();

        [MenuItem("Window/SuperScience/MissingReferences")]
        static void OnMenuItem()
        {
            GetWindow<MissingReferences>("MissingReferences");
        }

        void OnEnable()
        {
            m_MissingScriptStyle = new GUIStyle {richText = true};

            ScanProject();
        }

        void ScanProject()
        {
            m_Prefabs.Clear();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (Path.IsPathRooted(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && CheckForMissingRefs(prefab))
                {
                    m_Prefabs.Add(prefab);
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<UnityObject>(path);
                if (asset != null && CheckForMissingRefs(asset))
                    m_OtherAssets.Add(asset);
            }
        }

        void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
                ScanProject();

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;

                m_ShowPrefabs = EditorGUILayout.Foldout(m_ShowPrefabs, "Prefabs");
                if (m_ShowPrefabs)
                {
                    foreach (var prefab in m_Prefabs)
                    {
                        EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(k_Indent);
                            using (new EditorGUILayout.VerticalScope())
                            {
                                DrawObject(prefab);
                            }
                        }

                        EditorGUILayout.Separator();
                    }
                }

                m_ShowAssets = EditorGUILayout.Foldout(m_ShowAssets, "Assets");
                if (m_ShowAssets)
                {
                    foreach (var asset in m_OtherAssets)
                    {
                        EditorGUILayout.ObjectField(asset, typeof(GameObject), false);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(k_Indent);
                            using (new EditorGUILayout.VerticalScope())
                            {
                                DrawObject(asset);
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

            foreach (Transform child in obj.transform)
            {
                if (CheckForMissingRefs(child.gameObject))
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
            foreach (var component in gameObject.GetComponents<Component>())
            {
                DrawComponent(gameObject, component);
            }

            foreach (Transform child in gameObject.transform)
            {
                DrawObject(child.gameObject);
            }
        }

        void DrawObject(UnityObject obj)
        {
            var so = GetSerializedObjectForUnityObject(obj);

            var property = so.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    EditorGUILayout.PropertyField(property);
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

        void DrawComponentContext(GameObject gameObject, Component component)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.TextField(GetTransformPath(gameObject.transform));
                EditorGUILayout.ObjectField(component, typeof(Component), false, GUILayout.Width(150));
            }
        }

        static string GetTransformPath(Transform t, string childPath = "")
        {
            childPath = "/" + t.name + childPath;
            if (t.parent == null)
                return childPath;

            return GetTransformPath(t.parent, childPath);
        }
    }
}
