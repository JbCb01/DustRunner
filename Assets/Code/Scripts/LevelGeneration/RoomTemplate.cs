using System.Collections.Generic;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    public enum DoorDirection
    {
        Up,     // Z+
        Down,   // Z-
        Left,   // X-
        Right   // X+
    }

    [System.Serializable]
    public class DoorDefinition
    {
        public Vector2Int Position; // Local Grid Coords (np. 0,0)
        public DoorDirection Direction; // W którą stronę wychodzimy?
    }

    public class RoomTemplate : MonoBehaviour
    {
        [Header("Settings")]
        public Vector2Int Size = new Vector2Int(2, 2);
        
        [Header("Connectivity")]
        [Tooltip("Define exact door positions and their exit direction.")]
        public List<DoorDefinition> Doors;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private Color _boundsColor = new Color(0, 1, 0, 0.4f);
        [SerializeField] private Color _doorColor = new Color(0, 0, 1, 0.8f);
        [SerializeField] private Color _directionColor = new Color(1, 1, 0, 1f);

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            float unitSize = 5f; 

            Gizmos.color = _boundsColor;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Rysowanie pudełka (Pivot w lewym dolnym rogu 0,0)
            Vector3 localCenter = new Vector3(Size.x * unitSize * 0.5f, 2f, Size.y * unitSize * 0.5f);
            Vector3 localSize = new Vector3(Size.x * unitSize, 4f, Size.y * unitSize);

            Gizmos.DrawCube(localCenter, localSize);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(localCenter, localSize);

            // Rysowanie Drzwi
            if (Doors != null)
            {
                foreach (var door in Doors)
                {
                    Vector3 tileCenter = new Vector3(
                        (door.Position.x + 0.5f) * unitSize, 
                        1f, 
                        (door.Position.y + 0.5f) * unitSize
                    );

                    Gizmos.color = _doorColor;
                    Gizmos.DrawSphere(tileCenter, 0.5f);

                    // Rysowanie strzałki kierunku
                    Vector3 dirVec = Vector3.zero;
                    switch (door.Direction)
                    {
                        case DoorDirection.Up: dirVec = Vector3.forward; break;
                        case DoorDirection.Down: dirVec = Vector3.back; break;
                        case DoorDirection.Left: dirVec = Vector3.left; break;
                        case DoorDirection.Right: dirVec = Vector3.right; break;
                    }

                    Gizmos.color = _directionColor;
                    Vector3 arrowEnd = tileCenter + dirVec * (unitSize * 0.8f);
                    Gizmos.DrawLine(tileCenter, arrowEnd);
                    Gizmos.DrawSphere(arrowEnd, 0.2f);
                }
            }

            Gizmos.matrix = oldMatrix;
        }
    }
}