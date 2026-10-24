using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MainMenuSetup
{
    private static readonly string TriggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_setup_menu.txt");
    private static readonly string ResultPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "setup_menu_result.txt");

    static MainMenuSetup()
    {
        EditorApplication.update += CheckTrigger;
    }

    private static void CheckTrigger()
    {
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

            CreateMainMenuScene();
        }
    }

    [MenuItem("Build/Setup Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        string scenePath = "Assets/Scenes/MainMenu.unity";
        Debug.Log("[MainMenuSetup] Creating scene at: " + scenePath);

        // 1. Create a clean empty scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Setup Camera
        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        Camera cam = cameraObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 250f;
        cameraObj.AddComponent<AudioListener>();

        // Ensure URP Camera Data component is added if URP is active
        System.Type urpCameraDataType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (urpCameraDataType != null)
        {
            if (cameraObj.GetComponent(urpCameraDataType) == null)
            {
                cameraObj.AddComponent(urpCameraDataType);
            }
        }

        cameraObj.transform.position = new Vector3(18f, 12f, -18f);
        cameraObj.transform.rotation = Quaternion.Euler(28f, -45f, 0f);

        // 3. Setup Directional Light (Warm Sun)
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1.0f, 0.95f, 0.86f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(38f, -42f, 0f);

        // 4. Setup Lighting & Atmosphere RenderSettings
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.58f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.36f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.20f, 0.25f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.007f;
        RenderSettings.fogColor = new Color(0.68f, 0.78f, 0.90f);

        // 5. Setup MenuBackgroundDiorama
        GameObject dioramaObj = new GameObject("MenuBackgroundDiorama");
        MenuBackgroundDiorama diorama = dioramaObj.AddComponent<MenuBackgroundDiorama>();
        // Use reflection or serialized fields if needed, or Awake finds backgroundCamera
        var camField = typeof(MenuBackgroundDiorama).GetField("backgroundCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (camField != null)
        {
            camField.SetValue(diorama, cam);
        }

        // 6. Setup MainMenuController
        GameObject menuControllerObj = new GameObject("MainMenuController");
        menuControllerObj.AddComponent<MainMenuController>();

        // 7. Setup EventSystem
        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // 8. Save the Scene
        bool saved = EditorSceneManager.SaveScene(scene, scenePath);
        if (!saved)
        {
            Debug.LogError("[MainMenuSetup] Failed to save scene to: " + scenePath);
            File.WriteAllText(ResultPath, "FAILED: Scene could not be saved.");
            return;
        }

        Debug.Log("[MainMenuSetup] Scene saved successfully to: " + scenePath);

        // 9. Configure EditorBuildSettings with MainMenu at index 0 and Mainscene at index 1
        EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Mainscene.unity", true)
        };
        EditorBuildSettings.scenes = buildScenes;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string successMessage = $"SUCCESS: MainMenu.unity created and configured in Build Settings as scene 0. Scenes in build: {buildScenes.Length}";
        Debug.Log("[MainMenuSetup] " + successMessage);

        try
        {
            File.WriteAllText(ResultPath, successMessage + "\nTime: " + System.DateTime.Now);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[MainMenuSetup] Failed to write result file: " + ex.Message);
        }
    }
}
