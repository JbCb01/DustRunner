using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DustRunner.LevelGeneration
{
    public class CrawlerDungeonGenerator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private RoomTemplate startRoomPrefab;
        [SerializeField] private RoomTemplate endingRoomPrefab; // NOWOŚĆ: Pokój końcowy
        [SerializeField] private List<RoomTemplate> roomPrefabs;
        [SerializeField] private CorridorTileSet corridorTiles;

        [Header("Parameters")]
        [SerializeField] [Range(0f, 1f)] private float corridorChance = 0.4f;
        [SerializeField] private int minRoomCount = 10; // NOWOŚĆ: Gwarantowana liczba pokoi
        [SerializeField] private int maxStepsSafety = 100; // Zabezpieczenie przed pętlą nieskończoną
        
        [Header("Grid Settings")]
        [SerializeField] private float gridScale = 5.0f;
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

        // Licznik TYLKO pokoi (bez korytarzy)
        private int roomsSpawnedCount = 0;

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        [ContextMenu("Generate Dungeon")]
        public void Generate()
        {
            Cleanup();
            if (startRoomPrefab == null) return;

            // 1. Start Room
            PlaceRoom(startRoomPrefab, Vector3Int.zero, 0);

            // 2. Main Crawler Loop (Generuj, aż osiągniemy minimum pokoi)
            int steps = 0;
            int maxAttempts = maxStepsSafety * 10; // Backup safety counter
            
            // Warunek: Pętla działa dopóki mamy mniej pokoi niż chcemy I mamy otwarte wyjścia
            while (roomsSpawnedCount < minRoomCount && pendingExits.Count > 0 && maxAttempts > 0)
            {
                maxAttempts--;
                
                // Próba wykonania kroku
                if (ProcessNextStep()) 
                {
                    steps++;
                }
            }

            Debug.Log($"[Generator] Main Phase Done. Rooms: {roomsSpawnedCount}/{minRoomCount}. Steps taken: {steps}");

            // 3. Ending Room Phase (NOWOŚĆ)
            if (endingRoomPrefab != null)
            {
                PlaceEndingRoom();
            }

            // 4. Visuals & Cleanup
            ResolveCorridorVisuals();
            SealDungeon(); // Zamykamy resztę otworów
        }

        [ContextMenu("Cleanup")]
        public void Cleanup()
        {
            logicalGrid.Clear();
            allNodes.Clear();
            pendingExits.Clear();
            debugPaths.Clear();
            roomsSpawnedCount = 0;
            
            var children = new List<GameObject>();
            foreach (Transform child in transform) children.Add(child.gameObject);
            children.ForEach(c => DestroyImmediate(c));
        }

        // --- ENDING ROOM LOGIC ---

        private void PlaceEndingRoom()
        {
            // 1. Sortujemy dostępne wyjścia od najdalszego do najbliższego (względem Startu 0,0,0)
            // Używamy sqrMagnitude dla wydajności (odległość euklidesowa)
            var sortedExits = pendingExits.OrderByDescending(x => x.SourceGridPos.sqrMagnitude).ToList();

            bool placed = false;

            foreach (var exit in sortedExits)
            {
                // Próbujemy wstawić pokój końcowy
                if (TryPlaceSpecificRoom(endingRoomPrefab, exit))
                {
                    Debug.Log($"[Generator] Ending Room placed at distance: {Mathf.Sqrt(exit.SourceGridPos.sqrMagnitude) * gridScale}m");
                    
                    // Usuwamy zużyte wyjście z listy globalnej
                    pendingExits.Remove(exit);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                Debug.LogWarning("[Generator] Failed to place Ending Room! Dungeon might be dead-ended.");
            }
        }

        private bool TryPlaceSpecificRoom(RoomTemplate room, PendingExit entryInfo)
        {
            // Ta sama logika co w TryPlaceRoom, ale dla konkretnego prefaba
            Vector3Int entryCell = entryInfo.SourceGridPos + entryInfo.Direction;
            
            if (logicalGrid.ContainsKey(entryCell)) return false;

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
                    AddDebugPath(entryInfo.SourceGridPos, entryCell, Color.magenta); // Inny kolor dla Bossa
                    return true;
                }
            }
            return false;
        }

        // --- MAIN LOOP STEP ---

        private bool ProcessNextStep()
        {
            if (pendingExits.Count == 0) return false;

            // Wybieramy losowe wyjście (można zmienić na ważone, np. preferuj te dalej od środka)
            int index = Random.Range(0, pendingExits.Count);
            PendingExit exit = pendingExits[index];
            Vector3Int targetCell = exit.SourceGridPos + exit.Direction;

            if (logicalGrid.ContainsKey(targetCell))
            {
                pendingExits.RemoveAt(index);
                return false; 
            }

            bool placed = false;
            
            // Decyzja co stawiamy
            bool wantCorridor = Random.value < corridorChance;
            
            // Jeśli mamy już wystarczająco pokoi, forsujemy korytarze (żeby nie przeładować mapy)
            // LUB jeśli brakuje nam pokoi, zmniejszamy szansę na korytarz
            if (roomsSpawnedCount >= minRoomCount) wantCorridor = true; 

            if (wantCorridor)
            {
                placed = TryPlaceLogicalCorridor(targetCell, exit);
            }
            
            // Jeśli nie korytarz (lub korytarz się nie zmieścił), próbujemy pokój
            if (!placed)
            {
                placed = TryPlaceRoom(targetCell, exit);
                
                // Fallback: jeśli pokój się nie zmieścił, ratuj sytuację korytarzem (jest mniejszy)
                if (!placed) placed = TryPlaceLogicalCorridor(targetCell, exit); 
            }

            if (placed)
            {
                pendingExits.RemoveAt(index);
                return true;
            }

            return false;
        }

        // --- PLACEMENT LOGIC ---

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
            List<Vector3Int> occupiedOffsets = room.GetOccupiedCells(rotSteps);
            Vector3Int minOffset = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            foreach (var offset in occupiedOffsets) minOffset = Vector3Int.Min(minOffset, offset);
            Vector3Int visualGridPos = origin + minOffset;

            // Legacy Offset Logic (z poprzedniej poprawki)
            Vector3 offsetVector = Vector3.zero;
            switch (rotSteps)
            {
                case 0: offsetVector = Vector3.zero; break;
                case 1: offsetVector = new Vector3(0, 0, room.GridSize.x * gridScale); break;
                case 2: offsetVector = new Vector3(room.GridSize.x * gridScale, 0, room.GridSize.z * gridScale); break;
                case 3: offsetVector = new Vector3(room.GridSize.z * gridScale, 0, 0); break;
            }

            Vector3 finalPos = ((Vector3)visualGridPos * gridScale) + offsetVector;
            Quaternion rotation = Quaternion.Euler(0, rotSteps * 90, 0);

            GameObject go = Instantiate(room.gameObject, finalPos, rotation, transform);
            go.name = $"{room.name}_{origin}";

            DungeonNode node = new DungeonNode
            {
                Type = NodeType.Room,
                Instance = go,
                RoomData = room,
                RotationSteps = rotSteps
            };

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
            
            // Increment room counter
            roomsSpawnedCount++;
        }

        private void RegisterNode(DungeonNode node, Vector3Int origin, List<Vector3Int> occupiedOffsets)
        {
            allNodes.Add(node);
            foreach (var offset in occupiedOffsets)
            {
                logicalGrid[origin + offset] = node;
            }
        }

        // --- VALIDATION & UTILS ---

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

            // 2. Check Neighbor Connections
            foreach (var localCell in occupiedCells)
            {
                Vector3Int worldCell = origin + localCell;
                Vector3Int[] neighbors = { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right };

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

                            if (!doWeConnect) return false;
                        }
                    }
                }
            }

            // 3. Check Outgoing into Walls
            if (type == NodeType.Room)
            {
                foreach (var socket in room.Sockets)
                {
                    Vector3Int sPos = origin + RoomTemplate.RotateVectorInt(socket.LocalPosition, rotSteps);
                    Vector3Int sDir = RoomTemplate.RotateDirection(socket.GetDirectionVector(), rotSteps);
                    Vector3Int target = sPos + sDir;

                    if (logicalGrid.TryGetValue(target, out DungeonNode targetNode))
                    {
                        if (!HasSocketFacing(targetNode, target, -sDir)) return false;
                    }
                }
            }

            return true;
        }

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

        // --- CORRIDOR VISUALS ---

        private void ResolveCorridorVisuals()
        {
            Vector3 centerOffset = new Vector3(gridScale * 0.5f, 0, gridScale * 0.5f);
            
            foreach (var node in allNodes)
            {
                if (node.Type != NodeType.Corridor) continue;
                
                Vector3Int pos = Vector3Int.zero;
                // Find pos brute-force (można zoptymalizować dodając pole Position do Node)
                foreach(var kvp in logicalGrid) { if(kvp.Value == node) { pos = kvp.Key; break; } }

                bool n = HasConnection(pos, Vector3Int.forward); // +Z
                bool s = HasConnection(pos, Vector3Int.back);    // -Z
                bool e = HasConnection(pos, Vector3Int.right);   // +X
                bool w = HasConnection(pos, Vector3Int.left);    // -X

                GameObject prefab = corridorTiles.Straight;
                float yRot = 0;
                int mask = (n?1:0) + (e?2:0) + (s?4:0) + (w?8:0);

                switch (mask)
                {
                    // Straight
                    case 5: prefab = corridorTiles.Straight; yRot = 0; break;
                    case 10: prefab = corridorTiles.Straight; yRot = 90; break;
                    
                    // Corner (zakładamy model L: 0 rot łączy N+E)
                    case 3: prefab = corridorTiles.Corner; yRot = 0; break;
                    case 6: prefab = corridorTiles.Corner; yRot = 90; break;
                    case 12: prefab = corridorTiles.Corner; yRot = 180; break;
                    case 9: prefab = corridorTiles.Corner; yRot = 270; break;

                    // T-Junction (Wall Logic Fixed)
                    // Maska 11 (N+E+W) -> Wall S -> Rot 0 (Default)
                    case 11: 
                        prefab = corridorTiles.TJunction; 
                        yRot = 0; 
                        break; 
                    
                    // Maska 7 (N+E+S) -> Wall W -> Rot 90 (Ściana S -> W)
                    case 7: 
                        prefab = corridorTiles.TJunction; 
                        yRot = 90; 
                        break;

                    // Maska 14 (E+S+W) -> Wall N -> Rot 180 (Ściana S -> N)
                    case 14: 
                        prefab = corridorTiles.TJunction; 
                        yRot = 180; 
                        break;

                    // Maska 13 (S+W+N) -> Wall E -> Rot 270 (Ściana S -> E)
                    case 13: 
                        prefab = corridorTiles.TJunction; 
                        yRot = 270; 
                        break;

                    // Cross
                    case 15: prefab = corridorTiles.Cross; break;
                    
                    // Dead Ends (zakładamy model: wyjście na N)
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