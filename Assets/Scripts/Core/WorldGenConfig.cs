using System.Collections.Generic;
using UnityEngine;

namespace DungeonCity.Core
{
    /// <summary>
    /// 房间类型参数表：一个房间类型 = 一套参数。
    /// 后期要给某个房间类型加机制/参数，就加在这里（II 类矩阵的格子种类、III 类功能倾向等）。
    /// 注意：房间大小与内部装饰都是"每个房间随机"，同类型房间也不一样（内部多样性）。
    /// </summary>
    public class RoomProfile
    {
        public int typeId;
        public string displayName = "";

        // ---- 尺寸参数：a * b，a,b 范围 10-15，每个房间分别随机 ----
        public int minWidth = 10, maxWidth = 15;
        public int minHeight = 10, maxHeight = 15;

        // ---- II 类矩阵的内部装饰（每个房间随机取值，制造同类型房间的内部差异）----
        public bool cornerPillars = false;   // 是否放四角立柱
        public float pillarKeepChance = 1f;  // 每个角立柱各自出现的概率（<1 时每个房间的立柱组合不同）
        public int pillarInset = 2;          // 立柱离墙的格数
        public int rockCountMin = 0;         // 内部随机障碍块（地牢碎石）数量下限
        public int rockCountMax = 0;         // 上限；与下限相等就是固定数量；都为 0 表示不放
        public bool keepCrossFree = true;    // 保持中央十字（门到门）通道不被装饰占用 -> 保证房间连通

        // ---- 外观参数（同一套 II 码，不同房间类型不同外观）----
        public Color floorColor = new Color(0.93f, 0.94f, 0.96f);
        public Color floorAltColor = new Color(0.86f, 0.87f, 0.90f);
        public float floorAltChance = 0f;    // 地板随机色斑比例（纯视觉，不影响矩阵）
        public Color wallColor = new Color(0.55f, 0.60f, 0.72f);
        public Color doorColor = new Color(0.85f, 0.66f, 0.28f);
        public Color ladderColor = new Color(0.25f, 0.55f, 0.45f);
        public float wallHeight = 1.2f;
    }

    /// <summary>
    /// 世界生成的总配置：I 类矩阵、种子、房间类型表、刷怪参数等。
    /// 全部是纯 C# 数据，改起来不用开 Unity 编辑器。
    /// </summary>
    public class WorldGenConfig
    {
        public WorldKind kind;
        public int[,] iMatrix;                      // I 类矩阵 [x, y]：0 = 无房间，1 = 城市房间，2 = 地牢房间
        public int seed = 20240601;
        public float spacing = 18f;                 // 房间在世界空间中的摆放间距（须 > 最大房间尺寸，房间之间是伪连接）
        public Vector2Int spawnRoomGrid = new Vector2Int(-1, -1);     // 出生房间（I 矩阵坐标）
        public Vector2Int worldPortalRoomGrid = new Vector2Int(-1, -1); // 梯子所在房间；(-1,-1) = 自动取离出生点最远的房间
        public Dictionary<int, RoomProfile> profiles = new Dictionary<int, RoomProfile>();

        // ---- 刷怪参数（M=2 地牢，规则见 MonsterSpawnPlanner / MonsterSpawnDirector）----
        // 文档规则：每个"距离 >= 2"的房间内随机 10%（向下取整）个格子，每个格子以较低概率 P 生成怪物；
        //           每 t = 1min 判定一次；场上怪 <= 2 时立即判定；场上怪 >= 2 * 房间数 时判定成功也不生成。
        public float monsterCellSampleRatio = 0.1f;      // 每个房间抽样的格子比例（10%，向下取整）
        public float monsterSpawnChanceP = 0.02f;        // 每个被抽中的格子的生成概率 P（较低概率）
        public int monsterMinRoomDistance = 2;           // 距离 >= 2 的房间才刷怪
        public float monsterJudgeIntervalSeconds = 60f;  // 判定周期 t = 1min
        public int monsterForceJudgeMaxCount = 2;        // 场上怪 <= 2 时立即判定
        public float monsterForceJudgeMinInterval = 5f;  // "立即判定"的最小冷却（避免每帧都判定，参数待定，可改）
        public int monsterCapRoomsFactor = 2;            // 场上怪 >= 该系数 * 房间数 时，判定成功也不生成（系数 = 2）

        public RoomProfile ProfileOf(int typeId)
        {
            RoomProfile p;
            if (profiles.TryGetValue(typeId, out p)) return p;
            throw new System.Exception($"[WorldGenConfig] 未定义房间类型 {typeId}（世界 {kind}）");
        }

        /// <summary>本世界地图上允许出现的房间类型（I 类矩阵的取值）。</summary>
        public int ExpectedRoomTypeId => kind == WorldKind.City ? RoomTypeId.CityStandard : RoomTypeId.DungeonStandard;

        /// <summary>I 类矩阵 ASCII 预览（导入/导出/调试用）。</summary>
        public string DumpIMatrix()
        {
            int w = iMatrix.GetLength(0), h = iMatrix.GetLength(1);
            var sb = new System.Text.StringBuilder();
            for (int y = h - 1; y >= 0; y--)
            {
                sb.Append("y=").Append(y).Append(" ");
                for (int x = 0; x < w; x++) sb.Append(iMatrix[x, y]).Append(' ');
                sb.AppendLine();
            }
            sb.Append("     ");
            for (int x = 0; x < w; x++) sb.Append("x").Append(x).Append(' ');
            return sb.ToString();
        }

        /// <summary>把"从上到下的字符串数组"解析成 [x,y] 的 I 类矩阵。第一行是 y 最大的一行。</summary>
        public static int[,] Parse(string[] rowsTopFirst)
        {
            int h = rowsTopFirst.Length;
            int w = rowsTopFirst[0].Length;
            var m = new int[w, h];
            for (int r = 0; r < h; r++)
            {
                string line = rowsTopFirst[r].Replace(" ", "");
                if (line.Length != w) throw new System.Exception("[WorldGenConfig] I 矩阵每行长度必须一致");
                for (int c = 0; c < w; c++) m[c, h - 1 - r] = line[c] - '0';
            }
            return m;
        }
    }
}