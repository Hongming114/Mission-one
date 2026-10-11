using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 城市与地牢两个世界的生成配置（I 类矩阵 + 房间类型参数表）。
    /// 想改地图形状/房间大小/刷怪概率，只改这个文件。
    /// I 类矩阵取值：0 = 无房间，1 = 标准房间-城市，2 = 标准房间-地牢。
    /// </summary>
    public static class WorldCatalog
    {
        public const int DefaultCitySeed = 20240601;
        public const int DefaultDungeonSeed = 20240602;

        private static WorldGenConfig _city;
        private static WorldGenConfig _dungeon;

        public static WorldGenConfig For(WorldKind kind) => kind == WorldKind.City ? City() : Dungeon();
        public static WorldGenConfig City() { if (_city == null) _city = BuildCity(); return _city; }
        public static WorldGenConfig Dungeon() { if (_dungeon == null) _dungeon = BuildDungeon(); return _dungeon; }

        /// <summary>本世界的房间类型号（I 类矩阵里允许出现的非 0 值）。</summary>
        public static int ExpectedRoomTypeId(WorldKind kind)
            => kind == WorldKind.City ? RoomTypeId.CityStandard : RoomTypeId.DungeonStandard;

        /// <summary>测试用重置（改了配置后重新生成）。</summary>
        public static void ResetCache() { _city = null; _dungeon = null; }

        // ------------------------------------------------------------------
        // 城市 M=1
        // I 类矩阵 7 x 5：0 = 无房间，1 = 标准房间-城市，共 10 个房间
        //   y=4  0 0 0 0 0 0 0
        //   y=3  0 1 1 1 0 0 0
        //   y=2  0 1 1 1 1 1 0
        //   y=1  0 0 1 1 0 0 0
        //   y=0  0 0 0 0 0 0 0
        // 出生房间 (3,2)；唯一的 3 号梯子自动放在离出生点最远的房间 (1,3)（房间距离 3）
        // ------------------------------------------------------------------
        public static WorldGenConfig BuildCity()
        {
            var cfg = new WorldGenConfig
            {
                kind = WorldKind.City,
                seed = DefaultCitySeed,
                spacing = 18f,
                spawnRoomGrid = new Vector2Int(3, 2),
                worldPortalRoomGrid = new Vector2Int(-1, -1),   // -1 = 自动取离出生点最远的房间
                monsterMinRoomDistance = 2,
                iMatrix = WorldGenConfig.Parse(new[]
                {
                    "0000000",   // y = 4
                    "0111000",   // y = 3
                    "0111110",   // y = 2
                    "0011000",   // y = 1
                    "0000000",   // y = 0
                }),
            };

            cfg.profiles[RoomTypeId.CityStandard] = new RoomProfile
            {
                typeId = RoomTypeId.CityStandard,
                displayName = "测试城市房间",
                minWidth = 10, maxWidth = 15,
                minHeight = 10, maxHeight = 15,
                cornerPillars = true,        // 四角立柱
                pillarKeepChance = 0.7f,     // 每个角独立随机 -> 同类型房间内部也有差异
                pillarInset = 2,
                rockCountMin = 0, rockCountMax = 0,
                keepCrossFree = true,
                floorColor = new Color(0.93f, 0.94f, 0.96f),   // 白色光滑地板
                floorAltColor = new Color(0.86f, 0.88f, 0.92f),
                floorAltChance = 0.05f,
                wallColor = new Color(0.56f, 0.61f, 0.72f),
                doorColor = new Color(0.87f, 0.68f, 0.30f),
                ladderColor = new Color(0.30f, 0.62f, 0.52f),
                wallHeight = 1.2f,
            };
            return cfg;
        }

        // ------------------------------------------------------------------
        // 地牢 M=2
        // I 类矩阵 6 x 4：0 = 无房间，2 = 标准房间-地牢，共 7 个房间
        //   y=3  0 0 0 0 0 0
        //   y=2  0 2 2 2 0 0
        //   y=1  0 2 2 2 2 0
        //   y=0  0 0 0 0 0 0
        // 回城市的梯子固定放在 (1,2)
        // ------------------------------------------------------------------
        public static WorldGenConfig BuildDungeon()
        {
            var cfg = new WorldGenConfig
            {
                kind = WorldKind.Dungeon,
                seed = DefaultDungeonSeed,
                spacing = 18f,
                spawnRoomGrid = new Vector2Int(1, 2),     // 从城市梯子下来就落在这个房间的梯子格
                worldPortalRoomGrid = new Vector2Int(1, 2),
                monsterMinRoomDistance = 2,
                iMatrix = WorldGenConfig.Parse(new[]
                {
                    "000000",   // y = 3
                    "022200",   // y = 2
                    "022220",   // y = 1
                    "000000",   // y = 0
                }),
            };

            cfg.profiles[RoomTypeId.DungeonStandard] = new RoomProfile
            {
                typeId = RoomTypeId.DungeonStandard,
                displayName = "测试地牢房间",
                minWidth = 10, maxWidth = 15,
                minHeight = 10, maxHeight = 15,
                cornerPillars = false,
                rockCountMin = 3, rockCountMax = 9,   // 每个房间随机数量的碎石（保持中央十字通道 -> 一定连通）
                keepCrossFree = true,
                floorColor = new Color(0.36f, 0.36f, 0.39f),   // 灰色粗糙地板
                floorAltColor = new Color(0.29f, 0.29f, 0.32f),
                floorAltChance = 0.3f,
                wallColor = new Color(0.24f, 0.22f, 0.21f),
                doorColor = new Color(0.56f, 0.43f, 0.22f),
                ladderColor = new Color(0.35f, 0.66f, 0.78f),
                wallHeight = 1.4f,
            };
            return cfg;
        }
    }
}