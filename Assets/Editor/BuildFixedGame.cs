using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class BuildFixedGame
{
    private static readonly string TriggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_build.txt");
    private static readonly string MenuTriggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_setup_menu.txt");
    private static readonly string ResultPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "build_result.txt");

    static BuildFixedGame()
    {
        EditorApplication.update += CheckTrigger;
    }

    private static void CheckTrigger()
    {
        if (File.Exists(MenuTriggerPath))
        {
            try
            {
                File.Delete(MenuTriggerPath);
            }
            catch
            {
                return;
            }

            MainMenuSetup.CreateMainMenuScene();
        }

        if (File.Exists(TriggerPath))
        {
            try
            {
                File.Delete(TriggerPath);
            }
            catch
            {
                return;
            }

            PerformBuild();
        }
    }

    [MenuItem("Build/Build Windows Fixed")]
    public static void PerformBuild()
    {
        string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows_Fixed");
        if (Directory.Exists(buildDir))
        {
            try
            {
                string oldExe = Path.Combine(buildDir, "Voxelsandbox.exe");
                if (File.Exists(oldExe)) File.Delete(oldExe);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BuildFixedGame] Could not pre-clean old exe: " + ex.Message);
            }
        }
        else
        {
            Directory.CreateDirectory(buildDir);
        }

        string exePath = Path.Combine(buildDir, "Voxelsandbox.exe");

        Debug.Log("[BuildFixedGame] Starting build to: " + exePath);

        string menuScenePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scenes", "MainMenu.unity");
        if (!File.Exists(menuScenePath))
        {
            MainMenuSetup.CreateMainMenuScene();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Mainscene.unity" },
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        string resultMessage;
        if (summary.result == BuildResult.Succeeded)
        {
            resultMessage = $"SUCCESS: Build succeeded ({summary.totalSize} bytes in {summary.totalTime.TotalSeconds:F1}s). Output: {exePath}";
            Debug.Log("[BuildFixedGame] " + resultMessage);

            try
            {
                string standardBuildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
                if (Directory.Exists(standardBuildDir))
                {
                    CopyDirectory(buildDir, standardBuildDir);
                    Debug.Log("[BuildFixedGame] Mirrored build to: " + standardBuildDir);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BuildFixedGame] Could not mirror to standard build dir: " + ex.Message);
            }
        }
        else
        {
            resultMessage = $"FAILED: Build failed with {summary.totalErrors} errors ({summary.totalWarnings} warnings).";
            Debug.LogError("[BuildFixedGame] " + resultMessage);
        }

        try
        {
            string buildsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            if (!Directory.Exists(buildsFolder)) Directory.CreateDirectory(buildsFolder);
            File.WriteAllText(ResultPath, resultMessage + "\nTime: " + System.DateTime.Now);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[BuildFixedGame] Failed to write result file: " + ex.Message);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}
