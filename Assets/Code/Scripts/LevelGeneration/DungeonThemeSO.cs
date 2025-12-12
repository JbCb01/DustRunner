using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DustRunner.LevelGeneration
{
    [System.Serializable]
    public class WeightedPrefab
    {
        public GameObject Prefab;
        [Range(1, 1000)] public int Weight = 10;
    }

    [CreateAssetMenu(fileName = "NewDungeonTheme", menuName = "DustRunner/Dungeon Theme")]
    public class DungeonThemeSO : ScriptableObject
    {
        [Header("Paths")]
        public string CorridorFolderPath = "Prefabs/Env/Corridors/";
        public string RoomsFolderPath = "Prefabs/Env/Rooms";
        public string MandatoryFolderPath = "Prefabs/Env/Rooms/Mandatory";

        [Header("Mandatory Rooms (100% Spawn Chance)")]
        public List<GameObject> MandatoryRooms = new List<GameObject>();

        [Header("Random Rooms (Weighted)")]
        public List<WeightedPrefab> AvailableRooms = new List<WeightedPrefab>();

        [Header("Corridors")]
        public List<WeightedPrefab> Straights;
        public List<WeightedPrefab> Corners;
        public List<WeightedPrefab> TJunctions;
        public List<WeightedPrefab> Crosses;
        public List<WeightedPrefab> DeadEnds;
        public List<WeightedPrefab> FloorsOnly;

        [Header("Props")]
        public GameObject BlockedDoor; 

        // --- API ---
        public RoomTemplate GetRandomRoom() 
        {
            GameObject go = GetWeightedRandom(AvailableRooms);
            return go != null ? go.GetComponent<RoomTemplate>() : null;
        }

        // Helpers for corridors...
        public GameObject GetRandomStraight() => GetWeightedRandom(Straights);
        public GameObject GetRandomCorner() => GetWeightedRandom(Corners);
        public GameObject GetRandomTJunction() => GetWeightedRandom(TJunctions);
        public GameObject GetRandomCross() => GetWeightedRandom(Crosses);
        public GameObject GetRandomDeadEnd() => GetWeightedRandom(DeadEnds);
        public GameObject GetRandomFloor() => GetWeightedRandom(FloorsOnly);

        private GameObject GetWeightedRandom(List<WeightedPrefab> list)
        {
            if (list == null || list.Count == 0) return null;
            int totalWeight = 0;
            foreach (var item in list) totalWeight += item.Weight;
            int r = Random.Range(0, totalWeight);
            int sum = 0;
            foreach (var item in list) { sum += item.Weight; if (r < sum) return item.Prefab; }
            return list[0].Prefab;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Load All")]
        private void LoadAll()
        {
            // Load Mandatory (Simple List)
            MandatoryRooms = LoadSimpleListFromFolder(MandatoryFolderPath);

            // Load Randoms (Weighted List)
            LoadWeightedList(RoomsFolderPath, ref AvailableRooms, "room");
            
            // Load Corridors
            LoadWeightedList(CorridorFolderPath, ref Straights, "straight");
            LoadWeightedList(CorridorFolderPath, ref Corners, "corner");
            LoadWeightedList(CorridorFolderPath, ref TJunctions, new string[] { "tjunction", "3way" });
            LoadWeightedList(CorridorFolderPath, ref Crosses, new string[] { "cross", "4way" });
            LoadWeightedList(CorridorFolderPath, ref DeadEnds,  "straight");
            LoadWeightedList(CorridorFolderPath, ref FloorsOnly, new string[] { "cross", "4way" });

            EditorUtility.SetDirty(this);
        }

        private List<GameObject> LoadSimpleListFromFolder(string folderPath)
        {
            List<GameObject> list = new List<GameObject>();
            string fullPath = "Assets/" + folderPath;
            if (!System.IO.Directory.Exists(fullPath)) return list;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { fullPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) list.Add(prefab);
            }
            return list;
        }

        private void LoadWeightedList(string folderPath, ref List<WeightedPrefab> targetList, params string[] filters)
        {
            string fullPath = "Assets/" + folderPath;
            if (!System.IO.Directory.Exists(fullPath)) return;

            // Cache weights
            Dictionary<GameObject, int> cache = new Dictionary<GameObject, int>();
            if (targetList != null) foreach(var i in targetList) if(i.Prefab) cache[i.Prefab] = i.Weight;

            targetList = new List<WeightedPrefab>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { fullPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) continue;

                string name = go.name.ToLower();
                bool match = false;
                foreach(var f in filters) if(name.Contains(f)) match = true;

                if (match) targetList.Add(new WeightedPrefab { Prefab = go, Weight = cache.ContainsKey(go) ? cache[go] : 10 });
            }
        }
#endif
    }
}