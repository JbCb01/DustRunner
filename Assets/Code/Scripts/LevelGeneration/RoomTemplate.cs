using UnityEngine;
using System.Collections.Generic;

namespace DustRunner.LevelGeneration
{
    [SelectionBase]
    public class RoomTemplate : MonoBehaviour
    {
        [Header("Grid Logic")]
        [Tooltip("Wymiary pokoju (X=Szerokość, Z=Długość/Głębokość).")]
        public Vector3Int GridSize = new Vector3Int(1, 1, 1);
        
        public List<RoomSocket> Sockets = new List<RoomSocket>();

        [Header("Editor")]
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private Color GizmoColor = new Color(0, 1, 0, 0.4f);
        private const float GRID_SCALE = 5.0f;

        public static Vector3Int RotateVectorInt(Vector3Int v, int angle90Steps)
        {
            int steps = angle90Steps % 4;
            if (steps < 0) steps += 4;
            return steps switch
            {
                0 => v,
                1 => new Vector3Int(v.z, v.y, -v.x),
                2 => new Vector3Int(-v.x, v.y, -v.z),
                3 => new Vector3Int(-v.z, v.y, v.x),
                _ => v
            };
        }

        public static Vector3Int RotateDirection(Vector3Int dir, int angle90Steps) => RotateVectorInt(dir, angle90Steps);

        public List<Vector3Int> GetOccupiedCells(int rotationSteps)
        {
            List<Vector3Int> cells = new List<Vector3Int>();
            for (int x = 0; x < GridSize.x; x++)
            {
                for (int y = 0; y < GridSize.y; y++)
                {
                    for (int z = 0; z < GridSize.z; z++)
                    {
                        Vector3Int localPos = new Vector3Int(x, y, z);
                        cells.Add(RotateVectorInt(localPos, rotationSteps));
                    }
                }
            }
            return cells;
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            
            Vector3 size = new Vector3(GridSize.x, GridSize.y, GridSize.z) * GRID_SCALE;
            Vector3 center = size * 0.5f;

            Gizmos.color = GizmoColor;
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 1f);
            Gizmos.DrawWireCube(center, size);

            foreach (var socket in Sockets)
            {
                Vector3 cellCenter = (Vector3)socket.LocalPosition * GRID_SCALE + (Vector3.one * GRID_SCALE * 0.5f);
                Vector3 dirVec = (Vector3)socket.GetDirectionVector();
                Vector3 socketPos = cellCenter + (dirVec * GRID_SCALE * 0.5f);

                Gizmos.color = socket.Type == SocketType.Industrial ? Color.yellow : Color.cyan;
                Gizmos.DrawSphere(socketPos, 0.4f);
                Gizmos.DrawLine(socketPos, socketPos + dirVec * 2.0f);
            }
        }
    }
}