using System.Collections.Generic;
using UnityEngine;

namespace DustRunner.LevelGeneration
{
    public class MultiLevelDungeon : MonoBehaviour
    {
        [SerializeField] private DungeonGenerator _layerTop;
        [SerializeField] private DungeonGenerator _layerMiddle; // Layer 0
        [SerializeField] private DungeonGenerator _layerBottom;

        [Header("Config")]
        [SerializeField] private float _layerHeightGap = 5f; 
        [SerializeField] private int _globalSeed = 12345;
        [SerializeField] private bool _randomizeSeed = true;

        [ContextMenu("Generate Instant")]
        public void GenerateAll()
        {
            if (_randomizeSeed) _globalSeed = (int)System.DateTime.Now.Ticks;
            Random.InitState(_globalSeed);

            // 1. MIDDLE (Base)
            if (_layerMiddle != null)
            {
                _layerMiddle.GenerateLayer(_globalSeed, 0f, 0, null);
            }

            // Prepare Ghosts
            List<FixedRoomData> ghostsForTop = new List<FixedRoomData>();
            List<FixedRoomData> ghostsForBottom = new List<FixedRoomData>();

            if (_layerMiddle != null)
            {
                foreach (var room in _layerMiddle.PlacedRooms)
                {
                    if (room.PrefabSource.OccupiesLayerAbove)
                        ghostsForTop.Add(new FixedRoomData { Prefab = room.PrefabSource, Position = room.GridPos, Rotation = room.RotationIndex, SkipVisuals = true, OriginLayerIndex = 0 });

                    if (room.PrefabSource.OccupiesLayerBelow)
                        ghostsForBottom.Add(new FixedRoomData { Prefab = room.PrefabSource, Position = room.GridPos, Rotation = room.RotationIndex, SkipVisuals = true, OriginLayerIndex = 0 });
                }
            }

            // 2. TOP
            if (_layerTop != null)
            {
                _layerTop.GenerateLayer(_globalSeed + 1, _layerHeightGap, 1, ghostsForTop);
            }

            // 3. BOTTOM
            if (_layerBottom != null)
            {
                _layerBottom.GenerateLayer(_globalSeed - 1, -_layerHeightGap, -1, ghostsForBottom);
            }
        }
    }
}