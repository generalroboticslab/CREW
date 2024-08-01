#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Nakama.TinyJson;

namespace DojoEditor
{
    /// <summary>
    /// Custom configuration data structure
    /// </summary>
    [Serializable]
    public class ConfigPair
    {
        /** Configuration data */
        public DojoBuilderConfig Config;

        /** Enable build or not */
        public bool Enabled;
    }

    /// <summary>
    /// Unity editor window for building example projects
    /// </summary>
    public class DojoBuilderWindow : EditorWindow
    {
        private const string LOGSCOPE = "DojoBuilder";

        /** Array of configurations */
        public List<ConfigPair> Configs = new();
        private SerializedObject so;

        /** Menu for display current editor window */
        [MenuItem("DojoBuilder/Build All", false, -1)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(DojoBuilderWindow)) as DojoBuilderWindow;
            window.titleContent = new("Dojo Example Builder");
            window.Show();
        }

        private void OnEnable()
        {
            Configs.Clear();
            ScriptableObject target = this;
            so = new(target);

            var paths = EditorPrefs.GetString("DojoBuilder", "");
            if (!string.IsNullOrEmpty(paths))
            {
                try
                {
                    var dict = JsonParser.FromJson<Dictionary<string, bool>>(paths);
                    dict.Keys.ToList().ForEach(p =>
                    {
                        Configs.Add(new ConfigPair
                        {
                            Config = AssetDatabase.LoadAssetAtPath<DojoBuilderConfig>(p),
                            Enabled = dict[p]
                        });
                    });
                }
                catch { }
            }
        }

        private void OnDisable()
        {
            Dictionary<string, bool> paths = new();
            Configs.ForEach(c =>
            {
                paths.Add(AssetDatabase.GetAssetPath(c.Config), c.Enabled);
            });
            EditorPrefs.SetString("DojoBuilder", JsonWriter.ToJson(paths));
        }

        private void OnGUI()
        {
            GUILayout.Label("Scene build configurations", EditorStyles.boldLabel);

            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("Configs"), true);
            so.ApplyModifiedProperties();

            if (GUILayout.Button("Build"))
            {
                BuildAll();
            }
        }

        // refer to: https://github.com/game-ci/documentation/blob/main/example/BuildScript.cs
        private void BuildAll()
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.OSXStandalone.MacOSArchitecture.x64ARM64;

            Configs.ForEach(c =>
            {
                if (!c.Enabled)
                {
                    return;
                }
                if (!File.Exists(c.Config.ScenePath))
                {
                    Debug.LogError($"{LOGSCOPE}: Failed to find scene file {c.Config.ScenePath}");
                    return;
                }

                // if we are not windows, build for host machine first
#if UNITY_EDITOR_WIN
                BuildScene(c.Config, BuildTarget.StandaloneWindows, false, "Unity.exe");
                BuildScene(c.Config, BuildTarget.StandaloneWindows, true, "Unity.exe");
#elif UNITY_EDITOR_OSX
                BuildScene(c.Config, BuildTarget.StandaloneOSX, false, "Unity.app");
                BuildScene(c.Config, BuildTarget.StandaloneOSX, true, "Unity.app");
#endif

#if UNITY_EDITOR_LINUX
                BuildScene(c.Config, BuildTarget.StandaloneLinux64, false, "Unity.x86_64");
                BuildScene(c.Config, BuildTarget.StandaloneLinux64, true, "Unity.x86_64");
#else
                if (c.Config.EnableLinuxBuild)
                {
                    BuildScene(c.Config, BuildTarget.StandaloneLinux64, false, "Unity.x86_64");
                    BuildScene(c.Config, BuildTarget.StandaloneLinux64, true, "Unity.x86_64");
                }
#endif



                if (c.Config.EnableWebGLBuild)
                {
                    // generate WebGL build if requested
                    BuildScene(c.Config, BuildTarget.WebGL, false, "");
                }
            });
            Debug.Log($"{LOGSCOPE}: Build Complete!");
        }

        private void BuildScene(DojoBuilderConfig config, BuildTarget target, bool isServer, string filename)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Unknown, target))
            {
                Debug.LogWarning($"{LOGSCOPE}: Skipping non available target {target}");
                return;
            }

            var buildType = isServer ? "Server" : "Client";
            var output = Path.Join(config.OutputPath, $"{config.SceneName}-{target}-{buildType}", $"{filename}");

            ToggleGameObjectInSceneFile(config.ScenePath, config.DojoServerName, isServer);
            ToggleGameObjectInSceneFile(config.ScenePath, config.DojoClientName, !isServer);

            Debug.Log($"{LOGSCOPE}: Building {config.ScenePath} to {output} ({target})");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { config.ScenePath },
                target = target,
                locationPathName = output,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                //options = BuildOptions.Development
            };
            var summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"{LOGSCOPE}: Build Results\nDuration: {summary.totalTime}\nWarnings: {summary.totalWarnings}\nErrors: {summary.totalErrors}\nSize: {summary.totalSize}");

            CleanUpFolder(output);
        }

        private void ToggleGameObjectInSceneFile(string path, string name, bool active)
        {
            var lines = File.ReadAllLines(path).ToList();
            var locationIdx = -1;
            // locate block
            lines.Select((line, idx) => line.StartsWith("--- !u!1 ") ? idx : -1)
                .Where(idx => idx != -1).ToList()
                .ForEach(idx =>
                {
                    idx++;
                    var copyIdx = idx;
                    if (locationIdx >= 0)
                    {
                        return;
                    }
                    while (copyIdx < lines.Count)
                    {
                        var line = lines[copyIdx];
                        if (line.StartsWith("---"))
                        {
                            break;
                        }
                        if (line.Contains($"m_Name: {name}"))
                        {
                            locationIdx = idx;
                        }
                        copyIdx++;
                    }
                });
            // verify
            if (locationIdx < 0)
            {
                Debug.LogError($"{LOGSCOPE}: Failed to locate GameObject {name} in {path}");
                return;
            }
            // modify
            var toReplace = $"  m_IsActive: {(active ? 1 : 0)}";
            while (locationIdx < lines.Count)
            {
                var line = lines[locationIdx];
                if (line.StartsWith("---"))
                {
                    // we did not find m_IsActive
                    lines.Insert(locationIdx - 1, toReplace);
                    break;
                }
                if (line.Contains("m_IsActive:"))
                {
                    lines[locationIdx] = toReplace;
                    break;
                }
                locationIdx++;
            }
            // write scene file
            File.WriteAllLines(path, lines);
        }

        private void CleanUpFolder(string outputPath)
        {
            var path = Path.GetDirectoryName(outputPath);
            var target = Path.Join(path, "Unity_BurstDebugInformation_DoNotShip");
            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }
        }
    }
}


#endif
