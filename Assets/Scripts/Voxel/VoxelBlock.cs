using UnityEngine;

public enum BlockType
{
    Air,
    Grass,
    Dirt,
    Stone,
    Wood,
    Sand,
    Leaves,
    Water
}

[System.Serializable]
public class VoxelBlock
{
    public BlockType Type { get; private set; }
    public Vector3Int GridPosition { get; private set; }
    public GameObject BlockObject { get; private set; }

    public VoxelBlock(BlockType type, Vector3Int gridPosition, GameObject blockObject)
    {
        Type = type;
        GridPosition = gridPosition;
        BlockObject = blockObject;
    }
}
