using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

class MaterialDependencies : EditorWindow
{
    static readonly string[] k_SearchFolders = { "Assets" };
    static readonly string[] k_ExcludePaths = { "libs", "Resources" };

    readonly Dictionary<string, Dictionary<Object, string>> m_Shaders = new Dictionary<string, Dictionary<Object, string>>();
    readonly Dictionary<string, Dictionary<Object, string>> m_Materials = new Dictionary<string, Dictionary<Object, string>>();
    readonly Dictionary<string, Dictionary<Object, string>> m_Textures = new Dictionary<string, Dictionary<Object, string>>();

    Vector2 m_Scroll;

    [MenuItem("Window/Material Dependencies")]
    static void Init()
    {
        var window = GetWindow<MaterialDependencies>();
        window.Show();
    }

    void OnEnable()
    {
        FindReferences();
        EditorApplication.projectWindowChanged += FindReferences;
    }

    void OnDisable()
    {
        EditorApplication.projectWindowChanged -= FindReferences;
    }

    void FindReferences()
    {
        m_Shaders.Clear();
        m_Materials.Clear();
        m_Textures.Clear();

        var shaders = AssetDatabase.FindAssets("t:shader", k_SearchFolders);
        foreach (var guid in shaders)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            var shaderPath = AssetDatabase.GetAssetPath(shader);
            if (ExcludePath(shaderPath))
                continue;

            if (!string.IsNullOrEmpty(shaderPath) && shaderPath.Contains(".shader"))
                AddToDictionary(m_Shaders, AssetDatabase.AssetPathToGUID(shaderPath));
        }

        var materials = AssetDatabase.FindAssets("t:material", k_SearchFolders);
        foreach (var guid in materials)
        {
            var materialPath = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = material.shader;
            var shaderPath = AssetDatabase.GetAssetPath(shader);

            if (IsFont(materialPath))
                continue;

            if (ExcludePath(shaderPath) || ExcludePath(materialPath))
                continue;

            var propertyCount = ShaderUtil.GetPropertyCount(shader);
            for (var i = 0; i < propertyCount; i++)
            {
                var propertyType = ShaderUtil.GetPropertyType(shader, i);
                var propertyName = ShaderUtil.GetPropertyName(shader, i);
                switch (propertyType)
                {
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var texture = material.GetTexture(propertyName);
                        if (texture)
                        {
                            var texturePath = AssetDatabase.GetAssetPath(texture);
                            AddToDictionary(m_Textures, AssetDatabase.AssetPathToGUID(texturePath), material, propertyName);
                        }

                        break;
                }
            }

            if (!string.IsNullOrEmpty(shaderPath))
            {
                var shaderGUID = AssetDatabase.AssetPathToGUID(shaderPath);
                AddToDictionary(m_Shaders, shaderGUID, material);
            }

            if (!string.IsNullOrEmpty(shaderPath) && materialPath.Contains(".mat"))
                AddToDictionary(m_Materials, guid);
        }

        var scripts = AssetDatabase.FindAssets("t:script", k_SearchFolders);
        foreach (var guid in scripts)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as MonoImporter;
            if (importer)
            {
                var script = importer.GetScript();
                CheckFieldInfo(script.GetClass(), importer);
            }
        }

        var prefabs = AssetDatabase.FindAssets("t:prefab", k_SearchFolders);
        foreach (var guid in prefabs)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(guid);

            if (IsFont(prefabPath))
                continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var serializedObjects = new List<SerializedObject>();
            SerializePrefab(serializedObjects, prefab);

            foreach (var o in serializedObjects)
            {
                var property = o.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var obj = property.objectReferenceValue;

                        if (obj == null)
                            continue;

                        var guid1 = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
                        if (obj is Material)
                            AddToDictionary(m_Materials, guid1, prefab, property.propertyPath);

                        if (obj is Shader)
                            AddToDictionary(m_Shaders, guid1, prefab, property.propertyPath);

                        if (obj is Texture || obj is Sprite)
                            AddToDictionary(m_Textures, guid1, prefab, property.propertyPath);
                    }
                }
            }
        }

        var textures = AssetDatabase.FindAssets("t:texture", k_SearchFolders);
        foreach (var guid in textures)
        {
            var texturePath = AssetDatabase.GUIDToAssetPath(guid);
            if (ExcludePath(texturePath))
                continue;

            AddToDictionary(m_Textures, AssetDatabase.AssetPathToGUID(texturePath));
        }
    }

    void CheckFieldInfo(Type type, MonoImporter importer)
    {
        // Only show default properties for types that support it (so far only MonoBehaviour derived types)
        if (!IsTypeCompatible(type))
            return;

        CheckFieldInfo(type.BaseType, importer);

        var infos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        foreach (var field in infos)
        {
            if (!field.IsPublic)
            {
                var attr = field.GetCustomAttributes(typeof(SerializeField), true);
                if (attr.Length == 0)
                    continue;
            }

            if (field.FieldType.IsSubclassOf(typeof(Object)) || field.FieldType == typeof(Object))
            {
                var reference = importer.GetDefaultReference(field.Name);

                if (reference == null)
                    continue;

                if (field.FieldType == typeof(Shader))
                    AddToDictionary(m_Shaders, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference)), importer.GetScript(), field.Name);

                if (field.FieldType == typeof(Material))
                    AddToDictionary(m_Materials, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference)), importer.GetScript(), field.Name);

                if (field.FieldType == typeof(Texture) || field.FieldType == typeof(Sprite))
                    AddToDictionary(m_Textures, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference)), importer.GetScript(), field.Name);
            }
        }
    }

    static bool IsTypeCompatible(Type type)
    {
        if (type == null || !(type.IsSubclassOf(typeof(MonoBehaviour)) || type.IsSubclassOf(typeof(ScriptableObject))))
            return false;

        return true;
    }

    static void SerializePrefab(List<SerializedObject> serializedObjects, GameObject gameObject)
    {
        serializedObjects.Add(new SerializedObject(gameObject));
        foreach (var component in gameObject.GetComponents<Component>())
        {
            if (!component)
                continue;

            serializedObjects.Add(new SerializedObject(component));
        }

        foreach (Transform child in gameObject.transform)
        {
            SerializePrefab(serializedObjects, child.gameObject);
        }
    }

    static void AddToDictionary(Dictionary<string, Dictionary<Object, string>> dict, string key, Object obj = null, string propertyPath = null)
    {
        if (obj == null)
        {
            if (!dict.ContainsKey(key))
                dict[key] = null;

            return;
        }

        Dictionary<Object, string> references;
        if (!dict.TryGetValue(key, out references) || references == null)
        {
            references = new Dictionary<Object, string>();
            dict[key] = references;
        }

        references[obj] = propertyPath;
    }

    static bool ExcludePath(string assetPath)
    {
        return k_ExcludePaths.Any(assetPath.Contains);
    }

    static bool IsFont(string assetPath)
    {
        return assetPath.Contains(".ttf");
    }

    void OnGUI()
    {
        //Ctrl + w to close
        var current = Event.current;
        if (current.Equals(Event.KeyboardEvent("^w")))
        {
            Close();
            current.Use();
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("Refresh"))
            FindReferences();

        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

        MaterialGUI("Shaders", m_Shaders, position.width);
        MaterialGUI("Materials", m_Materials, position.width);
        MaterialGUI("Textures", m_Textures, position.width);

        EditorGUILayout.EndScrollView();
    }

    static void MaterialGUI(string header, Dictionary<string, Dictionary<Object, string>> dictionary, float width)
    {
        EditorGUILayout.LabelField(header, dictionary.Count.ToString());

        foreach (var kvp in dictionary)
        {
            var guid = kvp.Key;
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
            if (asset == null)
                EditorGUILayout.LabelField("Missing", guid);
            else
                EditorGUILayout.ObjectField(string.Empty, asset, type, false);

            GUILayout.BeginHorizontal();
            GUILayout.Space(25);
            GUILayout.BeginVertical();

            var references = kvp.Value;
            if (references == null)
            {
                GUILayout.Label("No References");
            }
            else
            {
                foreach (var o in kvp.Value)
                {
                    if (o.Key == null)
                    {
                        EditorGUILayout.LabelField("Missing", "");
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        var obj = o.Key;
                        var propertyPath = o.Value;
                        if (string.IsNullOrEmpty(propertyPath))
                        {
                            EditorGUILayout.ObjectField(string.Empty, obj, obj.GetType(), false);
                        }
                        else
                        {
                            EditorGUILayout.ObjectField(string.Empty, obj, obj.GetType(), false, GUILayout.Width(width * 0.5f));
                            GUILayout.Label(propertyPath);
                        }

                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.Space(15);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
