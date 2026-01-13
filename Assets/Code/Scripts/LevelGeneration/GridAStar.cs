using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DustRunner.LevelGeneration
{
    public static class GridAStar
    {
        public static List<Vector3Int> FindPath(Vector3Int start, Vector3Int end, Dictionary<Vector3Int, CrawlerDungeonGeneratorRef.DungeonNode> obstacles)
        {
            var openSet = new List<Node>();
            var closedSet = new HashSet<Vector3Int>();

            Node startNode = new Node(start, null, 0, GetDistance(start, end));
            openSet.Add(startNode);

            int safety = 1000; // Zabezpieczenie przed pętlą

            while (openSet.Count > 0 && safety > 0)
            {
                safety--;
                // Sortujemy po F cost (najniższy koszt)
                openSet.Sort((a, b) => a.FCost.CompareTo(b.FCost));
                Node currentNode = openSet[0];
                openSet.RemoveAt(0);
                closedSet.Add(currentNode.Position);

                // Cel osiągnięty
                if (currentNode.Position == end)
                {
                    return RetracePath(startNode, currentNode);
                }

                foreach (var neighborPos in GetNeighbors(currentNode.Position))
                {
                    // 1. Ignorujemy zamknięte
                    if (closedSet.Contains(neighborPos)) continue;
                    
                    // 2. Ignorujemy przeszkody (chyba że to cel)
                    if (obstacles.ContainsKey(neighborPos) && neighborPos != end) continue;

                    float newMovementCostToNeighbor = currentNode.GCost + 1;
                    Node neighborNode = openSet.FirstOrDefault(n => n.Position == neighborPos);

                    if (neighborNode == null || newMovementCostToNeighbor < neighborNode.GCost)
                    {
                        Node newNode = new Node(neighborPos, currentNode, newMovementCostToNeighbor, GetDistance(neighborPos, end));
                        
                        if (neighborNode == null) openSet.Add(newNode);
                        else 
                        {
                            // Update existing (uproszczone, w pełnym A* trzeba by zaktualizować w liście)
                            openSet.Remove(neighborNode);
                            openSet.Add(newNode);
                        }
                    }
                }
            }

            return null; // Nie znaleziono ścieżki
        }

        private static List<Vector3Int> RetracePath(Node startNode, Node endNode)
        {
            List<Vector3Int> path = new List<Vector3Int>();
            Node currentNode = endNode;

            while (currentNode != null)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }
            
            path.Reverse();
            return path;
        }

        private static List<Vector3Int> GetNeighbors(Vector3Int center)
        {
            // Tylko sąsiedzi w płaszczyźnie (bez góra/dół i skosów)
            return new List<Vector3Int>
            {
                center + Vector3Int.forward,
                center + Vector3Int.back,
                center + Vector3Int.left,
                center + Vector3Int.right
            };
        }

        private static float GetDistance(Vector3Int a, Vector3Int b)
        {
            // Manhattan distance jest lepszy do gridu bez skosów
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z); // Ignorujemy Y dla uproszczenia
        }

        private class Node
        {
            public Vector3Int Position;
            public Node Parent;
            public float GCost;
            public float HCost;
            public float FCost => GCost + HCost;

            public Node(Vector3Int pos, Node parent, float g, float h)
            {
                Position = pos;
                Parent = parent;
                GCost = g;
                HCost = h;
            }
        }
    }
}