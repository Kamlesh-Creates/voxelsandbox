using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    [Header("World Size")]
    [SerializeField] private int worldWidth = 128;
    [SerializeField] private int worldDepth = 128;
    [SerializeField] private int maximumTerrainHeight = 8;

    [Header("Terrain")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private float noiseScale = 0.08f;
    [SerializeField] private int baseTerrainHeight = 3;

    [Header("Environment")]
    [SerializeField] private int treeCount = 45;
    [SerializeField] private int flowerCount = 35;
    [SerializeField] private int tallGrassCount = 75;
    [SerializeField] private int pondWaterLevel = 3;
    [SerializeField] private int pondWidth = 18;
    [SerializeField] private int pondDepth = 14;

    [Header("Creatures")]
    [SerializeField] private int cowCount = 6;
    [SerializeField] private int sheepCount = 6;
    [SerializeField] private int pigCount = 5;
    [SerializeField] private int zombieCount = 5;

    private readonly Dictionary<Vector3Int, VoxelBlock> _blocks = new Dictionary<Vector3Int, VoxelBlock>();
    private readonly Dictionary<BlockType, Material> _materials = new Dictionary<BlockType, Material>();
    private readonly List<Texture2D> _generatedTextures = new List<Texture2D>();
    private readonly List<Vector2Int> _treePositions = new List<Vector2Int>();

    private Material _grassValleyMaterial;
    private Material _grassHighlandMaterial;
    private Material _cloudMaterial;
    private Material _plankMaterial;
    private Material _cobbleMaterial;
    private Material _redFlowerMaterial;
    private Material _yellowFlowerMaterial;
    private Material _whiteFlowerMaterial;
    private Material _stemMaterial;
    private Material _tallGrassMaterialA;
    private Material _tallGrassMaterialB;

    // Hierarchy Roots
    private GameObject _environmentRoot;
    private Transform _terrainRoot;
    private Transform _vegetationRoot;
    private Transform _flowersRoot;
    private Transform _tallGrassRoot;
    private Transform _treesRoot;
    private Transform _houseRoot;
    private Transform _decorationsRoot;
    private Transform _cloudsRoot;

    private GameObject _creaturesRoot;
    private Transform _passiveAnimalsRoot;
    private Transform _cowsRoot;
    private Transform _sheepRoot;
    private Transform _pigsRoot;
    private Transform _hostileRoot;
    private Transform _zombiesRoot;

    private Vector3Int _houseOrigin;
    private int _houseBaseY;

    public int WorldWidth => worldWidth;
    public int WorldDepth => worldDepth;
    public int MaximumTerrainHeight => maximumTerrainHeight;

    private void Start()
    {
        CreateHierarchyRoots();
        CreateMaterials();
        MobFactory.InitializeMaterials(ShaderUtils.GetLitShader());

        GenerateWorld();
        _houseOrigin = FindHouseOrigin();
        _houseBaseY = GetHouseBaseHeight(_houseOrigin);
        BuildHouse();
        DecorateHouseSurroundings();
        CreatePond();
        PlaceTrees();
        PlaceFlowers();
        PlaceTallGrass();
        PlaceSmallBushes();
        CreateClouds();
        SpawnCreatures();
        EnsureDayNightCycle();
        EnsureMinimap();
        EnsureAudioManager();
        EnsurePauseMenu();
        EnsurePlayerHealth();
        EnsurePlayerExperience();
    }

    private void OnDestroy()
    {
        foreach (Material material in _materials.Values)
        {
            if (material != null) Destroy(material);
        }
        _materials.Clear();

        if (_grassValleyMaterial != null) Destroy(_grassValleyMaterial);
        if (_grassHighlandMaterial != null) Destroy(_grassHighlandMaterial);
        if (_cloudMaterial != null) Destroy(_cloudMaterial);
        if (_plankMaterial != null) Destroy(_plankMaterial);
        if (_cobbleMaterial != null) Destroy(_cobbleMaterial);
        if (_redFlowerMaterial != null) Destroy(_redFlowerMaterial);
        if (_yellowFlowerMaterial != null) Destroy(_yellowFlowerMaterial);
        if (_whiteFlowerMaterial != null) Destroy(_whiteFlowerMaterial);
        if (_stemMaterial != null) Destroy(_stemMaterial);
        if (_tallGrassMaterialA != null) Destroy(_tallGrassMaterialA);
        if (_tallGrassMaterialB != null) Destroy(_tallGrassMaterialB);

        foreach (Texture2D tex in _generatedTextures)
        {
            if (tex != null) Destroy(tex);
        }
        _generatedTextures.Clear();

        if (_environmentRoot != null) Destroy(_environmentRoot);
        if (_creaturesRoot != null) Destroy(_creaturesRoot);
    }

    private void CreateHierarchyRoots()
    {
        _environmentRoot = new GameObject("Environment");

        GameObject terrainObj = new GameObject("Terrain");
        terrainObj.transform.SetParent(_environmentRoot.transform);
        _terrainRoot = terrainObj.transform;

        GameObject vegObj = new GameObject("Vegetation");
        vegObj.transform.SetParent(_environmentRoot.transform);
        _vegetationRoot = vegObj.transform;

        GameObject flowerObj = new GameObject("Flowers");
        flowerObj.transform.SetParent(_environmentRoot.transform);
        _flowersRoot = flowerObj.transform;

        GameObject grassObj = new GameObject("TallGrass");
        grassObj.transform.SetParent(_environmentRoot.transform);
        _tallGrassRoot = grassObj.transform;

        GameObject treeObj = new GameObject("Trees");
        treeObj.transform.SetParent(_environmentRoot.transform);
        _treesRoot = treeObj.transform;

        GameObject houseObj = new GameObject("House");
        houseObj.transform.SetParent(_environmentRoot.transform);
        _houseRoot = houseObj.transform;

        GameObject decorObj = new GameObject("Decorations");
        decorObj.transform.SetParent(_environmentRoot.transform);
        _decorationsRoot = decorObj.transform;

        GameObject cloudObj = new GameObject("Clouds");
        cloudObj.transform.SetParent(_environmentRoot.transform);
        _cloudsRoot = cloudObj.transform;

        _creaturesRoot = new GameObject("Creatures");

        GameObject passiveObj = new GameObject("PassiveAnimals");
        passiveObj.transform.SetParent(_creaturesRoot.transform);
        _passiveAnimalsRoot = passiveObj.transform;

        GameObject cowsObj = new GameObject("Cows");
        cowsObj.transform.SetParent(_passiveAnimalsRoot);
        _cowsRoot = cowsObj.transform;

        GameObject sheepObj = new GameObject("Sheep");
        sheepObj.transform.SetParent(_passiveAnimalsRoot);
        _sheepRoot = sheepObj.transform;

        GameObject pigsObj = new GameObject("Pigs");
        pigsObj.transform.SetParent(_passiveAnimalsRoot);
        _pigsRoot = pigsObj.transform;

        GameObject hostileObj = new GameObject("Hostile");
        hostileObj.transform.SetParent(_creaturesRoot.transform);
        _hostileRoot = hostileObj.transform;

        GameObject zombiesObj = new GameObject("Zombies");
        zombiesObj.transform.SetParent(_hostileRoot);
        _zombiesRoot = zombiesObj.transform;
    }

    public event System.Action<Vector3Int> OnBlockChanged;

    public VoxelBlock GetBlock(Vector3Int gridPosition)
    {
        _blocks.TryGetValue(gridPosition, out VoxelBlock block);
        return block;
    }

    public bool HasBlock(Vector3Int gridPosition)
    {
        return _blocks.ContainsKey(gridPosition);
    }

    public bool RemoveBlock(Vector3Int gridPosition)
    {
        if (!_blocks.TryGetValue(gridPosition, out VoxelBlock block))
        {
            return false;
        }

        if (block.BlockObject != null)
        {
            Destroy(block.BlockObject);
        }

        _blocks.Remove(gridPosition);
        OnBlockChanged?.Invoke(gridPosition);
        return true;
    }

    public bool AddBlock(Vector3Int gridPosition, BlockType blockType)
    {
        return PlaceBlock(gridPosition, blockType);
    }

    public bool PlaceBlock(Vector3Int gridPosition, BlockType blockType)
    {
        if (blockType == BlockType.Air || blockType == BlockType.Water || HasBlock(gridPosition))
        {
            return false;
        }

        if (gridPosition.x < 0 || gridPosition.x >= worldWidth ||
            gridPosition.z < 0 || gridPosition.z >= worldDepth ||
            gridPosition.y < 0 || gridPosition.y > maximumTerrainHeight * 3)
        {
            return false;
        }

        CreateBlock(gridPosition, blockType, true, _terrainRoot);
        OnBlockChanged?.Invoke(gridPosition);
        return true;
    }

    public int GetTerrainSurfaceHeight(int x, int z)
    {
        return GetSurfaceHeight(x, z);
    }

    private void GenerateWorld()
    {
        for (int x = 0; x < worldWidth; x++)
        {
            for (int z = 0; z < worldDepth; z++)
            {
                int surfaceHeight = GetSurfaceHeight(x, z);

                for (int y = 0; y <= surfaceHeight; y++)
                {
                    BlockType blockType = GetTerrainBlockType(x, y, z, surfaceHeight);
                    CreateBlock(new Vector3Int(x, y, z), blockType, true, _terrainRoot);
                }
            }
        }
    }

    private int GetSurfaceHeight(int x, int z)
    {
        // Multi-frequency Perlin noise for natural rolling terrain variation
        float nx1 = (x + seed) * noiseScale;
        float nz1 = (z + seed) * noiseScale;
        float baseNoise = Mathf.PerlinNoise(nx1, nz1);

        float nx2 = (x + seed + 200) * (noiseScale * 2.2f);
        float nz2 = (z + seed + 200) * (noiseScale * 2.2f);
        float detailNoise = Mathf.PerlinNoise(nx2, nz2) * 0.35f;

        float combined = baseNoise * 0.75f + detailNoise;
        int height = Mathf.FloorToInt(baseTerrainHeight + combined * (maximumTerrainHeight - baseTerrainHeight));

        return Mathf.Clamp(height, 2, maximumTerrainHeight - 1);
    }

    private int GetMaxLocalSlope(int x, int z)
    {
        int h = GetSurfaceHeight(x, z);
        int slope = 0;
        if (x > 0) slope = Mathf.Max(slope, Mathf.Abs(h - GetSurfaceHeight(x - 1, z)));
        if (x < worldWidth - 1) slope = Mathf.Max(slope, Mathf.Abs(h - GetSurfaceHeight(x + 1, z)));
        if (z > 0) slope = Mathf.Max(slope, Mathf.Abs(h - GetSurfaceHeight(x, z - 1)));
        if (z < worldDepth - 1) slope = Mathf.Max(slope, Mathf.Abs(h - GetSurfaceHeight(x, z + 1)));
        return slope;
    }

    private BlockType GetTerrainBlockType(int x, int y, int z, int surfaceHeight)
    {
        bool isCliff = GetMaxLocalSlope(x, z) >= 2;
        bool isRocky = IsRockyArea(x, z);

        if (y == surfaceHeight)
        {
            if (isCliff)
            {
                return BlockType.Stone;
            }

            if (isRocky && surfaceHeight >= maximumTerrainHeight - 3)
            {
                return BlockType.Stone;
            }

            return BlockType.Grass;
        }

        if (y >= surfaceHeight - 2)
        {
            if (isCliff && y == surfaceHeight - 1)
            {
                return BlockType.Stone;
            }

            return BlockType.Dirt;
        }

        return BlockType.Stone;
    }

    private bool IsRockyArea(int x, int z)
    {
        float ridgeNoise = Mathf.PerlinNoise((x + seed + 555) * 0.055f, (z + seed + 555) * 0.055f);
        return ridgeNoise > 0.65f;
    }

    private Vector3Int FindHouseOrigin()
    {
        Vector2 worldCenter = new Vector2(worldWidth / 2f, worldDepth / 2f);
        Vector3Int bestOrigin = new Vector3Int(4, 0, worldDepth - 12);
        float bestDistance = float.MaxValue;

        for (int x = 4; x < worldWidth - 12; x++)
        {
            for (int z = 4; z < worldDepth - 12; z++)
            {
                float distance = Vector2.Distance(new Vector2(x, z), worldCenter);
                if (IsFlatArea(x, z, 8, 7) && distance > 10f && distance < bestDistance)
                {
                    bestOrigin = new Vector3Int(x, 0, z);
                    bestDistance = distance;
                }
            }
        }

        return bestOrigin;
    }

    private bool IsFlatArea(int startX, int startZ, int width, int depth)
    {
        int minimumHeight = maximumTerrainHeight;
        int maximumHeight = 0;

        for (int x = startX; x < startX + width; x++)
        {
            for (int z = startZ; z < startZ + depth; z++)
            {
                int height = GetSurfaceHeight(x, z);
                minimumHeight = Mathf.Min(minimumHeight, height);
                maximumHeight = Mathf.Max(maximumHeight, height);
            }
        }

        return maximumHeight - minimumHeight <= 1;
    }

    private int GetHouseBaseHeight(Vector3Int origin)
    {
        int baseHeight = 0;

        for (int x = origin.x; x < origin.x + 8; x++)
        {
            for (int z = origin.z; z < origin.z + 7; z++)
            {
                baseHeight = Mathf.Max(baseHeight, GetSurfaceHeight(x, z));
            }
        }

        return baseHeight;
    }

    private void BuildHouse()
    {
        // Foundation & Floor
        for (int x = _houseOrigin.x; x < _houseOrigin.x + 8; x++)
        {
            for (int z = _houseOrigin.z; z < _houseOrigin.z + 7; z++)
            {
                for (int y = GetSurfaceHeight(x, z) + 1; y <= _houseBaseY; y++)
                {
                    CreateBlock(new Vector3Int(x, y, z), BlockType.Dirt, true, _houseRoot);
                }

                SetCustomBlock(new Vector3Int(x, _houseBaseY + 1, z), _plankMaterial, BlockType.Wood, true, _houseRoot);
            }
        }

        // Walls
        for (int y = _houseBaseY + 2; y <= _houseBaseY + 4; y++)
        {
            for (int x = _houseOrigin.x; x < _houseOrigin.x + 8; x++)
            {
                bool isCorner = (x == _houseOrigin.x || x == _houseOrigin.x + 7);
                Material wallMat = isCorner ? _materials[BlockType.Wood] : _plankMaterial;

                bool doorOpening = x == _houseOrigin.x + 3 || x == _houseOrigin.x + 4;
                if (!doorOpening || y == _houseBaseY + 4)
                {
                    SetCustomBlock(new Vector3Int(x, y, _houseOrigin.z), wallMat, BlockType.Wood, true, _houseRoot);
                }

                SetCustomBlock(new Vector3Int(x, y, _houseOrigin.z + 6), wallMat, BlockType.Wood, true, _houseRoot);
            }

            for (int z = _houseOrigin.z + 1; z < _houseOrigin.z + 6; z++)
            {
                bool windowOpening = z == _houseOrigin.z + 3 && y == _houseBaseY + 3;
                if (!windowOpening)
                {
                    SetCustomBlock(new Vector3Int(_houseOrigin.x, y, z), _plankMaterial, BlockType.Wood, true, _houseRoot);
                    SetCustomBlock(new Vector3Int(_houseOrigin.x + 7, y, z), _plankMaterial, BlockType.Wood, true, _houseRoot);
                }
            }
        }

        // Roof
        for (int x = _houseOrigin.x; x < _houseOrigin.x + 8; x++)
        {
            for (int z = _houseOrigin.z; z < _houseOrigin.z + 7; z++)
            {
                SetCustomBlock(new Vector3Int(x, _houseBaseY + 5, z), _plankMaterial, BlockType.Wood, true, _houseRoot);
            }
        }
    }

    private void DecorateHouseSurroundings()
    {
        // Cobblestone path from house door leading out
        int doorX1 = _houseOrigin.x + 3;
        int doorX2 = _houseOrigin.x + 4;
        int pathStartPosZ = _houseOrigin.z - 1;

        for (int zOffset = 0; zOffset < 9; zOffset++)
        {
            int pz = pathStartPosZ - zOffset;
            if (pz < 1) break;

            for (int px = doorX1; px <= doorX2; px++)
            {
                int surfaceY = GetSurfaceHeight(px, pz);
                SetCustomBlock(new Vector3Int(px, surfaceY, pz), _cobbleMaterial, BlockType.Stone, true, _decorationsRoot);
            }
        }

        // Small wooden fence enclosure on the side of the house
        int fenceStartX = _houseOrigin.x + 8;
        int fenceStartZ = _houseOrigin.z + 1;
        int fenceWidth = 5;
        int fenceDepth = 5;

        for (int fx = fenceStartX; fx <= fenceStartX + fenceWidth; fx++)
        {
            for (int fz = fenceStartZ; fz <= fenceStartZ + fenceDepth; fz++)
            {
                bool isBorder = (fx == fenceStartX || fx == fenceStartX + fenceWidth || fz == fenceStartZ || fz == fenceStartZ + fenceDepth);
                bool isGate = (fx == fenceStartX + fenceWidth / 2 && fz == fenceStartZ);

                if (isBorder && !isGate && fx < worldWidth - 2 && fz < worldDepth - 2)
                {
                    int surfaceY = GetSurfaceHeight(fx, fz);
                    CreateFencePost(new Vector3(fx + 0.5f, surfaceY + 1f, fz + 0.5f));
                }
            }
        }

        // Decorative garden flowers near the house
        for (int i = 0; i < 6; i++)
        {
            int gx = _houseOrigin.x - 1;
            int gz = _houseOrigin.z + 1 + i;
            if (gz < _houseOrigin.z + 6)
            {
                int surfaceY = GetSurfaceHeight(gx, gz);
                CreateFlower(new Vector3(gx + 0.5f, surfaceY + 1f, gz + 0.5f), (FlowerType)(i % 3), _flowersRoot);
            }
        }
    }

    private void CreateFencePost(Vector3 position)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "FencePost";
        post.transform.SetParent(_decorationsRoot);
        post.transform.position = position;
        post.transform.localScale = new Vector3(0.22f, 0.9f, 0.22f);
        post.GetComponent<Renderer>().sharedMaterial = _materials[BlockType.Wood];
        Destroy(post.GetComponent<Collider>());
    }

    private void CreatePond()
    {
        float pondCenterX = worldWidth - pondWidth * 0.5f - 4f;
        float pondCenterZ = 4f + pondDepth * 0.5f;
        float radX = pondWidth * 0.5f;
        float radZ = pondDepth * 0.5f;

        int minX = Mathf.Max(1, Mathf.FloorToInt(pondCenterX - radX - 4f));
        int maxX = Mathf.Min(worldWidth - 2, Mathf.CeilToInt(pondCenterX + radX + 4f));
        int minZ = Mathf.Max(1, Mathf.FloorToInt(pondCenterZ - radZ - 4f));
        int maxZ = Mathf.Min(worldDepth - 2, Mathf.CeilToInt(pondCenterZ + radZ + 4f));

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                float dx = (x - pondCenterX) / radX;
                float dz = (z - pondCenterZ) / radZ;

                // Organic shoreline curvature with smooth deterministic noise
                float warp = (Mathf.PerlinNoise(x * 0.35f + seed, z * 0.35f + seed) - 0.5f) * 0.35f;
                float distSq = dx * dx + dz * dz + warp;

                // 1. Water Basin
                if (distSq <= 1.0f)
                {
                    for (int y = pondWaterLevel; y <= maximumTerrainHeight; y++)
                    {
                        RemoveBlock(new Vector3Int(x, y, z));
                    }

                    SetBlock(new Vector3Int(x, pondWaterLevel, z), BlockType.Water, false, _terrainRoot);
                    SetBlock(new Vector3Int(x, pondWaterLevel - 1, z), BlockType.Dirt, true, _terrainRoot);
                }
                // 2. Sand Beach Shoreline
                else if (distSq <= 1.55f)
                {
                    int surfaceHeight = GetSurfaceHeight(x, z);
                    SetBlock(new Vector3Int(x, surfaceHeight, z), BlockType.Sand, true, _terrainRoot);
                }
                // 3. Natural transitional sand fringe into meadow
                else if (distSq <= 1.85f)
                {
                    float fringe = Mathf.PerlinNoise(x * 0.6f + seed + 20f, z * 0.6f + seed + 20f);
                    if (fringe > 0.45f)
                    {
                        int surfaceHeight = GetSurfaceHeight(x, z);
                        SetBlock(new Vector3Int(x, surfaceHeight, z), BlockType.Sand, true, _terrainRoot);
                    }
                }
            }
        }
    }

    private void PlaceTrees()
    {
        int placedTrees = 0;
        _treePositions.Clear();

        for (int attempt = 0; attempt < treeCount * 14 && placedTrees < treeCount; attempt++)
        {
            int x = 4 + Mathf.Abs(seed + attempt * 17) % (worldWidth - 8);
            int z = 4 + Mathf.Abs(seed + attempt * 31) % (worldDepth - 8);
            int surfaceHeight = GetSurfaceHeight(x, z);
            Vector3Int groundPosition = new Vector3Int(x, surfaceHeight, z);

            if (GetBlock(groundPosition)?.Type != BlockType.Grass ||
                IsNearHouse(x, z) ||
                IsNearPond(x, z) ||
                IsNearTree(x, z) ||
                IsRockyArea(x, z) ||
                Vector2.Distance(new Vector2(x, z), new Vector2(worldWidth / 2f, worldDepth / 2f)) < 8f)
            {
                continue;
            }

            // Elevation and terrain-aware tree species selection
            int treeVariation;
            if (surfaceHeight >= maximumTerrainHeight - 3)
            {
                // Pine / Conifer favored on rocky slopes and highlands
                treeVariation = (attempt % 3 == 0) ? 0 : 3;
            }
            else
            {
                treeVariation = (attempt * 7 + seed + x * 3 + z * 5) % 5;
            }

            BuildTreeVariation(x, surfaceHeight + 1, z, treeVariation);
            _treePositions.Add(new Vector2Int(x, z));
            placedTrees++;
        }
    }

    private bool IsNearTree(int x, int z)
    {
        for (int i = 0; i < _treePositions.Count; i++)
        {
            if (Vector2Int.Distance(new Vector2Int(x, z), _treePositions[i]) < 6f)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNearHouse(int x, int z)
    {
        return x >= _houseOrigin.x - 4 && x <= _houseOrigin.x + 15 && z >= _houseOrigin.z - 11 && z <= _houseOrigin.z + 10;
    }

    private bool IsNearPond(int x, int z)
    {
        return x >= worldWidth - pondWidth - 7 && z >= 1 && z <= pondDepth + 5;
    }

    private void BuildTreeVariation(int x, int startY, int z, int variation)
    {
        switch (variation)
        {
            case 0: // Lush Temperate Oak (rounded natural crown)
                BuildTemperateOak(x, startY, z);
                break;
            case 1: // Tall Forest Birch/Aspen (slender tall trunk, spire cap)
                BuildTallBirch(x, startY, z);
                break;
            case 2: // Spreading Meadow Oak (branch outcroppings, wide umbrella canopy)
                BuildSpreadingMeadowOak(x, startY, z);
                break;
            case 3: // Alpine Conifer / Pine (tiered conical evergreen)
                BuildPineConifer(x, startY, z);
                break;
            case 4: // Dwarf Shrubby Tree (compact round canopy)
                BuildDwarfTree(x, startY, z);
                break;
        }
    }

    private void BuildTemperateOak(int x, int startY, int z)
    {
        // 5 trunk blocks
        for (int y = startY; y < startY + 5; y++)
        {
            SetBlock(new Vector3Int(x, y, z), BlockType.Wood, true, _treesRoot);
        }

        int leafY = startY + 3;
        // Layer 1 (5x5 rounded)
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oz = -2; oz <= 2; oz++)
            {
                if (Mathf.Abs(ox) == 2 && Mathf.Abs(oz) == 2) continue; // Rounded corners
                if (ox != 0 || oz != 0 || leafY > startY + 4)
                {
                    SetBlock(new Vector3Int(x + ox, leafY, z + oz), BlockType.Leaves, false, _treesRoot);
                }
            }
        }

        // Layer 2 (5x5 rounded)
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oz = -2; oz <= 2; oz++)
            {
                if (Mathf.Abs(ox) == 2 && Mathf.Abs(oz) == 2) continue;
                if (ox != 0 || oz != 0 || leafY + 1 > startY + 4)
                {
                    SetBlock(new Vector3Int(x + ox, leafY + 1, z + oz), BlockType.Leaves, false, _treesRoot);
                }
            }
        }

        // Layer 3 (3x3)
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                SetBlock(new Vector3Int(x + ox, leafY + 2, z + oz), BlockType.Leaves, false, _treesRoot);
            }
        }

        // Layer 4 (top cross)
        SetBlock(new Vector3Int(x, leafY + 3, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x + 1, leafY + 3, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x - 1, leafY + 3, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x, leafY + 3, z + 1), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x, leafY + 3, z - 1), BlockType.Leaves, false, _treesRoot);
    }

    private void BuildTallBirch(int x, int startY, int z)
    {
        // 7-8 trunk blocks (tall and slender)
        int trunkHeight = 7 + (x + z) % 2;
        for (int y = startY; y < startY + trunkHeight; y++)
        {
            SetBlock(new Vector3Int(x, y, z), BlockType.Wood, true, _treesRoot);
        }

        int leafY = startY + trunkHeight - 3;
        // Layer 1: Compact 3x3 cross
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                if (ox == 0 || oz == 0)
                {
                    SetBlock(new Vector3Int(x + ox, leafY, z + oz), BlockType.Leaves, false, _treesRoot);
                }
            }
        }

        // Layer 2: 3x3 full
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                SetBlock(new Vector3Int(x + ox, leafY + 1, z + oz), BlockType.Leaves, false, _treesRoot);
            }
        }

        // Layer 3: 3x3 full
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                SetBlock(new Vector3Int(x + ox, leafY + 2, z + oz), BlockType.Leaves, false, _treesRoot);
            }
        }

        // Layer 4: Top cross & spire
        SetBlock(new Vector3Int(x, leafY + 3, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x + 1, leafY + 3, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x - 1, leafY + 3, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x, leafY + 3, z + 1), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x, leafY + 3, z - 1), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x, leafY + 4, z), BlockType.Leaves, false, _treesRoot);
    }

    private void BuildSpreadingMeadowOak(int x, int startY, int z)
    {
        // 5 trunk blocks with horizontal branch extensions
        for (int y = startY; y < startY + 5; y++)
        {
            SetBlock(new Vector3Int(x, y, z), BlockType.Wood, true, _treesRoot);
        }

        // Outward branch logs
        SetBlock(new Vector3Int(x + 1, startY + 3, z), BlockType.Wood, true, _treesRoot);
        SetBlock(new Vector3Int(x - 1, startY + 3, z), BlockType.Wood, true, _treesRoot);
        SetBlock(new Vector3Int(x, startY + 3, z + 1), BlockType.Wood, true, _treesRoot);
        SetBlock(new Vector3Int(x, startY + 3, z - 1), BlockType.Wood, true, _treesRoot);

        int leafY = startY + 3;
        // Wide 6x6 umbrella layer with natural perimeter
        for (int ox = -3; ox <= 3; ox++)
        {
            for (int oz = -3; oz <= 3; oz++)
            {
                if (ox * ox + oz * oz <= 8)
                {
                    SetBlock(new Vector3Int(x + ox, leafY, z + oz), BlockType.Leaves, false, _treesRoot);
                }
            }
        }

        // Mid Layer: 5x5 rounded
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oz = -2; oz <= 2; oz++)
            {
                if (Mathf.Abs(ox) == 2 && Mathf.Abs(oz) == 2) continue;
                SetBlock(new Vector3Int(x + ox, leafY + 1, z + oz), BlockType.Leaves, false, _treesRoot);
            }
        }

        // Crown Layer: 3x3
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                SetBlock(new Vector3Int(x + ox, leafY + 2, z + oz), BlockType.Leaves, false, _treesRoot);
            }
        }

        // Cap
        SetBlock(new Vector3Int(x, leafY + 3, z), BlockType.Leaves, false, _treesRoot);
    }

    private void BuildPineConifer(int x, int startY, int z)
    {
        // Tall straight trunk (8 blocks)
        for (int y = startY; y < startY + 8; y++)
        {
            SetBlock(new Vector3Int(x, y, z), BlockType.Wood, true, _treesRoot);
        }

        // Conical tiered evergreen foliage
        // Tier 1 (lowest & widest: 5x5 cross)
        int t1Y = startY + 3;
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oz = -2; oz <= 2; oz++)
            {
                if (Mathf.Abs(ox) + Mathf.Abs(oz) <= 2)
                {
                    SetBlock(new Vector3Int(x + ox, t1Y, z + oz), BlockType.Leaves, false, _treesRoot);
                }
            }
        }

        // Tier 2 (3x3 cross)
        int t2Y = startY + 5;
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                if (ox == 0 || oz == 0)
                {
                    SetBlock(new Vector3Int(x + ox, t2Y, z + oz), BlockType.Leaves, false, _treesRoot);
                }
            }
        }

        // Tier 3 (3x3 full)
        int t3Y = startY + 6;
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                SetBlock(new Vector3Int(x + ox, t3Y, z + oz), BlockType.Leaves, false, _treesRoot);
            }
        }

        // Tier 4 (1x1 cross)
        int t4Y = startY + 7;
        SetBlock(new Vector3Int(x, t4Y, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x + 1, t4Y, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x - 1, t4Y, z), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x, t4Y, z + 1), BlockType.Leaves, false, _treesRoot);
        SetBlock(new Vector3Int(x, t4Y, z - 1), BlockType.Leaves, false, _treesRoot);

        // Tier 5 (single spire tip)
        SetBlock(new Vector3Int(x, startY + 8, z), BlockType.Leaves, false, _treesRoot);
    }

    private void BuildDwarfTree(int x, int startY, int z)
    {
        // 3-4 trunk blocks
        for (int y = startY; y < startY + 3; y++)
        {
            SetBlock(new Vector3Int(x, y, z), BlockType.Wood, true, _treesRoot);
        }

        int leafY = startY + 2;
        // Compact spherical 3x3 canopy
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                if (ox != 0 || oz != 0 || leafY > startY + 2)
                {
                    SetBlock(new Vector3Int(x + ox, leafY, z + oz), BlockType.Leaves, false, _treesRoot);
                }
            }
        }

        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oz = -1; oz <= 1; oz++)
            {
                SetBlock(new Vector3Int(x + ox, leafY + 1, z + oz), BlockType.Leaves, false, _treesRoot);
            }
        }

        SetBlock(new Vector3Int(x, leafY + 2, z), BlockType.Leaves, false, _treesRoot);
    }

    private void PlaceFlowers()
    {
        int placed = 0;
        int clusterCount = 14;

        for (int c = 0; c < clusterCount && placed < flowerCount; c++)
        {
            // Pick meadow cluster center
            int centerX = 6 + Mathf.Abs(seed + c * 37) % (worldWidth - 12);
            int centerZ = 6 + Mathf.Abs(seed + c * 53) % (worldDepth - 12);

            FlowerType clusterSpecies = (FlowerType)(c % 3);
            int flowersInCluster = 3 + (c * 3) % 4; // 3 to 6 flowers per cluster

            for (int f = 0; f < flowersInCluster && placed < flowerCount; f++)
            {
                int ox = (f == 0) ? 0 : ((f * 17) % 5 - 2);
                int oz = (f == 0) ? 0 : ((f * 23) % 5 - 2);
                int x = Mathf.Clamp(centerX + ox, 3, worldWidth - 4);
                int z = Mathf.Clamp(centerZ + oz, 3, worldDepth - 4);
                int surfaceY = GetSurfaceHeight(x, z);

                Vector3Int groundPos = new Vector3Int(x, surfaceY, z);
                if (GetBlock(groundPos)?.Type != BlockType.Grass ||
                    IsNearHouse(x, z) ||
                    IsNearPond(x, z) ||
                    Vector2.Distance(new Vector2(x, z), new Vector2(worldWidth / 2f, worldDepth / 2f)) < 6f)
                {
                    continue;
                }

                // Harmonious cluster species with occasional wild accent flower
                FlowerType type = ((f + c) % 5 == 0) ? (FlowerType)((c + 1) % 3) : clusterSpecies;
                CreateFlower(new Vector3(x + 0.5f, surfaceY + 1f, z + 0.5f), type, _flowersRoot);
                placed++;
            }
        }
    }

    private enum FlowerType { Red, Yellow, White }

    private void CreateFlower(Vector3 position, FlowerType type, Transform parent)
    {
        GameObject flower = new GameObject("Flower_" + type);
        flower.transform.SetParent(parent);
        flower.transform.position = position;

        // Stem
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stem.name = "Stem";
        stem.transform.SetParent(flower.transform, false);
        stem.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        stem.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
        stem.GetComponent<Renderer>().sharedMaterial = _stemMaterial;
        Destroy(stem.GetComponent<Collider>());

        // Petals
        Material petalMat = type == FlowerType.Red ? _redFlowerMaterial : (type == FlowerType.Yellow ? _yellowFlowerMaterial : _whiteFlowerMaterial);
        GameObject petals = GameObject.CreatePrimitive(PrimitiveType.Cube);
        petals.name = "Petals";
        petals.transform.SetParent(flower.transform, false);
        petals.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        petals.transform.localScale = new Vector3(0.24f, 0.16f, 0.24f);
        petals.GetComponent<Renderer>().sharedMaterial = petalMat;
        Destroy(petals.GetComponent<Collider>());
    }

    private void PlaceTallGrass()
    {
        int placed = 0;
        int clumpCount = 20;

        for (int c = 0; c < clumpCount && placed < tallGrassCount; c++)
        {
            int centerX = 4 + Mathf.Abs(seed + c * 23) % (worldWidth - 8);
            int centerZ = 4 + Mathf.Abs(seed + c * 41) % (worldDepth - 8);
            int bladesInClump = 4 + (c * 5) % 4; // 4 to 7 patches

            for (int b = 0; b < bladesInClump && placed < tallGrassCount; b++)
            {
                int ox = (b == 0) ? 0 : ((b * 13) % 5 - 2);
                int oz = (b == 0) ? 0 : ((b * 19) % 5 - 2);
                int x = Mathf.Clamp(centerX + ox, 3, worldWidth - 4);
                int z = Mathf.Clamp(centerZ + oz, 3, worldDepth - 4);
                int surfaceY = GetSurfaceHeight(x, z);

                Vector3Int groundPos = new Vector3Int(x, surfaceY, z);
                if (GetBlock(groundPos)?.Type != BlockType.Grass ||
                    IsNearHouse(x, z) ||
                    IsNearPond(x, z) ||
                    Vector2.Distance(new Vector2(x, z), new Vector2(worldWidth / 2f, worldDepth / 2f)) < 5f)
                {
                    continue;
                }

                CreateTallGrassPatch(new Vector3(x + 0.5f, surfaceY + 1f, z + 0.5f), (b + c) % 2 == 0);
                placed++;
            }
        }
    }

    private void CreateTallGrassPatch(Vector3 position, bool variantA)
    {
        GameObject grass = new GameObject("TallGrassPatch");
        grass.transform.SetParent(_tallGrassRoot);
        grass.transform.position = position;

        float rotOffset = Mathf.Abs((position.x * 17f + position.z * 31f) % 60f) - 30f;
        grass.transform.localRotation = Quaternion.Euler(0f, rotOffset, 0f);

        Material grassMat = variantA ? _tallGrassMaterialA : _tallGrassMaterialB;

        // Blade 1
        GameObject blade1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade1.name = "Blade1";
        blade1.transform.SetParent(grass.transform, false);
        blade1.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        blade1.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        blade1.transform.localScale = new Vector3(0.06f, 0.5f, 0.35f);
        blade1.GetComponent<Renderer>().sharedMaterial = grassMat;
        Destroy(blade1.GetComponent<Collider>());

        // Blade 2
        GameObject blade2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade2.name = "Blade2";
        blade2.transform.SetParent(grass.transform, false);
        blade2.transform.localPosition = new Vector3(0.05f, 0.22f, -0.05f);
        blade2.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
        blade2.transform.localScale = new Vector3(0.06f, 0.44f, 0.32f);
        blade2.GetComponent<Renderer>().sharedMaterial = grassMat;
        Destroy(blade2.GetComponent<Collider>());
    }

    private void PlaceSmallBushes()
    {
        // Natural leafy bushes scattered along woodland margins and forest clearings
        for (int i = 0; i < 24; i++)
        {
            int x = 6 + Mathf.Abs(seed + i * 53) % (worldWidth - 12);
            int z = 6 + Mathf.Abs(seed + i * 67) % (worldDepth - 12);
            int surfaceY = GetSurfaceHeight(x, z);

            Vector3Int groundPos = new Vector3Int(x, surfaceY, z);
            if (GetBlock(groundPos)?.Type != BlockType.Grass || IsNearHouse(x, z) || IsNearPond(x, z)) continue;

            float scale = 0.65f + ((x + z + i) % 4) * 0.08f;
            GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bush.name = "SmallBush";
            bush.transform.SetParent(_vegetationRoot);
            bush.transform.position = new Vector3(x + 0.5f, surfaceY + scale * 0.5f + 1f, z + 0.5f);
            bush.transform.localScale = new Vector3(scale, scale * 0.85f, scale);
            bush.GetComponent<Renderer>().sharedMaterial = _materials[BlockType.Leaves];
            Destroy(bush.GetComponent<Collider>());
        }
    }

    private void SpawnCreatures()
    {
        // Passive Cows
        int cowsPlaced = 0;
        for (int i = 0; i < 40 && cowsPlaced < cowCount; i++)
        {
            int x = 12 + Mathf.Abs(seed + i * 23) % (worldWidth - 24);
            int z = 12 + Mathf.Abs(seed + i * 47) % (worldDepth - 24);
            if (IsValidMobSpawn(x, z))
            {
                int surfaceY = GetSurfaceHeight(x, z);
                MobFactory.CreateCow(new Vector3(x + 0.5f, surfaceY + 1f, z + 0.5f), _cowsRoot, this);
                cowsPlaced++;
            }
        }

        // Passive Sheep
        int sheepPlaced = 0;
        for (int i = 0; i < 40 && sheepPlaced < sheepCount; i++)
        {
            int x = 15 + Mathf.Abs(seed + 100 + i * 31) % (worldWidth - 30);
            int z = 15 + Mathf.Abs(seed + 100 + i * 59) % (worldDepth - 30);
            if (IsValidMobSpawn(x, z))
            {
                int surfaceY = GetSurfaceHeight(x, z);
                MobFactory.CreateSheep(new Vector3(x + 0.5f, surfaceY + 1f, z + 0.5f), _sheepRoot, this);
                sheepPlaced++;
            }
        }

        // Passive Pigs
        int pigsPlaced = 0;
        for (int i = 0; i < 40 && pigsPlaced < pigCount; i++)
        {
            int x = 10 + Mathf.Abs(seed + 200 + i * 37) % (worldWidth - 20);
            int z = 10 + Mathf.Abs(seed + 200 + i * 71) % (worldDepth - 20);
            if (IsValidMobSpawn(x, z))
            {
                int surfaceY = GetSurfaceHeight(x, z);
                MobFactory.CreatePig(new Vector3(x + 0.5f, surfaceY + 1f, z + 0.5f), _pigsRoot, this);
                pigsPlaced++;
            }
        }

        // Hostile Zombies (spawned at distance from player spawn 64, 64)
        int zombiesPlaced = 0;
        for (int i = 0; i < 40 && zombiesPlaced < zombieCount; i++)
        {
            int x = 8 + Mathf.Abs(seed + 500 + i * 41) % (worldWidth - 16);
            int z = 8 + Mathf.Abs(seed + 500 + i * 83) % (worldDepth - 16);

            float distToSpawn = Vector2.Distance(new Vector2(x, z), new Vector2(worldWidth / 2f, worldDepth / 2f));
            if (distToSpawn >= 20f && IsValidMobSpawn(x, z))
            {
                int surfaceY = GetSurfaceHeight(x, z);
                MobFactory.CreateZombie(new Vector3(x + 0.5f, surfaceY + 1f, z + 0.5f), _zombiesRoot, this);
                zombiesPlaced++;
            }
        }
    }

    private bool IsValidMobSpawn(int x, int z)
    {
        if (x < 6 || x >= worldWidth - 6 || z < 6 || z >= worldDepth - 6) return false;
        if (IsNearHouse(x, z) || IsNearPond(x, z)) return false;

        int surfaceY = GetSurfaceHeight(x, z);
        Vector3Int groundPos = new Vector3Int(x, surfaceY, z);
        VoxelBlock b = GetBlock(groundPos);
        return b != null && b.Type == BlockType.Grass;
    }

    private void EnsureDayNightCycle()
    {
        if (FindFirstObjectByType<DayNightCycle>() == null)
        {
            GameObject dnc = new GameObject("DayNightCycleController");
            dnc.AddComponent<DayNightCycle>();
        }
    }

    private void EnsureMinimap()
    {
        if (FindFirstObjectByType<MinimapController>() == null)
        {
            GameObject minimapObj = new GameObject("MinimapController");
            minimapObj.AddComponent<MinimapController>();
        }
    }

    private void EnsureAudioManager()
    {
        if (FindFirstObjectByType<AudioManager>() == null)
        {
            GameObject amObj = new GameObject("AudioManager");
            amObj.AddComponent<AudioManager>();
        }
    }

    private void EnsurePauseMenu()
    {
        if (FindFirstObjectByType<PauseMenuController>() == null)
        {
            GameObject pauseMenuObj = new GameObject("PauseMenuController");
            pauseMenuObj.AddComponent<PauseMenuController>();
        }
    }

    private void EnsurePlayerHealth()
    {
        if (FindFirstObjectByType<PlayerHealth>() == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.GetComponent<PlayerHealth>() == null)
            {
                player.gameObject.AddComponent<PlayerHealth>();
            }
        }
    }

    private void EnsurePlayerExperience()
    {
        if (FindFirstObjectByType<PlayerExperience>() == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.GetComponent<PlayerExperience>() == null)
            {
                player.gameObject.AddComponent<PlayerExperience>();
            }
        }
    }

    private void CreateClouds()
    {
        Vector3[] cloudPositions =
        {
            new Vector3(8f, 18f, 12f),
            new Vector3(30f, 21f, 22f),
            new Vector3(58f, 19f, 8f),
            new Vector3(91f, 22f, 18f),
            new Vector3(16f, 20f, 66f),
            new Vector3(48f, 18f, 102f),
            new Vector3(82f, 21f, 76f),
            new Vector3(116f, 19f, 54f),
            new Vector3(105f, 23f, 112f),
            new Vector3(65f, 20f, 42f)
        };

        for (int i = 0; i < cloudPositions.Length; i++)
        {
            CreateCloudGroup(cloudPositions[i], 3 + i % 3);
        }
    }

    private void CreateCloudGroup(Vector3 origin, int length)
    {
        GameObject cloudGroup = new GameObject("Cloud");
        cloudGroup.transform.SetParent(_cloudsRoot);
        cloudGroup.transform.position = origin;

        for (int i = 0; i < length; i++)
        {
            GameObject cloudBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloudBlock.name = "CloudBlock";
            cloudBlock.transform.SetParent(cloudGroup.transform);
            cloudBlock.transform.localPosition = new Vector3(i, 0f, i % 2 == 0 ? 0f : 1f);
            cloudBlock.transform.localScale = Vector3.one;
            cloudBlock.GetComponent<Renderer>().sharedMaterial = _cloudMaterial;
            Destroy(cloudBlock.GetComponent<Collider>());
        }
    }

    private void SetBlock(Vector3Int gridPosition, BlockType blockType, bool hasCollider, Transform parent)
    {
        RemoveBlock(gridPosition);
        CreateBlock(gridPosition, blockType, hasCollider, parent);
    }

    private void SetCustomBlock(Vector3Int gridPosition, Material material, BlockType blockType, bool hasCollider, Transform parent)
    {
        RemoveBlock(gridPosition);
        CreateCustomBlock(gridPosition, material, blockType, hasCollider, parent);
    }

    private void CreateBlock(Vector3Int gridPosition, BlockType blockType, bool hasCollider, Transform parent)
    {
        if (blockType == BlockType.Air || HasBlock(gridPosition))
        {
            return;
        }

        GameObject blockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blockObject.name = blockType + "_" + gridPosition.x + "_" + gridPosition.y + "_" + gridPosition.z;
        blockObject.transform.SetParent(parent != null ? parent : transform);
        blockObject.transform.position = gridPosition + Vector3.one * 0.5f;
        blockObject.transform.localScale = Vector3.one;

        Renderer blockRenderer = blockObject.GetComponent<Renderer>();

        Material blockMat = _materials[blockType];
        if (blockType == BlockType.Grass)
        {
            blockMat = GetGrassMaterial(gridPosition);
        }

        blockRenderer.sharedMaterial = blockMat;

        if (!hasCollider)
        {
            Destroy(blockObject.GetComponent<Collider>());
        }

        _blocks.Add(gridPosition, new VoxelBlock(blockType, gridPosition, blockObject));
    }

    private Material GetGrassMaterial(Vector3Int pos)
    {
        if (pos.y >= maximumTerrainHeight - 2)
        {
            return _grassHighlandMaterial;
        }
        if (pos.y <= baseTerrainHeight + 1)
        {
            return _grassValleyMaterial;
        }
        return _materials[BlockType.Grass];
    }

    private void CreateCustomBlock(Vector3Int gridPosition, Material material, BlockType blockType, bool hasCollider, Transform parent)
    {
        if (HasBlock(gridPosition))
        {
            return;
        }

        GameObject blockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blockObject.name = blockType + "_Custom_" + gridPosition.x + "_" + gridPosition.y + "_" + gridPosition.z;
        blockObject.transform.SetParent(parent != null ? parent : transform);
        blockObject.transform.position = gridPosition + Vector3.one * 0.5f;
        blockObject.transform.localScale = Vector3.one;

        Renderer blockRenderer = blockObject.GetComponent<Renderer>();
        blockRenderer.sharedMaterial = material;

        if (!hasCollider)
        {
            Destroy(blockObject.GetComponent<Collider>());
        }

        _blocks.Add(gridPosition, new VoxelBlock(blockType, gridPosition, blockObject));
    }

    private void CreateMaterials()
    {
        Shader blockShader = ShaderUtils.GetLitShader();
        if (blockShader == null)
        {
            Debug.LogError("VoxelWorld: Could not find URP/Lit shader, falling back to basic shaders.", this);
            blockShader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
        }

        // Generate original procedural textures
        Texture2D grassTex = TextureGenerator.CreateGrassTopTexture();
        Texture2D dirtTex = TextureGenerator.CreateDirtTexture();
        Texture2D stoneTex = TextureGenerator.CreateStoneTexture();
        Texture2D woodTex = TextureGenerator.CreateWoodTexture();
        Texture2D plankTex = TextureGenerator.CreatePlankTexture();
        Texture2D leavesTex = TextureGenerator.CreateLeavesTexture();
        Texture2D sandTex = TextureGenerator.CreateSandTexture();
        Texture2D waterTex = TextureGenerator.CreateWaterTexture();
        Texture2D cobbleTex = TextureGenerator.CreateCobbleTexture();

        _generatedTextures.Add(grassTex);
        _generatedTextures.Add(dirtTex);
        _generatedTextures.Add(stoneTex);
        _generatedTextures.Add(woodTex);
        _generatedTextures.Add(plankTex);
        _generatedTextures.Add(leavesTex);
        _generatedTextures.Add(sandTex);
        _generatedTextures.Add(waterTex);
        _generatedTextures.Add(cobbleTex);

        // Core Voxel Materials with elevation/biome grass variants
        _materials[BlockType.Grass] = CreateTexturedMaterial("GrassTemperate", grassTex, new Color(0.92f, 1.0f, 0.90f), blockShader, 0.1f);
        _grassValleyMaterial = CreateTexturedMaterial("GrassValley", grassTex, new Color(0.96f, 1.04f, 0.88f), blockShader, 0.1f);
        _grassHighlandMaterial = CreateTexturedMaterial("GrassHighland", grassTex, new Color(0.85f, 0.94f, 0.88f), blockShader, 0.1f);

        _materials[BlockType.Dirt] = CreateTexturedMaterial("Dirt", dirtTex, Color.white, blockShader, 0.05f);
        _materials[BlockType.Stone] = CreateTexturedMaterial("Stone", stoneTex, Color.white, blockShader, 0.15f);
        _materials[BlockType.Wood] = CreateTexturedMaterial("Wood", woodTex, Color.white, blockShader, 0.1f);
        _materials[BlockType.Sand] = CreateTexturedMaterial("Sand", sandTex, Color.white, blockShader, 0.05f);
        _materials[BlockType.Leaves] = CreateTexturedMaterial("Leaves", leavesTex, Color.white, blockShader, 0.1f);

        // Stylized Water Material
        _materials[BlockType.Water] = CreateTexturedMaterial("Water", waterTex, new Color(0.85f, 0.95f, 1.0f, 0.85f), blockShader, 0.85f);

        // Decor Materials
        _plankMaterial = CreateTexturedMaterial("WoodPlank", plankTex, Color.white, blockShader, 0.15f);
        _cobbleMaterial = CreateTexturedMaterial("CobblePath", cobbleTex, Color.white, blockShader, 0.2f);
        _cloudMaterial = CreateColorMaterial("Cloud", new Color(0.96f, 0.98f, 1.0f, 0.95f), blockShader, 0.0f);

        // Flower & Vegetation Materials
        _redFlowerMaterial = CreateColorMaterial("RedFlower", new Color(0.92f, 0.22f, 0.22f), blockShader, 0.2f);
        _yellowFlowerMaterial = CreateColorMaterial("YellowFlower", new Color(0.98f, 0.85f, 0.18f), blockShader, 0.2f);
        _whiteFlowerMaterial = CreateColorMaterial("WhiteFlower", new Color(0.95f, 0.95f, 0.95f), blockShader, 0.2f);
        _stemMaterial = CreateColorMaterial("Stem", new Color(0.28f, 0.62f, 0.18f), blockShader, 0.1f);
        _tallGrassMaterialA = CreateColorMaterial("TallGrassA", new Color(0.32f, 0.68f, 0.22f), blockShader, 0.1f);
        _tallGrassMaterialB = CreateColorMaterial("TallGrassB", new Color(0.42f, 0.74f, 0.25f), blockShader, 0.1f);
    }

    private Material CreateTexturedMaterial(string name, Texture2D texture, Color tint, Shader shader, float smoothness)
    {
        if (shader == null) shader = ShaderUtils.GetLitShader();
        Material mat = new Material(shader)
        {
            name = name + " Procedural Material"
        };
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private Material CreateColorMaterial(string name, Color color, Shader shader, float smoothness)
    {
        if (shader == null) shader = ShaderUtils.GetLitShader();
        Material mat = new Material(shader)
        {
            name = name + " Color Material"
        };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }
}
