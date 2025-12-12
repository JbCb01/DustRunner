using System.Collections.Generic;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    public enum DoorDirection { Up, Down, Left, Right }

    [System.Serializable]
    public class DoorDefinition
    {
        public Vector2Int Position;
        public DoorDirection Direction;
        
        [Range(-1, 1)] public int LayerOffset = 0; 
    }

    public class RoomTemplate : MonoBehaviour
    {
        [Header("Settings")]
        public Vector2Int Size = new Vector2Int(2, 2);
        
        [Header("Verticality")]
        public bool OccupiesLayerBelow = false;
        public bool OccupiesLayerAbove = false;

        [Header("Connectivity")]
        public List<DoorDefinition> Doors;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private float _layerHeight = 4f; // Odstęp wizualny dla gizmo
        [SerializeField] private Color _boundsColor = new Color(0, 1, 0, 0.4f);
        [SerializeField] private Color _phantomColor = new Color(0, 1, 1, 0.2f);
        [SerializeField] private Color _doorColor = new Color(0, 0, 1, 0.8f);

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            float unitSize = 5f; 
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // 1. Rysuj Główną Warstwę (0)
            DrawLayerGizmo(0, unitSize, _boundsColor);

            // 2. Rysuj Warstwy Dodatkowe (Jako "duchy")
            if (OccupiesLayerAbove) DrawLayerGizmo(1, unitSize, _phantomColor);
            if (OccupiesLayerBelow) DrawLayerGizmo(-1, unitSize, _phantomColor);

            Gizmos.matrix = oldMatrix;
        }

        private void DrawLayerGizmo(int layerIndex, float unitSize, Color color)
        {
            float yOffset = layerIndex * _layerHeight;
            Vector3 center = new Vector3(Size.x * unitSize * 0.5f, 2f + yOffset, Size.y * unitSize * 0.5f);
            Vector3 size = new Vector3(Size.x * unitSize, 4f, Size.y * unitSize);

            Gizmos.color = color;
            Gizmos.DrawCube(center, size);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center, size);

            // Rysuj drzwi należące do tej warstwy
            if (Doors != null)
            {
                foreach (var door in Doors)
                {
                    // Rysuj tylko jeśli offset drzwi pasuje do rysowanej warstwy
                    if (door.LayerOffset == layerIndex)
                    {
                        Vector3 tileCenter = new Vector3(
                            (door.Position.x + 0.5f) * unitSize, 
                            1f + yOffset, 
                            (door.Position.y + 0.5f) * unitSize
                        );

                        Gizmos.color = _doorColor;
                        Gizmos.DrawSphere(tileCenter, 0.5f);
                        
                        Vector3 dirVec = Vector3.zero;
                        switch (door.Direction) {
                            case DoorDirection.Up: dirVec = Vector3.forward; break;
                            case DoorDirection.Down: dirVec = Vector3.back; break;
                            case DoorDirection.Left: dirVec = Vector3.left; break;
                            case DoorDirection.Right: dirVec = Vector3.right; break;
                        }
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(tileCenter, tileCenter + dirVec * (unitSize * 0.8f));
                        Gizmos.DrawSphere(tileCenter + dirVec * (unitSize * 0.8f), 0.2f);
                    }
                }
            }
        }
    }
}