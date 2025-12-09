using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    // --- DATA STRUCTURES ---

    public class FixedRoomData
    {
        public RoomTemplate Prefab;
        public Vector2Int Position;
        public int Rotation;
        public bool SkipVisuals;
        public int OriginLayerIndex;
    }

    // [System.Serializable]
    // public class RoomConfig
    // {
    //     // USUNIĘTO POLE "ID"
    //     public RoomTemplate Prefab;
    //     public int SpawnWeight = 1;
    // }

    // [System.Serializable]
    // public class CorridorTileSet
    // {
    //     public GameObject Straight;
    //     public GameObject Corner;
    //     public GameObject TJunction;
    //     public GameObject Cross;
    //     public GameObject DeadEnd;
    //     public GameObject FloorOnly;
    //     [Header("Props")]
    //     public GameObject BlockedDoor;
    // }

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

    public class RoomInstance
    {
        public RoomTemplate PrefabSource;
        public Vector2Int GridPos;
        public int RotationIndex;
        public bool IsGhost;
        public int BaseLayerIndex; // NOWOŚĆ: Na jakiej warstwie stoi "stopa" tego pokoju?
        
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

        // --- POPRAWIONA LOGIKA POBIERANIA DRZWI ---
        public void CalculateDoorNodes(int targetGeneratorLayer)
        {
            Nodes.Clear();
            if (PrefabSource.Doors == null) return;

            Vector2Int originalSize = PrefabSource.Size;

            foreach (var doorDef in PrefabSource.Doors)
            {
                // Obliczamy ABSOLUTNĄ warstwę tych konkretnych drzwi
                // Np. Klatka Schodowa (Base: 0) + Offset Drzwi (1) = Warstwa 1
                // Np. Sypialnia na górze (Base: 1) + Offset Drzwi (0) = Warstwa 1
                int doorAbsoluteLayer = BaseLayerIndex + doorDef.LayerOffset;

                // Sprawdzamy czy te drzwi należą do warstwy, którą właśnie generujemy
                if (doorAbsoluteLayer != targetGeneratorLayer) continue;

                Vector2Int localPos = doorDef.Position;
                Vector2Int rotatedOffset = Vector2Int.zero;
                Vector2Int rotatedDir = Vector2Int.zero;

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

    // --- MAIN GENERATOR CLASS ---
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _worldContainer;

        [Header("Grid Settings")]
        [SerializeField] private int _gridWidth = 50;
        [SerializeField] private int _gridHeight = 50;
        [SerializeField] private float _unitSize = 5f;

        [Header("Generation Settings")]
        [SerializeField] private int _roomCount = 10;
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
        // [SerializeField] private CorridorTileSet _corridorTiles;
        [SerializeField] private DungeonThemeSO _dungeonTheme;
        // [SerializeField] private List<RoomConfig> _availableRooms;

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
            _currentLayerIndex = 0; // Assume layer 0 for standalone test
            _verticalOffset = 0;
            GenerateInternal(null);
        }

        // --- CORE PIPELINE ---

        private void GenerateInternal(List<FixedRoomData> fixedRooms)
        {
            ClearAllData();
            
            // ZMIANA: Sprawdzamy czy mamy Theme i czy ma pokoje
            if (_dungeonTheme == null)
            {
                Debug.LogError("No Dungeon Theme assigned!");
                return;
            }

            Random.InitState(_seed);
            
            // 1. Inject Fixed Rooms
            if (fixedRooms != null)
            {
                foreach (var fixedRoom in fixedRooms)
                {
                    Vector2Int rawSize = fixedRoom.Prefab.Size;
                    Vector2Int rotatedSize = (fixedRoom.Rotation == 1 || fixedRoom.Rotation == 3) ? new Vector2Int(rawSize.y, rawSize.x) : rawSize;
                    PlaceRoom(fixedRoom.Prefab, fixedRoom.Position, fixedRoom.Rotation, rotatedSize, fixedRoom.SkipVisuals, fixedRoom.OriginLayerIndex);
                }
            }

            // 2. Place Mandatory Rooms from Theme
            if (_dungeonTheme.MandatoryRooms != null)
            {
                foreach (var mandatoryPrefabObj in _dungeonTheme.MandatoryRooms)
                {
                    if (mandatoryPrefabObj == null) continue;
                    RoomTemplate template = mandatoryPrefabObj.GetComponent<RoomTemplate>();
                    if (template == null) continue;

                    // Próbujemy wstawić pokój obowiązkowy (dużo prób)
                    bool placed = TryPlaceSpecificRoom(template);
                    if (!placed) Debug.LogError($"Could not place MANDATORY room: {mandatoryPrefabObj.name} on Layer {_currentLayerIndex}");
                }
            }

            // 2. Place Random Rooms
            for (int i = 0; i < _roomCount; i++) TryPlaceRoom();

            // 3. Collect Nodes
            foreach (var room in _placedRooms)
            {
                room.CalculateDoorNodes(_currentLayerIndex);
                _allNodes.AddRange(room.Nodes);
            }

            // 4. Connect Graph
            ConnectDoorsGraph();

            // 5. Generate Paths
            GeneratePathfinding();

            // 6. Autotiling
            _activeDoorDirections.Clear();
            foreach (var node in _usedNodes)
            {
                if (!_activeDoorDirections.ContainsKey(node.GridPos))
                    _activeDoorDirections.Add(node.GridPos, node.ExitDirection);
            }

            // 7. Spawn
            SpawnWorld();
            SpawnDoorSeals();
        }

        [ContextMenu("Clear All")]
        public void ClearAllData()
        {
            if (_worldContainer != null) {
                for (int i = _worldContainer.childCount - 1; i >= 0; i--) DestroyImmediate(_worldContainer.GetChild(i).gameObject);
            }
            _occupiedCells.Clear(); _corridorCells.Clear(); _placedRooms.Clear();
            _allNodes.Clear(); _candidateEdges.Clear(); _finalEdges.Clear(); _usedNodes.Clear(); _activeDoorDirections.Clear();
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
                Vector2Int rawSize = randomPrefab.Size; // Bierzemy rozmiar z wylosowanego prefaba
                Vector2Int rotatedSize = (rotation == 1 || rotation == 3) ? new Vector2Int(rawSize.y, rawSize.x) : rawSize;

                int x = Random.Range(1, _gridWidth - rotatedSize.x - 1);
                int y = Random.Range(1, _gridHeight - rotatedSize.y - 1);
                Vector2Int potentialPos = new Vector2Int(x, y);

                if (IsPositionValid(potentialPos, rotatedSize))
                {
                    // BaseLayer = currentLayerIndex
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

        // --- GRAPH & PATHFINDING ---

        private void ConnectDoorsGraph()
        {
            if (_allNodes.Count < 2) return;

            _candidateEdges = DelaunayForDoors.Triangulate(_allNodes);
            _candidateEdges.RemoveAll(edge => 
            {
                if (edge.NodeA.ParentRoom == edge.NodeB.ParentRoom) return true; 
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
                RoomInstance cA = nodeToCluster[edge.NodeA]; RoomInstance cB = nodeToCluster[edge.NodeB];

                if (cA != cB)
                {
                    _finalEdges.Add(edge);
                    foreach (var n in clusterData[cB]) { nodeToCluster[n] = cA; clusterData[cA].Add(n); }
                    clusterData.Remove(cB);
                }
                else if (Random.value < _loopChance) _finalEdges.Add(edge);
            }
        }

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
                    if (hitRoom != null && hitRoom != a.ParentRoom && hitRoom != b.ParentRoom) return false; 
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
                if (pos.x >= r.GridPos.x && pos.x < r.GridPos.x + size.x && pos.y >= r.GridPos.y && pos.y < r.GridPos.y + size.y) return r;
            }
            return null;
        }

        private void GeneratePathfinding()
        {
            foreach (var edge in _finalEdges)
            {
                Vector2Int start = edge.NodeA.GetEntryTile(); Vector2Int end = edge.NodeB.GetEntryTile();
                if (IsOutOfBounds(start) || IsOutOfBounds(end)) continue;
                List<Vector2Int> path = FindPath(start, end);
                if (path != null)
                {
                    _usedNodes.Add(edge.NodeA); _usedNodes.Add(edge.NodeB);
                    _corridorCells.Add(start); _corridorCells.Add(end);
                    foreach (var p in path) if (!_occupiedCells.Contains(p)) _corridorCells.Add(p);
                }
            }
        }

        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, int> costSoFar = new Dictionary<Vector2Int, int>();
            var frontier = new SimplePriorityQueue<Vector2Int>();
            frontier.Enqueue(start, 0); cameFrom[start] = start; costSoFar[start] = 0;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                if (current == end) break;
                foreach (Vector2Int next in GetNeighbors(current))
                {
                    if (IsOutOfBounds(next) || _occupiedCells.Contains(next)) continue;
                    int moveCost = _corridorCells.Contains(next) ? _existingPathCost : _diggingCost;
                    int newCost = costSoFar[current] + moveCost;
                    if (current != start) {
                        Vector2Int parent = cameFrom[current]; if ((current - parent) != (next - current)) newCost += _turnCost;
                    }
                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next]) {
                        costSoFar[next] = newCost; frontier.Enqueue(next, newCost + GetManhattanDistance(next, end)); cameFrom[next] = current;
                    }
                }
            }
            if (!cameFrom.ContainsKey(end)) return null;
            List<Vector2Int> path = new List<Vector2Int>(); Vector2Int curr = end;
            while (curr != start) { path.Add(curr); curr = cameFrom[curr]; }
            path.Add(start); path.Reverse(); return path;
        }

        private IEnumerable<Vector2Int> GetNeighbors(Vector2Int p) { yield return p+Vector2Int.up; yield return p+Vector2Int.down; yield return p+Vector2Int.left; yield return p+Vector2Int.right; }
        private int GetManhattanDistance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        private bool IsOutOfBounds(Vector2Int p) => p.x < 0 || p.x >= _gridWidth || p.y < 0 || p.y >= _gridHeight;

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
            if (_dungeonTheme == null || _dungeonTheme.BlockedDoor == null) return; // Zmiana tutaj

            foreach (var node in _allNodes)
            {
                if (!_usedNodes.Contains(node))
                {
                    Vector3 worldPos = new Vector3((node.GridPos.x + 0.5f) * _unitSize, _verticalOffset, (node.GridPos.y + 0.5f) * _unitSize);
                    Vector3 dirVec = new Vector3(node.ExitDirection.x, 0, node.ExitDirection.y);
                    Quaternion rotation = Quaternion.LookRotation(dirVec);
                    // Zmiana tutaj:
                    Instantiate(_dungeonTheme.BlockedDoor, worldPos, rotation, _worldContainer);
                }
            }
        }

        private void SpawnCorridorTile(Vector2Int pos)
        {
            if (_dungeonTheme == null) return; // Safety check

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
                // Fallback: Jeśli nie ma DeadEnd, użyj Straight
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

        public class SimplePriorityQueue<T> { List<KeyValuePair<T, int>> e=new(); public int Count=>e.Count; public void Enqueue(T i, int p){e.Add(new(i,p));} public T Dequeue(){int b=0;for(int i=0;i<e.Count;i++)if(e[i].Value<e[b].Value)b=i;T r=e[b].Key;e.RemoveAt(b);return r;}}
    }

    public static class DelaunayForDoors
    {
        public class Vertex { public Vector2 Position; public DoorNode NodeRef; public Vertex(Vector2 pos, DoorNode node) { Position = pos; NodeRef = node; } }
        public class Triangle { public Vertex A, B, C; public Triangle(Vertex a, Vertex b, Vertex c) { A = a; B = b; C = c; } 
            public bool ContainsInCircumcircle(Vector2 p) { 
                float ax = A.Position.x, ay = A.Position.y; float bx = B.Position.x, by = B.Position.y; float cx = C.Position.x, cy = C.Position.y;
                float D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by)); float Ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / D; float Uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / D;
                float rSq = (ax - Ux) * (ax - Ux) + (ay - Uy) * (ay - Uy); float dSq = (p.x - Ux) * (p.x - Ux) + (p.y - Uy) * (p.y - Uy); return dSq <= rSq;
            } 
        }
        public class Edge { public Vertex U, V; public Edge(Vertex u, Vertex v) { U = u; V = v; } public override bool Equals(object obj) => obj is Edge e && ((U == e.U && V == e.V) || (U == e.V && V == e.U)); public override int GetHashCode() => U.GetHashCode() ^ V.GetHashCode(); }

        public static List<DoorEdge> Triangulate(List<DoorNode> nodes)
        {
            if (nodes.Count < 3) return ConvertLine(nodes);
            List<Vertex> vertices = nodes.Select(n => new Vertex(n.GridPos, n)).ToList();
            float minX = vertices.Min(v => v.Position.x); float minY = vertices.Min(v => v.Position.y); float maxX = vertices.Max(v => v.Position.x); float maxY = vertices.Max(v => v.Position.y); float dx = maxX - minX; float dy = maxY - minY; float deltaMax = Mathf.Max(dx, dy) * 2;
            Vertex p1 = new Vertex(new Vector2(minX - 1, minY - 1), null); Vertex p2 = new Vertex(new Vector2(minX - 1, maxY + deltaMax), null); Vertex p3 = new Vertex(new Vector2(maxX + deltaMax, minY - 1), null);
            List<Triangle> triangles = new List<Triangle> { new Triangle(p1, p2, p3) };
            foreach (var vertex in vertices) {
                List<Triangle> badTriangles = new List<Triangle>(); foreach (var t in triangles) if (t.ContainsInCircumcircle(vertex.Position)) badTriangles.Add(t);
                List<Edge> polygon = new List<Edge>(); foreach (var t in badTriangles) { AddPolygonEdge(polygon, new Edge(t.A, t.B)); AddPolygonEdge(polygon, new Edge(t.B, t.C)); AddPolygonEdge(polygon, new Edge(t.C, t.A)); }
                foreach (var t in badTriangles) triangles.Remove(t); foreach (var edge in polygon) triangles.Add(new Triangle(edge.U, edge.V, vertex));
            }
            HashSet<DoorEdge> resultEdges = new HashSet<DoorEdge>();
            foreach (var t in triangles) {
                bool hasP1 = t.A == p1 || t.B == p1 || t.C == p1; bool hasP2 = t.A == p2 || t.B == p2 || t.C == p2; bool hasP3 = t.A == p3 || t.B == p3 || t.C == p3;
                if (!hasP1 && !hasP2 && !hasP3) { AddDoorEdge(resultEdges, t.A, t.B); AddDoorEdge(resultEdges, t.B, t.C); AddDoorEdge(resultEdges, t.C, t.A); }
            }
            return resultEdges.ToList();
        }
        private static void AddPolygonEdge(List<Edge> polygon, Edge edge) { var existing = polygon.FirstOrDefault(e => e.Equals(edge)); if (existing != null) polygon.Remove(existing); else polygon.Add(edge); }
        private static void AddDoorEdge(HashSet<DoorEdge> set, Vertex u, Vertex v) { if (u.NodeRef != null && v.NodeRef != null) set.Add(new DoorEdge(u.NodeRef, v.NodeRef)); }
        private static List<DoorEdge> ConvertLine(List<DoorNode> nodes) { List<DoorEdge> edges = new List<DoorEdge>(); for (int i = 0; i < nodes.Count - 1; i++) edges.Add(new DoorEdge(nodes[i], nodes[i+1])); return edges; }
    }
}