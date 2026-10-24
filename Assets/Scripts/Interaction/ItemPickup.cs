using UnityEngine;

/// <summary>
/// Represents a floating/dropped voxel item in the world.
/// Automatically collects into player inventory when the player approaches within pickup range.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private BlockType blockType = BlockType.Grass;
    [SerializeField] private int quantity = 1;
    [SerializeField] private float pickupRadius = 1.6f;
    [SerializeField] private float floatSpeed = 3.0f;
    [SerializeField] private float floatHeight = 0.08f;
    [SerializeField] private float rotateSpeed = 90.0f;

    private Transform _playerTransform;
    private Vector3 _basePosition;
    private float _spawnTime;
    private bool _isCollected;
    private float _verticalVelocity = 2.5f;
    private bool _grounded;

    public void Setup(BlockType type, int count, Material blockMaterial)
    {
        blockType = type;
        quantity = count;

        Renderer r = GetComponent<Renderer>();
        if (r != null && blockMaterial != null)
        {
            r.sharedMaterial = blockMaterial;
        }
    }

    private void Start()
    {
        _spawnTime = Time.time;
        _basePosition = transform.position;

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
    }

    private void Update()
    {
        if (_isCollected) return;

        // Pop up and settle
        if (!_grounded)
        {
            _verticalVelocity -= 9.81f * Time.deltaTime;
            _basePosition.y += _verticalVelocity * Time.deltaTime;

            VoxelWorld world = FindFirstObjectByType<VoxelWorld>();
            if (world != null)
            {
                int gx = Mathf.FloorToInt(_basePosition.x);
                int gz = Mathf.FloorToInt(_basePosition.z);
                float groundY = world.GetTerrainSurfaceHeight(gx, gz) + 0.35f;

                if (_basePosition.y <= groundY)
                {
                    _basePosition.y = groundY;
                    _grounded = true;
                    _verticalVelocity = 0f;
                }
            }
            else if (Time.time - _spawnTime > 0.5f)
            {
                _grounded = true;
            }
        }

        // Floating bob and rotate
        float bob = Mathf.Sin((Time.time - _spawnTime) * floatSpeed) * floatHeight;
        transform.position = new Vector3(_basePosition.x, _basePosition.y + bob, _basePosition.z);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        // Check player distance for auto-pickup
        if (_playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }

        if (_playerTransform != null && Time.time - _spawnTime > 0.2f)
        {
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist <= pickupRadius)
            {
                Collect();
            }
        }
    }

    private void Collect()
    {
        _isCollected = true;

        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.AddItem(blockType, quantity);
        }

        Destroy(gameObject);
    }
}
