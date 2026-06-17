using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class VrBuildDeployWindow : EditorWindow
{
    private string _outputFolder = "Builds/Android";
    private string _apkFileName = "AetheriaVR.apk";
    private bool _developmentBuild = false;
    private bool _autoRunAfterInstall = true;
    private string _packageNameOverride = "";
    private string _deviceId = "";
    private Vector2 _scroll;
    private string _lastLog = "Ready.";
    private bool _isRunning;

    [MenuItem("Aetheria/VR One-Click Build + Deploy")]
    public static void Open()
    {
        var window = GetWindow<VrBuildDeployWindow>("VR Build + Deploy");
        window.minSize = new Vector2(560f, 420f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Standalone VR Build + Deploy", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click one button to build Android APK, replace-install on connected headset via adb, and launch app. " +
            "On success you'll get: 'You can unplug and test the game on the vr now'.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(_isRunning))
        {
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            _apkFileName = EditorGUILayout.TextField("APK File Name", _apkFileName);
            _developmentBuild = EditorGUILayout.Toggle("Development Build", _developmentBuild);
            _autoRunAfterInstall = EditorGUILayout.Toggle("Auto Run After Install", _autoRunAfterInstall);
            _packageNameOverride = EditorGUILayout.TextField("Package Override (optional)", _packageNameOverride);
            _deviceId = EditorGUILayout.TextField("Device ID (optional)", _deviceId);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Build Replace APK + Deploy To VR", GUILayout.Height(36)))
                RunBuildAndDeploy();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Operation Log", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_lastLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunBuildAndDeploy()
    {
        _isRunning = true;
        try
        {
            Log("=== VR Build + Deploy Started ===");
            EnsureAndroidTarget();
            ResolveKnownXrSimulationAssetCollisions();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new Exception("No enabled scenes found in Build Settings.");

            string outputDirAbs = MakeAbsoluteOutputFolder(_outputFolder);
            Directory.CreateDirectory(outputDirAbs);
            string apkName = NormalizeApkName(_apkFileName);
            string apkPath = Path.Combine(outputDirAbs, apkName);

            Log($"Building APK: {apkPath}");
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = _developmentBuild ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Build failed: {report.summary.result}");

            Log($"Build succeeded. Size={report.summary.totalSize / (1024 * 1024f):0.00} MB");

            string adb = ResolveAdbPath();
            Log($"Using adb: {adb}");
            RunProcess(adb, "start-server");

            var devices = RunProcess(adb, "devices");
            var connected = devices.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.EndsWith("\tdevice", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Split('\t')[0])
                .ToArray();
            if (connected.Length == 0)
                throw new Exception("No connected VR device found. Connect headset and enable USB debugging.");

            string selectedDevice = string.IsNullOrWhiteSpace(_deviceId) ? connected[0] : _deviceId.Trim();
            Log($"Deploying to device: {selectedDevice}");
            RunProcess(adb, $"-s {Quote(selectedDevice)} install -r {Quote(apkPath)}");
            Log("Install succeeded.");

            if (_autoRunAfterInstall)
            {
                string packageName = string.IsNullOrWhiteSpace(_packageNameOverride)
                    ? PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)
                    : _packageNameOverride.Trim();
                if (string.IsNullOrWhiteSpace(packageName))
                    throw new Exception("Package name not found. Set Android package identifier or provide override.");

                RunProcess(adb, $"-s {Quote(selectedDevice)} shell monkey -p {Quote(packageName)} -c android.intent.category.LAUNCHER 1");
                Log($"Launch requested for package: {packageName}");
            }

            string success = "You can unplug and test the game on the vr now";
            Log(success);
            EditorUtility.DisplayDialog("VR Deploy Success", success, "OK");
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            UnityEngine.Debug.LogError("[VR Build+Deploy] " + ex);
            EditorUtility.DisplayDialog(
                "VR Deploy Failed",
                "Build/deploy failed.\n\n" + ex.Message + "\n\nCheck Console/Operation Log for details.",
                "OK");
        }
        finally
        {
            _isRunning = false;
            Repaint();
        }
    }

    private static string NormalizeApkName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "AetheriaVR.apk";
        return name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ? name : name + ".apk";
    }

    private static string MakeAbsoluteOutputFolder(string path)
    {
        if (Path.IsPathRooted(path))
            return path;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private static void EnsureAndroidTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            return;
        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        if (!switched)
            throw new Exception("Could not switch active build target to Android. Install Android Build Support in Unity Hub.");
    }

    private void ResolveKnownXrSimulationAssetCollisions()
    {
        // XR Simulation sometimes leaves temp assets that collide with fixed destination files during BuildPipeline.
        // Keep destination assets and remove only conflicting temp copies.
        var collisions = new (string tempPath, string destinationPath)[]
        {
            ("Assets/XR/Temp/XRSimulationPreferences.asset", "Assets/XR/UserSimulationSettings/Resources/XRSimulationPreferences.asset"),
            ("Assets/XR/Temp/XRSimulationRuntimeSettings.asset", "Assets/XR/Resources/XRSimulationRuntimeSettings.asset"),
        };

        bool changed = false;
        foreach (var (tempPath, destinationPath) in collisions)
        {
            bool tempExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tempPath) != null;
            bool destinationExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destinationPath) != null;
            if (!tempExists || !destinationExists)
                continue;

            if (!AssetDatabase.DeleteAsset(tempPath))
                throw new Exception($"Could not delete conflicting XR temp asset: {tempPath}");

            Log($"Removed stale XR temp asset before build: {tempPath}");
            changed = true;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static string ResolveAdbPath()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? "", "platform-tools", "adb.exe"),
            Path.Combine(Environment.GetEnvironmentVariable("ANDROID_HOME") ?? "", "platform-tools", "adb.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe"),
        };

        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                return c;
        }

        // PATH fallback
        var fromPath = FindOnPath("adb.exe");
        if (!string.IsNullOrEmpty(fromPath))
            return fromPath;

        throw new Exception("adb not found. Install Android platform-tools or set ANDROID_SDK_ROOT/ANDROID_HOME.");
    }

    private static string FindOnPath(string exeName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            string full = Path.Combine(p.Trim(), exeName);
            if (File.Exists(full)) return full;
        }
        return "";
    }

    private static ProcessResult RunProcess(string fileName, string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        p.Start();
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        var result = new ProcessResult(p.ExitCode, stdout, stderr);
        if (p.ExitCode != 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Command failed ({p.ExitCode}): {fileName} {args}");
            if (!string.IsNullOrWhiteSpace(stdout)) sb.AppendLine(stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr)) sb.AppendLine(stderr.Trim());
            throw new Exception(sb.ToString().Trim());
        }
        return result;
    }

    private void Log(string msg)
    {
        _lastLog += $"{Environment.NewLine}{DateTime.Now:HH:mm:ss}  {msg}";
        Repaint();
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

    private readonly struct ProcessResult
    {
        public readonly int ExitCode;
        public readonly string Output;
        public readonly string Error;

        public ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }
    }
}
