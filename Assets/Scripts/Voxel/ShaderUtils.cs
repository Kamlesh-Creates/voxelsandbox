using UnityEngine;

/// <summary>
/// Provides safe, guaranteed shader resolution for both Editor and Standalone builds.
/// Prevents shader stripping issues in URP standalone player builds and provides safe fallbacks.
/// </summary>
public static class ShaderUtils
{
    private static Shader _cachedLitShader;
    private static Shader _cachedUnlitShader;

    public static Shader GetLitShader()
    {
        if (_cachedLitShader != null) return _cachedLitShader;

        // 1. Try standard URP Lit shader name
        _cachedLitShader = Shader.Find("Universal Render Pipeline/Lit");

        // 2. Try loading via reference material in Resources
        if (_cachedLitShader == null)
        {
            Material refMat = Resources.Load<Material>("Materials/URP_Lit_Reference");
            if (refMat != null && refMat.shader != null)
            {
                _cachedLitShader = refMat.shader;
            }
        }

        // 3. Fallbacks to other compatible URP shaders
        if (_cachedLitShader == null) _cachedLitShader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (_cachedLitShader == null) _cachedLitShader = Shader.Find("Universal Render Pipeline/Unlit");

        // 4. Fallbacks to built-in / basic shaders
        if (_cachedLitShader == null) _cachedLitShader = Shader.Find("Standard");
        if (_cachedLitShader == null) _cachedLitShader = Shader.Find("Sprites/Default");
        if (_cachedLitShader == null) _cachedLitShader = Shader.Find("Unlit/Color");

        if (_cachedLitShader == null)
        {
            Debug.LogError("[ShaderUtils] Could not find any valid Lit or fallback shader in current build.");
        }

        return _cachedLitShader;
    }

    public static Shader GetUnlitShader()
    {
        if (_cachedUnlitShader != null) return _cachedUnlitShader;

        _cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (_cachedUnlitShader == null)
        {
            Material refMat = Resources.Load<Material>("Materials/URP_Unlit_Reference");
            if (refMat != null && refMat.shader != null)
            {
                _cachedUnlitShader = refMat.shader;
            }
        }

        if (_cachedUnlitShader == null) _cachedUnlitShader = Shader.Find("Sprites/Default");
        if (_cachedUnlitShader == null) _cachedUnlitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (_cachedUnlitShader == null) _cachedUnlitShader = Shader.Find("Unlit/Color");
        if (_cachedUnlitShader == null) _cachedUnlitShader = Shader.Find("Standard");

        return _cachedUnlitShader;
    }
}
