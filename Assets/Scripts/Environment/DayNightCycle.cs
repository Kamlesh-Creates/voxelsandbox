using System;
using UnityEngine;

/// <summary>
/// Controls the day/night cycle, directional lighting, ambient sky atmosphere,
/// and the orbital visible Moon with smooth day-to-night transitions.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Cycle Settings")]
    [Tooltip("Full day/night cycle length in real-time seconds.")]
    [SerializeField] private float cycleDurationSeconds = 300f; // 5 minutes default

    [Tooltip("Starting time in 0-1 range (0.25 is sunrise, 0.5 is noon, 0.75 is sunset, 0.0 is midnight).")]
    [Range(0f, 1f)]
    [SerializeField] private float timeOfDay = 0.35f; // Morning start

    [Tooltip("Speed multiplier for testing/debugging.")]
    [SerializeField] private float timeScale = 1f;

    [Header("Lighting References")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private Transform moonTransform;

    [Header("Sun Colors & Intensity")]
    [SerializeField] private Color daySunColor = new Color(1.0f, 0.96f, 0.88f);
    [SerializeField] private Color sunsetSunColor = new Color(1.0f, 0.55f, 0.25f);
    [SerializeField] private Color nightSunColor = new Color(0.35f, 0.45f, 0.75f);
    [SerializeField] private float dayIntensity = 1.25f;
    [SerializeField] private float sunsetIntensity = 0.85f;
    [SerializeField] private float nightIntensity = 0.22f;

    [Header("Ambient Sky Colors")]
    [SerializeField] private Color daySkyColor = new Color(0.45f, 0.65f, 0.90f);
    [SerializeField] private Color sunsetSkyColor = new Color(0.65f, 0.40f, 0.35f);
    [SerializeField] private Color nightSkyColor = new Color(0.12f, 0.16f, 0.28f);

    [SerializeField] private Color dayEquatorColor = new Color(0.35f, 0.45f, 0.55f);
    [SerializeField] private Color nightEquatorColor = new Color(0.08f, 0.10f, 0.16f);

    [SerializeField] private Color dayGroundColor = new Color(0.18f, 0.22f, 0.15f);
    [SerializeField] private Color nightGroundColor = new Color(0.04f, 0.05f, 0.08f);

    [Header("Moon Settings")]
    [SerializeField] private float moonOrbitRadius = 135f;
    [SerializeField] private float moonScale = 14f;
    [SerializeField] private Color moonColor = new Color(0.92f, 0.95f, 1.0f);

    private Material _moonMaterial;
    private Renderer _moonRenderer;

    public float TimeOfDay => timeOfDay;
    public bool IsNight => timeOfDay < 0.22f || timeOfDay > 0.78f;
    public float DayNightRatio => Mathf.Clamp01(Mathf.Sin(timeOfDay * Mathf.PI * 2f - Mathf.PI * 0.5f) * 0.5f + 0.5f);

    public event Action<bool> OnDayNightChanged;
    private bool _previousIsNight;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (directionalLight == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    directionalLight = l;
                    break;
                }
            }
        }

        _previousIsNight = IsNight;
    }

    private void Start()
    {
        CreateMoonObject();
        UpdateLighting();
    }

    private void OnDestroy()
    {
        if (_moonMaterial != null)
        {
            Destroy(_moonMaterial);
        }
    }

    private void Update()
    {
        if (cycleDurationSeconds > 0.1f)
        {
            timeOfDay += (Time.deltaTime / cycleDurationSeconds) * timeScale;
            if (timeOfDay >= 1f)
            {
                timeOfDay -= 1f;
            }
        }

        UpdateLighting();

        bool currentIsNight = IsNight;
        if (currentIsNight != _previousIsNight)
        {
            _previousIsNight = currentIsNight;
            OnDayNightChanged?.Invoke(currentIsNight);
        }
    }

    private void CreateMoonObject()
    {
        if (moonTransform != null) return;

        GameObject moonObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moonObj.name = "Moon";
        moonObj.layer = LayerMask.NameToLayer("Ignore Raycast");

        // Remove collider so it never interferes with raycasts or physics
        Collider col = moonObj.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        // Set up parent hierarchy
        GameObject envObj = GameObject.Find("Environment");
        if (envObj != null)
        {
            moonObj.transform.SetParent(envObj.transform);
        }
        else
        {
            moonObj.transform.SetParent(transform);
        }

        moonObj.transform.localScale = Vector3.one * moonScale;
        moonTransform = moonObj.transform;

        // Create emissive moon material for crisp nighttime readability
        Shader shader = ShaderUtils.GetLitShader() ?? ShaderUtils.GetUnlitShader();
        if (shader == null) shader = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");

        _moonMaterial = new Material(shader)
        {
            name = "Moon_Material"
        };
        if (_moonMaterial.HasProperty("_BaseColor")) _moonMaterial.SetColor("_BaseColor", moonColor);
        if (_moonMaterial.HasProperty("_Color")) _moonMaterial.SetColor("_Color", moonColor);
        _moonMaterial.EnableKeyword("_EMISSION");
        if (_moonMaterial.HasProperty("_EmissionColor")) _moonMaterial.SetColor("_EmissionColor", moonColor * 1.8f);

        _moonRenderer = moonObj.GetComponent<Renderer>();
        if (_moonRenderer != null)
        {
            _moonRenderer.sharedMaterial = _moonMaterial;
            _moonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _moonRenderer.receiveShadows = false;
        }
    }

    private void UpdateLighting()
    {
        // 1. Sun pitch: 0 is midnight (below horizon), 0.25 is sunrise, 0.5 is noon (top), 0.75 is sunset
        float sunAngle = (timeOfDay * 360f) - 90f;
        float sunYaw = 170f; // Soft diagonal light angle across voxel landscape

        if (directionalLight != null)
        {
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0f);

            // Calculate lighting curves based on time of day
            Color targetLightColor;
            float targetIntensity;

            if (timeOfDay >= 0.20f && timeOfDay < 0.30f) // Dawn / Sunrise
            {
                float t = (timeOfDay - 0.20f) / 0.10f;
                targetLightColor = Color.Lerp(sunsetSunColor, daySunColor, t);
                targetIntensity = Mathf.Lerp(nightIntensity, dayIntensity, t);
            }
            else if (timeOfDay >= 0.30f && timeOfDay < 0.70f) // Midday
            {
                targetLightColor = daySunColor;
                targetIntensity = dayIntensity;
            }
            else if (timeOfDay >= 0.70f && timeOfDay < 0.80f) // Sunset / Dusk
            {
                float t = (timeOfDay - 0.70f) / 0.10f;
                targetLightColor = Color.Lerp(daySunColor, sunsetSunColor, t);
                targetIntensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
            }
            else // Night
            {
                targetLightColor = nightSunColor;
                targetIntensity = nightIntensity;
            }

            directionalLight.color = targetLightColor;
            directionalLight.intensity = targetIntensity;
        }

        // 2. Ambient atmosphere
        float skyT = DayNightRatio;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightSkyColor, daySkyColor, skyT);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightEquatorColor, dayEquatorColor, skyT);
        RenderSettings.ambientGroundColor = Color.Lerp(nightGroundColor, dayGroundColor, skyT);

        // 3. Moon Position & Orbit (orbits exactly opposite the sun across the world center 64, 0, 64)
        if (moonTransform != null)
        {
            Vector3 worldCenter = new Vector3(64f, 4f, 64f);
            float moonAngle = ((timeOfDay + 0.5f) * 360f) - 90f;
            Quaternion moonOrbitRot = Quaternion.Euler(moonAngle, sunYaw, 0f);

            Vector3 moonDirection = moonOrbitRot * Vector3.back;
            moonTransform.position = worldCenter + moonDirection * moonOrbitRadius;

            // Fade/hide moon slightly when deep underground during noon
            bool isAboveHorizon = moonTransform.position.y > -5f;
            if (_moonRenderer != null && _moonRenderer.enabled != isAboveHorizon)
            {
                _moonRenderer.enabled = isAboveHorizon;
            }
        }
    }
}
