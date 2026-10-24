using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data container for a single quest objective and reward.
/// </summary>
public class Quest
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public int TargetProgress { get; }
    public int RewardXP { get; }

    public int CurrentProgress { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsClaimed { get; set; }
    public string ProgressText { get; set; }

    public Quest(string id, string title, string description, int targetProgress, int rewardXP, string initialProgressText)
    {
        Id = id;
        Title = title;
        Description = description;
        TargetProgress = targetProgress;
        RewardXP = rewardXP;
        CurrentProgress = 0;
        IsCompleted = false;
        IsClaimed = false;
        ProgressText = initialProgressText;
    }
}

/// <summary>
/// Data container for an achievement unlock.
/// </summary>
public class Achievement
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public bool IsUnlocked { get; set; }

    public Achievement(string id, string title, string description)
    {
        Id = id;
        Title = title;
        Description = description;
        IsUnlocked = false;
    }
}

/// <summary>
/// Dedicated central manager for quests and achievements.
/// Tracks progress in-memory for the current session, automatically completes objectives,
/// grants XP rewards exactly once, and queues celebratory notifications.
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    private readonly List<Quest> _quests = new List<Quest>();
    private readonly List<Achievement> _achievements = new List<Achievement>();

    // Cumulative stats for the current session
    private int _totalBlocksBroken;
    private int _totalZombiesKilled;
    private bool _nightStartedWhileAlive;
    private bool _nightSurvived;
    private bool _isEvaluating;

    public int TotalBlocksBroken => _totalBlocksBroken;
    public int TotalZombiesKilled => _totalZombiesKilled;
    public bool NightSurvived => _nightSurvived;

    public IReadOnlyList<Quest> Quests => _quests;
    public IReadOnlyList<Achievement> Achievements => _achievements;

    // Events for UI and gameplay listeners
    public event Action<Quest> OnQuestUpdated;
    public event Action<Quest> OnQuestCompleted;
    public event Action<Achievement> OnAchievementUnlocked;
    public event Action OnDataRefreshed;

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

        InitializeQuestsAndAchievements();
        EnsureQuestUI();
    }

    private void Start()
    {
        SubscribeEvents();
        EvaluateInitialState();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializeQuestsAndAchievements()
    {
        _quests.Clear();
        _achievements.Clear();

        // 5 Starter Quests
        _quests.Add(new Quest(
            "q1_first_steps",
            "First Steps",
            "Break 10 blocks.",
            10,
            25,
            "0 / 10 Blocks Broken"
        ));

        _quests.Add(new Quest(
            "q2_dedicated_miner",
            "Dedicated Miner",
            "Break 50 blocks in total.",
            50,
            75,
            "0 / 50 Blocks Broken"
        ));

        _quests.Add(new Quest(
            "q3_zombie_hunter",
            "Zombie Hunter",
            "Kill 3 zombies.",
            3,
            75,
            "0 / 3 Zombies Killed"
        ));

        _quests.Add(new Quest(
            "q4_experienced_survivor",
            "Experienced Survivor",
            "Reach Level 3.",
            3,
            100,
            "Level 1 / 3"
        ));

        _quests.Add(new Quest(
            "q5_night_survivor",
            "Night Survivor",
            "Survive one complete nighttime period and reach daytime alive.",
            1,
            50,
            "0 / 1 Nights Survived"
        ));

        // 4 Achievements
        _achievements.Add(new Achievement(
            "ach_first_break",
            "First Break",
            "Break your first block."
        ));

        _achievements.Add(new Achievement(
            "ach_zombie_slayer",
            "Zombie Slayer",
            "Kill your first zombie."
        ));

        _achievements.Add(new Achievement(
            "ach_level_up",
            "Level Up",
            "Reach Level 2 for the first time."
        ));

        _achievements.Add(new Achievement(
            "ach_master_miner",
            "Master Miner",
            "Break 100 blocks in total."
        ));
    }

    private void SubscribeEvents()
    {
        BlockInteractionController.OnBlockBrokenByPlayer += HandleBlockBroken;
        ZombieController.OnZombieKilled += HandleZombieKilled;

        if (PlayerExperience.Instance != null)
        {
            PlayerExperience.Instance.OnLevelUp += HandleLevelUp;
            PlayerExperience.Instance.OnXPChanged += HandleXPChanged;
        }

        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.OnDayNightChanged += HandleDayNightChanged;
        }

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnPlayerDied += HandlePlayerDied;
        }
    }

    private void UnsubscribeEvents()
    {
        BlockInteractionController.OnBlockBrokenByPlayer -= HandleBlockBroken;
        ZombieController.OnZombieKilled -= HandleZombieKilled;

        if (PlayerExperience.Instance != null)
        {
            PlayerExperience.Instance.OnLevelUp -= HandleLevelUp;
            PlayerExperience.Instance.OnXPChanged -= HandleXPChanged;
        }

        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnPlayerDied -= HandlePlayerDied;
        }
    }

    private void EvaluateInitialState()
    {
        if (PlayerExperience.Instance != null)
        {
            int currentLevel = PlayerExperience.Instance.CurrentLevel;
            EvaluateLevelProgress(currentLevel);
        }

        OnDataRefreshed?.Invoke();
    }

    private void HandleBlockBroken(BlockType blockType, Vector3Int gridPosition)
    {
        _totalBlocksBroken++;

        // Update Quest 1: First Steps (10 blocks)
        Quest q1 = GetQuest("q1_first_steps");
        if (q1 != null)
        {
            q1.CurrentProgress = Mathf.Clamp(_totalBlocksBroken, 0, q1.TargetProgress);
            q1.ProgressText = $"{q1.CurrentProgress} / {q1.TargetProgress} Blocks Broken";
            OnQuestUpdated?.Invoke(q1);

            if (_totalBlocksBroken >= q1.TargetProgress && !q1.IsCompleted)
            {
                CompleteQuest(q1);
            }
        }

        // Update Quest 2: Dedicated Miner (50 blocks)
        Quest q2 = GetQuest("q2_dedicated_miner");
        if (q2 != null)
        {
            q2.CurrentProgress = Mathf.Clamp(_totalBlocksBroken, 0, q2.TargetProgress);
            q2.ProgressText = $"{q2.CurrentProgress} / {q2.TargetProgress} Blocks Broken";
            OnQuestUpdated?.Invoke(q2);

            if (_totalBlocksBroken >= q2.TargetProgress && !q2.IsCompleted)
            {
                CompleteQuest(q2);
            }
        }

        // Check Achievement: First Break (1 block)
        if (_totalBlocksBroken >= 1)
        {
            UnlockAchievement("ach_first_break");
        }

        // Check Achievement: Master Miner (100 blocks)
        if (_totalBlocksBroken >= 100)
        {
            UnlockAchievement("ach_master_miner");
        }

        OnDataRefreshed?.Invoke();
    }

    private void HandleZombieKilled(ZombieController zombie)
    {
        _totalZombiesKilled++;

        // Update Quest 3: Zombie Hunter (3 zombies)
        Quest q3 = GetQuest("q3_zombie_hunter");
        if (q3 != null)
        {
            q3.CurrentProgress = Mathf.Clamp(_totalZombiesKilled, 0, q3.TargetProgress);
            q3.ProgressText = $"{q3.CurrentProgress} / {q3.TargetProgress} Zombies Killed";
            OnQuestUpdated?.Invoke(q3);

            if (_totalZombiesKilled >= q3.TargetProgress && !q3.IsCompleted)
            {
                CompleteQuest(q3);
            }
        }

        // Check Achievement: Zombie Slayer (1 zombie)
        if (_totalZombiesKilled >= 1)
        {
            UnlockAchievement("ach_zombie_slayer");
        }

        OnDataRefreshed?.Invoke();
    }

    private void HandleLevelUp(int newLevel)
    {
        EvaluateLevelProgress(newLevel);
    }

    private void HandleXPChanged(int currentXP, int xpForNextLevel, int currentLevel)
    {
        EvaluateLevelProgress(currentLevel);
    }

    private void EvaluateLevelProgress(int currentLevel)
    {
        // Update Quest 4: Experienced Survivor (Reach Level 3)
        Quest q4 = GetQuest("q4_experienced_survivor");
        if (q4 != null)
        {
            q4.CurrentProgress = Mathf.Clamp(currentLevel, 1, q4.TargetProgress);
            q4.ProgressText = $"Level {q4.CurrentProgress} / {q4.TargetProgress}";
            OnQuestUpdated?.Invoke(q4);

            if (currentLevel >= q4.TargetProgress && !q4.IsCompleted)
            {
                CompleteQuest(q4);
            }
        }

        // Check Achievement: Level Up (Reach Level 2)
        if (currentLevel >= 2)
        {
            UnlockAchievement("ach_level_up");
        }

        OnDataRefreshed?.Invoke();
    }

    private void HandleDayNightChanged(bool isNight)
    {
        if (isNight)
        {
            // Night started. Only mark eligible if player is currently alive
            if (PlayerHealth.Instance == null || !PlayerHealth.Instance.IsDead)
            {
                _nightStartedWhileAlive = true;
            }
            else
            {
                _nightStartedWhileAlive = false;
            }
        }
        else
        {
            // Daytime arrived!
            // If the player entered the night alive and is STILL alive when morning arrives, they survived the night.
            if (_nightStartedWhileAlive && (PlayerHealth.Instance == null || !PlayerHealth.Instance.IsDead))
            {
                _nightSurvived = true;

                Quest q5 = GetQuest("q5_night_survivor");
                if (q5 != null)
                {
                    q5.CurrentProgress = 1;
                    q5.ProgressText = "1 / 1 Nights Survived";
                    OnQuestUpdated?.Invoke(q5);

                    if (!q5.IsCompleted)
                    {
                        CompleteQuest(q5);
                    }
                }

                OnDataRefreshed?.Invoke();
            }

            _nightStartedWhileAlive = false;
        }
    }

    private void HandlePlayerDied()
    {
        // Player died during the night: failed to survive this night
        _nightStartedWhileAlive = false;
    }

    private void CompleteQuest(Quest quest)
    {
        if (quest == null || quest.IsCompleted) return;
        if (_isEvaluating) return;

        _isEvaluating = true;
        try
        {
            quest.IsCompleted = true;
            quest.IsClaimed = true;

            // Award XP through the existing player experience manager
            PlayerExperience px = PlayerExperience.Instance;
            if (px == null) px = FindFirstObjectByType<PlayerExperience>();
            if (px != null && quest.RewardXP > 0)
            {
                px.AddXP(quest.RewardXP);
            }

            OnQuestCompleted?.Invoke(quest);

            // Queue notification toast in QuestUI
            if (QuestUI.Instance != null)
            {
                QuestUI.Instance.QueueQuestCompletedNotification(quest);
            }
        }
        finally
        {
            _isEvaluating = false;
        }
    }

    private void UnlockAchievement(string achievementId)
    {
        Achievement ach = GetAchievement(achievementId);
        if (ach == null || ach.IsUnlocked) return;

        ach.IsUnlocked = true;
        OnAchievementUnlocked?.Invoke(ach);

        // Queue notification toast in QuestUI
        if (QuestUI.Instance != null)
        {
            QuestUI.Instance.QueueAchievementNotification(ach);
        }
    }

    public Quest GetQuest(string id)
    {
        for (int i = 0; i < _quests.Count; i++)
        {
            if (_quests[i].Id == id) return _quests[i];
        }
        return null;
    }

    public Achievement GetAchievement(string id)
    {
        for (int i = 0; i < _achievements.Count; i++)
        {
            if (_achievements[i].Id == id) return _achievements[i];
        }
        return null;
    }

    private void EnsureQuestUI()
    {
        if (FindFirstObjectByType<QuestUI>() == null)
        {
            GameObject uiObj = new GameObject("QuestUI");
            uiObj.AddComponent<QuestUI>();
        }
    }
}
