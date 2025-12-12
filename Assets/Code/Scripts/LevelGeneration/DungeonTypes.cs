using System.Collections.Generic;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    // --- CONFIG DATA ---

    public class FixedRoomData
    {
        public RoomTemplate Prefab;
        public Vector2Int Position;
        public int Rotation;
        public bool SkipVisuals;
        public int OriginLayerIndex;
    }

    // --- GRAPH DATA ---

    public class DoorNode
    {
        public RoomInstance ParentRoom;
        public Vector2Int GridPos;
        public Vector2Int ExitDirection;
        public string NodeID;

        public DoorNode(RoomInstance parent, Vector2Int pos, Vector2Int dir)
        {
            ParentRoom = parent;
            GridPos = pos;
            ExitDirection = dir;
            NodeID = System.Guid.NewGuid().ToString();
        }

        public Vector2Int GetEntryTile() => GridPos + ExitDirection;
    }

    public class DoorEdge
    {
        public DoorNode NodeA;
        public DoorNode NodeB;
        public float Distance;

        public DoorEdge(DoorNode a, DoorNode b)
        {
            NodeA = a;
            NodeB = b;
            Distance = Vector2Int.Distance(a.GridPos, b.GridPos);
        }
        
        public override bool Equals(object obj) => obj is DoorEdge other && ((NodeA == other.NodeA && NodeB == other.NodeB) || (NodeA == other.NodeB && NodeB == other.NodeA));
        public override int GetHashCode() => NodeA.GetHashCode() ^ NodeB.GetHashCode();
    }

    // --- INSTANCE DATA ---

    public class RoomInstance
    {
        public RoomTemplate PrefabSource;
        public Vector2Int GridPos;
        public int RotationIndex;
        public bool IsGhost;
        public int BaseLayerIndex;
        
        public List<DoorNode> Nodes = new List<DoorNode>();

        public RoomInstance(RoomTemplate prefab, Vector2Int pos, int rotation, bool isGhost, int baseLayer)
        {
            PrefabSource = prefab;
            GridPos = pos;
            RotationIndex = rotation;
            IsGhost = isGhost;
            BaseLayerIndex = baseLayer;
        }

        public Vector2Int GetRotatedSize()
        {
            bool isRotated = RotationIndex == 1 || RotationIndex == 3;
            return isRotated ? new Vector2Int(PrefabSource.Size.y, PrefabSource.Size.x) : PrefabSource.Size;
        }

        public Vector3 GetWorldCenter(float unitSize)
        {
            Vector2Int size = GetRotatedSize();
            float x = (GridPos.x + size.x * 0.5f) * unitSize;
            float z = (GridPos.y + size.y * 0.5f) * unitSize;
            return new Vector3(x, 0, z);
        }

        public void CalculateDoorNodes(int targetGeneratorLayer)
        {
            Nodes.Clear();
            if (PrefabSource.Doors == null) return;

            Vector2Int originalSize = PrefabSource.Size;

            foreach (var doorDef in PrefabSource.Doors)
            {
                int doorAbsoluteLayer = BaseLayerIndex + doorDef.LayerOffset;
                if (doorAbsoluteLayer != targetGeneratorLayer) continue;

                Vector2Int localPos = doorDef.Position;
                Vector2Int rotatedOffset = Vector2Int.zero;
                Vector2Int rotatedDir = Vector2Int.zero;

                // Simple rotation logic matrix
                switch (RotationIndex)
                {
                    case 0: rotatedOffset = localPos; break;
                    case 1: rotatedOffset = new Vector2Int(localPos.y, originalSize.x - 1 - localPos.x); break;
                    case 2: rotatedOffset = new Vector2Int(originalSize.x - 1 - localPos.x, originalSize.y - 1 - localPos.y); break;
                    case 3: rotatedOffset = new Vector2Int(originalSize.y - 1 - localPos.y, localPos.x); break;
                }

                Vector2Int baseDir = GetDirVector(doorDef.Direction);
                switch (RotationIndex)
                {
                    case 0: rotatedDir = baseDir; break;
                    case 1: rotatedDir = new Vector2Int(baseDir.y, -baseDir.x); break;
                    case 2: rotatedDir = -baseDir; break;
                    case 3: rotatedDir = new Vector2Int(-baseDir.y, baseDir.x); break;
                }

                Nodes.Add(new DoorNode(this, GridPos + rotatedOffset, rotatedDir));
            }
        }

        private Vector2Int GetDirVector(DoorDirection dir)
        {
            switch (dir) {
                case DoorDirection.Up: return Vector2Int.up;
                case DoorDirection.Down: return Vector2Int.down;
                case DoorDirection.Left: return Vector2Int.left;
                case DoorDirection.Right: return Vector2Int.right;
                default: return Vector2Int.up;
            }
        }
    }
}