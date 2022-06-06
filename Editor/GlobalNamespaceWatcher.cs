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
    /// <summary>
    /// This utility helps identify types in the project which are in the global namespace.
    /// </summary>
    public class GlobalNamespaceWatcher : EditorWindow
    {
        /// <summary>
        /// Container for each assembly which will be drawn to the window
        /// </summary>
        class AssemblyRow
        {
            static readonly GUIContent k_OpenGUIContent = new GUIContent("Open", "Open this script in the default script editor.");
            static readonly GUIContent k_OpenAllGUIContent = new GUIContent("Open All", "Open all scripts in this assembly's " +
                "global namespace in the default script editor.\nWARNING: This process can lock the Editor for a long time and cannot be canceled.");
            static readonly GUILayoutOption k_OpenButtonWidth = GUILayout.Width(100);

            /// <summary>
            /// The path to the assembly, sourced from Assembly.Location.
            /// </summary>
            public string Path;

            /// <summary>
            /// The AssemblyDefinition asset for this assembly, if one exists.
            /// </summary>
            public UnityObject AssemblyDefinitionAsset;

            readonly SortedList<string, MonoScript> m_Types = new SortedList<string, MonoScript>();
            int m_MonoScriptCount;
            bool m_Expanded;


            /// <summary>
            /// Used by "Expand All" and "Collapse All" buttons to control expanded state.
            /// </summary>
            public bool Expanded { set => m_Expanded = value; }

            /// <summary>
            /// The number of types with an associated MonoScript.
            /// </summary>
            public int MonoScriptTypeCount => m_MonoScriptCount;

            /// <summary>
            /// The types in this assembly.
            /// </summary>
            public SortedList<string, MonoScript> Types => m_Types;

            /// <summary>
            /// Draw this assembly row to the GUI.
            /// </summary>
            /// <param name="assemblyName">The name of the assembly.</param>
            /// <param name="showOnlyMonoScriptTypes">(Optional) Only draw types with an associated MonoScript.</param>
            public void Draw(string assemblyName, bool showOnlyMonoScriptTypes = false)
            {
                var count = showOnlyMonoScriptTypes ? m_MonoScriptCount : m_Types.Count;
                using (new EditorGUILayout.HorizontalScope())
                {
                    m_Expanded = EditorGUILayout.Foldout(m_Expanded, $"{assemblyName}: ({count})", true);
                    using (new EditorGUI.DisabledScope(m_MonoScriptCount == 0))
                    {
                        if (GUILayout.Button(k_OpenAllGUIContent, k_OpenButtonWidth))
                        {
                            foreach (var kvp in m_Types)
                            {
                                var monoScript = kvp.Value;
                                if (monoScript == null)
                                    continue;

                                AssetDatabase.OpenAsset(monoScript);
                            }
                        }
                    }
                }

                if (m_Expanded)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.ObjectField(AssemblyDefinitionAsset, typeof(UnityObject), false);
                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (var kvp in m_Types)
                            {
                                var label = kvp.Key;
                                var monoScript = kvp.Value;
                                DrawScript(showOnlyMonoScriptTypes, monoScript, label);
                            }
                        }
                    }
                }
            }

            static void DrawScript(bool showOnlyMonoScriptTypes, MonoScript monoScript, string label)
            {
                if (showOnlyMonoScriptTypes && monoScript == null)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label);
                    using (new EditorGUI.DisabledScope(monoScript == null))
                    {
                        if (GUILayout.Button(k_OpenGUIContent, k_OpenButtonWidth))
                            AssetDatabase.OpenAsset(monoScript);
                    }
                }

                EditorGUILayout.ObjectField(monoScript, typeof(MonoScript), false);
            }

            /// <summary>
            /// Add a type to this assembly row.
            /// The type will be stored in a dictionary, and if there is a MonoScript, we increment a counter to show if only MonoScript types will be shown.
            /// </summary>
            /// <param name="typeName">The name of the type.</param>
            /// <param name="monoScript">An associated MonoScript, if one exists.</param>
            public void AddType(string typeName, MonoScript monoScript)
            {
                var label = typeName;
                if (monoScript != null)
                {
                    m_MonoScriptCount++;
                    var path = AssetDatabase.GetAssetPath(monoScript);
                    if (!string.IsNullOrEmpty(path))
                        label = path;
                }

                m_Types.Add(label, monoScript);
            }
        }

        const string k_HeaderText = "Types in the global namespace";
        const string k_ShowOnlyProjectAssembliesLabel = "Show only project assemblies";
        const string k_ShowOnlyMonoScriptTypesLabel = "Show only MonoScript types";
        const string k_ExpandAllLabel = "Expand All";
        const string k_CollapseAllLabel = "Collapse All";
        const int k_LabelWidth = 200;
        const string k_CongratulationsLabel = "Congratulations! There are no types in the global namespace. :)";

        static readonly GUIContent k_OpenEverythingGUIContent = new GUIContent("Open Everything", "Open all scripts in the " +
            "global namespace in the default script editor.\nWARNING: This process can lock the Editor for a long time and cannot be canceled.");

        static SortedList<string, AssemblyRow> s_Assemblies;
        static int s_TotalMonoScriptCount;

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
                // Reset total count, just in case it's gone out of sync with s_Assemblies.
                s_TotalMonoScriptCount = 0;

                // Prepare a map of MonoScript types for fast access.
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
                        var assemblyDefinitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
                        UnityObject assemblyDefinition = null;
                        if (!string.IsNullOrEmpty(assemblyDefinitionPath))
                            assemblyDefinition = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assemblyDefinitionPath);

                        var addedType = false;
                        var row = new AssemblyRow {AssemblyDefinitionAsset = assemblyDefinition, Path = assembly.Location};
                        foreach (var type in assembly.GetTypes())
                        {
                            // If the type has a namespace, we're good!
                            if (!string.IsNullOrEmpty(type.Namespace))
                                continue;

                            // Ignore nested types--their namespace property returns null but parent type will come up and we can check it.
                            if (type.IsNested)
                                continue;

                            // Some types that aren't backed by scripts will often show up in the global namespace, but there's nothing wrong with that.
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

                            // It's possible for the type name to be null.
                            var typeName = type.FullName;
                            if (string.IsNullOrEmpty(typeName))
                                continue;

                            monoScriptDictionary.TryGetValue(typeName, out var monoScript);
                            row.AddType(typeName, monoScript);
                            addedType = true;
                        }

                        if (addedType)
                        {
                            s_Assemblies.Add(assemblyName, row);
                            s_TotalMonoScriptCount += row.MonoScriptTypeCount;
                        }
                    }
                    catch
                    {
                        // Some assemblies cause exceptions when trying to access their location or type list
                    }
                }
            }
        }

        void OnGUI()
        {
            GUILayout.Label(k_HeaderText);

            // Increase the label width from its default value so that our long labels are readable
            EditorGUIUtility.labelWidth = k_LabelWidth;
            m_ShowOnlyProjectAssemblies = EditorGUILayout.Toggle(k_ShowOnlyProjectAssembliesLabel, m_ShowOnlyProjectAssemblies);
            m_ShowOnlyMonoScriptTypes = EditorGUILayout.Toggle(k_ShowOnlyMonoScriptTypesLabel, m_ShowOnlyMonoScriptTypes);

            using (new EditorGUI.DisabledScope(s_TotalMonoScriptCount == 0))
            {
                if (GUILayout.Button(k_OpenEverythingGUIContent))
                {
                    foreach (var kvp in s_Assemblies)
                    {
                        foreach (var kvp2 in kvp.Value.Types)
                        {
                            var monoScript = kvp2.Value;
                            if (monoScript != null)
                                continue;

                            AssetDatabase.OpenAsset(monoScript);
                        }
                    }
                }
            }

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

            var showedAnyAssemblies = false;
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

                    // Generally, user scripts will have an associated MonoScript object, which is useful for finding the script in the project folder.
                    // Users of this window can click the object field and see the script get pinged in the Project view. Some types may come from pre-compiled assemblies, or files that
                    // do not define a MonoScript, so it can be useful to remove this filter. Users must search the text of their project or find some other way to locate type definition.
                    if (m_ShowOnlyMonoScriptTypes && assemblyRow.MonoScriptTypeCount == 0)
                        continue;

                    // If we've passed all the filters, draw the assembly row
                    assemblyRow.Draw(kvp.Key, m_ShowOnlyMonoScriptTypes);
                    showedAnyAssemblies = true;
                }

                // Give users a little reward for a clean project :)
                if (!showedAnyAssemblies)
                    GUILayout.Label(k_CongratulationsLabel);
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
