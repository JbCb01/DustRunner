using UnityEngine;
using System;
using System.Collections.Generic;

namespace DustRunner.LevelGeneration
{
    [CreateAssetMenu(fileName = "NewLevelConfig", menuName = "DustRunner/Level Generation/Level Config")]
    public class LevelConfiguration : ScriptableObject
    {
        [Header("Rooms and Corridors")]
        public RoomTemplate StartRoomPrefab;
        public RoomTemplate EndingRoomPrefab;
        public List<RoomTemplate> RoomPrefabs;
        public CorridorTileSet CorridorTiles;

        [Header("Generation Settings")]
        [Range(0f, 1f)] public float CorridorChance = 0.4f;
        [Range(0f, 1f)] public float CapRoomChance = 0.5f;
        public int MinRoomCount = 10;
        public int MaxStepsSafety = 100;
        public float GridScale = 5.0f;
        public int Seed = 0;
        public bool UseRandomSeed = true;

        [Header("Shortcuts")]
        public bool EnableShortcuts = true;
        public int MaxShortcuts = 3;
        public int MaxShortcutLength = 10;
    }
    
    public enum SocketDirection { North, South, East, West, Up, Down }
    public enum SocketType { Standard, Industrial, Security, Vertical }
    public enum NodeType { Empty, Room, Corridor }

    [Serializable]
    public class RoomSocket
    {
        public Vector3Int LocalPosition;
        public SocketDirection Direction;
        public SocketType Type;

        [Header("Secret / Unique Room Data")]
        public RoomTemplate UniqueRoomPrefab;
        public float UniqueRoomChance = 0.1f;
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

    [Serializable]
    public struct CorridorTileSet
    {
        [Header("Modules (Center Pivot)")]
        public GameObject Straight;   // I-Shape
        public GameObject Corner;     // L-Shape
        public GameObject TJunction;  // T-Shape
        public GameObject Cross;      // X-Shape
        public GameObject DeadEnd;    // Opcjonalnie, jeśli algorytm zostawi ślepą uliczkę
        [Header("Utility")]
        public GameObject DoorBlocker;
    }
}