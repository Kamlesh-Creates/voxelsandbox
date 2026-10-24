using System;
using UnityEngine;

/// <summary>
/// Manages player XP, level progression, and leveling rewards.
/// 
/// Progression Rules:
/// - Starts at Level 1 with 0 XP.
/// - Level 1 requires 100 XP to reach Level 2.
/// - Each subsequent level requires 50 more XP:
///   Level 2: 150 XP, Level 3: 200 XP, Level 4: 250 XP, etc.
///   Formula: 100 + (level - 1) * 50.
/// - Excess XP carries over toward the next level.
/// - Supports multi-level jumps if a single reward crosses multiple thresholds.
/// - State persists in memory across player deaths and respawns during the session.
/// </summary>
public class PlayerExperience : MonoBehaviour
{
    public static PlayerExperience Instance { get; private set; }

    [Header("Level & XP State")]
    [SerializeField] private int startingLevel = 1;
    [SerializeField] private int baseLevelXP = 100;
    [SerializeField] private int xpIncreasePerLevel = 50;

    private int _currentLevel = 1;
    private int _currentXP = 0;

    // Events for UI and gameplay listeners
    public event Action<int, int, int> OnXPChanged; // (currentXP, xpForNextLevel, currentLevel)
    public event Action<int> OnLevelUp;             // (newLevel)

    public int CurrentLevel => _currentLevel;
    public int CurrentXP => _currentXP;
    public int XPForNextLevel => GetXPRequiredForLevel(_currentLevel);
    public float ProgressFraction => XPForNextLevel > 0 ? Mathf.Clamp01((float)_currentXP / XPForNextLevel) : 0f;

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

        _currentLevel = Mathf.Max(1, startingLevel);
        _currentXP = 0;

        EnsureExperienceUI();
    }

    private void Start()
    {
        // Broadcast initial state so any listeners immediately update
        BroadcastState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Computes the exact XP required to advance from the given level to the next.
    /// Level 1: 100 XP, Level 2: 150 XP, Level 3: 200 XP, Level 4: 250 XP, etc.
    /// </summary>
    public int GetXPRequiredForLevel(int level)
    {
        if (level < 1) level = 1;
        return baseLevelXP + (level - 1) * xpIncreasePerLevel;
    }

    /// <summary>
    /// Returns the XP reward for breaking a specific block type:
    /// - Grass block: 2 XP
    /// - Dirt block: 2 XP
    /// - Sand block: 2 XP
    /// - Stone block: 5 XP
    /// - Wood/log block: 4 XP
    /// - Leaves block: 1 XP
    /// - Cobblestone block: 4 XP
    /// - Any other breakable block: 2 XP by default
    /// </summary>
    public static int GetBlockBreakXP(BlockType blockType)
    {
        switch (blockType)
        {
            case BlockType.Grass:
                return 2;
            case BlockType.Dirt:
                return 2;
            case BlockType.Sand:
                return 2;
            case BlockType.Stone:
                return 5;
            case BlockType.Wood:
                return 4;
            case BlockType.Leaves:
                return 1;
            default:
                if (blockType.ToString().Equals("Cobblestone", StringComparison.OrdinalIgnoreCase))
                {
                    return 4;
                }
                return 2;
        }
    }

    /// <summary>
    /// Awards XP to the player for successfully breaking a block of the given type.
    /// </summary>
    public void AwardBlockBreakXP(BlockType blockType)
    {
        int xp = GetBlockBreakXP(blockType);
        AddXP(xp);
    }

    /// <summary>
    /// Adds XP to the player. Automatically processes level-up(s), carries over excess XP,
    /// and fires notifications.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount <= 0) return;

        _currentXP += amount;

        while (_currentXP >= GetXPRequiredForLevel(_currentLevel))
        {
            _currentXP -= GetXPRequiredForLevel(_currentLevel);
            _currentLevel++;
            OnLevelUp?.Invoke(_currentLevel);
        }

        BroadcastState();
    }

    /// <summary>
    /// Broadcasts the current XP state to all registered listeners.
    /// </summary>
    public void BroadcastState()
    {
        OnXPChanged?.Invoke(_currentXP, GetXPRequiredForLevel(_currentLevel), _currentLevel);
    }

    private void EnsureExperienceUI()
    {
        if (FindFirstObjectByType<PlayerExperienceUI>() == null)
        {
            GameObject uiObj = new GameObject("PlayerExperienceUI");
            uiObj.AddComponent<PlayerExperienceUI>();
        }
    }
}
