using UnityEngine;

public abstract class ItemData : ScriptableObject
{
    public string ID;
    public string DisplayName;
}

[CreateAssetMenu(menuName = "DustRunner/Items/EquipableData", fileName = "NewEquipableData")]
public class EquipableData : ItemData
{
    public GameObject WorldPrefab;
}

[CreateAssetMenu(menuName = "DustRunner/Items/PickableData", fileName = "NewPickableData")]
public class PickableData : ItemData
{
}