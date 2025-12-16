using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Lootable : MonoBehaviour, IInteractable, IScrollable
{
    [System.Serializable]
    public class LootEntry
    {
        public string Name;      // Nazwa wyświetlana
        public string ItemID;    // ID dla systemu inventory
        public int Quantity;
        public bool IsEquipable; // Czy to broń (slot) czy zasób (bag)?
    }

    [Header("Container Contents")]
    public List<LootEntry> Contents = new List<LootEntry>();
    
    private int _selectedIndex = 0;

    // --- IInteractable ---
    public bool CanInteract => Contents.Count > 0;
    public string InteractionName => "Loot";

    public void Interact(Player player)
    {
        if (Contents.Count == 0) return;

        // Pobierz aktualnie wybrany przedmiot
        LootEntry item = Contents[_selectedIndex];
        
        Debug.Log($"[Lootable] Looting selected: {item.Name} x{item.Quantity}");

        // TODO: Tu wepniemy PlayerInventory.AddItem(...)
        // if (player.Inventory.TryAddItem(item)) { ... }

        // Na razie symulujemy sukces:
        Contents.RemoveAt(_selectedIndex);
        ClampIndex();
    }

    // --- IScrollable (Nawigacja) ---
    public void OnScroll(float scrollDelta)
    {
        if (Contents.Count <= 1) return;

        // Scroll Up (>0) przesuwa selekcję w górę listy (zmniejsza indeks)
        if (scrollDelta > 0) _selectedIndex--;
        else if (scrollDelta < 0) _selectedIndex++;

        ClampIndex();
    }

    // Generowanie tekstu dla UI (Podgląd zawartości)
    public string GetCurrentSelectionInfo()
    {
        if (Contents.Count == 0) return "[ Empty ]";

        StringBuilder sb = new StringBuilder();
        
        // Pętla renderująca listę w stylu retro
        for (int i = 0; i < Contents.Count; i++)
        {
            if (i == _selectedIndex)
            {
                // Wybrany element w nawiasach
                sb.AppendLine($"[ {Contents[i].Name} ({Contents[i].Quantity}) ]");
            }
            else
            {
                // Pozostałe elementy
                sb.AppendLine($"{Contents[i].Name} ({Contents[i].Quantity})");
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
            // Zapętlanie listy (Modulo)
            _selectedIndex = (_selectedIndex % Contents.Count + Contents.Count) % Contents.Count;
        }
    }
}