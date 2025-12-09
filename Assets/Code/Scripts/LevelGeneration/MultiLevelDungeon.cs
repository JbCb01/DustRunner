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
        [SerializeField] private float _layerHeightGap = 4f; 
        [SerializeField] private int _globalSeed = 12345;
        [SerializeField] private bool _randomizeSeed = true;

        [ContextMenu("Generate")]
        public void GenerateAll()
        {
            if (_randomizeSeed) _globalSeed = (int)System.DateTime.Now.Ticks;

            // 1. Generate Middle Layer (Layer 0)
            if (_layerMiddle != null)
            {
                // Layer 0 is origin for itself
                _layerMiddle.GenerateLayer(_globalSeed, 0f, 0, null);
            }

            // 2. Prepare Ghosts based on Middle Layer
            List<FixedRoomData> ghostsForTop = new List<FixedRoomData>();
            List<FixedRoomData> ghostsForBottom = new List<FixedRoomData>();

            if (_layerMiddle != null)
            {
                foreach (var room in _layerMiddle.PlacedRooms)
                {
                    // Check flags in RoomTemplate
                    if (room.PrefabSource.OccupiesLayerAbove)
                    {
                        ghostsForTop.Add(new FixedRoomData {
                            Prefab = room.PrefabSource,
                            Position = room.GridPos,
                            Rotation = room.RotationIndex,
                            SkipVisuals = true,
                            OriginLayerIndex = 0 // Comes from layer 0
                        });
                    }

                    if (room.PrefabSource.OccupiesLayerBelow)
                    {
                        ghostsForBottom.Add(new FixedRoomData {
                            Prefab = room.PrefabSource,
                            Position = room.GridPos,
                            Rotation = room.RotationIndex,
                            SkipVisuals = true,
                            OriginLayerIndex = 0 // Comes from layer 0
                        });
                    }
                }
            }

            // 3. Generate Top (Layer 1)
            if (_layerTop != null)
            {
                _layerTop.GenerateLayer(_globalSeed + 1, _layerHeightGap, 1, ghostsForTop);
            }

            // 4. Generate Bottom (Layer -1)
            if (_layerBottom != null)
            {
                _layerBottom.GenerateLayer(_globalSeed - 1, -_layerHeightGap, -1, ghostsForBottom);
            }
        }
    }
}