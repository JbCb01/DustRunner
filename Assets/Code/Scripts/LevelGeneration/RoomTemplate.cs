using System.Collections.Generic;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    public class RoomTemplate : MonoBehaviour
    {
        [Header("Settings")]
        public Vector2Int Size = new Vector2Int(2, 2);
        
        [Tooltip("Local grid coordinates for doors. (0,0) is bottom-left corner.")]
        public List<Vector2Int> DoorPositions;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private Color _boundsColor = new Color(0, 1, 0, 0.4f);
        [SerializeField] private Color _doorColor = new Color(0, 0, 1, 0.8f);

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            float unitSize = 5f; // Must match generator unit size

            // 1. Draw Bounds
            Gizmos.color = _boundsColor;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 localCenter = new Vector3(Size.x * unitSize * 0.5f, 2f, Size.y * unitSize * 0.5f);
            Vector3 localSize = new Vector3(Size.x * unitSize, 4f, Size.y * unitSize);

            Gizmos.DrawCube(localCenter, localSize);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(localCenter, localSize);

            // 2. Draw Doors
            if (DoorPositions != null)
            {
                Gizmos.color = _doorColor;
                foreach (var door in DoorPositions)
                {
                    // Calculate center of the grid cell for the door
                    Vector3 doorPos = new Vector3(
                        (door.x + 0.5f) * unitSize, 
                        1f, 
                        (door.y + 0.5f) * unitSize
                    );
                    Gizmos.DrawSphere(doorPos, 1f);
                }
            }

            Gizmos.matrix = oldMatrix;
        }
    }
}