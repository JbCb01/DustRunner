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
    }

    public class RoomInstance
    {
        public RoomTemplate PrefabSource;
        public Vector2Int GridPos;
        public int RotationIndex;
        public string GUID = System.Guid.NewGuid().ToString();

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

        public Vector2Int GetCenterGrid()
        {
            Vector2Int size = GetRotatedSize();
            return GridPos + size / 2;
        }

        public Vector2Int GetClosestPointOnBorder(Vector2Int target)
        {
            Vector2Int size = GetRotatedSize();
            int minX = GridPos.x; int maxX = GridPos.x + size.x - 1;
            int minY = GridPos.y; int maxY = GridPos.y + size.y - 1;

            int x = Mathf.Clamp(target.x, minX, maxX);
            int y = Mathf.Clamp(target.y, minY, maxY);

            int dl = Mathf.Abs(x - minX); int dr = Mathf.Abs(x - maxX);
            int db = Mathf.Abs(y - minY); int dt = Mathf.Abs(y - maxY);
            int min = Mathf.Min(dl, dr, db, dt);

            if (min == dl) return new Vector2Int(minX, y);
            if (min == dr) return new Vector2Int(maxX, y);
            if (min == db) return new Vector2Int(x, minY);
            return new Vector2Int(x, maxY);
        }
    }

    public class RoomEdge
    {
        public RoomInstance RoomA;
        public RoomInstance RoomB;
        public float Distance;
        public RoomEdge(RoomInstance a, RoomInstance b) { RoomA = a; RoomB = b; Distance = Vector2Int.Distance(a.GridPos, b.GridPos); }
        public override bool Equals(object obj)
        {
            if (obj is RoomEdge other) return (RoomA == other.RoomA && RoomB == other.RoomB) || (RoomA == other.RoomB && RoomB == other.RoomA);
            return false;
        }
        public override int GetHashCode() => RoomA.GetHashCode() ^ RoomB.GetHashCode();
    }

    // --- MAIN GENERATOR ---
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

        [Header("Pathfinding Costs")]
        [Tooltip("Cost to dig a new tile. High value = prefers existing paths.")]
        [SerializeField] private int _diggingCost = 5; 
        [Tooltip("Cost to walk on existing corridor.")]
        [SerializeField] private int _existingPathCost = 1; 
        [Tooltip("Penalty for making a 90 degree turn.")]
        [SerializeField] private int _turnCost = 10;

        [Header("Assets")]
        [SerializeField] private CorridorTileSet _corridorTiles;
        [SerializeField] private List<RoomConfig> _availableRooms;

        [Header("Debug Visualization")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private bool _showGridBounds = true;
        [SerializeField] private bool _showRoomLogic = true;
        [SerializeField] private bool _showCorridorLogic = true;
        [SerializeField] private bool _showDelaunay = true;
        [SerializeField] private bool _showMST = true;

        // Data
        private HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> _corridorCells = new HashSet<Vector2Int>();
        private List<RoomInstance> _placedRooms = new List<RoomInstance>();
        private List<RoomEdge> _delaunayEdges = new List<RoomEdge>();
        private List<RoomEdge> _mstEdges = new List<RoomEdge>();

        [ContextMenu("Generate & Spawn")]
        public void Generate()
        {
            ClearAll();
            if (_availableRooms == null || _availableRooms.Count == 0) return;

            for (int i = 0; i < _roomCount; i++) TryPlaceRoom();
            ConnectRooms();
            GenerateCorridors();
            SpawnWorld();

            Debug.Log($"Generation Complete. Rooms: {_placedRooms.Count}");
        }

        [ContextMenu("Clear All Data")]
        public void ClearAll()
        {
            if (_worldContainer != null)
            {
                int childCount = _worldContainer.childCount;
                for (int i = childCount - 1; i >= 0; i--) DestroyImmediate(_worldContainer.GetChild(i).gameObject);
            }
            _occupiedCells.Clear();
            _corridorCells.Clear();
            _placedRooms.Clear();
            _delaunayEdges.Clear();
            _mstEdges.Clear();
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

            for (int x = xStart; x < xEnd; x++)
            {
                for (int y = yStart; y < yEnd; y++)
                {
                    if (_occupiedCells.Contains(new Vector2Int(x, y))) return false;
                }
            }
            return true;
        }

        private void PlaceRoom(RoomTemplate prefab, Vector2Int pos, int rotation, Vector2Int actualSize)
        {
            RoomInstance newRoom = new RoomInstance(prefab, pos, rotation);
            _placedRooms.Add(newRoom);
            for (int x = 0; x < actualSize.x; x++)
            {
                for (int y = 0; y < actualSize.y; y++)
                {
                    _occupiedCells.Add(new Vector2Int(pos.x + x, pos.y + y));
                }
            }
        }

        // --- STEP 2: CONNECTIVITY ---
        private void ConnectRooms()
        {
            _delaunayEdges = Delaunay2D.Triangulate(_placedRooms);
            List<RoomEdge> sortedEdges = _delaunayEdges.OrderBy(x => x.Distance).ToList();
            
            Dictionary<RoomInstance, HashSet<RoomInstance>> sets = new Dictionary<RoomInstance, HashSet<RoomInstance>>();
            foreach (var r in _placedRooms) sets[r] = new HashSet<RoomInstance>() { r };

            foreach (var edge in sortedEdges)
            {
                HashSet<RoomInstance> setA = sets[edge.RoomA];
                HashSet<RoomInstance> setB = sets[edge.RoomB];

                if (setA != setB)
                {
                    _mstEdges.Add(edge);
                    foreach (var r in setB) { setA.Add(r); sets[r] = setA; }
                }
                else
                {
                    if (Random.value < _loopChance) _mstEdges.Add(edge);
                }
            }
        }

        // --- STEP 3: PATHFINDING (MERGING LOGIC ADDED) ---
        private void GenerateCorridors()
        {
            foreach (var edge in _mstEdges)
            {
                Vector2Int startPos = edge.RoomA.GetClosestPointOnBorder(edge.RoomB.GridPos);
                Vector2Int endPos = edge.RoomB.GetClosestPointOnBorder(startPos);
                
                List<Vector2Int> path = FindPath(startPos, endPos);
                if (path != null)
                {
                    foreach (var p in path)
                    {
                        if (!_occupiedCells.Contains(p)) _corridorCells.Add(p);
                    }
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
                    bool isRoom = _occupiedCells.Contains(next);
                    if (isRoom && next != end && next != start) continue;

                    int moveCost = _diggingCost; // Default: expensive to dig
                    if (_corridorCells.Contains(next))
                    {
                        moveCost = _existingPathCost;
                    }

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

        private void SpawnWorld()
        {
            if (_worldContainer == null) return;

            foreach (var room in _placedRooms)
            {
                // Base position on grid (Bottom-Left of the allocated rect)
                Vector3 worldPos = new Vector3(room.GridPos.x * _unitSize, 0, room.GridPos.y * _unitSize);
                
                Quaternion rotation = Quaternion.Euler(0, room.RotationIndex * 90, 0);
                
                // Calculate Offset for Bottom-Left Pivot Logic
                // We need to shift the pivot so the mesh falls back into the positive quadrant relative to GridPos
                Vector3 offset = Vector3.zero;

                switch (room.RotationIndex)
                {
                    case 0: 
                        // 0 deg: (X+, Z+) -> No offset needed
                        offset = Vector3.zero; 
                        break;
                    case 1: 
                        // 90 deg: Rotates Z+ axis to X+, X+ axis to Z-
                        // The mesh swings into negative Z. We need to push it back up by its original Width (Size.x).
                        offset = new Vector3(0, 0, room.PrefabSource.Size.x * _unitSize); 
                        break;
                    case 2: 
                        // 180 deg: (X-, Z-). We need to push it X (Width) and Z (Length).
                        offset = new Vector3(room.PrefabSource.Size.x * _unitSize, 0, room.PrefabSource.Size.y * _unitSize); 
                        break;
                    case 3: 
                        // 270 deg: Rotates Z+ to X-, X+ to Z+.
                        // The mesh swings into negative X. We need to push it right by its original Length (Size.y).
                        offset = new Vector3(room.PrefabSource.Size.y * _unitSize, 0, 0); 
                        break;
                }

                GameObject roomObj = Instantiate(room.PrefabSource.gameObject, worldPos + offset, rotation, _worldContainer);
                roomObj.name = $"{room.PrefabSource.name}_{room.GridPos}";
            }

            foreach (Vector2Int pos in _corridorCells)
            {
                SpawnCorridorTile(pos);
            }
        }

        private void SpawnCorridorTile(Vector2Int pos)
        {
            bool up = IsSolid(pos + Vector2Int.up);
            bool down = IsSolid(pos + Vector2Int.down);
            bool left = IsSolid(pos + Vector2Int.left);
            bool right = IsSolid(pos + Vector2Int.right);

            GameObject prefabToSpawn = _corridorTiles.FloorOnly;
            float yRotation = 0;

            int connectionCount = (up?1:0) + (down?1:0) + (left?1:0) + (right?1:0);

            if (connectionCount == 4) { prefabToSpawn = _corridorTiles.Cross; }
            else if (connectionCount == 3)
            {
                prefabToSpawn = _corridorTiles.TJunction;
                if (!down) yRotation = 0; else if (!left) yRotation = 90; else if (!up) yRotation = 180; else if (!right) yRotation = 270;
            }
            else if (connectionCount == 2)
            {
                if (up && down) { prefabToSpawn = _corridorTiles.Straight; yRotation = 0; }
                else if (left && right) { prefabToSpawn = _corridorTiles.Straight; yRotation = 90; }
                else
                {
                    prefabToSpawn = _corridorTiles.Corner;
                    if (up && right) yRotation = 0; else if (right && down) yRotation = 90; else if (down && left) yRotation = 180; else if (left && up) yRotation = 270;
                }
            }
            else if (connectionCount <= 1)
            {
                prefabToSpawn = _corridorTiles.DeadEnd != null ? _corridorTiles.DeadEnd : _corridorTiles.Straight;
                if (up) yRotation = 0; else if (right) yRotation = 90; else if (down) yRotation = 180; else if (left) yRotation = 270;
            }

            if (prefabToSpawn != null)
            {
                Vector3 worldPos = new Vector3(pos.x * _unitSize, 0, pos.y * _unitSize);
                Vector3 centerPos = worldPos + new Vector3(_unitSize * 0.5f, 0, _unitSize * 0.5f);
                Instantiate(prefabToSpawn, centerPos, Quaternion.Euler(0, yRotation, 0), _worldContainer);
            }
        }

        private bool IsSolid(Vector2Int pos) => _corridorCells.Contains(pos) || _occupiedCells.Contains(pos);
        private IEnumerable<Vector2Int> GetNeighbors(Vector2Int p)
        {
            yield return new Vector2Int(p.x + 1, p.y); yield return new Vector2Int(p.x - 1, p.y);
            yield return new Vector2Int(p.x, p.y + 1); yield return new Vector2Int(p.x, p.y - 1);
        }
        private int GetManhattanDistance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        private bool IsOutOfBounds(Vector2Int p) => p.x < 0 || p.x >= _gridWidth || p.y < 0 || p.y >= _gridHeight;

        public class SimplePriorityQueue<T>
        {
            private List<KeyValuePair<T, int>> _elements = new List<KeyValuePair<T, int>>();
            public int Count => _elements.Count;
            public void Enqueue(T item, int priority) { _elements.Add(new KeyValuePair<T, int>(item, priority)); }
            public T Dequeue()
            {
                int bestIndex = 0;
                for (int i = 0; i < _elements.Count; i++) if (_elements[i].Value < _elements[bestIndex].Value) bestIndex = i;
                T bestItem = _elements[bestIndex].Key; _elements.RemoveAt(bestIndex); return bestItem;
            }
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            if (_showGridBounds)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.1f);
                Gizmos.DrawWireCube(new Vector3(_gridWidth * _unitSize / 2, 0, _gridHeight * _unitSize / 2), new Vector3(_gridWidth * _unitSize, 1, _gridHeight * _unitSize));
            }

            if (_showDelaunay && _delaunayEdges != null)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                foreach (var edge in _delaunayEdges) Gizmos.DrawLine(edge.RoomA.GetWorldCenter(_unitSize), edge.RoomB.GetWorldCenter(_unitSize));
            }

            if (_showMST && _mstEdges != null)
            {
                Gizmos.color = Color.green;
                foreach (var edge in _mstEdges) Gizmos.DrawLine(edge.RoomA.GetWorldCenter(_unitSize) + Vector3.up, edge.RoomB.GetWorldCenter(_unitSize) + Vector3.up);
            }

            if (_showCorridorLogic && _corridorCells != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var cell in _corridorCells) Gizmos.DrawCube(new Vector3((cell.x + 0.5f) * _unitSize, 0, (cell.y + 0.5f) * _unitSize), Vector3.one * _unitSize * 0.9f);
            }

            if (_showRoomLogic && _placedRooms != null)
            {
                foreach (var room in _placedRooms)
                {
                    Gizmos.color = Color.green;
                    Vector3 center = room.GetWorldCenter(_unitSize);
                    Vector2Int size = room.GetRotatedSize();
                    Gizmos.DrawWireCube(center, new Vector3(size.x * _unitSize, 2f, size.y * _unitSize));
                }
            }
        }
    }

    public static class Delaunay2D
    {
        public class Vertex
        {
            public Vector2 Position;
            public RoomInstance RoomRef;
            public Vertex(Vector2 pos, RoomInstance room) { Position = pos; RoomRef = room; }
        }

        public class Triangle
        {
            public Vertex A, B, C;
            public Triangle(Vertex a, Vertex b, Vertex c) { A = a; B = b; C = c; }
            public bool ContainsInCircumcircle(Vector2 p)
            {
                float ax = A.Position.x, ay = A.Position.y;
                float bx = B.Position.x, by = B.Position.y;
                float cx = C.Position.x, cy = C.Position.y;

                float D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
                float Ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / D;
                float Uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / D;

                float rSq = (ax - Ux) * (ax - Ux) + (ay - Uy) * (ay - Uy);
                float dSq = (p.x - Ux) * (p.x - Ux) + (p.y - Uy) * (p.y - Uy);

                return dSq <= rSq; 
            }
        }

        public class Edge
        {
            public Vertex U, V;
            public Edge(Vertex u, Vertex v) { U = u; V = v; }
            public override bool Equals(object obj)
            {
                if (obj is Edge e) return (U == e.U && V == e.V) || (U == e.V && V == e.U);
                return false;
            }
            public override int GetHashCode() => U.GetHashCode() ^ V.GetHashCode();
        }

        public static List<RoomEdge> Triangulate(List<RoomInstance> rooms)
        {
            if (rooms.Count < 3) return ConvertLine(rooms);

            List<Vertex> vertices = rooms.Select(r => new Vertex(r.GetCenterGrid(), r)).ToList();
            
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

            HashSet<RoomEdge> resultEdges = new HashSet<RoomEdge>();
            foreach (var t in triangles)
            {
                bool hasP1 = t.A == p1 || t.B == p1 || t.C == p1;
                bool hasP2 = t.A == p2 || t.B == p2 || t.C == p2;
                bool hasP3 = t.A == p3 || t.B == p3 || t.C == p3;

                if (!hasP1 && !hasP2 && !hasP3)
                {
                    AddRoomEdge(resultEdges, t.A, t.B);
                    AddRoomEdge(resultEdges, t.B, t.C);
                    AddRoomEdge(resultEdges, t.C, t.A);
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

        private static void AddRoomEdge(HashSet<RoomEdge> set, Vertex u, Vertex v)
        {
            if (u.RoomRef != null && v.RoomRef != null)
            {
                set.Add(new RoomEdge(u.RoomRef, v.RoomRef));
            }
        }

        private static List<RoomEdge> ConvertLine(List<RoomInstance> rooms)
        {
            List<RoomEdge> edges = new List<RoomEdge>();
            for (int i = 0; i < rooms.Count - 1; i++) edges.Add(new RoomEdge(rooms[i], rooms[i+1]));
            return edges;
        }
    }
}