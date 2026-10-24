using UnityEngine;

/// <summary>
/// Generates original procedural stylized 16x16 pixel textures for voxel blocks.
/// 100% procedurally generated with no external textures or assets.
/// </summary>
public static class TextureGenerator
{
    private const int TextureSize = 16;

    public static Texture2D CreateGrassTopTexture()
    {
        Texture2D texture = CreateBaseTexture("GrassTop");
        // Realistic temperate meadow grass palette
        Color baseGreen = new Color(0.28f, 0.62f, 0.20f);
        Color lightGreen = new Color(0.36f, 0.72f, 0.24f);
        Color darkGreen = new Color(0.19f, 0.46f, 0.13f);
        Color warmTip = new Color(0.40f, 0.74f, 0.22f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // Multi-frequency organic blade noise
                float n1 = Mathf.PerlinNoise(x * 0.55f + 14f, y * 0.55f + 14f);
                float n2 = Mathf.PerlinNoise(x * 1.15f + 42f, y * 1.15f + 42f);
                Color pixelColor = baseGreen;

                if (n1 > 0.60f) pixelColor = lightGreen;
                else if (n1 < 0.38f) pixelColor = darkGreen;

                // Subtle warm sunlight glint on blade tips
                if (n2 > 0.74f)
                {
                    pixelColor = Color.Lerp(pixelColor, warmTip, 0.65f);
                }

                // Soft border occlusion for clean voxel silhouette without harsh edges
                if (x == 0 || y == 0 || x == TextureSize - 1 || y == TextureSize - 1)
                {
                    pixelColor = Color.Lerp(pixelColor, darkGreen, 0.18f);
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreateDirtTexture()
    {
        Texture2D texture = CreateBaseTexture("Dirt");
        // Rich organic loam & humus soil palette
        Color baseLoam = new Color(0.40f, 0.25f, 0.13f);
        Color darkSoil = new Color(0.30f, 0.18f, 0.09f);
        Color lightLoam = new Color(0.48f, 0.31f, 0.17f);
        Color pebbleGrey = new Color(0.48f, 0.44f, 0.40f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.7f + 18f, y * 0.7f + 18f);
                float n2 = Mathf.PerlinNoise(x * 1.4f + 65f, y * 1.4f + 65f);
                Color pixelColor = baseLoam;

                if (n1 > 0.58f) pixelColor = lightLoam;
                else if (n1 < 0.40f) pixelColor = darkSoil;

                // Natural tiny river pebbles / silt specks
                if (n2 > 0.82f)
                {
                    pixelColor = Color.Lerp(pixelColor, pebbleGrey, 0.6f);
                }

                if (x == 0 || y == 0 || x == TextureSize - 1 || y == TextureSize - 1)
                {
                    pixelColor = Color.Lerp(pixelColor, darkSoil, 0.15f);
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreateStoneTexture()
    {
        Texture2D texture = CreateBaseTexture("Stone");
        // Natural weathered bedrock & granite striations
        Color baseRock = new Color(0.52f, 0.53f, 0.55f);
        Color darkCrevice = new Color(0.36f, 0.37f, 0.39f);
        Color lightMineral = new Color(0.63f, 0.65f, 0.67f);
        Color mossTint = new Color(0.42f, 0.48f, 0.38f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // Geological directional stratum noise
                float strata = Mathf.PerlinNoise(x * 0.35f + y * 0.2f + 80f, y * 0.8f + 80f);
                float micro = Mathf.PerlinNoise(x * 1.6f + 25f, y * 1.6f + 25f);
                Color pixelColor = baseRock;

                if (strata > 0.58f) pixelColor = lightMineral;
                else if (strata < 0.40f) pixelColor = darkCrevice;

                // Subtle organic moss tint in sheltered fissures
                if (micro < 0.22f && strata < 0.48f)
                {
                    pixelColor = Color.Lerp(pixelColor, mossTint, 0.45f);
                }

                // Natural jagged fracture lines
                if ((x + y * 2) % 11 == 0 && micro > 0.65f)
                {
                    pixelColor = Color.Lerp(pixelColor, darkCrevice, 0.55f);
                }

                if (x == 0 || y == 0 || x == TextureSize - 1 || y == TextureSize - 1)
                {
                    pixelColor = Color.Lerp(pixelColor, darkCrevice, 0.2f);
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreateWoodTexture()
    {
        Texture2D texture = CreateBaseTexture("Wood");
        // Realistic natural bark with undulating vertical grain
        Color baseBark = new Color(0.44f, 0.27f, 0.14f);
        Color furrowDark = new Color(0.28f, 0.16f, 0.08f);
        Color ridgeLight = new Color(0.53f, 0.33f, 0.17f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // Vertical bark grain with organic lateral sway
                float sway = Mathf.Sin(y * 0.45f) * 0.8f;
                float barkNoise = Mathf.PerlinNoise((x + sway) * 1.3f + 35f, y * 0.25f + 35f);
                Color pixelColor = baseBark;

                if ((x + Mathf.RoundToInt(sway)) % 4 == 0 || barkNoise < 0.34f)
                {
                    pixelColor = furrowDark;
                }
                else if (barkNoise > 0.62f)
                {
                    pixelColor = ridgeLight;
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreatePlankTexture()
    {
        Texture2D texture = CreateBaseTexture("Plank");
        Color basePlank = new Color(0.68f, 0.48f, 0.28f);
        Color darkPlank = new Color(0.52f, 0.35f, 0.18f);
        Color seamColor = new Color(0.36f, 0.22f, 0.10f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Color pixelColor = basePlank;
                float grain = Mathf.PerlinNoise(x * 0.3f + 10f, y * 1.5f + 10f);
                if (grain > 0.55f) pixelColor = Color.Lerp(basePlank, darkPlank, 0.3f);

                // Horizontal plank seams every 4 pixels
                if (y % 4 == 0)
                {
                    pixelColor = seamColor;
                }
                // Vertical nail / joint markings
                if ((y < 4 && x == 3) || (y >= 4 && y < 8 && x == 11) || (y >= 8 && y < 12 && x == 5) || (y >= 12 && x == 13))
                {
                    pixelColor = seamColor;
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreateLeavesTexture()
    {
        Texture2D texture = CreateBaseTexture("Leaves");
        // Deep volumetric foliage canopy palette with sunlit leaf clusters
        Color baseLeaf = new Color(0.22f, 0.52f, 0.16f);
        Color shadowLeaf = new Color(0.13f, 0.32f, 0.09f);
        Color sunlitLeaf = new Color(0.34f, 0.66f, 0.22f);
        Color edgeHighlight = new Color(0.44f, 0.74f, 0.28f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.85f + 90f, y * 0.85f + 90f);
                float n2 = Mathf.PerlinNoise(x * 1.5f + 20f, y * 1.5f + 20f);
                Color pixelColor = baseLeaf;

                if (n1 > 0.62f) pixelColor = sunlitLeaf;
                else if (n1 < 0.36f) pixelColor = shadowLeaf;

                // Subtle dithered canopy leaf highlights
                if (n2 > 0.78f)
                {
                    pixelColor = Color.Lerp(pixelColor, edgeHighlight, 0.75f);
                }

                // Subtle depth shadow on corners
                if ((x == 0 && y == 0) || (x == 15 && y == 15) || (x == 0 && y == 15) || (x == 15 && y == 0))
                {
                    pixelColor = Color.Lerp(pixelColor, shadowLeaf, 0.35f);
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreateSandTexture()
    {
        Texture2D texture = CreateBaseTexture("Sand");
        // Warm alluvial shoreline sand with delicate water ripple contours
        Color baseSand = new Color(0.85f, 0.78f, 0.54f);
        Color rippleDark = new Color(0.76f, 0.67f, 0.44f);
        Color sunlitGlint = new Color(0.92f, 0.86f, 0.64f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // Soft water ripple lines along x axis with gentle noise
                float ripple = Mathf.Sin((y * 1.4f) + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 3f);
                float grain = Mathf.PerlinNoise(x * 1.3f + 45f, y * 1.3f + 45f);
                Color pixelColor = baseSand;

                if (ripple > 0.45f) pixelColor = sunlitGlint;
                else if (ripple < -0.35f) pixelColor = rippleDark;

                if (grain > 0.78f) pixelColor = Color.Lerp(pixelColor, sunlitGlint, 0.4f);

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreateWaterTexture()
    {
        Texture2D texture = CreateBaseTexture("Water");
        Color baseWater = new Color(0.18f, 0.52f, 0.86f, 0.88f);
        Color lightWater = new Color(0.32f, 0.68f, 0.94f, 0.92f);
        Color waveCrest = new Color(0.55f, 0.82f, 1.0f, 0.95f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float wave = Mathf.Sin((x + y) * 0.6f) * 0.5f + 0.5f;
                Color pixelColor = Color.Lerp(baseWater, lightWater, wave * 0.7f);

                if ((x + y * 2) % 7 == 0)
                {
                    pixelColor = waveCrest;
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D CreateCobbleTexture()
    {
        Texture2D texture = CreateBaseTexture("Cobblestone");
        Color baseStone = new Color(0.48f, 0.48f, 0.50f);
        Color lightStone = new Color(0.60f, 0.60f, 0.62f);
        Color darkStone = new Color(0.34f, 0.34f, 0.36f);
        Color mortarColor = new Color(0.25f, 0.25f, 0.27f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // Stone paver pattern
                int px = x % 4;
                int py = y % 4;
                Color pixelColor = baseStone;

                if (px == 0 || py == 0)
                {
                    pixelColor = mortarColor;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.5f + 99f, y * 0.5f + 99f);
                    if (noise > 0.6f) pixelColor = lightStone;
                    else if (noise < 0.4f) pixelColor = darkStone;
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateBaseTexture(string name)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = name + "_ProceduralTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        return texture;
    }
}
