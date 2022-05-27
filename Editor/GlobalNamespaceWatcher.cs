using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using MonoScript = UnityEditor.MonoScript;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.SuperScience
{
    public class GlobalNamespaceWatcher : EditorWindow
    {
        class AssemblyRow
        {
            public string Path;
            public UnityObject AssemblyDefinitionAsset;

            readonly SortedList<string, MonoScript> m_Types = new SortedList<string, MonoScript>();
            int m_MonoScriptCount;
            bool m_Expanded;

            public bool Expanded { set { m_Expanded = value; } }
            public int MonoScriptTypeCount { get { return m_MonoScriptCount; } }

            public void Draw(string assemblyName, bool showOnlyMonoScriptTypes = false)
            {
                var count = showOnlyMonoScriptTypes ? m_MonoScriptCount : m_Types.Count;
                m_Expanded = EditorGUILayout.Foldout(m_Expanded, $"{assemblyName}: ({count})", true);
                if (m_Expanded)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.ObjectField(AssemblyDefinitionAsset, typeof(UnityObject), false);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (var kvp in m_Types)
                            {
                                var monoScript = kvp.Value;
                                if (showOnlyMonoScriptTypes && monoScript == null)
                                    continue;

                                EditorGUILayout.LabelField(kvp.Key);
                                EditorGUILayout.ObjectField(monoScript, typeof(MonoScript), false);
                            }
                        }
                    }
                }
            }

            public void AddType(string typeName, MonoScript monoScript)
            {
                if (monoScript != null)
                    m_MonoScriptCount++;

                m_Types.Add(typeName, monoScript);
            }
        }

        const string k_HeaderText = "Types in the global namespace";
        const string k_ShowOnlyProjectAssembliesLabel = "Show only project assemblies";
        const string k_ShowOnlyMonoScriptTypesLabel = "Show only MonoScript types";
        const string k_ExpandAllLabel = "Expand All";
        const string k_CollapseAllLabel = "Collapse All";
        const int k_LabelWidth = 200;

        static SortedList<string, AssemblyRow> s_Assemblies;

        [SerializeField]
        Vector2 m_ScrollPosition;

        [SerializeField]
        bool m_ShowOnlyProjectAssemblies = true;

        [SerializeField]
        bool m_ShowOnlyMonoScriptTypes = true;

        [MenuItem("Window/SuperScience/Global Namespace Watcher")]
        static void OnMenuItem()
        {
            GetWindow<GlobalNamespaceWatcher>("Global Namespace Watcher");
        }

        void OnEnable()
        {
            if (s_Assemblies == null)
            {
                var monoScripts = MonoImporter.GetAllRuntimeMonoScripts();
                var monoScriptDictionary = new Dictionary<string, MonoScript>(monoScripts.Length);
                foreach (var script in monoScripts)
                {
                    var scriptClass = script.GetClass();
                    if (scriptClass == null)
                        continue;

                    var className = script.GetClass().FullName;
                    if (string.IsNullOrEmpty(className))
                        continue;

                    monoScriptDictionary[className] = script;
                }

                s_Assemblies = new SortedList<string, AssemblyRow>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly == null)
                        continue;

                    try
                    {
                        var assemblyName = assembly.GetName().Name;
                        var assemblyDefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
                        UnityObject assemblyDefinition = null;
                        if (!string.IsNullOrEmpty(assemblyDefinitionPath))
                        {
                            assemblyDefinition = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assemblyDefinitionPath);
                        }

                        var addedType = false;
                        var row = new AssemblyRow {AssemblyDefinitionAsset = assemblyDefinition, Path = assembly.Location};
                        foreach (var type in assembly.GetTypes())
                        {
                            if (!string.IsNullOrEmpty(type.Namespace))
                                continue;

                            if (type.IsNested)
                                continue;

                            var hasCompilerGeneratedAttribute = false;
                            foreach (var attribute in type.CustomAttributes)
                            {
                                if (attribute.AttributeType == typeof(CompilerGeneratedAttribute))
                                {
                                    hasCompilerGeneratedAttribute = true;
                                    break;
                                }
                            }

                            if (hasCompilerGeneratedAttribute)
                                continue;

                            var typeName = type.FullName;
                            if (string.IsNullOrEmpty(typeName))
                                continue;

                            MonoScript monoScript;
                            monoScriptDictionary.TryGetValue(typeName, out monoScript);
                            row.AddType(typeName, monoScript);
                            addedType = true;
                        }

                        if (addedType)
                            s_Assemblies.Add(assemblyName, row);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        void OnGUI()
        {
            GUILayout.Label(k_HeaderText);

            EditorGUIUtility.labelWidth = k_LabelWidth;
            m_ShowOnlyProjectAssemblies = EditorGUILayout.Toggle(k_ShowOnlyProjectAssembliesLabel, m_ShowOnlyProjectAssemblies);
            m_ShowOnlyMonoScriptTypes = EditorGUILayout.Toggle(k_ShowOnlyMonoScriptTypesLabel, m_ShowOnlyMonoScriptTypes);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(k_ExpandAllLabel))
                {
                    foreach (var kvp in s_Assemblies)
                    {
                        kvp.Value.Expanded = true;
                    }
                }

                if (GUILayout.Button(k_CollapseAllLabel))
                {
                    foreach (var kvp in s_Assemblies)
                    {
                        kvp.Value.Expanded = false;
                    }
                }
            }

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                var projectPath = GetProjectPath();
                m_ScrollPosition = scrollView.scrollPosition;
                foreach (var kvp in s_Assemblies)
                {
                    var assemblyRow = kvp.Value;
                    if (m_ShowOnlyProjectAssemblies)
                    {
                        var path = assemblyRow.Path;
                        if (string.IsNullOrEmpty(path) || !path.Contains(projectPath))
                            continue;
                    }

                    if (m_ShowOnlyMonoScriptTypes && assemblyRow.MonoScriptTypeCount == 0)
                        continue;

                    assemblyRow.Draw(kvp.Key, m_ShowOnlyMonoScriptTypes);
                }
            }
        }

        static string GetProjectPath()
        {
            var path = Application.dataPath;
            var directory = new DirectoryInfo(path);
            var parent = directory.Parent;
            if (parent != null)
                return parent.FullName;

            return path;
        }
    }
}
