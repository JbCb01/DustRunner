using UnityEngine;
using TMPro;
using System.Text;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    public Player Player;
    public Crosshair Crosshair;
    public TextMeshProUGUI InteractionListText;
    public TextMeshProUGUI AmmoCounterText;
    public TextMeshProUGUI EquipmentListText;

    [Header("Settings")]
    public float NormalRadius = 2f;
    public float InteractionRadius = 6f; // Celownik powiększa się/zmienia przy interakcji
    public Color NormalColor = Color.white;
    public Color InteractionColor = new Color(1f, 0.5f, 0f); // Np. Pomarańczowy/Amber

    private float _currentRadius;
    private float _targetRadius;

    public void Initialize(Player player)
    {
        Player = player;
        if (InteractionListText) InteractionListText.text = "";
        if (AmmoCounterText) AmmoCounterText.text = "";
        if (EquipmentListText) EquipmentListText.text = "";
    }

    private void Update()
    {
        if (Player == null) return;

        UpdateInteractionUI();   // Środek (Celownik + Loot)
        UpdateEquipmentSlotUI(); // Prawy Dół (Lista broni)
        UpdateAmmoCounterUI();   // Lewy Dół (Ammo)
        
        UpdateCrosshairVisuals();
    }

    private void UpdateInteractionUI()
    {
        if (Player.Interaction == null) return;

        if (Player.Interaction.CurrentScrollable != null)
        {
            string lootText = Player.Interaction.CurrentScrollable.GetCurrentSelectionInfo();
            SetText(InteractionListText, lootText);
            SetCrosshairState(true);
        }
        else if (Player.Interaction.CurrentInteractable != null && Player.Interaction.CurrentInteractable.CanInteract)
        {
            SetText(InteractionListText, ""); 
            SetCrosshairState(true);
        }
        else
        {
            SetText(InteractionListText, "");
            SetCrosshairState(false);
        }
    }

    private void UpdateAmmoCounterUI()
    {
        if (AmmoCounterText == null || Player.Inventory == null) return;

        var currentItem = Player.Inventory.CurrentEquippedItem;

        if (currentItem != null)
        {
            string status = currentItem.GetAmmoStatus();

            if (!string.IsNullOrEmpty(status))
            {
                AmmoCounterText.text = status;
                return;
            }
        }

        AmmoCounterText.text = "";
    }

    private void UpdateEquipmentSlotUI()
    {
        if (EquipmentListText == null || Player.Inventory == null) return;

        var slots = Player.Inventory.GetSlots();
        int currentIndex = Player.Inventory.GetCurrentSlotIndex();

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < slots.Length; i++)
        {
            string itemName = (slots[i] != null) ? slots[i].ItemName : "---";
            
            if (i == currentIndex)
            {
                sb.AppendLine($"> [ {itemName} ]");
            }
            else
            {
                sb.AppendLine($"  {itemName}");
            }
        }

        EquipmentListText.text = sb.ToString();
    }

    private void SetInteractionText(string text)
    {
        if (InteractionListText != null)
        {
            InteractionListText.text = text;
        }
    }

    private void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    private void SetCrosshairState(bool isInteraction)
    {
        if (Crosshair == null) return;

        if (isInteraction)
        {
            _targetRadius = InteractionRadius;
            Crosshair.color = Color.Lerp(Crosshair.color, InteractionColor, Time.deltaTime * 15f);
            Crosshair.SetFilled(true);
        }
        else
        {
            _targetRadius = NormalRadius;
            Crosshair.color = Color.Lerp(Crosshair.color, NormalColor, Time.deltaTime * 10f);
            Crosshair.SetFilled(false);
        }
    }

    private void UpdateCrosshairVisuals()
    {
        if (Crosshair == null) return;
        _currentRadius = Mathf.Lerp(_currentRadius, _targetRadius, Time.deltaTime * 20f);
        Crosshair.SetRadius(_currentRadius, 2f);
    }

    
}