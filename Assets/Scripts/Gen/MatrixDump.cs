using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 把 I/II/III 三类矩阵导出成纯文本（不用开 Unity 就能检查生成结果）。
    /// </summary>
    public static class MatrixDump
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string DumpWorld(WorldData w, WorldGenConfig cfg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================================");
            sb.AppendLine($"世界：M={(int)w.kind}（{w.KindName}）  种子={w.seed}  房间={w.RoomCount}  I 类矩阵={w.iWidth}x{w.iHeight}");
            sb.AppendLine($"出生房间={w.spawnRoom} 出生格={w.spawnCell}   跨世界梯子房间={w.worldPortalRoom} 格={w.worldPortalCell}");
            sb.AppendLine("==================================================================");
            sb.AppendLine();
            sb.AppendLine("【I 类矩阵】0 = 无房间，1 = 标准房间-城市（白地板），2 = 标准房间-地牢（灰地板）；S = 出生房间，L = 跨世界梯子房间（两者同格时显示 S）");
            sb.AppendLine("（上面一行是 y 最大的一行）");
            for (int y = w.iHeight - 1; y >= 0; y--)
            {
                sb.Append("  y=").Append(y).Append("  ");
                for (int x = 0; x < w.iWidth; x++)
                {
                    char c;
                    if (new Vector2Int(x, y) == w.spawnRoom) c = 'S';
                    else if (new Vector2Int(x, y) == w.worldPortalRoom) c = 'L';
                    else if (w.iMatrix[x, y] == RoomTypeId.None) c = '.';
                    else c = (char)('0' + w.iMatrix[x, y]);
                    sb.Append(c).Append(' ');
                }
                sb.AppendLine();
            }
            sb.Append("        ");
            for (int x = 0; x < w.iWidth; x++) sb.Append("x").Append(x).Append(' ');
            sb.AppendLine();
            sb.AppendLine();

            foreach (Vector2Int key in WorldGenerator.SortedKeys(w))
            {
                sb.AppendLine(DumpRoom(w.Room(key)));
                sb.AppendLine();
            }

            sb.AppendLine("【房间之间的门（方位对应，坐标可以不同）】");
            foreach (RoomPortal p in w.portals) sb.AppendLine("  " + p.Describe());
            sb.AppendLine();

            MonsterSpawnPlan plan = MonsterSpawnPlanner.Plan(w, w.spawnRoom, 0);
            sb.AppendLine("【刷怪计划示例：以出生房间为玩家所在房间】");
            sb.AppendLine(plan.Describe());
            return sb.ToString();
        }

        public static string DumpRoom(RoomData r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"房间{r.Key}  {r.typeName}  {r.width}x{r.height}  门[{DoorDirUtil.Describe(r.doors)}]  世界原点({r.worldOrigin.x},{r.worldOrigin.y},{r.worldOrigin.z})");
            sb.AppendLine("   II 类矩阵 (# 墙, . 地板, + 门, L 梯子)          III 类矩阵 (. 0不可达, o 1可达, + 2.1房间传送, O 2.2跨世界)");
            sb.AppendLine("     " + Header(r.width) + "     " + Header(r.width));
            for (int y = r.height - 1; y >= 0; y--)
            {
                string ii = Row(r.width, x => TileSymbol.Of(r.tiles[x, y]));
                string iii = Row(r.width, x => FuncCode.Symbol(r.func[x, y]));
                sb.AppendLine("y=" + y.ToString().PadLeft(3) + " " + ii + "     " + iii);
            }
            return sb.ToString();
        }

        private static string Header(int w)
        {
            var sb = new StringBuilder();
            for (int x = 0; x < w; x++)
            {
                if (x > 0) sb.Append(' ');
                sb.Append(x < 10 ? (char)('0' + x) : (char)('A' + (x - 10)));
            }
            return sb.ToString();
        }

        private static string Row(int w, Func<int, char> symbol)
        {
            var sb = new StringBuilder();
            for (int x = 0; x < w; x++)
            {
                if (x > 0) sb.Append(' ');
                sb.Append(symbol(x));
            }
            return sb.ToString();
        }

        /// <summary>把两个世界的矩阵导出到目录（默认 <项目>/Docs/Generated）。</summary>
        public static List<string> WriteAll(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var written = new List<string>();

            WorldData city = WorldGenerator.Generate(WorldCatalog.For(WorldKind.City), WorldCatalog.DefaultCitySeed);
            string cityFile = Path.Combine(outputDir, "城市_矩阵导出.txt");
            File.WriteAllText(cityFile, DumpWorld(city, WorldCatalog.City()), Utf8NoBom);
            written.Add(cityFile);

            WorldData dungeon = WorldGenerator.Generate(WorldCatalog.For(WorldKind.Dungeon), WorldCatalog.DefaultDungeonSeed);
            string dungeonFile = Path.Combine(outputDir, "地牢_矩阵导出.txt");
            File.WriteAllText(dungeonFile, DumpWorld(dungeon, WorldCatalog.Dungeon()), Utf8NoBom);
            written.Add(dungeonFile);

            return written;
        }
    }
}