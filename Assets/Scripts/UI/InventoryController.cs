using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages player inventory item counts for voxel blocks and tools.
/// Automatically notifies listeners (such as HotbarController) when quantities change.
/// </summary>
public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance { get; private set; }

    public event Action OnInventoryChanged;

    private readonly Dictionary<BlockType, int> _blockQuantities = new Dictionary<BlockType, int>
    {
        { BlockType.Grass, 0 },
        { BlockType.Dirt, 0 },
        { BlockType.Stone, 0 },
        { BlockType.Wood, 0 },
        { BlockType.Sand, 0 },
        { BlockType.Leaves, 0 }
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    public int GetItemCount(BlockType type)
    {
        _blockQuantities.TryGetValue(type, out int count);
        return count;
    }

    public bool HasItem(BlockType type, int amount = 1)
    {
        return GetItemCount(type) >= amount;
    }

    public void AddItem(BlockType type, int amount = 1)
    {
        if (type == BlockType.Air || type == BlockType.Water) return;

        if (_blockQuantities.ContainsKey(type))
        {
            _blockQuantities[type] += amount;
        }
        else
        {
            _blockQuantities[type] = amount;
        }

        OnInventoryChanged?.Invoke();
    }

    public bool RemoveItem(BlockType type, int amount = 1)
    {
        if (GetItemCount(type) < amount)
        {
            return false;
        }

        _blockQuantities[type] -= amount;
        OnInventoryChanged?.Invoke();
        return true;
    }
}
