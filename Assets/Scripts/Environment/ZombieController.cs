using UnityEngine;

/// <summary>
/// Controls the original voxel Zombie creature.
/// Inactive during the day; actively tracks and approaches the player at night,
/// performing an attack swing motion within melee range.
/// </summary>
public class ZombieController : MonoBehaviour
{
    [Header("Movement & AI")]
    [SerializeField] private float wanderSpeed = 1.4f;
    [SerializeField] private float chaseSpeed = 2.4f;
    [SerializeField] private float detectionRadius = 18f;
    [SerializeField] private float attackDistance = 1.6f;

    [Header("Combat & Damage")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackInterval = 1.2f;
    private float _attackCooldownTimer;

    [Header("Health & Death")]
    [SerializeField] private int maxHealth = 30;
    private int _currentHealth;
    private bool _isDead;

    [Header("XP Rewards")]
    [SerializeField] private int xpReward = 25;
    private bool _xpAwarded;

    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => _isDead;
    public int XpReward => xpReward;

    public static event System.Action<ZombieController> OnZombieKilled;

    [Header("Audio Config")]
    [SerializeField] private float groanIntervalMin = 10.0f;
    [SerializeField] private float groanIntervalMax = 22.0f;

    [Header("Body References")]
    [SerializeField] private Transform torsoTransform;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

    private Transform _playerTransform;
    private VoxelWorld _world;
    private bool _isActiveAtNight;
    private float _wanderTimer;
    private float _groanTimer;
    private Vector3 _wanderDir;
    private float _walkAnimTime;
    private float _attackAnimTime;
    private bool _isAttacking;

    public void Setup(VoxelWorld world, Transform torso, Transform head, Transform la, Transform ra, Transform ll, Transform rl)
    {
        _world = world;
        torsoTransform = torso;
        headTransform = head;
        leftArm = la;
        rightArm = ra;
        leftLeg = ll;
        rightLeg = rl;
        ResetGroanTimer();
    }

    private void Awake()
    {
        _currentHealth = maxHealth;
        _isDead = false;
    }

    private void Start()
    {
        if (_currentHealth <= 0 && !_isDead)
        {
            _currentHealth = maxHealth;
        }

        if (_world == null)
        {
            _world = FindFirstObjectByType<VoxelWorld>();
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
        else
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _playerTransform = pc.transform;
        }

        ResetGroanTimer();
        _attackCooldownTimer = 0.5f;
    }

    private void Update()
    {
        if (_isDead) return;

        if (_attackCooldownTimer > 0f)
        {
            _attackCooldownTimer -= Time.deltaTime;
        }

        bool isNight = DayNightCycle.Instance != null ? DayNightCycle.Instance.IsNight : true;
        _isActiveAtNight = isNight;

        if (!_isActiveAtNight)
        {
            PerformDayIdle();
        }
        else
        {
            _groanTimer -= Time.deltaTime;
            if (_groanTimer <= 0f)
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayZombieGroan(transform.position);
                }
                ResetGroanTimer();
            }

            PerformNightBehavior();
        }

        UpdateGroundPosition();
    }

    private void ResetGroanTimer()
    {
        _groanTimer = Random.Range(groanIntervalMin, groanIntervalMax);
    }

    private void PerformDayIdle()
    {
        // Slump arms and head down during daytime
        if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(15f, 0f, 0f), Time.deltaTime * 3f);
        if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(15f, 0f, 0f), Time.deltaTime * 3f);
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, Time.deltaTime * 3f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, Time.deltaTime * 3f);
        if (headTransform != null) headTransform.localRotation = Quaternion.Slerp(headTransform.localRotation, Quaternion.Euler(20f, 0f, 0f), Time.deltaTime * 3f);
    }

    private void PerformNightBehavior()
    {
        // If player is dead, disengage and wander peacefully
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
        {
            _isAttacking = false;
            Wander();
            return;
        }

        if (_playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }

        float distToPlayer = _playerTransform != null ? Vector3.Distance(transform.position, _playerTransform.position) : 999f;

        if (distToPlayer <= attackDistance)
        {
            // Close enough to attack
            PerformAttack();
        }
        else if (distToPlayer <= detectionRadius)
        {
            // Chase player
            _isAttacking = false;
            ChasePlayer();
        }
        else
        {
            // Wander
            _isAttacking = false;
            Wander();
        }
    }

    private void ChasePlayer()
    {
        Vector3 dir = (_playerTransform.position - transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
        {
            dir.Normalize();
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);

            if (IsDirectionSafe(transform.forward, 0.6f))
            {
                transform.position += transform.forward * (chaseSpeed * Time.deltaTime);
                AnimateWalk(chaseSpeed * 7f);
            }
        }
    }

    private void Wander()
    {
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            _wanderTimer = Random.Range(3f, 7f);
            float angle = Random.Range(0f, 360f);
            _wanderDir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;
        }

        if (_wanderDir.sqrMagnitude > 0.01f && IsDirectionSafe(_wanderDir, 1.5f))
        {
            Quaternion targetRot = Quaternion.LookRotation(_wanderDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3f);

            if (IsDirectionSafe(transform.forward, 0.5f))
            {
                transform.position += transform.forward * (wanderSpeed * Time.deltaTime);
                AnimateWalk(wanderSpeed * 5f);
                return;
            }
        }

        // Idle standing pose if can't move
        AnimateZombiePose();
    }

    private void PerformAttack()
    {
        _isAttacking = true;
        _attackAnimTime += Time.deltaTime * 6f;

        // Face player
        if (_playerTransform != null)
        {
            Vector3 dir = (_playerTransform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), Time.deltaTime * 8f);
            }
        }

        // Swing arms up and down aggressively
        float swing = Mathf.Sin(_attackAnimTime) * 35f;
        if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-90f + swing, 0f, 0f);
        if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-90f - swing, 0f, 0f);

        if (leftLeg != null) leftLeg.localRotation = Quaternion.identity;
        if (rightLeg != null) rightLeg.localRotation = Quaternion.identity;

        // Deliver attack damage when cooldown expires
        if (_attackCooldownTimer <= 0f)
        {
            float dist = _playerTransform != null ? Vector3.Distance(transform.position, _playerTransform.position) : 999f;
            if (dist <= attackDistance)
            {
                if (PlayerHealth.Instance != null && !PlayerHealth.Instance.IsDead)
                {
                    PlayerHealth.Instance.TakeDamage(attackDamage);
                    _attackCooldownTimer = attackInterval;
                }
            }
        }
    }

    /// <summary>
    /// Inflicts damage on the zombie. Returns true if damage was successfully applied.
    /// </summary>
    public bool TakeDamage(int amount)
    {
        if (_isDead || amount <= 0) return false;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);

        // Spatial audio groan feedback on damage
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayZombieSound(transform.position);
        }

        // Slight knockback away from player for hit responsiveness
        if (_playerTransform != null)
        {
            Vector3 knockDir = (transform.position - _playerTransform.position);
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude > 0.001f)
            {
                transform.position += knockDir.normalized * 0.25f;
            }
        }

        if (_currentHealth <= 0)
        {
            if (!_xpAwarded)
            {
                _xpAwarded = true;
                PlayerExperience px = PlayerExperience.Instance;
                if (px == null) px = FindFirstObjectByType<PlayerExperience>();
                if (px != null)
                {
                    px.AddXP(xpReward);
                }

                // Notify quest and achievement systems
                OnZombieKilled?.Invoke(this);
            }

            Die();
        }

        return true;
    }

    /// <summary>
    /// Triggers zombie death exactly once: halts AI and attacks, disables colliders and visuals,
    /// and safely destroys the GameObject.
    /// </summary>
    public void Die()
    {
        if (_isDead) return;
        _isDead = true;

        _isAttacking = false;

        // Disable all colliders immediately so zombie cannot be attacked or block player
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        // Disable all renderers so visual model is removed immediately
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = false;
        }

        // Clean up zombie GameObject safely
        Destroy(gameObject, 0.1f);
    }

    /// <summary>
    /// Resets the attack cooldown timer to give the player safety breathing room (e.g. after respawn).
    /// </summary>
    public void ResetAttackCooldown(float delay = 1.5f)
    {
        if (_isDead) return;
        _attackCooldownTimer = Mathf.Max(_attackCooldownTimer, delay);
        _isAttacking = false;
    }

    private void AnimateWalk(float animSpeed)
    {
        _walkAnimTime += Time.deltaTime * animSpeed;
        float legSwing = Mathf.Sin(_walkAnimTime) * 30f;
        float armBob = Mathf.Cos(_walkAnimTime) * 10f;

        // Outstretched zombie arms with walking bob
        if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-90f + armBob, 0f, 0f);
        if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-90f - armBob, 0f, 0f);

        if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);

        if (headTransform != null)
        {
            headTransform.localRotation = Quaternion.Euler(5f, Mathf.Sin(_walkAnimTime * 0.5f) * 8f, 0f);
        }
    }

    private void AnimateZombiePose()
    {
        // Default outstretched arms
        if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(-90f, 0f, 0f), Time.deltaTime * 4f);
        if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(-90f, 0f, 0f), Time.deltaTime * 4f);
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, Time.deltaTime * 4f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, Time.deltaTime * 4f);
    }

    private bool IsDirectionSafe(Vector3 direction, float distance)
    {
        Vector3 targetPos = transform.position + direction * distance;
        int targetX = Mathf.FloorToInt(targetPos.x);
        int targetZ = Mathf.FloorToInt(targetPos.z);

        if (_world != null)
        {
            if (targetX < 4 || targetX >= _world.WorldWidth - 4 || targetZ < 4 || targetZ >= _world.WorldDepth - 4)
            {
                return false;
            }

            int currentY = Mathf.FloorToInt(transform.position.y);
            int targetGroundY = _world.GetTerrainSurfaceHeight(targetX, targetZ);

            if (Mathf.Abs(targetGroundY - currentY) > 1)
            {
                return false;
            }

            Vector3Int blockGrid = new Vector3Int(targetX, targetGroundY, targetZ);
            VoxelBlock block = _world.GetBlock(blockGrid);
            if (block != null && block.Type == BlockType.Water)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateGroundPosition()
    {
        if (_world == null) return;

        int gridX = Mathf.FloorToInt(transform.position.x);
        int gridZ = Mathf.FloorToInt(transform.position.z);
        float targetY = _world.GetTerrainSurfaceHeight(gridX, gridZ) + 1.0f; // Stand on block surface

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * 12f);
        transform.position = pos;
    }
}
