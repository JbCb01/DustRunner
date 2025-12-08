using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    // --- DATA CLASSES ---
    [System.Serializable]
    public class RoomConfig
    {
        public string ID;
        public RoomTemplate Prefab;
        public int SpawnWeight = 1;
    }

    [System.Serializable]
    public class CorridorTileSet
    {
        public GameObject Straight;
        public GameObject Corner;
        public GameObject TJunction;
        public GameObject Cross;
        public GameObject DeadEnd;
        public GameObject FloorOnly;
        [Header("Props")]
        public GameObject BlockedDoor; 
    }

    // Klasa reprezentująca węzeł w grafie (konkretne drzwi)
    public class DoorNode
    {
        public RoomInstance ParentRoom;
        public Vector2Int GridPos;      
        public Vector2Int ExitDirection;
        public string ID;

        public DoorNode(RoomInstance parent, Vector2Int pos, Vector2Int dir)
        {
            ParentRoom = parent;
            GridPos = pos;
            ExitDirection = dir;
            ID = System.Guid.NewGuid().ToString();
        }

        public Vector2Int GetEntryTile() => GridPos + ExitDirection;
    }

    public class RoomInstance
    {
        public RoomTemplate PrefabSource;
        public Vector2Int GridPos;
        public int RotationIndex;
        public string GUID = System.Guid.NewGuid().ToString();
        public List<DoorNode> Nodes = new List<DoorNode>();

        public RoomInstance(RoomTemplate prefab, Vector2Int pos, int rotation)
        {
            PrefabSource = prefab;
            GridPos = pos;
            RotationIndex = rotation;
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

        public void CalculateDoorNodes()
        {
            Nodes.Clear();
            if (PrefabSource.Doors == null) return;

            Vector2Int originalSize = PrefabSource.Size;

            foreach (var doorDef in PrefabSource.Doors)
            {
                Vector2Int localPos = doorDef.Position;
                Vector2Int rotatedOffset = Vector2Int.zero;
                Vector2Int rotatedDir = Vector2Int.zero;

                // 1. Position Rotation
                switch (RotationIndex)
                {
                    case 0: rotatedOffset = localPos; break;
                    case 1: rotatedOffset = new Vector2Int(localPos.y, originalSize.x - 1 - localPos.x); break;
                    case 2: rotatedOffset = new Vector2Int(originalSize.x - 1 - localPos.x, originalSize.y - 1 - localPos.y); break;
                    case 3: rotatedOffset = new Vector2Int(originalSize.y - 1 - localPos.y, localPos.x); break;
                }

                // 2. Direction Rotation
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

        // Helper to prevent duplicates (A-B is same as B-A)
        public override bool Equals(object obj)
        {
            if (obj is DoorEdge other)
            {
                return (NodeA == other.NodeA && NodeB == other.NodeB) || 
                       (NodeA == other.NodeB && NodeB == other.NodeA);
            }
            return false;
        }
        public override int GetHashCode() => NodeA.GetHashCode() ^ NodeB.GetHashCode();
    }

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
        
        // --- SEED SETTINGS ---
        [Tooltip("If true, a new random seed is generated every time.")]
        [SerializeField] private bool _useRandomSeed = true;
        [Tooltip("Manually set seed for reproducible results.")]
        [SerializeField] private int _seed = 12345;

        [Header("Pathfinding Costs")]
        [SerializeField] private int _diggingCost = 5; 
        [SerializeField] private int _existingPathCost = 1; 
        [SerializeField] private int _turnCost = 10;

        [Header("Assets")]
        [SerializeField] private CorridorTileSet _corridorTiles;
        [SerializeField] private List<RoomConfig> _availableRooms;

        [Header("Debug Visualization")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private bool _showGraph = true; 
        [SerializeField] private bool _showLogic = true; 

        // --- STATE DATA ---
        private HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> _corridorCells = new HashSet<Vector2Int>();
        private List<RoomInstance> _placedRooms = new List<RoomInstance>();
        
        private List<DoorNode> _allNodes = new List<DoorNode>();
        private List<DoorEdge> _candidateEdges = new List<DoorEdge>(); 
        private List<DoorEdge> _finalEdges = new List<DoorEdge>();     
        private HashSet<DoorNode> _usedNodes = new HashSet<DoorNode>(); 
        
        // Dictionary to store active door positions AND their outward direction
        private Dictionary<Vector2Int, Vector2Int> _activeDoorDirections = new Dictionary<Vector2Int, Vector2Int>();

        [ContextMenu("Generate & Spawn")]
        public void Generate()
        {
            ClearAll();
            if (_availableRooms == null || _availableRooms.Count == 0) return;

            // 0. Initialize RNG
            if (_useRandomSeed)
            {
                _seed = (int)System.DateTime.Now.Ticks;
            }
            Random.InitState(_seed);
            Debug.Log($"<color=yellow>Generating Dungeon with Seed: <b>{_seed}</b></color>");

            // 1. Place Rooms on Grid
            for (int i = 0; i < _roomCount; i++) TryPlaceRoom();

            // 2. Collect Nodes & Calculate global positions
            foreach (var room in _placedRooms)
            {
                room.CalculateDoorNodes();
                _allNodes.AddRange(room.Nodes);
            }

            // 3. Graph Logic (The "Door Graph" algorithm)
            ConnectDoorsGraph();

            // 4. Physical Paths
            GeneratePathfinding();

            // 5. Prepare Autotiling Data
            _activeDoorDirections.Clear();
            foreach (var node in _usedNodes)
            {
                if (!_activeDoorDirections.ContainsKey(node.GridPos))
                {
                    _activeDoorDirections.Add(node.GridPos, node.ExitDirection);
                }
            }

            // 6. Spawning
            SpawnWorld();
            SpawnDoorSeals();

            Debug.Log($"Generation Complete. Rooms: {_placedRooms.Count}, Corridors: {_finalEdges.Count}");
        }

        [ContextMenu("Clear All Data")]
        public void ClearAll()
        {
            if (_worldContainer != null) {
                int childCount = _worldContainer.childCount;
                for (int i = childCount - 1; i >= 0; i--) DestroyImmediate(_worldContainer.GetChild(i).gameObject);
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

        // --- STEP 1: PLACEMENT ---
        private void TryPlaceRoom()
        {
            RoomConfig config = _availableRooms[Random.Range(0, _availableRooms.Count)];
            if (config.Prefab == null) return;

            for (int attempt = 0; attempt < _maxPlacementAttempts; attempt++)
            {
                int rotation = Random.Range(0, 4);
                Vector2Int rawSize = config.Prefab.Size;
                Vector2Int rotatedSize = (rotation == 1 || rotation == 3) ? new Vector2Int(rawSize.y, rawSize.x) : rawSize;

                int x = Random.Range(1, _gridWidth - rotatedSize.x - 1);
                int y = Random.Range(1, _gridHeight - rotatedSize.y - 1);
                Vector2Int potentialPos = new Vector2Int(x, y);

                if (IsPositionValid(potentialPos, rotatedSize))
                {
                    PlaceRoom(config.Prefab, potentialPos, rotation, rotatedSize);
                    return;
                }
            }
        }

        private bool IsPositionValid(Vector2Int pos, Vector2Int size)
        {
            int xStart = pos.x - 1; int xEnd = pos.x + size.x + 1;
            int yStart = pos.y - 1; int yEnd = pos.y + size.y + 1;
            for (int x = xStart; x < xEnd; x++) {
                for (int y = yStart; y < yEnd; y++) {
                    if (_occupiedCells.Contains(new Vector2Int(x, y))) return false;
                }
            }
            return true;
        }

        private void PlaceRoom(RoomTemplate prefab, Vector2Int pos, int rotation, Vector2Int actualSize)
        {
            RoomInstance newRoom = new RoomInstance(prefab, pos, rotation);
            _placedRooms.Add(newRoom);
            for (int x = 0; x < actualSize.x; x++) {
                for (int y = 0; y < actualSize.y; y++) {
                    _occupiedCells.Add(new Vector2Int(pos.x + x, pos.y + y));
                }
            }
        }

        // --- STEP 3: DOOR GRAPH CONNECTION ---
        private void ConnectDoorsGraph()
        {
            if (_allNodes.Count < 2) return;

            // A. Full Bowyer-Watson Delaunay Triangulation
            _candidateEdges = DelaunayForDoors.Triangulate(_allNodes);

            // B. Filter Edges (Visibility Check & Internal Check)
            _candidateEdges.RemoveAll(edge => 
            {
                if (edge.NodeA.ParentRoom == edge.NodeB.ParentRoom) return true; 
                if (!IsLineClear(edge.NodeA, edge.NodeB)) return true; 
                return false;
            });

            // C. Kruskal's MST
            Dictionary<DoorNode, RoomInstance> nodeToCluster = new Dictionary<DoorNode, RoomInstance>();
            Dictionary<RoomInstance, HashSet<DoorNode>> clusterData = new Dictionary<RoomInstance, HashSet<DoorNode>>();

            foreach (var room in _placedRooms)
            {
                clusterData[room] = new HashSet<DoorNode>();
                foreach (var node in room.Nodes)
                {
                    nodeToCluster[node] = room;
                    clusterData[room].Add(node);
                }
            }

            _candidateEdges = _candidateEdges.OrderBy(x => x.Distance).ToList();

            foreach (var edge in _candidateEdges)
            {
                RoomInstance clusterA = nodeToCluster[edge.NodeA];
                RoomInstance clusterB = nodeToCluster[edge.NodeB];

                if (clusterA != clusterB)
                {
                    _finalEdges.Add(edge);
                    foreach (var nodeInB in clusterData[clusterB])
                    {
                        nodeToCluster[nodeInB] = clusterA;
                        clusterData[clusterA].Add(nodeInB);
                    }
                    clusterData.Remove(clusterB);
                }
                else
                {
                    if (Random.value < _loopChance) _finalEdges.Add(edge);
                }
            }
        }

        private bool IsLineClear(DoorNode a, DoorNode b)
        {
            Vector2Int start = a.GetEntryTile();
            Vector2Int end = b.GetEntryTile();

            int x0 = start.x; int y0 = start.y;
            int x1 = end.x; int y1 = end.y;
            int dx = Mathf.Abs(x1 - x0); int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0); int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                Vector2Int p = new Vector2Int(x0, y0);
                
                if (_occupiedCells.Contains(p))
                {
                    RoomInstance hitRoom = GetRoomAt(p);
                    if (hitRoom != null && hitRoom != a.ParentRoom && hitRoom != b.ParentRoom)
                    {
                        return false; 
                    }
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
            return true;
        }

        private RoomInstance GetRoomAt(Vector2Int pos)
        {
            foreach(var r in _placedRooms)
            {
                Vector2Int size = r.GetRotatedSize();
                if (pos.x >= r.GridPos.x && pos.x < r.GridPos.x + size.x &&
                    pos.y >= r.GridPos.y && pos.y < r.GridPos.y + size.y)
                {
                    return r;
                }
            }
            return null;
        }

        // --- STEP 4: PATHFINDING ---
        private void GeneratePathfinding()
        {
            foreach (var edge in _finalEdges)
            {
                Vector2Int start = edge.NodeA.GetEntryTile();
                Vector2Int end = edge.NodeB.GetEntryTile();

                if (IsOutOfBounds(start) || IsOutOfBounds(end)) continue;

                List<Vector2Int> path = FindPath(start, end);
                if (path != null)
                {
                    _usedNodes.Add(edge.NodeA);
                    _usedNodes.Add(edge.NodeB);

                    _corridorCells.Add(start);
                    _corridorCells.Add(end);
                    foreach (var p in path) if (!_occupiedCells.Contains(p)) _corridorCells.Add(p);
                }
                else
                {
                    Debug.LogWarning("Failed to pathfind generated edge. Obstacle?");
                }
            }
        }

        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
        {
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, int> costSoFar = new Dictionary<Vector2Int, int>();
            var frontier = new SimplePriorityQueue<Vector2Int>();
            
            frontier.Enqueue(start, 0);
            cameFrom[start] = start;
            costSoFar[start] = 0;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                if (current == end) break;

                foreach (Vector2Int next in GetNeighbors(current))
                {
                    if (IsOutOfBounds(next)) continue;
                    if (_occupiedCells.Contains(next)) continue; 

                    int moveCost = _diggingCost; 
                    if (_corridorCells.Contains(next)) moveCost = _existingPathCost;

                    int newCost = costSoFar[current] + moveCost;

                    if (current != start)
                    {
                        Vector2Int parent = cameFrom[current];
                        if ((current - parent) != (next - current)) newCost += _turnCost;
                    }

                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        frontier.Enqueue(next, newCost + GetManhattanDistance(next, end));
                        cameFrom[next] = current;
                    }
                }
            }
            if (!cameFrom.ContainsKey(end)) return null;
            
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int curr = end;
            while (curr != start) { path.Add(curr); curr = cameFrom[curr]; }
            path.Add(start);
            path.Reverse();
            return path;
        }

        // --- STEP 5: SPAWNING ---
        private void SpawnWorld()
        {
            if (_worldContainer == null) return;

            foreach (var room in _placedRooms)
            {
                Vector3 worldPos = new Vector3(room.GridPos.x * _unitSize, 0, room.GridPos.y * _unitSize);
                Quaternion rotation = Quaternion.Euler(0, room.RotationIndex * 90, 0);
                Vector3 offset = Vector3.zero;

                switch (room.RotationIndex) {
                    case 0: offset = Vector3.zero; break;
                    case 1: offset = new Vector3(0, 0, room.PrefabSource.Size.x * _unitSize); break;
                    case 2: offset = new Vector3(room.PrefabSource.Size.x * _unitSize, 0, room.PrefabSource.Size.y * _unitSize); break;
                    case 3: offset = new Vector3(room.PrefabSource.Size.y * _unitSize, 0, 0); break;
                }
                Instantiate(room.PrefabSource.gameObject, worldPos + offset, rotation, _worldContainer);
            }

            foreach (Vector2Int pos in _corridorCells) SpawnCorridorTile(pos);
        }

        private void SpawnDoorSeals()
        {
            if (_corridorTiles.BlockedDoor == null) return;

            foreach (var node in _allNodes)
            {
                if (!_usedNodes.Contains(node))
                {
                    Vector3 worldPos = new Vector3((node.GridPos.x + 0.5f) * _unitSize, 0, (node.GridPos.y + 0.5f) * _unitSize);
                    Vector3 dirVec = new Vector3(node.ExitDirection.x, 0, node.ExitDirection.y);
                    Quaternion rotation = Quaternion.LookRotation(dirVec);
                    Instantiate(_corridorTiles.BlockedDoor, worldPos, rotation, _worldContainer);
                }
            }
        }

        private void SpawnCorridorTile(Vector2Int pos)
        {
            bool up = ShouldConnect(pos + Vector2Int.up, Vector2Int.up);
            bool down = ShouldConnect(pos + Vector2Int.down, Vector2Int.down);
            bool left = ShouldConnect(pos + Vector2Int.left, Vector2Int.left);
            bool right = ShouldConnect(pos + Vector2Int.right, Vector2Int.right);

            GameObject prefab = _corridorTiles.FloorOnly;
            float rot = 0;
            int c = (up?1:0)+(down?1:0)+(left?1:0)+(right?1:0);

            if (c == 4) prefab = _corridorTiles.Cross;
            else if (c == 3) {
                prefab = _corridorTiles.TJunction;
                if (!down) rot = 0; else if (!left) rot = 90; else if (!up) rot = 180; else rot = 270;
            } else if (c == 2) {
                if (up && down) { prefab = _corridorTiles.Straight; rot = 0; }
                else if (left && right) { prefab = _corridorTiles.Straight; rot = 90; }
                else {
                    prefab = _corridorTiles.Corner;
                    if (up&&right) rot = 0; else if (right&&down) rot = 90; else if (down&&left) rot = 180; else rot = 270;
                }
            } else {
                prefab = _corridorTiles.DeadEnd ?? _corridorTiles.Straight;
                if(up) rot=0; else if(right) rot=90; else if(down) rot=180; else rot=270;
            }
            if (prefab != null) Instantiate(prefab, new Vector3((pos.x+0.5f)*_unitSize, 0, (pos.y+0.5f)*_unitSize), Quaternion.Euler(0,rot,0), _worldContainer);
        }

        private bool ShouldConnect(Vector2Int targetPos, Vector2Int directionToTarget)
        {
            if (_corridorCells.Contains(targetPos)) return true;
            if (_occupiedCells.Contains(targetPos))
            {
                if (_activeDoorDirections.TryGetValue(targetPos, out Vector2Int doorExitDir))
                {
                    return doorExitDir == -directionToTarget;
                }
            }
            return false;
        }

        // Helpers
        private bool IsSolid(Vector2Int pos) => _corridorCells.Contains(pos) || _occupiedCells.Contains(pos);
        private IEnumerable<Vector2Int> GetNeighbors(Vector2Int p) { yield return p+Vector2Int.up; yield return p+Vector2Int.down; yield return p+Vector2Int.left; yield return p+Vector2Int.right; }
        private int GetManhattanDistance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        private bool IsOutOfBounds(Vector2Int p) => p.x < 0 || p.x >= _gridWidth || p.y < 0 || p.y >= _gridHeight;
        
        public class SimplePriorityQueue<T> {
            private List<KeyValuePair<T, int>> _elements = new List<KeyValuePair<T, int>>();
            public int Count => _elements.Count;
            public void Enqueue(T item, int priority) { _elements.Add(new KeyValuePair<T, int>(item, priority)); }
            public T Dequeue() {
                int bestIndex = 0;
                for (int i = 0; i < _elements.Count; i++) if (_elements[i].Value < _elements[bestIndex].Value) bestIndex = i;
                T bestItem = _elements[bestIndex].Key; _elements.RemoveAt(bestIndex); return bestItem;
            }
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;
            if (_showLogic) {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                foreach (var r in _placedRooms) {
                    Vector3 c = r.GetWorldCenter(_unitSize); Vector2Int s = r.GetRotatedSize();
                    Gizmos.DrawWireCube(c, new Vector3(s.x * _unitSize, 2, s.y * _unitSize));
                }
                Gizmos.color = Color.cyan;
                foreach (var c in _corridorCells) Gizmos.DrawCube(new Vector3((c.x+0.5f)*_unitSize, 0, (c.y+0.5f)*_unitSize), Vector3.one * _unitSize * 0.9f);
            }
            if (_showGraph && _candidateEdges != null) {
                // Show Delaunay (faint)
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.1f);
                foreach (var e in _candidateEdges) Gizmos.DrawLine(new Vector3((e.NodeA.GridPos.x+0.5f)*_unitSize, 2, (e.NodeA.GridPos.y+0.5f)*_unitSize), new Vector3((e.NodeB.GridPos.x+0.5f)*_unitSize, 2, (e.NodeB.GridPos.y+0.5f)*_unitSize));
            }
            if (_showGraph && _finalEdges != null) {
                // Show MST (green)
                Gizmos.color = Color.green;
                foreach (var e in _finalEdges) Gizmos.DrawLine(new Vector3((e.NodeA.GridPos.x+0.5f)*_unitSize, 2.1f, (e.NodeA.GridPos.y+0.5f)*_unitSize), new Vector3((e.NodeB.GridPos.x+0.5f)*_unitSize, 2.1f, (e.NodeB.GridPos.y+0.5f)*_unitSize));
            }
        }
    }

    // --- PEŁNA IMPLEMENTACJA BOWYER-WATSON (Triangulacja) ---
    public static class DelaunayForDoors
    {
        public class Vertex { public Vector2 Position; public DoorNode NodeRef; public Vertex(Vector2 pos, DoorNode node) { Position = pos; NodeRef = node; } }
        public class Triangle { public Vertex A, B, C; public Triangle(Vertex a, Vertex b, Vertex c) { A = a; B = b; C = c; } 
            public bool ContainsInCircumcircle(Vector2 p) { 
                float ax = A.Position.x, ay = A.Position.y; float bx = B.Position.x, by = B.Position.y; float cx = C.Position.x, cy = C.Position.y;
                float D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
                float Ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / D;
                float Uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / D;
                float rSq = (ax - Ux) * (ax - Ux) + (ay - Uy) * (ay - Uy); float dSq = (p.x - Ux) * (p.x - Ux) + (p.y - Uy) * (p.y - Uy);
                return dSq <= rSq;
            } 
        }
        public class Edge { public Vertex U, V; public Edge(Vertex u, Vertex v) { U = u; V = v; } public override bool Equals(object obj) { if (obj is Edge e) return (U == e.U && V == e.V) || (U == e.V && V == e.U); return false; } public override int GetHashCode() => U.GetHashCode() ^ V.GetHashCode(); }

        public static List<DoorEdge> Triangulate(List<DoorNode> nodes)
        {
            if (nodes.Count < 3) return ConvertLine(nodes);

            List<Vertex> vertices = nodes.Select(n => new Vertex(n.GridPos, n)).ToList();
            
            float minX = vertices.Min(v => v.Position.x);
            float minY = vertices.Min(v => v.Position.y);
            float maxX = vertices.Max(v => v.Position.x);
            float maxY = vertices.Max(v => v.Position.y);
            float dx = maxX - minX; float dy = maxY - minY;
            float deltaMax = Mathf.Max(dx, dy) * 2;
            
            Vertex p1 = new Vertex(new Vector2(minX - 1, minY - 1), null);
            Vertex p2 = new Vertex(new Vector2(minX - 1, maxY + deltaMax), null);
            Vertex p3 = new Vertex(new Vector2(maxX + deltaMax, minY - 1), null);
            
            List<Triangle> triangles = new List<Triangle>();
            triangles.Add(new Triangle(p1, p2, p3));

            foreach (var vertex in vertices)
            {
                List<Triangle> badTriangles = new List<Triangle>();
                foreach (var t in triangles)
                {
                    if (t.ContainsInCircumcircle(vertex.Position)) badTriangles.Add(t);
                }

                List<Edge> polygon = new List<Edge>();
                foreach (var t in badTriangles)
                {
                    AddPolygonEdge(polygon, new Edge(t.A, t.B));
                    AddPolygonEdge(polygon, new Edge(t.B, t.C));
                    AddPolygonEdge(polygon, new Edge(t.C, t.A));
                }

                foreach (var t in badTriangles) triangles.Remove(t);

                foreach (var edge in polygon)
                {
                    triangles.Add(new Triangle(edge.U, edge.V, vertex));
                }
            }

            HashSet<DoorEdge> resultEdges = new HashSet<DoorEdge>();
            foreach (var t in triangles)
            {
                bool hasP1 = t.A == p1 || t.B == p1 || t.C == p1;
                bool hasP2 = t.A == p2 || t.B == p2 || t.C == p2;
                bool hasP3 = t.A == p3 || t.B == p3 || t.C == p3;

                if (!hasP1 && !hasP2 && !hasP3)
                {
                    AddDoorEdge(resultEdges, t.A, t.B);
                    AddDoorEdge(resultEdges, t.B, t.C);
                    AddDoorEdge(resultEdges, t.C, t.A);
                }
            }

            return resultEdges.ToList();
        }

        private static void AddPolygonEdge(List<Edge> polygon, Edge edge)
        {
            var existing = polygon.FirstOrDefault(e => e.Equals(edge));
            if (existing != null) polygon.Remove(existing);
            else polygon.Add(edge);
        }

        private static void AddDoorEdge(HashSet<DoorEdge> set, Vertex u, Vertex v)
        {
            if (u.NodeRef != null && v.NodeRef != null)
            {
                set.Add(new DoorEdge(u.NodeRef, v.NodeRef));
            }
        }

        private static List<DoorEdge> ConvertLine(List<DoorNode> nodes)
        {
            List<DoorEdge> edges = new List<DoorEdge>();
            for (int i = 0; i < nodes.Count - 1; i++) edges.Add(new DoorEdge(nodes[i], nodes[i+1]));
            return edges;
        }
    }
}