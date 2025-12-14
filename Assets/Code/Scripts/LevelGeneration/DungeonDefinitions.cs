using UnityEngine;
using System;

namespace DustRunner.LevelGeneration
{
    public enum SocketDirection { North, South, East, West, Up, Down }
    public enum SocketType { Standard, Industrial, Security, Vertical }
    
    // Rozróżniamy, czym jest komórka w gridzie
    public enum NodeType { Empty, Room, Corridor }

    [Serializable]
    public class RoomSocket
    {
        public Vector3Int LocalPosition;
        public SocketDirection Direction;
        public SocketType Type;

        public Vector3Int GetDirectionVector()
        {
            return Direction switch
            {
                SocketDirection.North => new Vector3Int(0, 0, 1),
                SocketDirection.South => new Vector3Int(0, 0, -1),
                SocketDirection.East => new Vector3Int(1, 0, 0),
                SocketDirection.West => new Vector3Int(-1, 0, 0),
                SocketDirection.Up => new Vector3Int(0, 1, 0),
                SocketDirection.Down => new Vector3Int(0, -1, 0),
                _ => Vector3Int.zero
            };
        }
        
        // Helper do odwracania kierunku (ważne przy łączeniu)
        public static SocketDirection GetOpposite(SocketDirection dir)
        {
            return dir switch
            {
                SocketDirection.North => SocketDirection.South,
                SocketDirection.South => SocketDirection.North,
                SocketDirection.East => SocketDirection.West,
                SocketDirection.West => SocketDirection.East,
                SocketDirection.Up => SocketDirection.Down,
                SocketDirection.Down => SocketDirection.Up,
                _ => dir
            };
        }
    }

    // Struktura do trzymania prefabów korytarzy dla Autotilingu
    [Serializable]
    public struct CorridorTileSet
    {
        [Header("Modules (Center Pivot)")]
        public GameObject Straight;   // I-Shape
        public GameObject Corner;     // L-Shape
        public GameObject TJunction;  // T-Shape
        public GameObject Cross;      // X-Shape
        public GameObject DeadEnd;    // Opcjonalnie, jeśli algorytm zostawi ślepą uliczkę
        public GameObject VerticalShaft; // Opcjonalnie do drabin
        [Header("Utility")]
        public GameObject DoorBlocker;
    }
}