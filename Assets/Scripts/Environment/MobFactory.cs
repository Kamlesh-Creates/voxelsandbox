using UnityEngine;

/// <summary>
/// Procedurally constructs and spawns original voxel animals (Cows, Sheep, Pigs)
/// and Hostile Zombies using Unity primitives and original materials.
/// </summary>
public static class MobFactory
{
    private static Material _cowBodyMat;
    private static Material _cowSpotMat;
    private static Material _sheepWoolMat;
    private static Material _sheepFaceMat;
    private static Material _pigMat;
    private static Material _pigSnoutMat;
    private static Material _zombieSkinMat;
    private static Material _zombieShirtMat;
    private static Material _zombiePantsMat;

    public static void InitializeMaterials(Shader litShader)
    {
        if (litShader == null) litShader = ShaderUtils.GetLitShader();

        _cowBodyMat = CreateMaterial("CowWhite", new Color(0.92f, 0.92f, 0.90f), litShader);
        _cowSpotMat = CreateMaterial("CowBlack", new Color(0.18f, 0.18f, 0.18f), litShader);
        _sheepWoolMat = CreateMaterial("SheepWool", new Color(0.95f, 0.94f, 0.88f), litShader);
        _sheepFaceMat = CreateMaterial("SheepFace", new Color(0.72f, 0.62f, 0.52f), litShader);
        _pigMat = CreateMaterial("PigPink", new Color(0.96f, 0.68f, 0.65f), litShader);
        _pigSnoutMat = CreateMaterial("PigSnout", new Color(0.88f, 0.52f, 0.50f), litShader);
        _zombieSkinMat = CreateMaterial("ZombieSkin", new Color(0.38f, 0.56f, 0.32f), litShader);
        _zombieShirtMat = CreateMaterial("ZombieShirt", new Color(0.18f, 0.38f, 0.48f), litShader);
        _zombiePantsMat = CreateMaterial("ZombiePants", new Color(0.24f, 0.22f, 0.30f), litShader);
    }

    public static GameObject CreateCow(Vector3 position, Transform parent, VoxelWorld world)
    {
        GameObject root = new GameObject("Cow");
        root.transform.SetParent(parent);
        root.transform.position = position;

        // Main Body
        GameObject body = CreateCube("Body", root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.75f, 1.3f), _cowBodyMat);

        // Body spots (black patches)
        CreateCube("Spot1", body.transform, new Vector3(0.25f, 0.1f, 0.2f), new Vector3(0.55f, 0.5f, 0.5f), _cowSpotMat);
        CreateCube("Spot2", body.transform, new Vector3(-0.25f, -0.1f, -0.2f), new Vector3(0.55f, 0.45f, 0.45f), _cowSpotMat);

        // Head root (pivot near neck)
        GameObject headRoot = new GameObject("HeadPivot");
        headRoot.transform.SetParent(root.transform);
        headRoot.transform.localPosition = new Vector3(0f, 1.15f, 0.7f);

        GameObject head = CreateCube("Head", headRoot.transform, new Vector3(0f, 0.05f, 0.15f), new Vector3(0.5f, 0.5f, 0.5f), _cowSpotMat);
        CreateCube("Muzzle", head.transform, new Vector3(0f, -0.25f, 0.3f), new Vector3(0.4f, 0.3f, 0.3f), _cowBodyMat);
        CreateCube("HornL", head.transform, new Vector3(-0.3f, 0.35f, 0f), new Vector3(0.12f, 0.22f, 0.12f), _cowBodyMat);
        CreateCube("HornR", head.transform, new Vector3(0.3f, 0.35f, 0f), new Vector3(0.12f, 0.22f, 0.12f), _cowBodyMat);

        // Legs (pivots at top of legs)
        Transform legFL = CreateLegPivot("LegFL", root.transform, new Vector3(-0.35f, 0.55f, 0.45f), new Vector3(0.22f, 0.6f, 0.22f), _cowBodyMat);
        Transform legFR = CreateLegPivot("LegFR", root.transform, new Vector3(0.35f, 0.55f, 0.45f), new Vector3(0.22f, 0.6f, 0.22f), _cowSpotMat);
        Transform legBL = CreateLegPivot("LegBL", root.transform, new Vector3(-0.35f, 0.55f, -0.45f), new Vector3(0.22f, 0.6f, 0.22f), _cowSpotMat);
        Transform legBR = CreateLegPivot("LegBR", root.transform, new Vector3(0.35f, 0.55f, -0.45f), new Vector3(0.22f, 0.6f, 0.22f), _cowBodyMat);

        PassiveAnimal ai = root.AddComponent<PassiveAnimal>();
        ai.Setup(AnimalType.Cow, world, headRoot.transform, legFL, legFR, legBL, legBR);

        return root;
    }

    public static GameObject CreateSheep(Vector3 position, Transform parent, VoxelWorld world)
    {
        GameObject root = new GameObject("Sheep");
        root.transform.SetParent(parent);
        root.transform.position = position;

        // Fluffy Wool Body
        GameObject body = CreateCube("WoolBody", root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.95f, 0.8f, 1.25f), _sheepWoolMat);

        // Head root
        GameObject headRoot = new GameObject("HeadPivot");
        headRoot.transform.SetParent(root.transform);
        headRoot.transform.localPosition = new Vector3(0f, 1.05f, 0.65f);

        GameObject head = CreateCube("Head", headRoot.transform, new Vector3(0f, 0f, 0.15f), new Vector3(0.42f, 0.42f, 0.45f), _sheepFaceMat);
        CreateCube("WoolCap", head.transform, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.44f, 0.2f, 0.35f), _sheepWoolMat);

        // Legs
        Transform legFL = CreateLegPivot("LegFL", root.transform, new Vector3(-0.32f, 0.5f, 0.4f), new Vector3(0.2f, 0.55f, 0.2f), _sheepFaceMat);
        Transform legFR = CreateLegPivot("LegFR", root.transform, new Vector3(0.32f, 0.5f, 0.4f), new Vector3(0.2f, 0.55f, 0.2f), _sheepFaceMat);
        Transform legBL = CreateLegPivot("LegBL", root.transform, new Vector3(-0.32f, 0.5f, -0.4f), new Vector3(0.2f, 0.55f, 0.2f), _sheepFaceMat);
        Transform legBR = CreateLegPivot("LegBR", root.transform, new Vector3(0.32f, 0.5f, -0.4f), new Vector3(0.2f, 0.55f, 0.2f), _sheepFaceMat);

        PassiveAnimal ai = root.AddComponent<PassiveAnimal>();
        ai.Setup(AnimalType.Sheep, world, headRoot.transform, legFL, legFR, legBL, legBR);

        return root;
    }

    public static GameObject CreatePig(Vector3 position, Transform parent, VoxelWorld world)
    {
        GameObject root = new GameObject("Pig");
        root.transform.SetParent(parent);
        root.transform.position = position;

        // Pink Body
        GameObject body = CreateCube("Body", root.transform, new Vector3(0f, 0.65f, 0f), new Vector3(0.8f, 0.65f, 1.15f), _pigMat);

        // Head root
        GameObject headRoot = new GameObject("HeadPivot");
        headRoot.transform.SetParent(root.transform);
        headRoot.transform.localPosition = new Vector3(0f, 0.85f, 0.55f);

        GameObject head = CreateCube("Head", headRoot.transform, new Vector3(0f, 0f, 0.12f), new Vector3(0.45f, 0.45f, 0.45f), _pigMat);
        CreateCube("Snout", head.transform, new Vector3(0f, -0.1f, 0.28f), new Vector3(0.24f, 0.16f, 0.16f), _pigSnoutMat);

        // Legs
        Transform legFL = CreateLegPivot("LegFL", root.transform, new Vector3(-0.28f, 0.4f, 0.35f), new Vector3(0.18f, 0.45f, 0.18f), _pigMat);
        Transform legFR = CreateLegPivot("LegFR", root.transform, new Vector3(0.28f, 0.4f, 0.35f), new Vector3(0.18f, 0.45f, 0.18f), _pigMat);
        Transform legBL = CreateLegPivot("LegBL", root.transform, new Vector3(-0.28f, 0.4f, -0.35f), new Vector3(0.18f, 0.45f, 0.18f), _pigMat);
        Transform legBR = CreateLegPivot("LegBR", root.transform, new Vector3(0.28f, 0.4f, -0.35f), new Vector3(0.18f, 0.45f, 0.18f), _pigMat);

        PassiveAnimal ai = root.AddComponent<PassiveAnimal>();
        ai.Setup(AnimalType.Pig, world, headRoot.transform, legFL, legFR, legBL, legBR);

        return root;
    }

    public static GameObject CreateZombie(Vector3 position, Transform parent, VoxelWorld world)
    {
        GameObject root = new GameObject("Zombie");
        root.transform.SetParent(parent);
        root.transform.position = position;

        // Torso
        GameObject torso = CreateCube("Torso", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.7f, 0.3f), _zombieShirtMat);

        // Head
        GameObject headRoot = new GameObject("HeadPivot");
        headRoot.transform.SetParent(root.transform);
        headRoot.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        GameObject head = CreateCube("Head", headRoot.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.44f, 0.44f, 0.44f), _zombieSkinMat);

        // Arms (Pivots at shoulder)
        Transform leftArm = CreateArmPivot("LeftArm", root.transform, new Vector3(-0.38f, 1.45f, 0f), new Vector3(0.2f, 0.65f, 0.2f), _zombieShirtMat, _zombieSkinMat);
        Transform rightArm = CreateArmPivot("RightArm", root.transform, new Vector3(0.38f, 1.45f, 0f), new Vector3(0.2f, 0.65f, 0.2f), _zombieShirtMat, _zombieSkinMat);

        // Legs (Pivots at hip)
        Transform leftLeg = CreateLegPivot("LeftLeg", root.transform, new Vector3(-0.16f, 0.8f, 0f), new Vector3(0.22f, 0.75f, 0.22f), _zombiePantsMat);
        Transform rightLeg = CreateLegPivot("RightLeg", root.transform, new Vector3(0.16f, 0.8f, 0f), new Vector3(0.22f, 0.75f, 0.22f), _zombiePantsMat);

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1.0f, 0f);
        col.height = 2.0f;
        col.radius = 0.4f;

        ZombieController zombie = root.AddComponent<ZombieController>();
        zombie.Setup(world, torso.transform, headRoot.transform, leftArm, rightArm, leftLeg, rightLeg);

        return root;
    }

    private static Transform CreateLegPivot(string name, Transform parent, Vector3 pivotPos, Vector3 legScale, Material mat)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(parent);
        pivot.transform.localPosition = pivotPos;

        GameObject legMesh = CreateCube("LegMesh", pivot.transform, new Vector3(0f, -legScale.y * 0.5f, 0f), legScale, mat);
        return pivot.transform;
    }

    private static Transform CreateArmPivot(string name, Transform parent, Vector3 pivotPos, Vector3 armScale, Material sleeveMat, Material handMat)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(parent);
        pivot.transform.localPosition = pivotPos;
        pivot.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // Outstretched

        GameObject sleeve = CreateCube("Sleeve", pivot.transform, new Vector3(0f, -armScale.y * 0.3f, 0f), new Vector3(armScale.x, armScale.y * 0.6f, armScale.z), sleeveMat);
        GameObject hand = CreateCube("Hand", pivot.transform, new Vector3(0f, -armScale.y * 0.75f, 0f), new Vector3(armScale.x * 0.9f, armScale.y * 0.4f, armScale.z * 0.9f), handMat);

        return pivot.transform;
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = localScale;

        Renderer r = cube.GetComponent<Renderer>();
        if (r != null && material != null)
        {
            r.sharedMaterial = material;
        }

        Collider col = cube.GetComponent<Collider>();
        if (col != null)
        {
            Object.Destroy(col);
        }

        return cube;
    }

    private static Material CreateMaterial(string name, Color color, Shader shader)
    {
        if (shader == null) shader = ShaderUtils.GetLitShader();
        Material mat = new Material(shader)
        {
            name = name + " Runtime Material"
        };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }
}
