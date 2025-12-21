using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class LootEntry
{
    public ItemData Data;
    public int Amount = 1;

    // Konstruktor dla wygody w kodzie
    public LootEntry(ItemData data, int amount)
    {
        Data = data;
        Amount = amount;
    }
}

public class Lootable : MonoBehaviour, IInteractable, IScrollable
{
    [Header("Container Contents")]
    public List<LootEntry> Contents = new();
    
    private int _selectedIndex = 0;

    public bool CanInteract => Contents.Count > 0;
    public void Interact(Player player)
    {
        if (Contents.Count == 0) return;

        LootEntry entry = Contents[_selectedIndex];
        player.Inventory.HandleLoot(entry.Data, entry.Amount);
        Contents.RemoveAt(_selectedIndex);
        ClampIndex();
    }

    public void OnScroll(float scrollDelta)
    {
        if (Contents.Count <= 1) return;

        if (scrollDelta > 0) _selectedIndex--;
        else if (scrollDelta < 0) _selectedIndex++;

        ClampIndex();
    }

    public void AddItem(ItemData item, int amount = 1)
    {
        if (item is PickableData)
        {
            foreach (var entry in Contents)
            {
                if (entry.Data == item)
                {
                    entry.Amount += amount;
                    Debug.Log($"[Lootable] Merged stack. New amount: {entry.Amount}");
                    return;
                }
            }
        }
        Contents.Add(new LootEntry(item, amount));
        Debug.Log($"[Lootable] Added new item: {item.DisplayName}");
    }

    public string GetCurrentSelectionInfo()
    {
        if (Contents.Count == 0) return "[ -- Empty -- ]";

        StringBuilder sb = new StringBuilder();
        
        for (int i = 0; i < Contents.Count; i++)
        {
            LootEntry entry = Contents[i];
            
            string line = $"{entry.Data.DisplayName} (x{entry.Amount})";

            if (i == _selectedIndex)
            {
                sb.AppendLine($"[ {line} ]"); // Zaznaczony element
            }
            else
            {
                sb.AppendLine($"  {line}");   // Niezaznaczony
            }
        }
        return sb.ToString();
    }

    private void ClampIndex()
    {
        if (Contents.Count == 0) 
        {
            _selectedIndex = 0;
        }
        else
        {
            _selectedIndex = (_selectedIndex % Contents.Count + Contents.Count) % Contents.Count;
        }
    }
}