using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio manager providing 2D player-local sounds (footsteps, breaking, placing)
/// and pooled 3D spatial audio for wildlife (Cows, Sheep, Pigs) and Zombies.
/// Includes procedural audio synthesis fallback with customizable inspector clip fields.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AudioManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Volume Settings")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.5f;

    [Header("Optional Custom AudioClips (Leave empty to use procedural audio)")]
    [SerializeField] private AudioClip grassFootstepClip;
    [SerializeField] private AudioClip stoneFootstepClip;
    [SerializeField] private AudioClip woodFootstepClip;
    [SerializeField] private AudioClip sandFootstepClip;
    [SerializeField] private AudioClip dirtFootstepClip;

    [SerializeField] private AudioClip cowMooClip;
    [SerializeField] private AudioClip sheepBaaClip;
    [SerializeField] private AudioClip pigOinkClip;
    [SerializeField] private AudioClip zombieGroanClip;
    [SerializeField] private AudioClip playerHurtClip;
    [SerializeField] private AudioClip playerDeathClip;

    private AudioSource _2dSource;
    private readonly List<AudioSource> _3dSourcePool = new List<AudioSource>();
    private const int PoolSize = 10;

    // Synthesized clip cache
    // Footstep: 3 variation clips per surface for natural random selection
    private readonly Dictionary<BlockType, AudioClip[]> _footstepClips = new Dictionary<BlockType, AudioClip[]>();
    private readonly Dictionary<BlockType, AudioClip> _breakClips = new Dictionary<BlockType, AudioClip>();
    private readonly Dictionary<BlockType, AudioClip> _placeClips = new Dictionary<BlockType, AudioClip>();
    private const int FootstepVariations = 3;
    private AudioClip _synthCowClip;
    private AudioClip _synthSheepClip;
    private AudioClip _synthPigClip;
    private AudioClip _synthZombieClip;
    private AudioClip _synthPlayerHurtClip;
    private AudioClip _synthPlayerDeathClip;

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
        }
    }

    public float SfxVolume
    {
        get => sfxVolume;
        set => sfxVolume = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = masterVolume;
            InitializeAudioSources();
            InitializeClips();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void InitializeAudioSources()
    {
        // 1. 2D AudioSource for player footsteps & block interaction
        GameObject sfx2D = new GameObject("2D_SFX_Source");
        sfx2D.transform.SetParent(transform);
        _2dSource = sfx2D.AddComponent<AudioSource>();
        _2dSource.playOnAwake = false;
        _2dSource.spatialBlend = 0f; // 2D

        // 2. 3D Spatial AudioSource Pool
        GameObject poolRoot = new GameObject("3D_Spatial_Pool");
        poolRoot.transform.SetParent(transform);

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject srcObj = new GameObject("3D_AudioSource_" + i);
            srcObj.transform.SetParent(poolRoot.transform);
            AudioSource src = srcObj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1.0f; // 100% 3D spatial
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2.0f;
            src.maxDistance = 25.0f;
            src.dopplerLevel = 0f;
            _3dSourcePool.Add(src);
        }
    }

    private void InitializeClips()
    {
        // Generate procedural audio clips — 3 variation clips per surface type
        BlockType[] types = { BlockType.Grass, BlockType.Dirt, BlockType.Stone, BlockType.Wood, BlockType.Sand, BlockType.Leaves, BlockType.Water };
        foreach (BlockType type in types)
        {
            AudioClip[] variants = new AudioClip[FootstepVariations];
            for (int v = 0; v < FootstepVariations; v++)
            {
                variants[v] = AudioSynthesizer.CreateFootstepClip(type, v);
            }
            _footstepClips[type] = variants;

            // Break and place clips only for standard block types
            if (type != BlockType.Water)
            {
                _breakClips[type] = AudioSynthesizer.CreateBlockBreakClip(type);
                _placeClips[type] = AudioSynthesizer.CreateBlockPlaceClip(type);
            }
        }

        _synthCowClip = AudioSynthesizer.CreateCowMooClip();
        _synthSheepClip = AudioSynthesizer.CreateSheepBaaClip();
        _synthPigClip = AudioSynthesizer.CreatePigOinkClip();
        _synthZombieClip = AudioSynthesizer.CreateZombieGroanClip();
        _synthPlayerHurtClip = AudioSynthesizer.CreatePlayerHurtClip();
        _synthPlayerDeathClip = AudioSynthesizer.CreatePlayerDeathClip();
    }

    // -----------------------------------------------------------------------
    // Footsteps
    // -----------------------------------------------------------------------
    public void PlayFootstep(BlockType surfaceType, Vector3 position)
    {
        // 1. Try inspector-assigned custom clip first
        AudioClip clip = GetCustomFootstep(surfaceType);

        // 2. Fall back to procedural variation clips
        if (clip == null && _footstepClips.TryGetValue(surfaceType, out AudioClip[] variants) && variants.Length > 0)
        {
            clip = variants[Random.Range(0, variants.Length)];
        }

        if (clip != null && _2dSource != null)
        {
            // Subtle pitch variation — tight range prevents tonal ringing
            _2dSource.pitch  = Random.Range(0.96f, 1.04f);
            // Volume variation — soft, not jarring
            float vol = Random.Range(0.90f, 1.00f) * 0.48f * masterVolume * sfxVolume;
            _2dSource.PlayOneShot(clip, vol);
        }
    }

    private AudioClip GetCustomFootstep(BlockType type)
    {
        switch (type)
        {
            case BlockType.Grass:  return grassFootstepClip;
            case BlockType.Dirt:   return dirtFootstepClip;
            case BlockType.Stone:  return stoneFootstepClip;
            case BlockType.Wood:   return woodFootstepClip;
            case BlockType.Sand:   return sandFootstepClip;
            default:               return null;
        }
    }

    // -----------------------------------------------------------------------
    // Block Breaking & Placing
    // -----------------------------------------------------------------------
    public void PlayBlockBreak(BlockType blockType, Vector3 position)
    {
        if (_breakClips.TryGetValue(blockType, out AudioClip clip) && _2dSource != null)
        {
            _2dSource.pitch = Random.Range(0.95f, 1.05f);
            _2dSource.PlayOneShot(clip, 0.65f * masterVolume * sfxVolume);
        }
    }

    public void PlayBlockPlace(BlockType blockType, Vector3 position)
    {
        if (_placeClips.TryGetValue(blockType, out AudioClip clip) && _2dSource != null)
        {
            _2dSource.pitch = Random.Range(0.95f, 1.05f);
            _2dSource.PlayOneShot(clip, 0.55f * masterVolume * sfxVolume);
        }
    }

    // -----------------------------------------------------------------------
    // Player Health, Damage & Death Audio
    // -----------------------------------------------------------------------
    public void PlayPlayerHurt()
    {
        AudioClip clip = playerHurtClip != null ? playerHurtClip : _synthPlayerHurtClip;
        if (clip != null && _2dSource != null)
        {
            _2dSource.pitch = Random.Range(0.95f, 1.05f);
            _2dSource.PlayOneShot(clip, 0.85f * masterVolume * sfxVolume);
        }
    }

    public void PlayPlayerDeath()
    {
        AudioClip clip = playerDeathClip != null ? playerDeathClip : _synthPlayerDeathClip;
        if (clip != null && _2dSource != null)
        {
            _2dSource.pitch = 1.0f;
            _2dSource.PlayOneShot(clip, 1.0f * masterVolume * sfxVolume);
        }
    }

    // -----------------------------------------------------------------------
    // 3D Spatial Animal & Zombie Vocalizations
    // -----------------------------------------------------------------------
    public void PlayAnimalSound(AnimalType animalType, Vector3 position)
    {
        AudioClip clip = null;
        switch (animalType)
        {
            case AnimalType.Cow:
                clip = cowMooClip != null ? cowMooClip : _synthCowClip;
                break;
            case AnimalType.Sheep:
                clip = sheepBaaClip != null ? sheepBaaClip : _synthSheepClip;
                break;
            case AnimalType.Pig:
                clip = pigOinkClip != null ? pigOinkClip : _synthPigClip;
                break;
        }

        Play3DSound(clip, position, Random.Range(0.94f, 1.06f), 0.6f);
    }

    public void PlayZombieSound(Vector3 position)
    {
        PlayZombieGroan(position);
    }

    public void PlayZombieGroan(Vector3 position)
    {
        AudioClip clip = zombieGroanClip != null ? zombieGroanClip : _synthZombieClip;
        Play3DSound(clip, position, Random.Range(0.90f, 1.05f), 0.65f);
    }

    private void Play3DSound(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        if (clip == null) return;

        AudioSource availableSource = GetAvailable3DSource();
        if (availableSource != null)
        {
            availableSource.transform.position = position;
            availableSource.pitch = pitch;
            availableSource.volume = volume * masterVolume * sfxVolume;
            availableSource.clip = clip;
            availableSource.Play();
        }
    }

    private AudioSource GetAvailable3DSource()
    {
        for (int i = 0; i < _3dSourcePool.Count; i++)
        {
            if (!_3dSourcePool[i].isPlaying)
            {
                return _3dSourcePool[i];
            }
        }

        // If all busy, reuse the first one
        return _3dSourcePool.Count > 0 ? _3dSourcePool[0] : null;
    }
}
