using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    public static class DungeonAlgorithms
    {
        // --- PATHFINDING (A*) ---

        public struct PathfindingContext
        {
            public int GridWidth;
            public int GridHeight;
            public HashSet<Vector2Int> OccupiedCells;
            public HashSet<Vector2Int> ExistingCorridors;
            public int DiggingCost;
            public int ExistingPathCost;
            public int TurnCost;
        }

        public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, PathfindingContext ctx)
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
                    if (IsOutOfBounds(next, ctx.GridWidth, ctx.GridHeight)) continue;
                    // Treat target as walkable even if occupied (it's a door)
                    if (next != end && ctx.OccupiedCells.Contains(next)) continue;

                    int moveCost = ctx.ExistingCorridors.Contains(next) ? ctx.ExistingPathCost : ctx.DiggingCost;
                    int newCost = costSoFar[current] + moveCost;

                    if (current != start) 
                    {
                        Vector2Int parent = cameFrom[current]; 
                        if ((current - parent) != (next - current)) newCost += ctx.TurnCost;
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
            while (curr != start) 
            { 
                path.Add(curr); 
                curr = cameFrom[curr]; 
            }
            path.Add(start); 
            path.Reverse(); 
            return path;
        }

        private static IEnumerable<Vector2Int> GetNeighbors(Vector2Int p) 
        { 
            yield return p + Vector2Int.up; 
            yield return p + Vector2Int.down; 
            yield return p + Vector2Int.left; 
            yield return p + Vector2Int.right; 
        }
        
        private static int GetManhattanDistance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        private static bool IsOutOfBounds(Vector2Int p, int w, int h) => p.x < 0 || p.x >= w || p.y < 0 || p.y >= h;

        // --- DELAUNAY ---

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
            
            float minX = vertices.Min(v => v.Position.x); float minY = vertices.Min(v => v.Position.y); 
            float maxX = vertices.Max(v => v.Position.x); float maxY = vertices.Max(v => v.Position.y); 
            float dx = maxX - minX; float dy = maxY - minY; float deltaMax = Mathf.Max(dx, dy) * 2;
            
            Vertex p1 = new Vertex(new Vector2(minX - 1, minY - 1), null); 
            Vertex p2 = new Vertex(new Vector2(minX - 1, maxY + deltaMax), null); 
            Vertex p3 = new Vertex(new Vector2(maxX + deltaMax, minY - 1), null);
            
            List<Triangle> triangles = new List<Triangle> { new Triangle(p1, p2, p3) };
            
            foreach (var vertex in vertices) {
                List<Triangle> badTriangles = new List<Triangle>(); 
                foreach (var t in triangles) if (t.ContainsInCircumcircle(vertex.Position)) badTriangles.Add(t);
                
                List<Edge> polygon = new List<Edge>(); 
                foreach (var t in badTriangles) { AddPolygonEdge(polygon, new Edge(t.A, t.B)); AddPolygonEdge(polygon, new Edge(t.B, t.C)); AddPolygonEdge(polygon, new Edge(t.C, t.A)); }
                
                foreach (var t in badTriangles) triangles.Remove(t); 
                foreach (var edge in polygon) triangles.Add(new Triangle(edge.U, edge.V, vertex));
            }
            
            HashSet<DoorEdge> resultEdges = new HashSet<DoorEdge>();
            foreach (var t in triangles) {
                bool hasP1 = t.A == p1 || t.B == p1 || t.C == p1; 
                bool hasP2 = t.A == p2 || t.B == p2 || t.C == p2; 
                bool hasP3 = t.A == p3 || t.B == p3 || t.C == p3;
                if (!hasP1 && !hasP2 && !hasP3) { AddDoorEdge(resultEdges, t.A, t.B); AddDoorEdge(resultEdges, t.B, t.C); AddDoorEdge(resultEdges, t.C, t.A); }
            }
            return resultEdges.ToList();
        }

        private static void AddPolygonEdge(List<Edge> polygon, Edge edge) { var existing = polygon.FirstOrDefault(e => e.Equals(edge)); if (existing != null) polygon.Remove(existing); else polygon.Add(edge); }
        private static void AddDoorEdge(HashSet<DoorEdge> set, Vertex u, Vertex v) { if (u.NodeRef != null && v.NodeRef != null) set.Add(new DoorEdge(u.NodeRef, v.NodeRef)); }
        private static List<DoorEdge> ConvertLine(List<DoorNode> nodes) { List<DoorEdge> edges = new List<DoorEdge>(); for (int i = 0; i < nodes.Count - 1; i++) edges.Add(new DoorEdge(nodes[i], nodes[i+1])); return edges; }

        // --- UTILS ---
        public class SimplePriorityQueue<T> { List<KeyValuePair<T, int>> e=new(); public int Count=>e.Count; public void Enqueue(T i, int p){e.Add(new(i,p));} public T Dequeue(){int b=0;for(int i=0;i<e.Count;i++)if(e[i].Value<e[b].Value)b=i;T r=e[b].Key;e.RemoveAt(b);return r;}}
    }
}