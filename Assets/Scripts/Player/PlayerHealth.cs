using System;
using UnityEngine;

/// <summary>
/// Manages player health, damage reception, death state, and respawning.
///
/// Features:
/// - Maximum health: 100. Starting health: 100.
/// - Safe public methods for taking damage and healing with bounds clamping.
/// - Fires events for health changes, damage feedback, player death, and respawn.
/// - Triggers death exactly once, halting gameplay (timeScale = 0) and unlocking cursor.
/// - Automatically restores health, teleports player to safe terrain, and resets zombie aggression on respawn.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;

    [Header("Safe Spawn Fallback")]
    [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(64f, 12f, 64f);

    private int _currentHealth;
    private bool _isDead;
    private Vector3 _recordedSpawnPosition;

    // Events for UI and gameplay listeners
    public event Action<int, int> OnHealthChanged; // (currentHealth, maxHealth)
    public event Action<int> OnDamageTaken;        // (damageAmount)
    public event Action OnPlayerDied;
    public event Action OnPlayerRespawned;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthFraction => maxHealth > 0 ? Mathf.Clamp01((float)_currentHealth / maxHealth) : 0f;
    public bool IsDead => _isDead;

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

        _currentHealth = maxHealth;
        _isDead = false;
        _recordedSpawnPosition = transform.position;

        EnsureHealthUI();
    }

    private void Start()
    {
        // Broadcast initial health state to any listeners
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        // Record or refine safe initial spawn position from world
        VoxelWorld world = FindFirstObjectByType<VoxelWorld>();
        if (world != null)
        {
            int startX = Mathf.FloorToInt(transform.position.x);
            int startZ = Mathf.FloorToInt(transform.position.z);
            int groundY = world.GetTerrainSurfaceHeight(startX, startZ);
            _recordedSpawnPosition = new Vector3(startX + 0.5f, groundY + 1.2f, startZ + 0.5f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Inflicts damage on the player. Rejects negative or zero values, and prevents damage while dead.
    /// Returns true if damage was successfully applied.
    /// </summary>
    public bool TakeDamage(int amount)
    {
        if (_isDead || amount <= 0) return false;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);

        // Notify health change and damage taken
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnDamageTaken?.Invoke(amount);

        // Audio feedback
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPlayerHurt();
        }

        // Check for death
        if (_currentHealth <= 0)
        {
            Die();
        }

        return true;
    }

    /// <summary>
    /// Restores player health up to maxHealth.
    /// </summary>
    public void Heal(int amount)
    {
        if (_isDead || amount <= 0) return;

        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// Triggers player death exactly once.
    /// </summary>
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Play death audio
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPlayerDeath();
        }

        // Pause time and unlock cursor for death screen interaction
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnPlayerDied?.Invoke();
    }

    /// <summary>
    /// Respawns the player: restores full health, resets death state, teleports to safe ground,
    /// resets zombie aggression, and restores timescale and cursor locking.
    /// </summary>
    public void Respawn()
    {
        if (!_isDead) return;

        _currentHealth = maxHealth;
        _isDead = false;

        // Determine safe spawn position
        Vector3 safeSpawn = CalculateSafeSpawnPosition();

        // Move player safely via PlayerController
        PlayerController player = GetComponent<PlayerController>();
        if (player == null) player = FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            player.ResetToPosition(safeSpawn);
        }
        else
        {
            transform.position = safeSpawn;
        }

        // Reset attack cooldowns of all active zombies so player is not instantly struck
        ZombieController[] zombies = FindObjectsByType<ZombieController>();
        for (int i = 0; i < zombies.Length; i++)
        {
            if (zombies[i] != null)
            {
                zombies[i].ResetAttackCooldown(2.5f);
            }
        }

        // Restore gameplay timescale and lock cursor
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Notify listeners
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnPlayerRespawned?.Invoke();
    }

    /// <summary>
    /// Computes a safe non-water, non-void spawn position above the solid terrain surface.
    /// </summary>
    public Vector3 CalculateSafeSpawnPosition()
    {
        VoxelWorld world = FindFirstObjectByType<VoxelWorld>();
        if (world != null)
        {
            int targetX = Mathf.Clamp(Mathf.FloorToInt(_recordedSpawnPosition.x), 4, world.WorldWidth - 5);
            int targetZ = Mathf.Clamp(Mathf.FloorToInt(_recordedSpawnPosition.z), 4, world.WorldDepth - 5);

            // Search outward in a spiral if the exact coordinate is water or invalid
            for (int r = 0; r <= 8; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        int checkX = targetX + dx;
                        int checkZ = targetZ + dz;
                        if (checkX >= 4 && checkX < world.WorldWidth - 4 && checkZ >= 4 && checkZ < world.WorldDepth - 4)
                        {
                            int groundY = world.GetTerrainSurfaceHeight(checkX, checkZ);
                            VoxelBlock block = world.GetBlock(new Vector3Int(checkX, groundY, checkZ));
                            if (block != null && block.Type != BlockType.Water && block.Type != BlockType.Air)
                            {
                                return new Vector3(checkX + 0.5f, groundY + 1.2f, checkZ + 0.5f);
                            }
                        }
                    }
                }
            }

            int fallbackY = world.GetTerrainSurfaceHeight(targetX, targetZ);
            return new Vector3(targetX + 0.5f, fallbackY + 1.2f, targetZ + 0.5f);
        }

        return fallbackSpawnPosition;
    }

    private void EnsureHealthUI()
    {
        if (FindFirstObjectByType<PlayerHealthUI>() == null)
        {
            GameObject uiObj = new GameObject("PlayerHealthUI");
            uiObj.AddComponent<PlayerHealthUI>();
        }
    }
}
