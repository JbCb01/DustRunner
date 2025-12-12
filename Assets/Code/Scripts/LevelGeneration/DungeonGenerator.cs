using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _worldContainer;

        [Header("Grid Settings")]
        [SerializeField] private int _gridWidth = 50;
        [SerializeField] private int _gridHeight = 50;
        [SerializeField] private float _unitSize = 5f;

        [Header("Generation Budget")]
        [Tooltip("Całkowita docelowa liczba pokoi (Duchy + Obowiązkowe + Losowe).")]
        [SerializeField] private int _targetTotalRooms = 15; 
        [SerializeField] private int _maxPlacementAttempts = 50;
        [Range(0f, 1f)] [SerializeField] private float _loopChance = 0.15f;
        
        [Header("Seed Settings")]
        [SerializeField] private bool _useRandomSeed = true;
        [SerializeField] private int _seed = 12345;

        [Header("Pathfinding Costs")]
        [SerializeField] private int _diggingCost = 5; 
        [SerializeField] private int _existingPathCost = 1; 
        [SerializeField] private int _turnCost = 10;

        [Header("Assets")]
        [SerializeField] private DungeonThemeSO _dungeonTheme;

        [Header("Debug Visualization")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private bool _showGraph = true;
        [SerializeField] private bool _showDelaunay = false;
        [SerializeField] private bool _showLogic = true;

        // --- INTERNAL STATE ---
        public List<RoomInstance> PlacedRooms => _placedRooms;
        
        private HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> _corridorCells = new HashSet<Vector2Int>();
        private List<RoomInstance> _placedRooms = new List<RoomInstance>();
        
        private List<DoorNode> _allNodes = new List<DoorNode>();
        private List<DoorEdge> _candidateEdges = new List<DoorEdge>(); 
        private List<DoorEdge> _finalEdges = new List<DoorEdge>();     
        private HashSet<DoorNode> _usedNodes = new HashSet<DoorNode>(); 
        private Dictionary<Vector2Int, Vector2Int> _activeDoorDirections = new Dictionary<Vector2Int, Vector2Int>();

        private float _verticalOffset = 0f;
        private int _currentLayerIndex = 0; 

        // --- PUBLIC API ---

        public void GenerateLayer(int seed, float yOffset, int layerIndex, List<FixedRoomData> fixedRooms = null)
        {
            _verticalOffset = yOffset;
            _currentLayerIndex = layerIndex;
            _seed = seed;
            _useRandomSeed = false;
            
            GenerateInternal(fixedRooms);
        }

        [ContextMenu("Generate Single Layer")]
        public void GenerateStandalone()
        {
            if (_useRandomSeed) _seed = (int)System.DateTime.Now.Ticks;
            _currentLayerIndex = 0;
            _verticalOffset = 0;
            GenerateInternal(null);
        }

        // --- CORE PIPELINE ---

        private void GenerateInternal(List<FixedRoomData> fixedRooms)
        {
            ClearAllData();
            
            if (_dungeonTheme == null)
            {
                Debug.LogError($"[DungeonGenerator] No Dungeon Theme assigned to {gameObject.name}!");
                return;
            }

            Random.InitState(_seed);
            
            // Licznik wykorzystanego budżetu
            int currentRoomCount = 0;

            // 1. INFRASTRUKTURA: Pokoje "Fixed" i "Ghost"
            // One są priorytetem - wliczają się do budżetu
            if (fixedRooms != null)
            {
                foreach (var fixedRoom in fixedRooms)
                {
                    Vector2Int rawSize = fixedRoom.Prefab.Size;
                    Vector2Int rotatedSize = (fixedRoom.Rotation == 1 || fixedRoom.Rotation == 3) ? new Vector2Int(rawSize.y, rawSize.x) : rawSize;
                    PlaceRoom(fixedRoom.Prefab, fixedRoom.Position, fixedRoom.Rotation, rotatedSize, fixedRoom.SkipVisuals, fixedRoom.OriginLayerIndex);
                    currentRoomCount++;
                }
            }

            // 2. OBOWIĄZKOWE: Mandatory Rooms
            // One też zjadają budżet
            if (_dungeonTheme.MandatoryRooms != null)
            {
                foreach (var mandatoryPrefabObj in _dungeonTheme.MandatoryRooms)
                {
                    if (mandatoryPrefabObj == null) continue;
                    RoomTemplate template = mandatoryPrefabObj.GetComponent<RoomTemplate>();
                    if (template == null) continue;

                    bool placed = TryPlaceSpecificRoom(template);
                    if (placed)
                    {
                        currentRoomCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[DungeonGenerator] Could not place MANDATORY room: {mandatoryPrefabObj.name} on Layer {_currentLayerIndex}");
                    }
                }
            }

            // 3. ZAWARTOŚĆ: Dopełnienie do Targetu
            // Obliczamy ile miejsca zostało w budżecie
            int roomsToSpawn = _targetTotalRooms - currentRoomCount;

            if (roomsToSpawn > 0)
            {
                for (int i = 0; i < roomsToSpawn; i++) TryPlaceRoom();
            }
            else
            {
                Debug.Log($"[Layer {_currentLayerIndex}] Budget filled by Ghosts/Mandatory ({currentRoomCount}/{_targetTotalRooms}). Skipping random generation.");
            }

            // 4. Collect Nodes
            foreach (var room in _placedRooms)
            {
                room.CalculateDoorNodes(_currentLayerIndex);
                _allNodes.AddRange(room.Nodes);
            }

            // 5. Connect Graph
            ConnectDoorsGraph();

            // 6. Generate Paths
            GeneratePathfinding();

            // 7. Autotiling Preparation
            _activeDoorDirections.Clear();
            foreach (var node in _usedNodes)
            {
                if (!_activeDoorDirections.ContainsKey(node.GridPos))
                    _activeDoorDirections.Add(node.GridPos, node.ExitDirection);
            }

            // 8. Spawning
            SpawnWorld();
            SpawnDoorSeals();
        }

        [ContextMenu("Clear All")]
        public void ClearAllData()
        {
            if (_worldContainer != null) {
                while (_worldContainer.childCount > 0) DestroyImmediate(_worldContainer.GetChild(0).gameObject);
            }
            _occupiedCells.Clear(); 
            _corridorCells.Clear(); 
            _placedRooms.Clear();
            _allNodes.Clear(); 
            _candidateEdges.Clear(); 
            _finalEdges.Clear(); 
            _usedNodes.Clear(); 
            _activeDoorDirections.Clear();
        }

        private bool TryPlaceSpecificRoom(RoomTemplate template)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int rotation = Random.Range(0, 4);
                Vector2Int rawSize = template.Size;
                Vector2Int rotatedSize = (rotation == 1 || rotation == 3) ? new Vector2Int(rawSize.y, rawSize.x) : rawSize;
                
                int x = Random.Range(1, _gridWidth - rotatedSize.x - 1);
                int y = Random.Range(1, _gridHeight - rotatedSize.y - 1);
                Vector2Int potentialPos = new Vector2Int(x, y);

                if (IsPositionValid(potentialPos, rotatedSize))
                {
                    PlaceRoom(template, potentialPos, rotation, rotatedSize, false, _currentLayerIndex);
                    return true;
                }
            }
            return false;
        }

        private void TryPlaceRoom()
        {
            RoomTemplate randomPrefab = _dungeonTheme.GetRandomRoom();
            if (randomPrefab == null) return;

            for (int attempt = 0; attempt < _maxPlacementAttempts; attempt++)
            {
                int rotation = Random.Range(0, 4);
                Vector2Int rawSize = randomPrefab.Size;
                Vector2Int rotatedSize = (rotation == 1 || rotation == 3) ? new Vector2Int(rawSize.y, rawSize.x) : rawSize;

                int x = Random.Range(1, _gridWidth - rotatedSize.x - 1);
                int y = Random.Range(1, _gridHeight - rotatedSize.y - 1);
                Vector2Int potentialPos = new Vector2Int(x, y);

                if (IsPositionValid(potentialPos, rotatedSize))
                {
                    PlaceRoom(randomPrefab, potentialPos, rotation, rotatedSize, false, _currentLayerIndex);
                    return;
                }
            }
        }

        private bool IsPositionValid(Vector2Int pos, Vector2Int size)
        {
            for (int x = pos.x - 1; x < pos.x + size.x + 1; x++)
                for (int y = pos.y - 1; y < pos.y + size.y + 1; y++)
                    if (_occupiedCells.Contains(new Vector2Int(x, y))) return false;
            return true;
        }

        private void PlaceRoom(RoomTemplate prefab, Vector2Int pos, int rotation, Vector2Int actualSize, bool isGhost, int baseLayer)
        {
            RoomInstance newRoom = new RoomInstance(prefab, pos, rotation, isGhost, baseLayer);
            _placedRooms.Add(newRoom);
            for (int x = 0; x < actualSize.x; x++)
                for (int y = 0; y < actualSize.y; y++)
                    _occupiedCells.Add(new Vector2Int(pos.x + x, pos.y + y));
        }

        // --- GRAPH LOGIC ---

        private void ConnectDoorsGraph()
        {
            if (_allNodes.Count < 2) return;

            _candidateEdges = DungeonAlgorithms.Triangulate(_allNodes);
            
            _candidateEdges.RemoveAll(edge => 
            {
                if (edge.NodeA.ParentRoom == edge.NodeB.ParentRoom) return true; 
                // KLUCZOWE: Sprawdzamy widoczność uwzględniając przezroczystość duchów
                if (!IsLineClear(edge.NodeA, edge.NodeB)) return true; 
                return false;
            });

            Dictionary<DoorNode, RoomInstance> nodeToCluster = new Dictionary<DoorNode, RoomInstance>();
            Dictionary<RoomInstance, HashSet<DoorNode>> clusterData = new Dictionary<RoomInstance, HashSet<DoorNode>>();

            foreach (var room in _placedRooms)
            {
                clusterData[room] = new HashSet<DoorNode>();
                foreach (var node in room.Nodes) { nodeToCluster[node] = room; clusterData[room].Add(node); }
            }

            _candidateEdges = _candidateEdges.OrderBy(x => x.Distance).ToList();

            foreach (var edge in _candidateEdges)
            {
                if(!nodeToCluster.ContainsKey(edge.NodeA) || !nodeToCluster.ContainsKey(edge.NodeB)) continue;
                RoomInstance cA = nodeToCluster[edge.NodeA]; 
                RoomInstance cB = nodeToCluster[edge.NodeB];

                if (cA != cB)
                {
                    _finalEdges.Add(edge);
                    foreach (var n in clusterData[cB]) { nodeToCluster[n] = cA; clusterData[cA].Add(n); }
                    clusterData.Remove(cB);
                }
                else if (Random.value < _loopChance) _finalEdges.Add(edge);
            }
        }

        // --- GHOST TRANSPARENCY FIX ---
        private bool IsLineClear(DoorNode a, DoorNode b)
        {
            Vector2Int start = a.GetEntryTile(); Vector2Int end = b.GetEntryTile();
            int x0 = start.x; int y0 = start.y; int x1 = end.x; int y1 = end.y;
            int dx = Mathf.Abs(x1 - x0); int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0); int sy = y0 < y1 ? 1 : -1; int err = dx + dy;

            while (true)
            {
                Vector2Int p = new Vector2Int(x0, y0);
                if (_occupiedCells.Contains(p))
                {
                    RoomInstance hitRoom = GetRoomAt(p);
                    // Jeśli trafiliśmy w pokój, który nie jest żadnym z końców krawędzi...
                    if (hitRoom != null && hitRoom != a.ParentRoom && hitRoom != b.ParentRoom)
                    {
                        // FIX: Jeśli to Ghost Room, ignorujemy kolizję (pozwalamy na połączenie logiczne)
                        // A* później znajdzie drogę naokoło fizycznego modelu
                        if (!hitRoom.IsGhost) return false;
                    }
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err; if (e2 >= dy) { err += dy; x0 += sx; } if (e2 <= dx) { err += dx; y0 += sy; }
            }
            return true;
        }

        private RoomInstance GetRoomAt(Vector2Int pos)
        {
            foreach(var r in _placedRooms) {
                Vector2Int size = r.GetRotatedSize();
                if (pos.x >= r.GridPos.x && pos.x < r.GridPos.x + size.x && 
                    pos.y >= r.GridPos.y && pos.y < r.GridPos.y + size.y) return r;
            }
            return null;
        }

        private void GeneratePathfinding()
        {
            var ctx = new DungeonAlgorithms.PathfindingContext
            {
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
                OccupiedCells = _occupiedCells,
                ExistingCorridors = _corridorCells,
                DiggingCost = _diggingCost,
                ExistingPathCost = _existingPathCost,
                TurnCost = _turnCost
            };

            foreach (var edge in _finalEdges)
            {
                Vector2Int start = edge.NodeA.GetEntryTile(); 
                Vector2Int end = edge.NodeB.GetEntryTile();
                
                ctx.ExistingCorridors = _corridorCells; 

                List<Vector2Int> path = DungeonAlgorithms.FindPath(start, end, ctx);
                
                if (path != null)
                {
                    _usedNodes.Add(edge.NodeA); 
                    _usedNodes.Add(edge.NodeB);
                    _corridorCells.Add(start); 
                    _corridorCells.Add(end);
                    foreach (var p in path) if (!_occupiedCells.Contains(p)) _corridorCells.Add(p);
                }
            }
        }

        // --- SPAWNING ---

        private void SpawnWorld()
        {
            if (_worldContainer == null) return;
            foreach (var room in _placedRooms)
            {
                if (room.IsGhost) continue;
                Vector3 worldPos = new Vector3(room.GridPos.x * _unitSize, _verticalOffset, room.GridPos.y * _unitSize);
                Quaternion rotation = Quaternion.Euler(0, room.RotationIndex * 90, 0);
                
                Vector3 offset = Vector3.zero;
                switch (room.RotationIndex) {
                    case 0: offset = Vector3.zero; break;
                    case 1: offset = new Vector3(0, 0, room.PrefabSource.Size.x * _unitSize); break;
                    case 2: offset = new Vector3(room.PrefabSource.Size.x * _unitSize, 0, room.PrefabSource.Size.y * _unitSize); break;
                    case 3: offset = new Vector3(room.PrefabSource.Size.y * _unitSize, 0, 0); break;
                }
                
                GameObject roomObj = Instantiate(room.PrefabSource.gameObject, worldPos + offset, rotation, _worldContainer);
                roomObj.name = $"{room.PrefabSource.name}_{room.GridPos}";
            }
            foreach (Vector2Int pos in _corridorCells) SpawnCorridorTile(pos);
        }

        private void SpawnDoorSeals()
        {
            if (_dungeonTheme == null || _dungeonTheme.BlockedDoor == null) return;

            foreach (var node in _allNodes)
            {
                if (!_usedNodes.Contains(node))
                {
                    Vector3 worldPos = new Vector3((node.GridPos.x + 0.5f) * _unitSize, _verticalOffset, (node.GridPos.y + 0.5f) * _unitSize);
                    Vector3 dirVec = new Vector3(node.ExitDirection.x, 0, node.ExitDirection.y);
                    Quaternion rotation = Quaternion.LookRotation(dirVec);
                    Instantiate(_dungeonTheme.BlockedDoor, worldPos, rotation, _worldContainer);
                }
            }
        }

        private void SpawnCorridorTile(Vector2Int pos)
        {
            if (_dungeonTheme == null) return;

            bool up = ShouldConnect(pos + Vector2Int.up, Vector2Int.up);
            bool down = ShouldConnect(pos + Vector2Int.down, Vector2Int.down);
            bool left = ShouldConnect(pos + Vector2Int.left, Vector2Int.left);
            bool right = ShouldConnect(pos + Vector2Int.right, Vector2Int.right);

            GameObject prefab = _dungeonTheme.GetRandomFloor(); 
            float rot = 0;
            int c = (up?1:0)+(down?1:0)+(left?1:0)+(right?1:0);

            if (c == 4) 
            { 
                prefab = _dungeonTheme.GetRandomCross(); 
            }
            else if (c == 3) 
            { 
                prefab = _dungeonTheme.GetRandomTJunction(); 
                if (!down) rot = 0; else if (!left) rot = 90; else if (!up) rot = 180; else rot = 270; 
            }
            else if (c == 2) 
            {
                if (up && down) 
                { 
                    prefab = _dungeonTheme.GetRandomStraight(); 
                    rot = 0; 
                }
                else if (left && right) 
                { 
                    prefab = _dungeonTheme.GetRandomStraight(); 
                    rot = 90; 
                }
                else 
                {
                    prefab = _dungeonTheme.GetRandomCorner();
                    if (up&&right) rot = 0; else if (right&&down) rot = 90; else if (down&&left) rot = 180; else rot = 270;
                }
            } 
            else 
            {
                prefab = _dungeonTheme.GetRandomDeadEnd();
                if (prefab == null) prefab = _dungeonTheme.GetRandomStraight();
                if(up) rot=0; else if(right) rot=90; else if(down) rot=180; else rot=270;
            }

            if (prefab != null) 
            {
                Instantiate(prefab, new Vector3((pos.x+0.5f)*_unitSize, _verticalOffset, (pos.y+0.5f)*_unitSize), Quaternion.Euler(0,rot,0), _worldContainer);
            }
        }

        private bool ShouldConnect(Vector2Int targetPos, Vector2Int directionToTarget)
        {
            if (_corridorCells.Contains(targetPos)) return true;
            if (_occupiedCells.Contains(targetPos)) {
                if (_activeDoorDirections.TryGetValue(targetPos, out Vector2Int doorExitDir)) return doorExitDir == -directionToTarget;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;
            if (_showLogic)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.1f);
                Gizmos.DrawWireCube(new Vector3(_gridWidth * _unitSize * 0.5f, _verticalOffset, _gridHeight * _unitSize * 0.5f), new Vector3(_gridWidth * _unitSize, 1, _gridHeight * _unitSize));
                foreach (var room in _placedRooms) {
                    Gizmos.color = room.IsGhost ? new Color(0,1,1,0.2f) : new Color(0,1,0,0.3f);
                    Vector3 center = room.GetWorldCenter(_unitSize); center.y = _verticalOffset;
                    Vector2Int size = room.GetRotatedSize();
                    Gizmos.DrawWireCube(center, new Vector3(size.x * _unitSize, 2f, size.y * _unitSize));
                }
                Gizmos.color = Color.cyan;
                foreach (var cell in _corridorCells) Gizmos.DrawCube(new Vector3((cell.x + 0.5f) * _unitSize, _verticalOffset, (cell.y + 0.5f) * _unitSize), Vector3.one * _unitSize * 0.9f);
            }
            if (_showDelaunay && _candidateEdges != null) {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                foreach (var e in _candidateEdges) Gizmos.DrawLine(new Vector3((e.NodeA.GridPos.x+0.5f)*_unitSize, _verticalOffset + 2f, (e.NodeA.GridPos.y+0.5f)*_unitSize), new Vector3((e.NodeB.GridPos.x+0.5f)*_unitSize, _verticalOffset + 2f, (e.NodeB.GridPos.y+0.5f)*_unitSize));
            }
            if (_showGraph && _finalEdges != null) {
                Gizmos.color = Color.green;
                foreach (var e in _finalEdges) Gizmos.DrawLine(new Vector3((e.NodeA.GridPos.x+0.5f)*_unitSize, _verticalOffset + 2.1f, (e.NodeA.GridPos.y+0.5f)*_unitSize), new Vector3((e.NodeB.GridPos.x+0.5f)*_unitSize, _verticalOffset + 2.1f, (e.NodeB.GridPos.y+0.5f)*_unitSize));
            }
        }
    }
}