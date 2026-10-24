using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles raycast targeting from the first-person camera for:
/// - Left-Click: Breaking voxel blocks with the equipped Axe and spawning item drops.
/// - Right-Click: Placing the currently selected hotbar block on adjacent faces with player collision safety.
/// </summary>
public class BlockInteractionController : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float maxReachDistance = 6.0f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask blockLayerMask = ~0;

    [Header("Melee Combat Settings")]
    [SerializeField] private float attackRange = 3.0f;
    [SerializeField] private int axeAttackDamage = 10;
    [SerializeField] private float attackCooldown = 0.4f;
    private float _attackCooldownTimer;
    private ZombieController _targetedZombie;

    // Public targeting state for BlockHighlightController and external systems
    public bool HasTarget => _hasTarget;
    public Vector3Int TargetGridPosition => _targetGridPosition;
    public BlockType TargetBlockType => _targetBlockType;
    public VoxelBlock TargetBlock => _targetBlock;
    public RaycastHit TargetHit => _targetHit;
    public Vector3Int TargetNormal => _targetNormal;
    public float MaxReachDistance => maxReachDistance;
    public Camera PlayerCamera => playerCamera;
    public LayerMask BlockLayerMask => blockLayerMask;

    public ZombieController TargetedZombie => _targetedZombie;
    public bool HasZombieTarget => _targetedZombie != null;
    public float AttackRange => attackRange;
    public int AxeAttackDamage => axeAttackDamage;
    public float AttackCooldown => attackCooldown;

    public event System.Action<bool, Vector3Int, BlockType> OnTargetBlockChanged;
    public static event System.Action<BlockType, Vector3Int> OnBlockBrokenByPlayer;

    private bool _hasTarget;
    private Vector3Int _targetGridPosition;
    private BlockType _targetBlockType = BlockType.Air;
    private VoxelBlock _targetBlock;
    private RaycastHit _targetHit;
    private Vector3Int _targetNormal;

    private readonly RaycastHit[] _raycastHits = new RaycastHit[16];
    private static readonly System.Collections.Generic.IComparer<RaycastHit> HitDistanceComparer = new RaycastHitComparer();

    private class RaycastHitComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }

    private VoxelWorld _world;
    private HotbarController _hotbarController;
    private EquippedItemController _equippedItemController;
    private CharacterController _playerCharacterController;
    private Transform _playerTransform;

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;
        }

        _world = FindFirstObjectByType<VoxelWorld>();
        if (_world != null)
        {
            _world.OnBlockChanged += HandleBlockChanged;
        }

        _hotbarController = FindFirstObjectByType<HotbarController>();
        _equippedItemController = FindFirstObjectByType<EquippedItemController>();

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            _playerCharacterController = player.GetComponent<CharacterController>();
            _playerTransform = player.transform;
        }

        // Ensure InventoryController exists
        if (InventoryController.Instance == null)
        {
            GameObject invObj = new GameObject("InventoryController");
            invObj.AddComponent<InventoryController>();
        }

        // Ensure BlockHighlightController exists on this camera/object
        if (GetComponent<BlockHighlightController>() == null && FindFirstObjectByType<BlockHighlightController>() == null)
        {
            gameObject.AddComponent<BlockHighlightController>();
        }
    }

    private void OnDestroy()
    {
        if (_world != null)
        {
            _world.OnBlockChanged -= HandleBlockChanged;
        }
    }

    private void HandleBlockChanged(Vector3Int changedPos)
    {
        // Re-evaluate target immediately if the changed block is our current target or directly adjacent
        if (_hasTarget && (_targetGridPosition == changedPos || Vector3Int.Distance(_targetGridPosition, changedPos) <= 1.5f))
        {
            UpdateTargetRaycast();
        }
    }

    private void Update()
    {
        // Suppress targeting, block highlight, and interactions while paused, dead, or viewing quests
        if (PauseMenuController.IsPaused || Time.timeScale <= 0f || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) || QuestUI.IsOpen)
        {
            _targetedZombie = null;
            ClearTarget();
            return;
        }

        if (_attackCooldownTimer > 0f)
        {
            _attackCooldownTimer -= Time.deltaTime;
        }

        UpdateTargetRaycast();

        // Only process block interactions when the cursor is locked (i.e. the game
        // window has focus and the player is in first-person control).
        // This prevents the cursor-re-lock click (fired when pressing LMB after
        // pressing Escape) from also triggering a break/place attempt on the
        // same frame — which always fails silently and makes breaking feel unreliable.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // Left Click = Attack Zombie OR Break Block (with Axe)
        if (mouse.leftButton.wasPressedThisFrame || (mouse.leftButton.isPressed && _attackCooldownTimer <= 0f && _targetedZombie != null))
        {
            TryHandleLeftClick();
        }

        // Miss-swing: axe equipped, LMB just pressed, cooldown ready, but nothing targeted - still play animation
        if (mouse.leftButton.wasPressedThisFrame && _attackCooldownTimer <= 0f &&
            !_hasTarget && _targetedZombie == null &&
            (_hotbarController == null || _hotbarController.SelectedIndex == 0))
        {
            if (_equippedItemController != null)
            {
                _equippedItemController.TriggerSwing();
            }
        }

        // Right Click = Place Block (with selected block from Hotbar)
        if (mouse.rightButton.wasPressedThisFrame)
        {
            TryPlaceAdjacentBlock();
        }
    }

    public void UpdateTargetRaycast()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null)
            {
                ClearTarget();
                return;
            }
        }

        if (_world == null)
        {
            _world = FindFirstObjectByType<VoxelWorld>();
            if (_world != null)
            {
                _world.OnBlockChanged += HandleBlockChanged;
            }
        }

        if (_world == null)
        {
            ClearTarget();
            return;
        }

        if (_playerTransform == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerCharacterController = player.GetComponent<CharacterController>();
                _playerTransform = player.transform;
            }
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int hitCount = Physics.RaycastNonAlloc(ray, _raycastHits, maxReachDistance, blockLayerMask, QueryTriggerInteraction.Ignore);

        _targetedZombie = null;

        if (hitCount > 0)
        {
            if (hitCount > 1)
            {
                System.Array.Sort(_raycastHits, 0, hitCount, HitDistanceComparer);
            }

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastHits[i];

                // Skip player's character controller or any player body parts
                if (_playerCharacterController != null && hit.collider == _playerCharacterController)
                    continue;
                if (_playerTransform != null && hit.collider.transform.IsChildOf(_playerTransform))
                    continue;

                // Priority check: Is the targeted object a living Zombie?
                ZombieController zombie = hit.collider.GetComponentInParent<ZombieController>();
                if (zombie != null && !zombie.IsDead)
                {
                    // Target zombie if within melee attack range (~3.0 units)
                    if (hit.distance <= attackRange)
                    {
                        _targetedZombie = zombie;
                    }

                    // A zombie physically occludes voxel blocks behind it
                    ClearTarget();
                    return;
                }

                // Skip non-voxel entities (e.g. passive animals or pickups) so they are never misidentified as blocks
                if (hit.collider.GetComponentInParent<PassiveAnimal>() != null || hit.collider.GetComponentInParent<ItemPickup>() != null)
                {
                    ClearTarget();
                    return;
                }

                // Priority 1: Direct voxel block grid coordinate from block GameObject transform
                Vector3 colPos = hit.collider.transform.position;
                Vector3Int colliderGridPos = new Vector3Int(
                    Mathf.RoundToInt(colPos.x - 0.5f),
                    Mathf.RoundToInt(colPos.y - 0.5f),
                    Mathf.RoundToInt(colPos.z - 0.5f)
                );

                if (IsValidGridPosition(colliderGridPos))
                {
                    VoxelBlock block = _world.GetBlock(colliderGridPos);
                    if (block != null && block.Type != BlockType.Air && block.Type != BlockType.Water)
                    {
                        SetTarget(colliderGridPos, block, hit);
                        return;
                    }
                }

                // Priority 2: Standard face-inward calculation fallback
                Vector3 insidePoint = hit.point - hit.normal * 0.01f;
                Vector3Int gridPosition = new Vector3Int(
                    Mathf.FloorToInt(insidePoint.x),
                    Mathf.FloorToInt(insidePoint.y),
                    Mathf.FloorToInt(insidePoint.z)
                );

                if (IsValidGridPosition(gridPosition))
                {
                    VoxelBlock block = _world.GetBlock(gridPosition);
                    if (block != null && block.Type != BlockType.Air && block.Type != BlockType.Water)
                    {
                        SetTarget(gridPosition, block, hit);
                        return;
                    }
                }
            }
        }

        ClearTarget();
    }

    private bool IsValidGridPosition(Vector3Int pos)
    {
        return _world != null &&
               pos.x >= 0 && pos.x < _world.WorldWidth &&
               pos.z >= 0 && pos.z < _world.WorldDepth &&
               pos.y >= 0 && pos.y <= _world.MaximumTerrainHeight * 3;
    }

    private void SetTarget(Vector3Int gridPosition, VoxelBlock block, RaycastHit hit)
    {
        bool changed = !_hasTarget || _targetGridPosition != gridPosition || _targetBlock != block;

        _hasTarget = true;
        _targetGridPosition = gridPosition;
        _targetBlock = block;
        _targetBlockType = block.Type;
        _targetHit = hit;
        _targetNormal = Vector3Int.RoundToInt(hit.normal);

        if (changed)
        {
            OnTargetBlockChanged?.Invoke(true, _targetGridPosition, _targetBlockType);
        }
    }

    private void ClearTarget()
    {
        if (_hasTarget)
        {
            _hasTarget = false;
            _targetBlock = null;
            _targetBlockType = BlockType.Air;
            _targetGridPosition = Vector3Int.zero;
            _targetNormal = Vector3Int.zero;
            OnTargetBlockChanged?.Invoke(false, Vector3Int.zero, BlockType.Air);
        }
    }

    private void TryHandleLeftClick()
    {
        // Only allow axe attacks or block breaks when holding the Axe (Hotbar Slot 1, index 0)
        bool holdingAxe = (_hotbarController == null || _hotbarController.SelectedIndex == 0);

        if (holdingAxe && _targetedZombie != null)
        {
            TryAttackZombie();
            return;
        }

        // Only try breaking blocks on explicit press
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryBreakTargetedBlock();
        }
    }

    private void TryAttackZombie()
    {
        if (_targetedZombie == null || _targetedZombie.IsDead) return;
        if (_attackCooldownTimer > 0f) return;

        // Apply attack cooldown to prevent unlimited damage per frame
        _attackCooldownTimer = attackCooldown;

        // Trigger axe swing visual feedback
        if (_equippedItemController != null)
        {
            _equippedItemController.TriggerSwing();
        }

        // Inflict damage on the targeted zombie
        _targetedZombie.TakeDamage(axeAttackDamage);

        // Immediately update raycast target state
        UpdateTargetRaycast();
    }

    private void TryBreakTargetedBlock()
    {
        if (playerCamera == null) return;
        if (_world == null) _world = FindFirstObjectByType<VoxelWorld>();

        // Only allow breaking when holding the Axe (Hotbar Slot 1, index 0)
        if (_hotbarController != null && _hotbarController.SelectedIndex != 0)
        {
            return;
        }

        if (!_hasTarget || _targetBlock == null) return;

        Vector3Int gridPosition = _targetGridPosition;

        // Check bedrock protection (Y > 0)
        if (gridPosition.y <= 0)
        {
            return;
        }

        if (IsBreakable(_targetBlock.Type))
        {
            BlockType brokenType = _targetBlock.Type;
            Vector3 spawnPos = gridPosition + Vector3.one * 0.5f;

            // Remove block from VoxelWorld
            if (_world.RemoveBlock(gridPosition))
            {
                // Award XP for breaking block exactly once on successful removal
                AwardBlockBreakXP(brokenType);

                // Notify quest and achievement systems
                OnBlockBrokenByPlayer?.Invoke(brokenType, gridPosition);

                // Trigger axe chop animation
                if (_equippedItemController != null)
                {
                    _equippedItemController.TriggerSwing();
                }

                // Play block break sound
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBlockBreak(brokenType, spawnPos);
                }

                // Spawn physical dropped item pickup
                SpawnItemDrop(brokenType, spawnPos);

                // Immediately re-evaluate raycast so target and outline update on the same frame
                UpdateTargetRaycast();
            }
        }
    }

    private void AwardBlockBreakXP(BlockType blockType)
    {
        PlayerExperience px = PlayerExperience.Instance;
        if (px == null) px = FindFirstObjectByType<PlayerExperience>();
        if (px != null)
        {
            px.AwardBlockBreakXP(blockType);
        }
    }

    private void TryPlaceAdjacentBlock()
    {
        if (playerCamera == null) return;
        if (_world == null) _world = FindFirstObjectByType<VoxelWorld>();
        if (_hotbarController == null) return;

        // Slot 0 is Axe (cannot be placed)
        int selectedIndex = _hotbarController.SelectedIndex;
        if (selectedIndex == 0) return;

        BlockType selectedBlock = _hotbarController.SelectedItem.BlockType;
        if (!IsPlaceable(selectedBlock)) return;

        // Check if player has at least 1 of the block in inventory
        if (InventoryController.Instance == null || !InventoryController.Instance.HasItem(selectedBlock, 1))
        {
            return;
        }

        if (!_hasTarget) return;

        Vector3Int targetGridPosition = _targetGridPosition;
        Vector3Int placeGridPosition = targetGridPosition + _targetNormal;

        // 1. Bounds Validation
        if (placeGridPosition.x < 0 || placeGridPosition.x >= _world.WorldWidth ||
            placeGridPosition.z < 0 || placeGridPosition.z >= _world.WorldDepth ||
            placeGridPosition.y <= 0 || placeGridPosition.y > _world.MaximumTerrainHeight * 3)
        {
            return;
        }

        // 2. Cell must be empty (Air)
        if (_world.HasBlock(placeGridPosition))
        {
            return;
        }

        // 3. Player Collision Safety: Do not allow placing blocks inside the player's body
        if (IsOverlappingPlayer(placeGridPosition))
        {
            return;
        }

        // 4. Place Block in VoxelWorld and consume 1 from inventory
        if (_world.PlaceBlock(placeGridPosition, selectedBlock))
        {
            InventoryController.Instance.RemoveItem(selectedBlock, 1);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBlockPlace(selectedBlock, new Vector3(placeGridPosition.x + 0.5f, placeGridPosition.y + 0.5f, placeGridPosition.z + 0.5f));
            }

            // Immediately re-evaluate raycast so target and outline update on the same frame
            UpdateTargetRaycast();
        }
    }

    private bool IsOverlappingPlayer(Vector3Int placeGridPosition)
    {
        Bounds blockBounds = new Bounds(
            new Vector3(placeGridPosition.x + 0.5f, placeGridPosition.y + 0.5f, placeGridPosition.z + 0.5f),
            new Vector3(0.95f, 0.95f, 0.95f)
        );

        if (_playerCharacterController != null)
        {
            return _playerCharacterController.bounds.Intersects(blockBounds);
        }

        // Fallback check against player position if controller reference is lost
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Bounds fallbackBounds = new Bounds(player.transform.position + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f));
            return fallbackBounds.Intersects(blockBounds);
        }

        return false;
    }

    private bool IsBreakable(BlockType type)
    {
        switch (type)
        {
            case BlockType.Grass:
            case BlockType.Dirt:
            case BlockType.Stone:
            case BlockType.Wood:
            case BlockType.Sand:
            case BlockType.Leaves:
                return true;
            default:
                return false;
        }
    }

    private bool IsPlaceable(BlockType type)
    {
        switch (type)
        {
            case BlockType.Grass:
            case BlockType.Dirt:
            case BlockType.Stone:
            case BlockType.Wood:
            case BlockType.Sand:
                return true;
            default:
                return false;
        }
    }

    private void SpawnItemDrop(BlockType type, Vector3 position)
    {
        GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drop.name = type + "_Pickup";
        drop.transform.position = position + Vector3.up * 0.15f;
        drop.transform.localScale = Vector3.one * 0.28f;

        Collider col = drop.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Material mat = GetPickupMaterial(type);
        if (mat != null)
        {
            drop.GetComponent<Renderer>().sharedMaterial = mat;
        }

        ItemPickup pickup = drop.AddComponent<ItemPickup>();
        pickup.Setup(type, 1, mat);
    }

    private Material GetPickupMaterial(BlockType type)
    {
        Shader shader = ShaderUtils.GetLitShader();
        if (shader == null) shader = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
        if (shader == null) return null;

        Material mat = new Material(shader)
        {
            name = type + "_Pickup_Material"
        };

        Color color;
        switch (type)
        {
            case BlockType.Grass: color = new Color(0.32f, 0.68f, 0.22f); break;
            case BlockType.Dirt: color = new Color(0.48f, 0.30f, 0.16f); break;
            case BlockType.Stone: color = new Color(0.52f, 0.54f, 0.56f); break;
            case BlockType.Wood: color = new Color(0.46f, 0.28f, 0.14f); break;
            case BlockType.Sand: color = new Color(0.86f, 0.78f, 0.52f); break;
            case BlockType.Leaves: color = new Color(0.24f, 0.58f, 0.18f); break;
            default: color = Color.white; break;
        }

        mat.SetColor("_BaseColor", color);
        return mat;
    }
}
