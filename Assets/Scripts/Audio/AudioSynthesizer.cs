using UnityEngine;

/// <summary>
/// Generates original procedural audio waveforms using PCM synthesis.
///
/// FOOTSTEPS — v2 Design (non-metallic):
///   OLD: Mathf.Sin() tone + noise → metallic ringing because sine waves
///        have a clean pitched character even at low frequencies.
///   NEW: Pure broadband noise burst filtered by a one-pole IIR low-pass
///        filter, shaped with a two-stage envelope (ultra-sharp attack
///        transient + short exponential tail). Zero sine components.
///        lpCutoff controls brightness per surface.
///
/// All other clips (block breaks, placements, animals, zombie) are unchanged.
/// </summary>
public static class AudioSynthesizer
{
    private const int SampleRate = 44100;

    // -----------------------------------------------------------------------
    // FOOTSTEPS — Noise-based, IIR-filtered, surface-specific
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates one footstep AudioClip for the given surface.
    /// variationSeed (0, 1, 2) controls subtle per-clip difference
    /// using a deterministic XorShift PRNG so clips vary but are stable.
    /// </summary>
    public static AudioClip CreateFootstepClip(BlockType surfaceType, int variationSeed = 0)
    {
        // --- Per-surface parameters ---
        float duration;       // clip length (s)
        float lpCutoff;       // IIR LP coefficient [0..1]; lower = darker/softer
        float attackDecay;    // primary transient decay rate (larger = faster)
        float tailDecay;      // secondary body decay rate
        float tailWeight;     // tail mix weight [0..1]
        float gainScale;      // loudness trim

        // Tiny detune per variation so clips feel different without changing character
        float detune = 1f + (variationSeed % 3 - 1) * 0.012f;

        switch (surfaceType)
        {
            case BlockType.Grass:
            case BlockType.Leaves:
                // Very soft, muffled earthy rustle
                duration    = 0.065f;
                lpCutoff    = Mathf.Clamp01(0.08f * detune);  // very dark
                attackDecay = 320f;
                tailDecay   = 55f;
                tailWeight  = 0.18f;
                gainScale   = 0.38f;
                break;

            case BlockType.Dirt:
                // Dry soft thud
                duration    = 0.07f;
                lpCutoff    = Mathf.Clamp01(0.12f * detune);  // dark-mid
                attackDecay = 280f;
                tailDecay   = 48f;
                tailWeight  = 0.20f;
                gainScale   = 0.40f;
                break;

            case BlockType.Sand:
                // Very muffled, hushed — softest surface
                duration    = 0.06f;
                lpCutoff    = Mathf.Clamp01(0.06f * detune);  // darkest
                attackDecay = 350f;
                tailDecay   = 60f;
                tailWeight  = 0.12f;
                gainScale   = 0.30f;
                break;

            case BlockType.Stone:
                // Hard, short bright tap — NOT metallic because no sine,
                // just wide-band noise with a faster, sharper transient
                duration    = 0.055f;
                lpCutoff    = Mathf.Clamp01(0.55f * detune);  // bright
                attackDecay = 500f;
                tailDecay   = 90f;
                tailWeight  = 0.10f;
                gainScale   = 0.42f;
                break;

            case BlockType.Wood:
                // Mid-frequency thud with brief body
                duration    = 0.075f;
                lpCutoff    = Mathf.Clamp01(0.28f * detune);  // mid
                attackDecay = 260f;
                tailDecay   = 40f;
                tailWeight  = 0.25f;
                gainScale   = 0.40f;
                break;

            case BlockType.Water:
                // Soft splash — darkest, longer tail
                duration    = 0.09f;
                lpCutoff    = Mathf.Clamp01(0.05f * detune);
                attackDecay = 200f;
                tailDecay   = 30f;
                tailWeight  = 0.40f;
                gainScale   = 0.28f;
                break;

            default:
                // Generic earthy step
                duration    = 0.065f;
                lpCutoff    = Mathf.Clamp01(0.10f * detune);
                attackDecay = 300f;
                tailDecay   = 50f;
                tailWeight  = 0.18f;
                gainScale   = 0.36f;
                break;
        }

        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        // Deterministic XorShift32 PRNG — seeded per surface + variation
        uint rng = (uint)(12345 + variationSeed * 7919 + (int)surfaceType * 1031);

        float lpState = 0f;   // IIR filter memory

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            // XorShift32 — fast, no allocation, deterministic
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            float white = (rng / (float)uint.MaxValue) * 2f - 1f;

            // One-pole IIR low-pass: y[n] = c*x[n] + (1-c)*y[n-1]
            lpState = lpCutoff * white + (1f - lpCutoff) * lpState;

            // Two-stage envelope: sharp click + soft body
            float env = Mathf.Exp(-t * attackDecay) + Mathf.Exp(-t * tailDecay) * tailWeight;

            samples[i] = lpState * env * gainScale;
        }

        // Soft-limit peak (never amplify, only prevent clipping)
        NormalizeSamples(samples, gainScale);

        AudioClip clip = AudioClip.Create(
            "Footstep_" + surfaceType + "_v" + variationSeed,
            sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip CreateBlockBreakClip(BlockType blockType)
    {
        float duration = 0.16f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        float baseFreq = 220f;
        float decaySpeed = 25f;

        switch (blockType)
        {
            case BlockType.Stone:
                baseFreq = 380f;
                decaySpeed = 30f;
                break;
            case BlockType.Wood:
                baseFreq = 240f;
                decaySpeed = 22f;
                break;
            case BlockType.Grass:
            case BlockType.Leaves:
                baseFreq = 160f;
                decaySpeed = 28f;
                break;
            case BlockType.Sand:
            case BlockType.Dirt:
                baseFreq = 140f;
                decaySpeed = 20f;
                break;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Exp(-t * decaySpeed);

            float tone1 = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
            float tone2 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 1.5f) * t) * 0.5f;
            float noise = (Random.value * 2f - 1f) * 0.6f;

            samples[i] = (tone1 + tone2 + noise) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("BlockBreak_" + blockType, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip CreateBlockPlaceClip(BlockType blockType)
    {
        float duration = 0.09f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        float pitch = 240f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Exp(-t * 50f);
            float currentFreq = Mathf.Lerp(pitch * 1.4f, pitch * 0.8f, t / duration);

            float tone = Mathf.Sin(2f * Mathf.PI * currentFreq * t);
            float noise = (Random.value * 2f - 1f) * 0.25f;

            samples[i] = (tone + noise) * envelope * 0.45f;
        }

        AudioClip clip = AudioClip.Create("BlockPlace_" + blockType, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip CreateCowMooClip()
    {
        float duration = 0.75f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            // Smooth bell envelope
            float envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);

            // Low resonant pitch modulation (130Hz -> 105Hz)
            float freq = Mathf.Lerp(130f, 105f, t / duration) + Mathf.Sin(2f * Mathf.PI * 5f * t) * 4f;
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.35f;

            samples[i] = tone * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("CowMoo", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip CreateSheepBaaClip()
    {
        float duration = 0.55f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);

            // Tremolo vibrato (rapid amplitude oscillation)
            float vibrato = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 12f * t);
            float freq = 260f + Mathf.Sin(2f * Mathf.PI * 6f * t) * 15f;
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + Mathf.Sin(2f * Mathf.PI * (freq * 1.5f) * t) * 0.3f;

            samples[i] = tone * vibrato * envelope * 0.32f;
        }

        AudioClip clip = AudioClip.Create("SheepBaa", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip CreatePigOinkClip()
    {
        float duration = 0.28f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);

            // Short downward pitch grunt (240Hz -> 140Hz)
            float freq = Mathf.Lerp(240f, 140f, t / duration);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
            float noise = (Random.value * 2f - 1f) * 0.35f;

            samples[i] = (tone + noise) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("PigOink", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public static AudioClip CreateZombieGroanClip()
    {
        float duration = 0.95f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);

            // Low guttural downward moan (95Hz -> 65Hz)
            float freq = Mathf.Lerp(95f, 65f, t / duration) + Mathf.Sin(2f * Mathf.PI * 7f * t) * 3f;
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.4f;
            float rasp = (Random.value * 2f - 1f) * 0.2f;

            samples[i] = (tone + rasp) * envelope * 0.32f;
        }

        AudioClip clip = AudioClip.Create("ZombieGroan", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // -----------------------------------------------------------------------
    // PLAYER DAMAGE & DEATH
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates a punchy blunt impact / hurt grunt sound (duration ~0.16s).
    /// Sharp transient attack + rapid downward pitch glide (140Hz -> 55Hz) with dampening.
    /// </summary>
    public static AudioClip CreatePlayerHurtClip()
    {
        float duration = 0.16f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float progress = t / duration;

            // Sharp attack (6ms) + exponential decay envelope
            float attack = Mathf.Clamp01(t / 0.006f);
            float decay = Mathf.Exp(-progress * 18f);
            float envelope = attack * decay;

            // Pitch drop: 145Hz down to 55Hz
            float freq = Mathf.Lerp(145f, 55f, Mathf.Sqrt(progress));
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);

            // Punch transient noise
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-progress * 35f);

            samples[i] = (tone * 0.75f + noise * 0.25f) * envelope * 0.5f;
        }

        NormalizeSamples(samples, 0.7f);

        AudioClip clip = AudioClip.Create("PlayerHurt", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Generates a heavy, resonant descending death impact (duration ~0.55s).
    /// </summary>
    public static AudioClip CreatePlayerDeathClip()
    {
        float duration = 0.55f;
        int sampleCount = Mathf.FloorToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float progress = t / duration;

            float attack = Mathf.Clamp01(t / 0.012f);
            float decay = Mathf.Exp(-progress * 6f);
            float envelope = attack * decay;

            // Descending sub-bass drone: 90Hz -> 35Hz
            float freq = Mathf.Lerp(90f, 35f, progress);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.5f;

            // Impact rumble
            float rumble = (Random.value * 2f - 1f) * Mathf.Exp(-progress * 15f) * 0.3f;

            samples[i] = (tone + rumble) * envelope * 0.6f;
        }

        NormalizeSamples(samples, 0.8f);

        AudioClip clip = AudioClip.Create("PlayerDeath", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Scales down samples so the peak stays at or below targetPeak.
    /// Never amplifies — only prevents clipping.
    /// </summary>
    private static void NormalizeSamples(float[] samples, float targetPeak)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Mathf.Abs(samples[i]);
            if (abs > peak) peak = abs;
        }

        if (peak > targetPeak && peak > 0f)
        {
            float scale = targetPeak / peak;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }
    }
}
