using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a lightweight, decorative 3D voxel diorama landscape for the Main Menu.
/// Completely independent from gameplay systems: no zombies, no player, no mobs, no quests.
/// Features a scenic voxel island with gentle hills, a cozy wooden cabin, pond, trees, flowers,
/// floating clouds, and a cinematic slowly orbiting background camera.
/// </summary>
public class MenuBackgroundDiorama : MonoBehaviour
{
    [Header("Camera & Motion")]
    [SerializeField] private Camera backgroundCamera;
    [SerializeField] private float orbitSpeed = 2.2f;
    [SerializeField] private float orbitDistance = 24f;
    [SerializeField] private float orbitHeight = 11.5f;
    [SerializeField] private Vector3 focusPoint = new Vector3(0f, 3.2f, 0f);

    [Header("Island Settings")]
    [SerializeField] private int islandRadius = 12;

    private float _currentOrbitAngle = 45f;
    private GameObject _dioramaRoot;

    private readonly Dictionary<BlockType, Material> _materials = new Dictionary<BlockType, Material>();
    private readonly List<Texture2D> _generatedTextures = new List<Texture2D>();
    private readonly List<Mesh> _generatedMeshes = new List<Mesh>();

    private Material _plankMaterial;
    private Material _cobbleMaterial;
    private Material _cloudMaterial;
    private Material _redFlowerMaterial;
    private Material _yellowFlowerMaterial;
    private Material _whiteFlowerMaterial;
    private Material _stemMaterial;

    private void Awake()
    {
        if (backgroundCamera == null)
        {
            backgroundCamera = GetComponent<Camera>();
            if (backgroundCamera == null)
            {
                backgroundCamera = Camera.main;
            }
        }
    }

    private void Start()
    {
        BuildDiorama();
        SetupCameraPosition();
    }

    private void Update()
    {
        UpdateCameraOrbit();
    }

    private void OnDestroy()
    {
        CleanupResources();
    }

    private void SetupCameraPosition()
    {
        if (backgroundCamera == null) return;

        backgroundCamera.fieldOfView = 50f;
        backgroundCamera.nearClipPlane = 0.3f;
        backgroundCamera.farClipPlane = 200f;
        backgroundCamera.clearFlags = CameraClearFlags.Skybox;

        UpdateCameraOrbit();
    }

    private void UpdateCameraOrbit()
    {
        if (backgroundCamera == null) return;

        _currentOrbitAngle += orbitSpeed * Time.deltaTime;
        if (_currentOrbitAngle >= 360f) _currentOrbitAngle -= 360f;

        float rad = _currentOrbitAngle * Mathf.Deg2Rad;
        Vector3 camPos = new Vector3(
            focusPoint.x + Mathf.Cos(rad) * orbitDistance,
            focusPoint.y + orbitHeight,
            focusPoint.z + Mathf.Sin(rad) * orbitDistance
        );

        backgroundCamera.transform.position = camPos;
        backgroundCamera.transform.LookAt(focusPoint);
    }

    private void BuildDiorama()
    {
        _dioramaRoot = new GameObject("MenuDiorama_Root");
        _dioramaRoot.transform.SetParent(transform, false);

        CreateMaterials();

        // Dictionary of blocks by position: pos -> blockType
        Dictionary<Vector3Int, BlockType> voxelGrid = new Dictionary<Vector3Int, BlockType>();

        // 1. Generate island terrain heights
        for (int x = -islandRadius; x <= islandRadius; x++)
        {
            for (int z = -islandRadius; z <= islandRadius; z++)
            {
                float dist = Mathf.Sqrt(x * x + z * z);
                if (dist > islandRadius) continue;

                // Edge tapering
                float edgeFactor = 1f - Mathf.Clamp01(dist / islandRadius);
                float noise = Mathf.PerlinNoise((x + 50) * 0.12f, (z + 50) * 0.12f);
                int surfaceY = Mathf.RoundToInt(1f + edgeFactor * 4f + noise * 1.5f);

                // Small tranquil pond in negative quadrant
                bool isPond = (x >= -7 && x <= -3 && z >= -6 && z <= -2);
                if (isPond)
                {
                    surfaceY = 1;
                }

                // Place terrain column
                for (int y = -2; y <= surfaceY; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, z);
                    if (y == surfaceY)
                    {
                        if (isPond)
                        {
                            voxelGrid[pos] = BlockType.Sand;
                            voxelGrid[new Vector3Int(x, 2, z)] = BlockType.Water;
                        }
                        else
                        {
                            voxelGrid[pos] = BlockType.Grass;
                        }
                    }
                    else if (y >= surfaceY - 1)
                    {
                        voxelGrid[pos] = BlockType.Dirt;
                    }
                    else
                    {
                        voxelGrid[pos] = BlockType.Stone;
                    }
                }
            }
        }

        // 2. Build cozy voxel cabin on the gentle hill
        Vector3Int cabinOrigin = new Vector3Int(2, 4, 1);
        BuildCabin(voxelGrid, cabinOrigin);

        // 3. Build stylized trees
        BuildTree(voxelGrid, new Vector3Int(-6, 4, 4));
        BuildTree(voxelGrid, new Vector3Int(5, 4, -5));
        BuildTree(voxelGrid, new Vector3Int(-2, 4, 6));

        // 4. Batch voxels by material into combined meshes for optimal rendering performance
        Dictionary<Material, List<CombineInstance>> combines = new Dictionary<Material, List<CombineInstance>>();

        Mesh unitCube = CreateUnitCubeMesh();
        _generatedMeshes.Add(unitCube);

        foreach (KeyValuePair<Vector3Int, BlockType> kvp in voxelGrid)
        {
            Vector3Int p = kvp.Key;
            BlockType type = kvp.Value;

            // Simple culling: only include blocks with at least one exposed face
            if (IsBlockSurrounded(voxelGrid, p)) continue;

            Material mat = GetMaterialForBlock(type);
            if (mat == null) continue;

            if (!combines.ContainsKey(mat))
            {
                combines[mat] = new List<CombineInstance>();
            }

            CombineInstance ci = new CombineInstance
            {
                mesh = unitCube,
                transform = Matrix4x4.TRS(new Vector3(p.x, p.y, p.z), Quaternion.identity, Vector3.one)
            };
            combines[mat].Add(ci);
        }

        foreach (KeyValuePair<Material, List<CombineInstance>> kvp in combines)
        {
            if (kvp.Value.Count == 0) continue;

            GameObject subObj = new GameObject("Diorama_" + kvp.Key.name);
            subObj.transform.SetParent(_dioramaRoot.transform, false);

            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(kvp.Value.ToArray(), true, true);
            _generatedMeshes.Add(combinedMesh);

            MeshFilter mf = subObj.AddComponent<MeshFilter>();
            mf.sharedMesh = combinedMesh;

            MeshRenderer mr = subObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = kvp.Key;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        // 5. Add scenic floating voxel clouds in the sky
        BuildClouds();
    }

    private bool IsBlockSurrounded(Dictionary<Vector3Int, BlockType> grid, Vector3Int p)
    {
        return grid.ContainsKey(new Vector3Int(p.x + 1, p.y, p.z)) &&
               grid.ContainsKey(new Vector3Int(p.x - 1, p.y, p.z)) &&
               grid.ContainsKey(new Vector3Int(p.x, p.y + 1, p.z)) &&
               grid.ContainsKey(new Vector3Int(p.x, p.y - 1, p.z)) &&
               grid.ContainsKey(new Vector3Int(p.x, p.y, p.z + 1)) &&
               grid.ContainsKey(new Vector3Int(p.x, p.y, p.z - 1));
    }

    private void BuildCabin(Dictionary<Vector3Int, BlockType> grid, Vector3Int origin)
    {
        int width = 5;
        int depth = 5;
        int height = 3;

        // Cobblestone foundation
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                grid[new Vector3Int(origin.x + x, origin.y, origin.z + z)] = BlockType.Wood;
            }
        }

        // Corner pillars & plank walls
        for (int y = 1; y <= height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    bool isCorner = (x == 0 || x == width - 1) && (z == 0 || z == depth - 1);
                    bool isWall = (x == 0 || x == width - 1 || z == 0 || z == depth - 1);

                    // Door opening at front
                    if (z == 0 && x == 2 && y <= 2) continue;

                    // Window opening at sides
                    if ((x == 0 || x == width - 1) && z == 2 && y == 2) continue;

                    if (isCorner)
                    {
                        grid[new Vector3Int(origin.x + x, origin.y + y, origin.z + z)] = BlockType.Wood;
                    }
                    else if (isWall)
                    {
                        grid[new Vector3Int(origin.x + x, origin.y + y, origin.z + z)] = BlockType.Wood;
                    }
                }
            }
        }

        // Roof overhang
        int roofY = origin.y + height + 1;
        for (int x = -1; x <= width; x++)
        {
            for (int z = -1; z <= depth; z++)
            {
                grid[new Vector3Int(origin.x + x, roofY, origin.z + z)] = BlockType.Wood;
            }
        }

        // Cobblestone chimney
        for (int y = 1; y <= height + 2; y++)
        {
            grid[new Vector3Int(origin.x + width - 1, origin.y + y, origin.z + 1)] = BlockType.Stone;
        }
    }

    private void BuildTree(Dictionary<Vector3Int, BlockType> grid, Vector3Int origin)
    {
        // Trunk
        int trunkHeight = 4;
        for (int y = 0; y < trunkHeight; y++)
        {
            grid[new Vector3Int(origin.x, origin.y + y, origin.z)] = BlockType.Wood;
        }

        // Foliage Crown
        int leafBaseY = origin.y + trunkHeight - 1;
        for (int lx = -2; lx <= 2; lx++)
        {
            for (int lz = -2; lz <= 2; lz++)
            {
                for (int ly = 0; ly <= 2; ly++)
                {
                    if (Mathf.Abs(lx) == 2 && Mathf.Abs(lz) == 2 && ly == 2) continue;
                    Vector3Int leafPos = new Vector3Int(origin.x + lx, leafBaseY + ly, origin.z + lz);
                    if (!grid.ContainsKey(leafPos))
                    {
                        grid[leafPos] = BlockType.Leaves;
                    }
                }
            }
        }

        // Top single foliage cap
        grid[new Vector3Int(origin.x, leafBaseY + 3, origin.z)] = BlockType.Leaves;
    }

    private void BuildClouds()
    {
        Vector3[] cloudOffsets =
        {
            new Vector3(-10f, 13f, 4f),
            new Vector3(8f, 15f, -6f),
            new Vector3(4f, 12f, 12f)
        };

        foreach (Vector3 basePos in cloudOffsets)
        {
            GameObject cloudObj = new GameObject("Cloud");
            cloudObj.transform.SetParent(_dioramaRoot.transform, false);
            cloudObj.transform.position = basePos;

            // Small 3-block cloud puff
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    puff.name = "Puff";
                    puff.transform.SetParent(cloudObj.transform, false);
                    puff.transform.localPosition = new Vector3(dx * 1.4f, 0f, dz * 1.4f);
                    puff.transform.localScale = new Vector3(1.6f, 0.7f, 1.4f);

                    Collider col = puff.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    MeshRenderer mr = puff.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = _cloudMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }
    }

    private Material GetMaterialForBlock(BlockType type)
    {
        if (_materials.TryGetValue(type, out Material mat))
        {
            return mat;
        }
        return null;
    }

    private void CreateMaterials()
    {
        Shader litShader = ShaderUtils.GetLitShader();

        Texture2D grassTex = TextureGenerator.CreateGrassTopTexture();
        Texture2D dirtTex = TextureGenerator.CreateDirtTexture();
        Texture2D stoneTex = TextureGenerator.CreateStoneTexture();
        Texture2D woodTex = TextureGenerator.CreateWoodTexture();
        Texture2D leavesTex = TextureGenerator.CreateLeavesTexture();
        Texture2D sandTex = TextureGenerator.CreateSandTexture();
        Texture2D waterTex = TextureGenerator.CreateWaterTexture();
        Texture2D cobbleTex = TextureGenerator.CreateCobbleTexture();
        Texture2D plankTex = TextureGenerator.CreatePlankTexture();

        _generatedTextures.Add(grassTex);
        _generatedTextures.Add(dirtTex);
        _generatedTextures.Add(stoneTex);
        _generatedTextures.Add(woodTex);
        _generatedTextures.Add(leavesTex);
        _generatedTextures.Add(sandTex);
        _generatedTextures.Add(waterTex);
        _generatedTextures.Add(cobbleTex);
        _generatedTextures.Add(plankTex);

        _materials[BlockType.Grass] = CreateTexturedMat("Menu_Grass", grassTex, new Color(0.92f, 1.0f, 0.90f), litShader, 0.1f);
        _materials[BlockType.Dirt] = CreateTexturedMat("Menu_Dirt", dirtTex, Color.white, litShader, 0.05f);
        _materials[BlockType.Stone] = CreateTexturedMat("Menu_Stone", stoneTex, Color.white, litShader, 0.15f);
        _materials[BlockType.Wood] = CreateTexturedMat("Menu_Wood", woodTex, Color.white, litShader, 0.1f);
        _materials[BlockType.Leaves] = CreateTexturedMat("Menu_Leaves", leavesTex, Color.white, litShader, 0.1f);
        _materials[BlockType.Sand] = CreateTexturedMat("Menu_Sand", sandTex, Color.white, litShader, 0.05f);
        _materials[BlockType.Water] = CreateTexturedMat("Menu_Water", waterTex, new Color(0.85f, 0.95f, 1.0f, 0.85f), litShader, 0.85f);

        _cloudMaterial = CreateColorMat("Menu_Cloud", new Color(0.96f, 0.98f, 1.0f, 0.92f), litShader, 0f);
    }

    private Material CreateTexturedMat(string name, Texture2D texture, Color tint, Shader shader, float smoothness)
    {
        Material mat = new Material(shader) { name = name };
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private Material CreateColorMat(string name, Color color, Shader shader, float smoothness)
    {
        Material mat = new Material(shader) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private Mesh CreateUnitCubeMesh()
    {
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = Instantiate(tempCube.GetComponent<MeshFilter>().sharedMesh);
        Destroy(tempCube);
        return mesh;
    }

    private void CleanupResources()
    {
        foreach (Material mat in _materials.Values)
        {
            if (mat != null) Destroy(mat);
        }
        _materials.Clear();

        if (_cloudMaterial != null) Destroy(_cloudMaterial);

        foreach (Texture2D tex in _generatedTextures)
        {
            if (tex != null) Destroy(tex);
        }
        _generatedTextures.Clear();

        foreach (Mesh m in _generatedMeshes)
        {
            if (m != null) Destroy(m);
        }
        _generatedMeshes.Clear();

        if (_dioramaRoot != null)
        {
            Destroy(_dioramaRoot);
        }
    }
}
