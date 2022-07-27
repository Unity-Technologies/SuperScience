using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using MonoScript = UnityEditor.MonoScript;
using UnityObject = UnityEngine.Object;
using Assembly = System.Reflection.Assembly;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// This utility allows users to explore the currently loaded assemblies.
    /// </summary>
    public class AssemblyExplorer : EditorWindow
    {
        /// <summary>
        /// Container for each assembly which will be drawn to the window
        /// </summary>
        class AssemblyRow
        {
            /// <summary>
            /// The assembly represented by this row
            /// </summary>
            readonly Assembly m_Assembly;

            /// <summary>
            /// The path to the assembly, sourced from Assembly.Location.
            /// </summary>
            readonly string m_Path;

            /// <summary>
            /// The AssemblyDefinition asset for this assembly, if one exists.
            /// </summary>
            UnityObject m_AssemblyDefinitionAsset;

            /// <summary>
            /// The types contained in this assembly
            /// </summary>
            SortedList<string, MonoScript> m_Types;

            int m_MonoScriptCount;
            bool m_Expanded;

            /// <summary>
            /// Used by "Expand All" and "Collapse All" buttons to control expanded state.
            /// </summary>
            public bool Expanded { set => m_Expanded = value; }

            /// <summary>
            /// The path to the assembly, sourced from Assembly.Location.
            /// </summary>
            public string Path => m_Path;

            public AssemblyRow(Assembly assembly)
            {
                m_Assembly = assembly;

                try
                {
                    m_Path = m_Assembly.Location;
                }
                catch
                {
                    // Some assemblies cause exceptions when trying to access their properties
                }
            }

            /// <summary>
            /// Draw this assembly row to the GUI.
            /// </summary>
            /// <param name="assemblyName">The name of the assembly.</param>
            /// <param name="showOnlyMonoScriptTypes">(Optional) Only draw types with an associated MonoScript.</param>
            public void Draw(string assemblyName, bool showOnlyMonoScriptTypes = false)
            {
                // Generally, user scripts will have an associated MonoScript object, which is useful for finding the script in the project folder.
                // Users of this window can click the object field and see the script get pinged in the Project view. Some types may come from pre-compiled assemblies, or files that
                // do not define a MonoScript, so it can be useful to remove this filter. Users must search the text of their project or find some other way to locate type definition.
                if (m_Types != null && showOnlyMonoScriptTypes && m_MonoScriptCount == 0)
                    return;

                var count = "?";
                if (m_Types != null)
                    count = (showOnlyMonoScriptTypes ? m_MonoScriptCount : m_Types.Count).ToString();

                m_Expanded = EditorGUILayout.Foldout(m_Expanded, $"{assemblyName}: ({count})", true);
                if (m_Expanded)
                {
                    if (m_Types == null)
                    {
                        m_Types = new SortedList<string, MonoScript>();
                        try
                        {
                            foreach (var type in m_Assembly.GetTypes())
                            {
                                // Ignore nested types--they will show up as children in the UI
                                if (type.IsNested)
                                    continue;

                                // It's possible for the type name to be null.
                                var typeName = type.FullName;
                                if (string.IsNullOrEmpty(typeName))
                                    continue;

                                s_MonoScriptDictionary.TryGetValue(typeName, out var monoScript);
                                if (monoScript != null)
                                    m_MonoScriptCount++;

                                m_Types.Add(typeName, monoScript);
                            }

                            var assemblyDefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
                            if (!string.IsNullOrEmpty(assemblyDefinitionPath))
                                m_AssemblyDefinitionAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assemblyDefinitionPath);
                        }
                        catch
                        {
                            // Some assemblies cause exceptions when trying to access their properties
                        }
                    }

                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.ObjectField(m_AssemblyDefinitionAsset, typeof(UnityObject), false);
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
        }

        const string k_HeaderText = "Currently loaded assemblies";
        const string k_ShowOnlyProjectAssembliesLabel = "Show only project assemblies";
        const string k_ShowOnlyMonoScriptTypesLabel = "Show only MonoScript types";
        const string k_ExpandAllLabel = "Expand All";
        const string k_CollapseAllLabel = "Collapse All";
        const int k_LabelWidth = 200;
        const string k_RefreshButtonText = "Refresh";

        static SortedList<string, AssemblyRow> s_Assemblies;
        static Dictionary<string, MonoScript> s_MonoScriptDictionary;

        [SerializeField]
        Vector2 m_ScrollPosition;

        [SerializeField]
        bool m_ShowOnlyProjectAssemblies = true;

        [SerializeField]
        bool m_ShowOnlyMonoScriptTypes = true;

        [MenuItem("Window/SuperScience/Assembly Explorer")]
        static void OnMenuItem()
        {
            GetWindow<AssemblyExplorer>("Assembly Explorer");
        }

        void OnEnable()
        {
            if (s_Assemblies == null)
                ScanForAssemblies();
        }

        static void ScanForAssemblies()
        {
            // Prepare a map of MonoScript types for fast access.
            var monoScripts = MonoImporter.GetAllRuntimeMonoScripts();
            s_MonoScriptDictionary = new Dictionary<string, MonoScript>(monoScripts.Length);
            foreach (var script in monoScripts)
            {
                var scriptClass = script.GetClass();
                if (scriptClass == null)
                    continue;

                var className = script.GetClass().FullName;
                if (string.IsNullOrEmpty(className))
                    continue;

                s_MonoScriptDictionary[className] = script;
            }

            // Collect information about all assemblies in the current domain.
            // Note: This will not include assemblies which are not compiled or loaded in the Editor, like some platform SDKs.
            s_Assemblies = new SortedList<string, AssemblyRow>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // It probably won't happen, but if there is a null reference in the list, skip it.
                if (assembly == null)
                    continue;

                try
                {
                    var assemblyName = assembly.GetName().Name;
                    var row = new AssemblyRow(assembly);
                    s_Assemblies.Add(assemblyName, row);
                }
                catch
                {
                    // Some assemblies cause exceptions when trying to access their properties
                }
            }
        }

        void OnGUI()
        {
            GUILayout.Label(k_HeaderText);

            if (GUILayout.Button(k_RefreshButtonText))
                ScanForAssemblies();

                // Increase the label width from its default value so that our long labels are readable
            EditorGUIUtility.labelWidth = k_LabelWidth;
            m_ShowOnlyProjectAssemblies = EditorGUILayout.Toggle(k_ShowOnlyProjectAssembliesLabel, m_ShowOnlyProjectAssemblies);
            m_ShowOnlyMonoScriptTypes = EditorGUILayout.Toggle(k_ShowOnlyMonoScriptTypesLabel, m_ShowOnlyMonoScriptTypes);

            // Give users convenient buttons to expand/collapse the assembly rows
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

                    // There are a bunch of assemblies from Unity that we can't control, so by default, only show assemblies within the project folder
                    if (m_ShowOnlyProjectAssemblies)
                    {
                        // Assemblies in Assets and Packages will be stored in Library/ScriptAssemblies, so we can filter based on whether their path string includes the project folder
                        var path = assemblyRow.Path;
                        if (string.IsNullOrEmpty(path) || !path.Contains(projectPath))
                            continue;
                    }

                    // If we've passed all the filters, draw the assembly row
                    assemblyRow.Draw(kvp.Key, m_ShowOnlyMonoScriptTypes);
                }
            }
        }

        /// <summary>
        /// Get the path to the project folder.
        /// </summary>
        /// <returns>The project folder path</returns>
        static string GetProjectPath()
        {
            // Application.dataPath returns the path including /Assets, which we need to strip off
            var path = Application.dataPath;
            var directory = new DirectoryInfo(path);
            var parent = directory.Parent;
            if (parent != null)
                return parent.FullName;

            return path;
        }
    }
}
