using UnityEngine;

public enum ItemKind
{
    Axe,
    Block
}

[System.Serializable]
public class ItemData
{
    public string DisplayName;
    public ItemKind Kind;
    public BlockType BlockType;
    public Color IconColor;

    public ItemData(string displayName, ItemKind kind, BlockType blockType, Color iconColor)
    {
        DisplayName = displayName;
        Kind = kind;
        BlockType = blockType;
        IconColor = iconColor;
    }
}
