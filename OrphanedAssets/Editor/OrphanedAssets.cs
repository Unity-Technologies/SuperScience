using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

class OrphanedAssets : EditorWindow
{
    static readonly string[] k_SearchFolders = {"Assets"};
    static readonly string[] k_ExcludePaths = {"libs"};

    [MenuItem("Window/Orphaned Assets")]
    static void Init()
    {
        var window = GetWindow<OrphanedAssets>();
        window.Show();
    }

    [NonSerialized] List<string> m_OrphanedShaders;

    [NonSerialized] List<string> m_OrphanedMaterials;

    [NonSerialized] List<string> m_OrphanedTextures;

    [NonSerialized] List<string> m_OrphanedScenes;

    Vector2 m_Scroll;

    void OnEnable()
    {
        if (m_OrphanedShaders == null)
            FindOrphans();
        EditorApplication.projectWindowChanged += FindOrphans;
    }

    void OnDisable()
    {
        EditorApplication.projectWindowChanged -= FindOrphans;
    }

    void FindOrphans()
    {
        m_OrphanedShaders = AssetDatabase.FindAssets("t:shader", k_SearchFolders).ToList();
        m_OrphanedShaders.RemoveAll(guid => !AssetDatabase.GUIDToAssetPath(guid).Contains(".shader")
                                            || ExcludePath(AssetDatabase.GUIDToAssetPath(guid)));

        m_OrphanedTextures = AssetDatabase.FindAssets("t:texture", k_SearchFolders).ToList();
        m_OrphanedTextures.RemoveAll(guid =>
        {
            var materialPath = AssetDatabase.GUIDToAssetPath(guid);
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(materialPath);

            // Cubemaps can have texture dependencies
            var dependencies = EditorUtility.CollectDependencies(new[] {texture});
            foreach (var dependency in dependencies)
            {
                OnReferenceFound(dependency);
            }

            return ExcludePath(AssetDatabase.GUIDToAssetPath(guid));
        });

        m_OrphanedMaterials = AssetDatabase.FindAssets("t:material", k_SearchFolders).ToList();

        m_OrphanedScenes = AssetDatabase.FindAssets("t:scene", k_SearchFolders).ToList();

        m_OrphanedMaterials.RemoveAll(guid =>
        {
            var materialPath = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = material.shader;
            var shaderPath = AssetDatabase.GetAssetPath(shader);

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
                            m_OrphanedTextures.Remove(AssetDatabase.AssetPathToGUID(texturePath));
                        }

                        break;
                }
            }

            m_OrphanedShaders.Remove(AssetDatabase.AssetPathToGUID(shaderPath));

            return string.IsNullOrEmpty(materialPath) || materialPath.Contains(".ttf") || ExcludePath(materialPath);
        });

        var prefabs = AssetDatabase.FindAssets("t:prefab", k_SearchFolders);
        foreach (var guid in prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
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

                        OnReferenceFound(obj);
                    }
                }
            }
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

        var buildScenes = EditorBuildSettings.scenes.Select(scene => scene.guid.ToString());
        m_OrphanedScenes.RemoveAll(guid =>
        {
            var materialPath = AssetDatabase.GUIDToAssetPath(guid);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(materialPath);
            var dependencies = EditorUtility.CollectDependencies(new[] {sceneAsset});
            foreach (var dependency in dependencies)
            {
                OnReferenceFound(dependency);
            }

            return buildScenes.Contains(guid);
        });
    }

    void OnReferenceFound(UnityObject obj)
    {
        var assetPath = AssetDatabase.GetAssetPath(obj);
        if (obj is Material)
            m_OrphanedMaterials.Remove(AssetDatabase.AssetPathToGUID(assetPath));

        if (obj is Shader)
            m_OrphanedShaders.Remove(AssetDatabase.AssetPathToGUID(assetPath));

        if (obj is Texture || obj is Sprite)
            m_OrphanedTextures.Remove(AssetDatabase.AssetPathToGUID(assetPath));
    }

    static bool ExcludePath(string assetPath)
    {
        foreach (var path in k_ExcludePaths)
        {
            if (assetPath.Contains(path) || assetPath.Contains(path))
                return true;
        }

        return false;
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

    void CheckFieldInfo(Type type, MonoImporter importer)
    {
        // Only show default properties for types that support it (so far only MonoBehaviour derived types)
        if (!IsTypeCompatible(type))
            return;

        CheckFieldInfo(type.BaseType, importer);

        var infos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.DeclaredOnly);
        foreach (var field in infos)
        {
            if (!field.IsPublic)
            {
                var attr = field.GetCustomAttributes(typeof(SerializeField), true);
                if (attr == null || attr.Length == 0)
                    continue;
            }

            if (field.FieldType.IsSubclassOf(typeof(UnityObject)) || field.FieldType == typeof(UnityObject))
            {
                var reference = importer.GetDefaultReference(field.Name);

                if (typeof(Shader).IsAssignableFrom(field.FieldType))
                    m_OrphanedShaders.Remove(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference)));

                if (typeof(Material).IsAssignableFrom(field.FieldType))
                    m_OrphanedMaterials.Remove(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference)));

                if (typeof(Texture).IsAssignableFrom(field.FieldType))
                    m_OrphanedTextures.Remove(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference)));

                if (typeof(Sprite).IsAssignableFrom(field.FieldType))
                    m_OrphanedTextures.Remove(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reference)));
            }
        }
    }

    static bool IsTypeCompatible(Type type)
    {
        if (type == null || !(type.IsSubclassOf(typeof(MonoBehaviour)) || type.IsSubclassOf(typeof(ScriptableObject))))
            return false;
        return true;
    }

    void OnGUI()
    {
        if (GUILayout.Button("Refresh"))
            FindOrphans();

        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

        OrphanGUI("Orphaned Shaders", m_OrphanedShaders, typeof(Shader));
        OrphanGUI("Orphaned Materials", m_OrphanedMaterials, typeof(Material));
        OrphanGUI("Orphaned Textures", m_OrphanedTextures, typeof(Texture));
        OrphanGUI("Orphaned Scenes", m_OrphanedScenes, typeof(SceneAsset));

        EditorGUILayout.EndScrollView();
    }

    static void OrphanGUI(string header, List<string> orphans, Type type)
    {
        EditorGUILayout.LabelField(header, orphans.Count.ToString());

        foreach (var orphan in orphans)
        {
            var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(orphan), type);
            EditorGUILayout.ObjectField(asset.name, asset, type, false);
        }

        GUILayout.Space(15);
    }
}
