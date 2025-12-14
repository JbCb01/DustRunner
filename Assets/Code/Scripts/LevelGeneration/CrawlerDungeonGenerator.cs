using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DustRunner.LevelGeneration
{
    public class CrawlerDungeonGenerator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private RoomTemplate startRoomPrefab;
        [SerializeField] private List<RoomTemplate> roomPrefabs;
        [SerializeField] private CorridorTileSet corridorTiles;

        [Header("Parameters")]
        [SerializeField] [Range(0f, 1f)] private float corridorChance = 0.5f;
        [SerializeField] private int targetStepCount = 20;
        [SerializeField] private float gridScale = 5.0f; // Scale 5.0 from current repo
        [SerializeField] private bool generateOnStart = false;

        // --- Structures ---
        private class DungeonNode
        {
            public NodeType Type;
            public GameObject Instance; 
            public RoomTemplate RoomData;
            public int RotationSteps;
            public List<(Vector3Int pos, Vector3Int dir)> ActiveSocketsWorld = new List<(Vector3Int, Vector3Int)>();
        }

        private class PendingExit
        {
            public Vector3Int SourceGridPos;
            public Vector3Int Direction;
            public SocketType RequiredType;
        }

        // --- State ---
        private Dictionary<Vector3Int, DungeonNode> logicalGrid = new Dictionary<Vector3Int, DungeonNode>();
        private List<DungeonNode> allNodes = new List<DungeonNode>(); 
        private List<PendingExit> pendingExits = new List<PendingExit>();
        private List<(Vector3 start, Vector3 end, Color color)> debugPaths = new List<(Vector3, Vector3, Color)>();

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("Generate Dungeon")]
        public void Generate()
        {
            Cleanup();
            if (startRoomPrefab == null) return;

            PlaceRoom(startRoomPrefab, Vector3Int.zero, 0);

            int steps = 0;
            int maxAttempts = targetStepCount * 20;
            
            while (steps < targetStepCount && pendingExits.Count > 0 && maxAttempts > 0)
            {
                maxAttempts--;
                if (ProcessNextStep()) steps++;
            }

            ResolveCorridorVisuals();
            SealDungeon();

            Debug.Log($"[Generator] Generated. Steps: {steps}, Nodes: {allNodes.Count}");
        }

        [ContextMenu("Cleanup")]
        public void Cleanup()
        {
            logicalGrid.Clear();
            allNodes.Clear();
            pendingExits.Clear();
            debugPaths.Clear();
            
            var children = new List<GameObject>();
            foreach (Transform child in transform) children.Add(child.gameObject);
            children.ForEach(c => DestroyImmediate(c));
        }

        private bool ProcessNextStep()
        {
            if (pendingExits.Count == 0) return false;

            int index = Random.Range(0, pendingExits.Count);
            PendingExit exit = pendingExits[index];
            Vector3Int targetCell = exit.SourceGridPos + exit.Direction;

            if (logicalGrid.ContainsKey(targetCell))
            {
                pendingExits.RemoveAt(index);
                return false; 
            }

            bool placed = false;
            
            if (Random.value < corridorChance)
                placed = TryPlaceLogicalCorridor(targetCell, exit);
            
            if (!placed)
            {
                placed = TryPlaceRoom(targetCell, exit);
                if (!placed) placed = TryPlaceLogicalCorridor(targetCell, exit); // Fallback
            }

            if (placed)
            {
                pendingExits.RemoveAt(index);
                return true;
            }

            return false;
        }

        // --- PLACEMENT ---

        private bool TryPlaceLogicalCorridor(Vector3Int targetPos, PendingExit entryInfo)
        {
            if (!IsValidConfiguration(null, targetPos, 0, NodeType.Corridor)) return false;

            DungeonNode node = new DungeonNode { Type = NodeType.Corridor, RotationSteps = 0 };
            RegisterNode(node, targetPos, new List<Vector3Int>{Vector3Int.zero});
            
            AddDebugPath(entryInfo.SourceGridPos, targetPos, Color.green);

            Vector3Int[] dirs = { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right };
            foreach (var d in dirs)
            {
                if (d == -entryInfo.Direction) continue;
                node.ActiveSocketsWorld.Add((targetPos, d));

                if (!logicalGrid.ContainsKey(targetPos + d))
                {
                    pendingExits.Add(new PendingExit {
                        SourceGridPos = targetPos, Direction = d, RequiredType = entryInfo.RequiredType
                    });
                }
            }
            return true;
        }

        private bool TryPlaceRoom(Vector3Int entryCell, PendingExit entryInfo)
        {
            var candidates = roomPrefabs.OrderBy(x => Random.value).ToList();

            foreach (var room in candidates)
            {
                foreach (var socket in room.Sockets)
                {
                    if (socket.Type != entryInfo.RequiredType) continue;

                    int rotationSteps = CalculateRotationSteps(-entryInfo.Direction, socket.GetDirectionVector());
                    if (rotationSteps == -1) continue;

                    Vector3Int rotatedSocketLocal = RoomTemplate.RotateVectorInt(socket.LocalPosition, rotationSteps);
                    Vector3Int potentialOrigin = entryCell - rotatedSocketLocal;

                    if (IsValidConfiguration(room, potentialOrigin, rotationSteps, NodeType.Room))
                    {
                        PlaceRoom(room, potentialOrigin, rotationSteps);
                        AddDebugPath(entryInfo.SourceGridPos, entryCell, Color.white);
                        return true;
                    }
                }
            }
            return false;
        }

        private void PlaceRoom(RoomTemplate room, Vector3Int origin, int rotSteps)
        {
            // 1. Oblicz logiczne komórki, które zajmie pokój (względem pivota)
            List<Vector3Int> occupiedOffsets = room.GetOccupiedCells(rotSteps);
            
            // 2. Znajdź "Minimale Koordynaty" (Dolny-Lewy róg bounding boxa w gridzie)
            // To odpowiada polu `GridPos` ze starego generatora (DungeonGenerator.cs)
            Vector3Int minOffset = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            foreach (var offset in occupiedOffsets)
            {
                minOffset = Vector3Int.Min(minOffset, offset);
            }
            Vector3Int visualGridPos = origin + minOffset;

            // 3. Oblicz Offset Wizualny (Legacy Logic z DungeonGenerator.cs)
            // W starym kodzie offset zależy od obrotu i wymiarów (Size.x, Size.y).
            // Tutaj mapujemy GridSize.x -> OldSize.x, GridSize.z -> OldSize.y (Depth)
            Vector3 offsetVector = Vector3.zero;
            
            // Konwersja rotacji (0,1,2,3) na offsety Unity
            switch (rotSteps)
            {
                case 0: 
                    offsetVector = Vector3.zero; 
                    break;
                case 1: // 90 deg
                    offsetVector = new Vector3(0, 0, room.GridSize.x * gridScale); 
                    break;
                case 2: // 180 deg
                    offsetVector = new Vector3(room.GridSize.x * gridScale, 0, room.GridSize.z * gridScale); 
                    break;
                case 3: // 270 deg
                    offsetVector = new Vector3(room.GridSize.z * gridScale, 0, 0); 
                    break;
            }

            // 4. Instancjacja
            Vector3 worldPos = (Vector3)visualGridPos * gridScale;
            Quaternion rotation = Quaternion.Euler(0, rotSteps * 90, 0);
            
            // Finalna pozycja = Pozycja Gridu (Dolny Róg) + Korekta Obrotu
            Vector3 finalPos = worldPos + offsetVector;

            GameObject go = Instantiate(room.gameObject, finalPos, rotation, transform);
            go.name = $"{room.name}_{origin}";

            DungeonNode node = new DungeonNode
            {
                Type = NodeType.Room,
                Instance = go,
                RoomData = room,
                RotationSteps = rotSteps
            };

            // Rejestracja socketów
            foreach (var s in room.Sockets)
            {
                Vector3Int sPos = origin + RoomTemplate.RotateVectorInt(s.LocalPosition, rotSteps);
                Vector3Int sDir = RoomTemplate.RotateDirection(s.GetDirectionVector(), rotSteps);
                node.ActiveSocketsWorld.Add((sPos, sDir));
                
                Vector3Int neighbor = sPos + sDir;
                if (!logicalGrid.ContainsKey(neighbor))
                {
                    pendingExits.Add(new PendingExit { SourceGridPos = sPos, Direction = sDir, RequiredType = s.Type });
                }
            }

            RegisterNode(node, origin, occupiedOffsets);
        }

        private void RegisterNode(DungeonNode node, Vector3Int origin, List<Vector3Int> occupiedOffsets)
        {
            allNodes.Add(node);
            foreach (var offset in occupiedOffsets)
            {
                logicalGrid[origin + offset] = node;
            }
        }

        // --- VALIDATION (Keep Current Logic) ---

        private bool IsValidConfiguration(RoomTemplate room, Vector3Int origin, int rotSteps, NodeType type)
        {
            List<Vector3Int> occupiedCells;
            if (type == NodeType.Corridor) occupiedCells = new List<Vector3Int> { Vector3Int.zero };
            else occupiedCells = room.GetOccupiedCells(rotSteps);

            // 1. Hard Collision
            foreach (var localCell in occupiedCells)
            {
                if (logicalGrid.ContainsKey(origin + localCell)) return false;
            }

            // 2. Check connections (Neighbor -> Me)
            foreach (var localCell in occupiedCells)
            {
                Vector3Int worldCell = origin + localCell;
                Vector3Int[] neighbors = { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };

                foreach (var dir in neighbors)
                {
                    Vector3Int neighborPos = worldCell + dir;
                    if (logicalGrid.TryGetValue(neighborPos, out DungeonNode neighborNode))
                    {
                        if (HasSocketFacing(neighborNode, neighborPos, -dir))
                        {
                            bool doWeConnect = false;
                            if (type == NodeType.Corridor) doWeConnect = true; 
                            else doWeConnect = RoomHasSocketAt(room, localCell, dir, rotSteps);

                            if (!doWeConnect) return false; // Blocked door!
                        }
                    }
                }
            }

            // 3. Check outgoing (Me -> Neighbor)
            if (type == NodeType.Room)
            {
                foreach (var socket in room.Sockets)
                {
                    Vector3Int sPos = origin + RoomTemplate.RotateVectorInt(socket.LocalPosition, rotSteps);
                    Vector3Int sDir = RoomTemplate.RotateDirection(socket.GetDirectionVector(), rotSteps);
                    Vector3Int target = sPos + sDir;

                    if (logicalGrid.TryGetValue(target, out DungeonNode targetNode))
                    {
                        if (!HasSocketFacing(targetNode, target, -sDir)) return false; // Wall hit!
                    }
                }
            }

            return true;
        }

        // --- SEALING (Keep Fixed Logic) ---

        private void SealDungeon()
        {
            if (corridorTiles.DoorBlocker == null) return;
            HashSet<Vector3Int> sealedPositions = new HashSet<Vector3Int>();

            foreach (var node in allNodes)
            {
                if (node.Type != NodeType.Room) continue;

                foreach (var socket in node.ActiveSocketsWorld)
                {
                    Vector3Int targetCell = socket.pos + socket.dir;
                    if (!logicalGrid.ContainsKey(targetCell))
                    {
                        Vector3 center = (Vector3)socket.pos * gridScale + (Vector3.one * gridScale * 0.5f);
                        Vector3 blockerPos = center + ((Vector3)socket.dir * (gridScale * 0.5f));
                        
                        Vector3Int discreteBlockerPos = Vector3Int.RoundToInt(blockerPos * 100); 

                        if (!sealedPositions.Contains(discreteBlockerPos))
                        {
                            Quaternion rot = Quaternion.LookRotation((Vector3)socket.dir);
                            var go = Instantiate(corridorTiles.DoorBlocker, blockerPos, rot, transform);
                            go.name = "Seal";
                            sealedPositions.Add(discreteBlockerPos);
                        }
                    }
                }
            }
        }

        // --- HELPERS ---

        private bool HasSocketFacing(DungeonNode node, Vector3Int nodeWorldPos, Vector3Int worldLookDir)
        {
            if (node.Type == NodeType.Corridor) return true; 
            foreach (var s in node.ActiveSocketsWorld)
            {
                if (s.pos == nodeWorldPos && s.dir == worldLookDir) return true;
            }
            return false;
        }

        private bool RoomHasSocketAt(RoomTemplate room, Vector3Int localCell, Vector3Int requiredWorldDir, int rotSteps)
        {
            foreach (var s in room.Sockets)
            {
                Vector3Int rotPos = RoomTemplate.RotateVectorInt(s.LocalPosition, rotSteps);
                Vector3Int rotDir = RoomTemplate.RotateDirection(s.GetDirectionVector(), rotSteps);
                if (rotPos == localCell && rotDir == requiredWorldDir) return true;
            }
            return false;
        }

        private int CalculateRotationSteps(Vector3Int targetDir, Vector3Int currentDir)
        {
            for (int k = 0; k < 4; k++)
            {
                if (RoomTemplate.RotateDirection(currentDir, k) == targetDir) return k;
            }
            return -1;
        }

        private void ResolveCorridorVisuals()
        {
            Vector3 centerOffset = new Vector3(gridScale * 0.5f, 0, gridScale * 0.5f);
            
            foreach (var node in allNodes)
            {
                if (node.Type != NodeType.Corridor) continue;
                
                Vector3Int pos = Vector3Int.zero;
                bool found = false;
                foreach(var kvp in logicalGrid) { if(kvp.Value == node) { pos = kvp.Key; found = true; break; } }
                if(!found) continue;

                bool n = HasConnection(pos, Vector3Int.forward);
                bool s = HasConnection(pos, Vector3Int.back);
                bool e = HasConnection(pos, Vector3Int.right);
                bool w = HasConnection(pos, Vector3Int.left);

                GameObject prefab = corridorTiles.Straight;
                float yRot = 0;
                int mask = (n?1:0) + (e?2:0) + (s?4:0) + (w?8:0);

                switch (mask)
                {
                    case 5: prefab = corridorTiles.Straight; yRot = 0; break;
                    case 10: prefab = corridorTiles.Straight; yRot = 90; break;
                    case 3: prefab = corridorTiles.Corner; yRot = 0; break;
                    case 6: prefab = corridorTiles.Corner; yRot = 90; break;
                    case 12: prefab = corridorTiles.Corner; yRot = 180; break;
                    case 9: prefab = corridorTiles.Corner; yRot = 270; break;
                    case 7: prefab = corridorTiles.TJunction; yRot = 0; break;
                    case 14: prefab = corridorTiles.TJunction; yRot = 90; break;
                    case 13: prefab = corridorTiles.TJunction; yRot = 180; break;
                    case 11: prefab = corridorTiles.TJunction; yRot = 270; break;
                    case 15: prefab = corridorTiles.Cross; break;
                    case 1: prefab = corridorTiles.DeadEnd; yRot = 0; break;
                    case 2: prefab = corridorTiles.DeadEnd; yRot = 90; break;
                    case 4: prefab = corridorTiles.DeadEnd; yRot = 180; break;
                    case 8: prefab = corridorTiles.DeadEnd; yRot = 270; break;
                }

                if (prefab)
                {
                    var go = Instantiate(prefab, (Vector3)pos * gridScale + centerOffset, Quaternion.Euler(0, yRot, 0), transform);
                    go.name = $"Corridor_{pos}";
                    node.Instance = go;
                }
            }
        }

        private bool HasConnection(Vector3Int pos, Vector3Int dir)
        {
            Vector3Int neighbor = pos + dir;
            if (logicalGrid.TryGetValue(neighbor, out DungeonNode node))
            {
                if (node.Type == NodeType.Corridor) return true;
                return HasSocketFacing(node, neighbor, -dir);
            }
            return false;
        }

        private void AddDebugPath(Vector3Int start, Vector3Int end, Color c)
        {
            Vector3 off = new Vector3(gridScale/2, 0.5f, gridScale/2);
            debugPaths.Add(((Vector3)start * gridScale + off, (Vector3)end * gridScale + off, c));
        }

        private void OnDrawGizmos()
        {
            foreach (var p in debugPaths)
            {
                Gizmos.color = p.color;
                Gizmos.DrawLine(p.start, p.end);
                Gizmos.DrawSphere(p.end, 0.2f);
            }
        }
    }
}