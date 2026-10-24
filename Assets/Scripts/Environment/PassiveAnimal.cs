using UnityEngine;

public enum AnimalType
{
    Cow,
    Sheep,
    Pig
}

/// <summary>
/// Controls passive animal behavior (wandering, grazing, ground snapping, water/cliff avoidance)
/// and handles procedural voxel body animation.
/// </summary>
public class PassiveAnimal : MonoBehaviour
{
    [Header("Animal Config")]
    [SerializeField] private AnimalType animalType = AnimalType.Cow;
    [SerializeField] private float walkSpeed = 1.6f;
    [SerializeField] private float idleDurationMin = 2.5f;
    [SerializeField] private float idleDurationMax = 6.0f;
    [SerializeField] private float walkDurationMin = 2.0f;
    [SerializeField] private float walkDurationMax = 5.0f;

    [Header("Audio Config")]
    [SerializeField] private float vocalIntervalMin = 12.0f;
    [SerializeField] private float vocalIntervalMax = 28.0f;

    [Header("Procedural Hierarchy")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform legFL;
    [SerializeField] private Transform legFR;
    [SerializeField] private Transform legBL;
    [SerializeField] private Transform legBR;

    private VoxelWorld _world;
    private bool _isWalking;
    private float _stateTimer;
    private float _vocalTimer;
    private Vector3 _moveDirection;
    private float _walkAnimTime;
    private float _headBobTime;
    private Vector3 _initialHeadLocalPos;
    private Quaternion _initialHeadLocalRot;

    public void Setup(AnimalType type, VoxelWorld world, Transform head, Transform fl, Transform fr, Transform bl, Transform br)
    {
        animalType = type;
        _world = world;
        headTransform = head;
        legFL = fl;
        legFR = fr;
        legBL = bl;
        legBR = br;

        if (headTransform != null)
        {
            _initialHeadLocalPos = headTransform.localPosition;
            _initialHeadLocalRot = headTransform.localRotation;
        }

        switch (animalType)
        {
            case AnimalType.Cow:
                walkSpeed = 1.4f;
                break;
            case AnimalType.Sheep:
                walkSpeed = 1.6f;
                break;
            case AnimalType.Pig:
                walkSpeed = 1.8f;
                break;
        }

        ResetVocalTimer();
        EnterIdleState();
    }

    private void Start()
    {
        if (_world == null)
        {
            _world = FindFirstObjectByType<VoxelWorld>();
        }

        if (headTransform != null && _initialHeadLocalRot == Quaternion.identity)
        {
            _initialHeadLocalPos = headTransform.localPosition;
            _initialHeadLocalRot = headTransform.localRotation;
        }

        if (_vocalTimer <= 0f)
        {
            ResetVocalTimer();
        }
    }

    private void Update()
    {
        _stateTimer -= Time.deltaTime;
        _vocalTimer -= Time.deltaTime;

        if (_vocalTimer <= 0f)
        {
            PlayAnimalVocalization();
            ResetVocalTimer();
        }

        if (_stateTimer <= 0f)
        {
            if (_isWalking)
            {
                EnterIdleState();
            }
            else
            {
                EnterWalkState();
            }
        }

        if (_isWalking)
        {
            PerformWalk();
        }
        else
        {
            PerformIdle();
        }

        UpdateGroundPosition();
    }

    private void ResetVocalTimer()
    {
        _vocalTimer = Random.Range(vocalIntervalMin, vocalIntervalMax);
    }

    private void PlayAnimalVocalization()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAnimalSound(animalType, transform.position);
        }
    }

    private void EnterIdleState()
    {
        _isWalking = false;
        _stateTimer = Random.Range(idleDurationMin, idleDurationMax);
        ResetLegRotations();
    }

    private void EnterWalkState()
    {
        // Pick a random walk direction
        float randomAngle = Random.Range(0f, 360f);
        Vector3 candidateDir = new Vector3(Mathf.Cos(randomAngle * Mathf.Deg2Rad), 0f, Mathf.Sin(randomAngle * Mathf.Deg2Rad)).normalized;

        // Check if the path ahead is safe (not water, not off edge)
        if (IsDirectionSafe(candidateDir, 2f))
        {
            _moveDirection = candidateDir;
            _isWalking = true;
            _stateTimer = Random.Range(walkDurationMin, walkDurationMax);
        }
        else
        {
            // If unsafe, stay idle and retry next tick
            _isWalking = false;
            _stateTimer = 1.0f;
        }
    }

    private bool IsDirectionSafe(Vector3 direction, float distance)
    {
        Vector3 targetPos = transform.position + direction * distance;
        int targetX = Mathf.FloorToInt(targetPos.x);
        int targetZ = Mathf.FloorToInt(targetPos.z);

        if (_world != null)
        {
            // Check world bounds
            if (targetX < 4 || targetX >= _world.WorldWidth - 4 || targetZ < 4 || targetZ >= _world.WorldDepth - 4)
            {
                return false;
            }

            // Sample ground height at current vs target
            int currentY = Mathf.FloorToInt(transform.position.y);
            int targetGroundY = _world.GetTerrainSurfaceHeight(targetX, targetZ);

            // Avoid steep drop or climb (> 1 block difference)
            if (Mathf.Abs(targetGroundY - currentY) > 1)
            {
                return false;
            }

            // Avoid water
            Vector3Int blockGrid = new Vector3Int(targetX, targetGroundY, targetZ);
            VoxelBlock block = _world.GetBlock(blockGrid);
            if (block != null && block.Type == BlockType.Water)
            {
                return false;
            }
        }

        return true;
    }

    private void PerformWalk()
    {
        // Turn towards move direction
        if (_moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(_moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6f);
        }

        // Check ahead before moving
        if (!IsDirectionSafe(transform.forward, 0.6f))
        {
            EnterIdleState();
            return;
        }

        // Move forward
        transform.position += transform.forward * (walkSpeed * Time.deltaTime);

        // Animate legs
        _walkAnimTime += Time.deltaTime * (walkSpeed * 6f);
        float swing = Mathf.Sin(_walkAnimTime) * 28f;

        if (legFL != null) legFL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (legBR != null) legBR.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (legFR != null) legFR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (legBL != null) legBL.localRotation = Quaternion.Euler(-swing, 0f, 0f);

        // Subtle head swing while walking
        if (headTransform != null)
        {
            headTransform.localRotation = _initialHeadLocalRot * Quaternion.Euler(0f, Mathf.Sin(_walkAnimTime * 0.5f) * 6f, 0f);
        }
    }

    private void PerformIdle()
    {
        _headBobTime += Time.deltaTime * 1.5f;

        // Grazing head animation (lowers head every few seconds)
        if (headTransform != null)
        {
            float grazeFactor = Mathf.Clamp01(Mathf.Sin(_headBobTime) * 1.2f - 0.2f);
            float pitch = Mathf.Lerp(0f, 25f, grazeFactor);
            headTransform.localRotation = _initialHeadLocalRot * Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void ResetLegRotations()
    {
        if (legFL != null) legFL.localRotation = Quaternion.identity;
        if (legFR != null) legFR.localRotation = Quaternion.identity;
        if (legBL != null) legBL.localRotation = Quaternion.identity;
        if (legBR != null) legBR.localRotation = Quaternion.identity;
    }

    private void UpdateGroundPosition()
    {
        if (_world == null) return;

        int gridX = Mathf.FloorToInt(transform.position.x);
        int gridZ = Mathf.FloorToInt(transform.position.z);
        float targetY = _world.GetTerrainSurfaceHeight(gridX, gridZ) + 1.0f; // Stand on top of the block

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * 12f);
        transform.position = pos;
    }
}
