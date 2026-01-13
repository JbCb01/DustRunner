using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DustRunner.LevelGeneration
{
    public class CrawlerDungeonGeneratorRef : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private LevelConfiguration levelConfig;

        [Header("Generator Settings")]
        [SerializeField] private bool generateOnStart = false;

        public class DungeonNode
        {
            public NodeType Type;
            public GameObject Instance; 
            public RoomTemplate RoomData;
            public int RotationSteps;
            public List<(Vector3Int pos, Vector3Int dir, SocketType type)> ActiveSocketsWorld = new();
        }
        private struct ShortcutCandidate
        {
            public Vector3Int OccupiedPos; // Tu stoi istniejący korytarz lub pokój
            public Vector3Int EmptyPos;    // Tu zacznie się nowy tunel
            public Vector3Int Direction;   // W którą stronę patrzymy
        }
        private List<ShortcutCandidate> debugCandidates = new List<ShortcutCandidate>();
        private List<Vector3Int> debugStarts = new List<Vector3Int>();
        private List<Vector3Int> debugEnds = new List<Vector3Int>();
        private class PendingExit
        {
            public Vector3Int SourceGridPos;
            public Vector3Int Direction;
            public SocketType RequiredType;

            // NOWOŚĆ: Dane o potencjalnym sekretnym pokoju z tego wyjścia
            public RoomTemplate UniqueRoomPrefab;
            public float UniqueRoomChance;
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
            if (levelConfig == null)
            {
                Debug.LogError("[Generator] Brak przypisanego LevelConfiguration!");
                return;
            }

            if (levelConfig.UseRandomSeed)
            {
                Random.InitState(System.Environment.TickCount);
            }
            else
            {
                Random.InitState(levelConfig.Seed);
            }

            Cleanup();
            if (levelConfig.StartRoomPrefab == null) return;

            // 1. Start Room
            PlaceRoom(levelConfig.StartRoomPrefab, Vector3Int.zero, 0);

            // 2. Main Crawler Loop
            int steps = 0;
            int maxAttempts = levelConfig.MaxStepsSafety * 10;
            
            while (roomsSpawnedCount < levelConfig.MinRoomCount && pendingExits.Count > 0 && maxAttempts > 0)
            {
                maxAttempts--;
                if (ProcessNextStep()) 
                {
                    steps++;
                }
            }

            Debug.Log($"[Generator] Main Phase Done. Rooms: {roomsSpawnedCount}/{levelConfig.MinRoomCount}. Steps taken: {steps}");

            // 3. Ending Room Phase
            if (levelConfig.EndingRoomPrefab != null)
            {
                PlaceEndingRoom();
            }

            GenerateSecretRooms();
            if (levelConfig.EnableShortcuts) GenerateShortcuts();

            // 4. Visuals & Cleanup
            ResolveCorridorVisuals();
            SealDungeon(); 
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
            var sortedExits = pendingExits.OrderByDescending(x => x.SourceGridPos.sqrMagnitude).ToList();
            bool placed = false;

            foreach (var exit in sortedExits)
            {
                if (TryPlaceSpecificRoom(levelConfig.EndingRoomPrefab, exit, Color.magenta))
                {
                    Debug.Log($"[Generator] Ending Room placed at distance: {Mathf.Sqrt(exit.SourceGridPos.sqrMagnitude) * levelConfig.GridScale}m");
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

        private void GenerateSecretRooms()
        {
            var secretCandidates = pendingExits
                .Where(x => x.UniqueRoomPrefab != null)
                .ToList();

            foreach (var exit in secretCandidates)
            {
                Vector3Int targetCell = exit.SourceGridPos + exit.Direction;
                if (logicalGrid.ContainsKey(targetCell)) continue;

                if (Random.value < exit.UniqueRoomChance)
                {
                    if (TryPlaceSpecificRoom(exit.UniqueRoomPrefab, exit, Color.yellow))
                    {
                        Debug.Log($"[Generator] Secret Room '{exit.UniqueRoomPrefab.name}' spawned at {targetCell}");
                        pendingExits.Remove(exit);
                    }
                }
            }
        }

        // Zmodyfikowana metoda: dodano parametr koloru debugowania
        private bool TryPlaceSpecificRoom(RoomTemplate room, PendingExit entryInfo, Color debugColor)
        {
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
                    AddDebugPath(entryInfo.SourceGridPos, entryCell, debugColor);
                    return true;
                }
            }
            return false;
        }

        // --- MAIN LOOP STEP ---

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
            bool wantCorridor = Random.value < levelConfig.CorridorChance;
            
            if (roomsSpawnedCount >= levelConfig.MinRoomCount) wantCorridor = true; 

            if (wantCorridor)
            {
                placed = TryPlaceLogicalCorridor(targetCell, exit);
            }
            
            if (!placed)
            {
                placed = TryPlaceRoom(targetCell, exit);
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
                node.ActiveSocketsWorld.Add((targetPos, d, entryInfo.RequiredType));

                if (!logicalGrid.ContainsKey(targetPos + d))
                {
                    pendingExits.Add(new PendingExit {
                        SourceGridPos = targetPos, 
                        Direction = d, 
                        RequiredType = entryInfo.RequiredType
                        // Korytarze standardowo nie mają przypisanych sekretów
                    });
                }
            }
            return true;
        }

        private bool TryPlaceRoom(Vector3Int entryCell, PendingExit entryInfo)
        {
            var candidates = levelConfig.RoomPrefabs.OrderBy(x => Random.value).ToList();

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

            float scale = levelConfig.GridScale;
            Vector3 offsetVector = Vector3.zero;

            switch (rotSteps)
            {
                case 0: offsetVector = Vector3.zero; break;
                case 1: offsetVector = new Vector3(0, 0, room.GridSize.x * scale); break;
                case 2: offsetVector = new Vector3(room.GridSize.x * scale, 0, room.GridSize.z * scale); break;
                case 3: offsetVector = new Vector3(room.GridSize.z * scale, 0, 0); break;
            }

            Vector3 finalPos = ((Vector3)visualGridPos * scale) + offsetVector;
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
                node.ActiveSocketsWorld.Add((sPos, sDir, s.Type));
                
                Vector3Int neighbor = sPos + sDir;
                if (!logicalGrid.ContainsKey(neighbor))
                {
                    // NOWOŚĆ: Przekazujemy dane o UniqueRoom do PendingExit
                    pendingExits.Add(new PendingExit { 
                        SourceGridPos = sPos, 
                        Direction = sDir, 
                        RequiredType = s.Type,
                        UniqueRoomPrefab = s.UniqueRoomPrefab,
                        UniqueRoomChance = s.UniqueRoomChance
                    });
                }
            }

            RegisterNode(node, origin, occupiedOffsets);
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

        private void GenerateShortcuts()
        {
            debugPaths.Clear();
            debugStarts.Clear();
            debugEnds.Clear();

            if (levelConfig.MaxShortcuts <= 0) return;

            // Lista terminali: (Pozycja startowa, Kierunek - opcjonalny/niewykorzystywany przy DeadEnd, Rodzic)
            var terminals = new List<(Vector3Int startPos, DungeonNode parent)>();

            // 1. KANDYDACI Z POKOI (Niewykorzystane sockety)
            // Tu musimy startować z PUSTEJ kratki przed drzwiami, bo nie możemy wiercić w ścianach pokoju bez socketa
            foreach (var exit in pendingExits)
            {
                Vector3Int target = exit.SourceGridPos + exit.Direction;
                
                if (logicalGrid.TryGetValue(exit.SourceGridPos, out DungeonNode roomNode))
                {
                    if (!logicalGrid.ContainsKey(target))
                    {
                        terminals.Add((target, roomNode));
                    }
                }
            }

            // 2. KANDYDACI Z KORYTARZY (Dead-Endy)
            // ZMIANA: Startujemy bezpośrednio z zajętej kratki korytarza!
            foreach (var node in allNodes)
            {
                if (node.Type != NodeType.Corridor) continue;

                Vector3Int nodePos = Vector3Int.zero;
                foreach(var kvp in logicalGrid) { if(kvp.Value == node) { nodePos = kvp.Key; break; } }

                // Sprawdzamy czy to końcówka (ma tylko 1 sąsiada)
                int connectionCount = 0;
                Vector3Int[] dirs = { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right };
                foreach (var d in dirs) if (logicalGrid.ContainsKey(nodePos + d)) connectionCount++;

                if (connectionCount == 1)
                {
                    // Dodajemy sam korytarz jako punkt startowy.
                    // A* sam zdecyduje, w którą wolną stronę wyjść (przód lub boki).
                    terminals.Add((nodePos, node));
                }
            }

            terminals = terminals.OrderBy(x => Random.value).ToList();

            int shortcutsBuilt = 0;
            int safety = 100;

            while (shortcutsBuilt < levelConfig.MaxShortcuts && terminals.Count > 1 && safety > 0)
            {
                safety--;
                var startTerm = terminals[0];
                terminals.RemoveAt(0);

                int bestPartnerIdx = -1;

                for (int i = 0; i < terminals.Count; i++)
                {
                    var endTerm = terminals[i];
                    
                    // 1. Odrzucamy ten sam obiekt
                    if (startTerm.parent == endTerm.parent) continue;

                    // 2. Odrzucamy bezpośrednich sąsiadów
                    if (AreNodesConnected(startTerm.parent, endTerm.parent)) continue;

                    // 3. Poziom Y
                    if (startTerm.startPos.y != endTerm.startPos.y) continue;

                    float dist = Vector3.Distance(startTerm.startPos, endTerm.startPos);

                    // Zmniejszyłem minimalny dystans do 2.0f, bo teraz startujemy "głębiej" (w dead-endzie)
                    if (dist > 2.0f && dist <= levelConfig.MaxShortcutLength)
                    {
                        bestPartnerIdx = i;
                        break;
                    }
                }

                if (bestPartnerIdx != -1)
                {
                    var endTerm = terminals[bestPartnerIdx];

                    debugStarts.Add(startTerm.startPos);
                    debugEnds.Add(endTerm.startPos);

                    // A* musi obsłużyć start z zajętego pola (DeadEnd) do celu (Empty lub DeadEnd)
                    List<Vector3Int> path = GridAStar.FindPath(startTerm.startPos, endTerm.startPos, logicalGrid);

                    // Walidacja:
                    // path.Count > 1: Bo path zawiera start i end. 
                    // Jeśli start==DeadEnd i end==Empty (sąsiad), to path ma długość 2.
                    // Chcemy jednak unikać łączenia "przez ścianę" bez korytarza.
                    // Jeśli DeadEnd (A) i Empty (B) są obok siebie, to path=[A, B].
                    // BuildCorridorPath zignoruje A (bo zajęte) i zbuduje B.
                    // To jest OK (DeadEnd otworzy się na B).
                    if (path != null && path.Count >= 2 && path.Count <= levelConfig.MaxShortcutLength)
                    {
                        BuildCorridorPath(path);
                        shortcutsBuilt++;
                        AddDebugPath(path[0], path[path.Count - 1], Color.cyan);
                        terminals.RemoveAt(bestPartnerIdx);
                    }
                }
            }
        }

        private bool AreNodesConnected(DungeonNode nodeA, DungeonNode nodeB)
        {
            // Sprawdzamy czy jakikolwiek socket z A prowadzi do B
            // (To wykryje sytuację: Pokój -> Korytarz, bo Pokój ma socket wyjściowy na ten korytarz)
            if (CheckConnectionOneWay(nodeA, nodeB)) return true;

            // Sprawdzamy czy jakikolwiek socket z B prowadzi do A
            // (To wykryje sytuację odwrotną lub połączenie korytarz-korytarz)
            if (CheckConnectionOneWay(nodeB, nodeA)) return true;

            return false;
        }

        private bool CheckConnectionOneWay(DungeonNode from, DungeonNode to)
        {
            foreach (var socket in from.ActiveSocketsWorld)
            {
                Vector3Int targetCell = socket.pos + socket.dir;
                
                // Sprawdzamy co jest w kratce docelowej
                if (logicalGrid.TryGetValue(targetCell, out DungeonNode neighbor))
                {
                    // Jeśli sąsiadem jest węzeł, którego szukamy -> są połączone
                    if (neighbor == to) return true;
                }
            }
            return false;
        }

        private void BuildCorridorPath(List<Vector3Int> path)
        {
            foreach (var pos in path)
            {
                // Podwójne sprawdzenie (A* omija przeszkody, ale dla pewności)
                if (logicalGrid.ContainsKey(pos)) continue;

                // Tworzymy Node logiczny
                DungeonNode node = new DungeonNode 
                { 
                    Type = NodeType.Corridor, 
                    RotationSteps = 0 
                };
                
                // Rejestrujemy w Gridzie i na liście wszystkich nodów
                // Ważne: Nie dodajemy ActiveSocketsWorld, bo te korytarze są "pasywne"
                // System wizualny sam wykryje, że mają sąsiadów.
                logicalGrid[pos] = node;
                allNodes.Add(node);
            }
        }

        // --- CORRIDOR VISUALS ---

        private void ResolveCorridorVisuals()
        {
            float scale = levelConfig.GridScale;
            Vector3 centerOffset = new Vector3(scale * 0.5f, 0, scale * 0.5f);
            var tiles = levelConfig.CorridorTiles;
            
            foreach (var node in allNodes)
            {
                if (node.Type != NodeType.Corridor) continue;
                
                Vector3Int pos = Vector3Int.zero;
                foreach(var kvp in logicalGrid) { if(kvp.Value == node) { pos = kvp.Key; break; } }

                bool n = HasConnection(pos, Vector3Int.forward);
                bool s = HasConnection(pos, Vector3Int.back);
                bool e = HasConnection(pos, Vector3Int.right);
                bool w = HasConnection(pos, Vector3Int.left);

                GameObject prefab = tiles.Straight;
                float yRot = 0;
                int mask = (n?1:0) + (e?2:0) + (s?4:0) + (w?8:0);

                switch (mask)
                {
                    case 5: prefab = tiles.Straight; yRot = 0; break;
                    case 10: prefab = tiles.Straight; yRot = 90; break;
                    case 3: prefab = tiles.Corner; yRot = 0; break;
                    case 6: prefab = tiles.Corner; yRot = 90; break;
                    case 12: prefab = tiles.Corner; yRot = 180; break;
                    case 9: prefab = tiles.Corner; yRot = 270; break;
                    case 11: prefab = tiles.TJunction; yRot = 0; break; 
                    case 7: prefab = tiles.TJunction; yRot = 90; break;
                    case 14: prefab = tiles.TJunction; yRot = 180; break;
                    case 13: prefab = tiles.TJunction; yRot = 270; break;
                    case 15: prefab = tiles.Cross; break;
                    case 1: prefab = tiles.DeadEnd; yRot = 0; break;
                    case 2: prefab = tiles.DeadEnd; yRot = 90; break;
                    case 4: prefab = tiles.DeadEnd; yRot = 180; break;
                    case 8: prefab = tiles.DeadEnd; yRot = 270; break;
                }

                if (prefab)
                {
                    var go = Instantiate(prefab, (Vector3)pos * scale + centerOffset, Quaternion.Euler(0, yRot, 0), transform);
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
            float scale = levelConfig.GridScale;
            HashSet<Vector3Int> sealedPositions = new HashSet<Vector3Int>();

            // 1. Znajdź wszystkie kandydatów na pokoje końcowe (pokoje z dokładnie 1 socketem)
            var capRoomCandidates = levelConfig.RoomPrefabs.Where(r => r.Sockets.Count == 1).ToList();

            // 2. Zbierz wszystkie otwarte sockety TYLKO Z POKOI
            List<PendingExit> openSockets = new List<PendingExit>();

            foreach (var node in allNodes)
            {
                // POPRAWKA: Jeśli to korytarz, pomijamy go.
                // Korytarze "ślepe" są obsługiwane wizualnie w ResolveCorridorVisuals (prefab DeadEnd/Gruzowisko),
                // więc nie potrzebują CapRoom ani DoorBlocker.
                if (node.Type != NodeType.Room) continue;

                foreach (var socket in node.ActiveSocketsWorld)
                {
                    Vector3Int targetCell = socket.pos + socket.dir;
                    if (!logicalGrid.ContainsKey(targetCell))
                    {
                        openSockets.Add(new PendingExit {
                            SourceGridPos = socket.pos,
                            Direction = socket.dir,
                            RequiredType = socket.type
                        });
                    }
                }
            }

            // 3. Dla każdego otwartego otworu POKOJU podejmij decyzję
            foreach (var exit in openSockets)
            {
                Vector3Int targetCell = exit.SourceGridPos + exit.Direction;
                
                if (logicalGrid.ContainsKey(targetCell)) continue;

                bool capRoomPlaced = false;

                // A. Próba wstawienia pokoju kończącego (Cap Room)
                if (capRoomCandidates.Count > 0 && Random.value < levelConfig.CapRoomChance)
                {
                    var room = capRoomCandidates[Random.Range(0, capRoomCandidates.Count)];
                    
                    if (TryPlaceSpecificRoom(room, exit, Color.blue))
                    {
                        capRoomPlaced = true;
                        Debug.Log($"[Generator] Placed Cap Room at {targetCell}");
                    }
                }

                // B. Fallback: Jeśli nie wstawiono pokoju -> stawiamy Door Blocker
                if (!capRoomPlaced)
                {
                    if (levelConfig.CorridorTiles.DoorBlocker == null) continue;

                    Vector3 center = (Vector3)exit.SourceGridPos * scale + (Vector3.one * scale * 0.5f);
                    Vector3 blockerPos = center + ((Vector3)exit.Direction * (scale * 0.5f));
                    Vector3Int discreteBlockerPos = Vector3Int.RoundToInt(blockerPos * 100); 

                    if (!sealedPositions.Contains(discreteBlockerPos))
                    {
                        Quaternion rot = Quaternion.LookRotation((Vector3)exit.Direction);
                        var go = Instantiate(levelConfig.CorridorTiles.DoorBlocker, blockerPos, rot, transform);
                        go.name = "Seal_Blocker";
                        sealedPositions.Add(discreteBlockerPos);
                    }
                }
            }
        }

        private void AddDebugPath(Vector3Int start, Vector3Int end, Color c)
        {
            float scale = levelConfig.GridScale;
            Vector3 off = new Vector3(scale/2, 0.5f, scale/2);
            debugPaths.Add(((Vector3)start * scale + off, (Vector3)end * scale + off, c));
        }

        private void OnDrawGizmos()
        {
            float s = (levelConfig != null) ? levelConfig.GridScale : 5.0f;
            Vector3 off = new Vector3(s * 0.5f, s * 0.5f, s * 0.5f);
            foreach (var p in debugPaths)
            {
                Gizmos.color = p.color;
                Gizmos.DrawLine(p.start, p.end);
                Gizmos.DrawSphere(p.end, 0.2f);
            }
            Gizmos.color = Color.yellow;
            foreach (var start in debugStarts)
            {
                Gizmos.DrawWireSphere((Vector3)start * s + off, s * 0.4f);
            }

            Gizmos.color = Color.red;
            foreach (var end in debugEnds)
            {
                Gizmos.DrawWireSphere((Vector3)end * s + off, s * 0.4f);
            }
        }
    }
}