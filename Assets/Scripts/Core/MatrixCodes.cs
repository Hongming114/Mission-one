using UnityEngine;

namespace DungeonCity.Core
{
    /// <summary>
    /// 变量 M：决定当前所处的大世界。城市 = 1，地牢 = 2。
    /// </summary>
    public enum WorldKind
    {
        City = 1,
        Dungeon = 2,
    }

    /// <summary>
    /// I 类矩阵（大地图房间分布）的数值 = 房间类型。
    /// 文档规定三种：0 = 无房间，1 = 标准房间-城市，2 = 标准房间-地牢。
    /// 数值是"房间类型"本身，所以城市地图里只会出现 1，地牢地图里只会出现 2。
    /// </summary>
    public static class RoomTypeId
    {
        public const int None = 0;             // 无房间
        public const int CityStandard = 1;     // 标准房间-城市
        public const int DungeonStandard = 2;  // 标准房间-地牢
    }

    /// <summary>
    /// II 类矩阵（房间内部格子）的数值。城市与地牢共用同一套码，
    /// 具体外观（白地板/灰地板）由 RoomProfile 按房间类型决定。
    /// </summary>
    public enum TileCode
    {
        Floor = 0,   // 城市：白色光滑地板；地牢：灰色粗糙地板
        Wall = 1,    // 墙
        Door = 2,    // 门（连接相邻房间）
        Ladder = 3,  // 跨世界梯子：城市=地牢入口，地牢=城市入口
    }

    /// <summary>
    /// III 类矩阵（功能矩阵）的数值。按要求书写为 0 / 1 / 2.1 / 2.2。
    /// 浮点比较统一走 FuncCode.Is，禁止直接用 == 比较。
    /// </summary>
    public static class FuncCode
    {
        public const float Blocked = 0f;        // 不可达单元格
        public const float Walkable = 1f;       // 普通可达单元格
        public const float RoomPortal = 2.1f;   // 传送单元格（到相邻房间）
        public const float WorldPortal = 2.2f;  // 跨世界传送：城市<->地牢（梯子）
        public const float Epsilon = 0.0001f;

        public static bool Is(float value, float code) => Mathf.Abs(value - code) < Epsilon;
        public static bool IsBlocked(float v) => Is(v, Blocked);
        public static bool IsWalkable(float v) => Is(v, Walkable);
        public static bool IsRoomPortal(float v) => Is(v, RoomPortal);
        public static bool IsWorldPortal(float v) => Is(v, WorldPortal);

        /// <summary>是否允许角色站立/进入（非不可达）。</summary>
        public static bool CanStand(float v) => !IsBlocked(v);

        public static string Label(float v)
        {
            if (Is(v, Blocked)) return "0 不可达";
            if (Is(v, Walkable)) return "1 可达";
            if (Is(v, RoomPortal)) return "2.1 房间传送";
            if (Is(v, WorldPortal)) return "2.2 跨世界传送";
            return v + " ?";
        }

        /// <summary>III 类矩阵的 ASCII 单字符（导出矩阵图用）。</summary>
        public static char Symbol(float v)
        {
            if (Is(v, Blocked)) return '.';
            if (Is(v, Walkable)) return 'o';
            if (Is(v, RoomPortal)) return '+';
            if (Is(v, WorldPortal)) return 'O';
            return '?';
        }
    }

    /// <summary>II 类矩阵的 ASCII 单字符（导出矩阵图用）。</summary>
    public static class TileSymbol
    {
        public static char Of(int tile)
        {
            switch ((TileCode)tile)
            {
                case TileCode.Floor: return '.';
                case TileCode.Wall: return '#';
                case TileCode.Door: return '+';
                case TileCode.Ladder: return 'L';
                default: return '?';
            }
        }
    }

    /// <summary>四方位（与 I 类矩阵坐标一致：x 向右，y 向上）。</summary>
    public enum Dir
    {
        North = 0, // +y
        East = 1,  // +x
        South = 2, // -y
        West = 3,  // -x
    }

    public static class DirUtil
    {
        public static readonly Dir[] All = { Dir.North, Dir.East, Dir.South, Dir.West };

        public static Vector2Int Step(Dir d)
        {
            switch (d)
            {
                case Dir.North: return new Vector2Int(0, 1);
                case Dir.East: return new Vector2Int(1, 0);
                case Dir.South: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }

        public static Dir Opposite(Dir d) => (Dir)(((int)d + 2) % 4);

        public static string Short(Dir d)
        {
            switch (d)
            {
                case Dir.North: return "北";
                case Dir.East: return "东";
                case Dir.South: return "南";
                default: return "西";
            }
        }
    }

    /// <summary>本房间在哪些方位开了门（由 I 类矩阵的邻居决定）。</summary>
    [System.Flags]
    public enum DoorDir
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8,
    }

    public static class DoorDirUtil
    {
        public static bool Has(DoorDir set, DoorDir d) => (set & d) != 0;
        public static DoorDir Of(Dir d)
        {
            switch (d)
            {
                case Dir.North: return DoorDir.North;
                case Dir.East: return DoorDir.East;
                case Dir.South: return DoorDir.South;
                default: return DoorDir.West;
            }
        }
        public static string Describe(DoorDir set)
        {
            if (set == DoorDir.None) return "无门";
            var s = "";
            foreach (var d in DirUtil.All) if (Has(set, Of(d))) s += DirUtil.Short(d);
            return s;
        }
    }
}