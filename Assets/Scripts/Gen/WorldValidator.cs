using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 生成结果自检：I/II/III 三类矩阵之间、以及房间与门的关系必须自洽。
    /// 生成后立刻跑一遍，任何一条不满足都会抛异常（日志里能直接看到问题）。
    /// </summary>
    public static class WorldValidator
    {
        public static List<string> Run(WorldData w)
        {
            var errs = new List<string>();
            WorldGenConfig cfg = WorldCatalog.For(w.kind);

            // 0) I 类矩阵：只能出现 0 或本世界的房间类型（城市=1，地牢=2），且非 0 必须真的生成了房间
            int expected = cfg.ExpectedRoomTypeId;
            for (int y = 0; y < w.iHeight; y++)
                for (int x = 0; x < w.iWidth; x++)
                {
                    int v = w.iMatrix[x, y];
                    var key = new Vector2Int(x, y);
                    if (v != RoomTypeId.None && v != expected)
                        errs.Add($"I 类矩阵 ({x},{y}) = {v}，本世界（{w.KindName}）只允许 0 或 {expected}");
                    if (v != RoomTypeId.None && !w.HasRoom(key))
                        errs.Add($"I 类矩阵 ({x},{y}) = {v}，但没有生成对应房间");
                    if (v == RoomTypeId.None && w.HasRoom(key))
                        errs.Add($"I 类矩阵 ({x},{y}) = 0，却生成了房间");
                }
            // 1) 房间尺寸 / 边界墙 / 门的位置
            foreach (var kv in w.rooms)
            {
                RoomData room = kv.Value;
                RoomProfile p = cfg.ProfileOf(room.typeId);
                if (room.width < p.minWidth || room.width > p.maxWidth || room.height < p.minHeight || room.height > p.maxHeight)
                    errs.Add($"{room.Key} 尺寸 {room.width}x{room.height} 超出类型参数范围 {p.minWidth}-{p.maxWidth} x {p.minHeight}-{p.maxHeight}");

                for (int y = 0; y < room.height; y++)
                    for (int x = 0; x < room.width; x++)
                    {
                        bool border = x == 0 || y == 0 || x == room.width - 1 || y == room.height - 1;
                        int t = room.tiles[x, y];
                        if (border && t != (int)TileCode.Wall && t != (int)TileCode.Door)
                            errs.Add($"{room.Key} 边界格 ({x},{y}) 是 {(TileCode)t}，必须是墙或门");
                        if (!border && t == (int)TileCode.Door)
                            errs.Add($"{room.Key} 内部格 ({x},{y}) 不该出现门（门只能开在墙的中间）");
                    }

                // 门标志与 II 类矩阵一致
                foreach (Dir dir in DirUtil.All)
                {
                    bool has = DoorDirUtil.Has(room.doors, DoorDirUtil.Of(dir));
                    Vector2Int c = WorldGenerator.DoorCell(room, dir);
                    bool isDoor = room.Tile(c.x, c.y) == (int)TileCode.Door;
                    if (has != isDoor)
                        errs.Add($"{room.Key} 方位{DirUtil.Short(dir)}：门标志={has} 但 II 上是 {(TileCode)room.Tile(c.x, c.y)}");
                }

                // 门数 = I 类矩阵里邻居的个数
                int neighbourCount = 0;
                foreach (Dir dir in DirUtil.All)
                {
                    Vector2Int nk = room.grid + DirUtil.Step(dir);
                    if (nk.x < 0 || nk.y < 0 || nk.x >= w.iWidth || nk.y >= w.iHeight) continue;
                    if (w.iMatrix[nk.x, nk.y] != RoomTypeId.None) neighbourCount++;
                }
                int doorCount = 0;
                foreach (Dir dir in DirUtil.All) if (DoorDirUtil.Has(room.doors, DoorDirUtil.Of(dir))) doorCount++;
                if (doorCount != neighbourCount)
                    errs.Add($"{room.Key} 门数 {doorCount} != I 类矩阵里的相邻房间数 {neighbourCount}");

                // 2) III 类矩阵必须由 II 类矩阵推导而来
                for (int y = 0; y < room.height; y++)
                    for (int x = 0; x < room.width; x++)
                        if (Mathf.Abs(room.func[x, y] - RoomBuilder.FuncOfTile(room.tiles[x, y])) > FuncCode.Epsilon)
                            errs.Add($"{room.Key} 格({x},{y}) III={room.func[x, y]} 与 II={(TileCode)room.tiles[x, y]} 不对应");

                // 3) 房间内部可达性
                if (!RoomConnectivity.IsConnected(room))
                    errs.Add($"{room.Key} 内部存在走不到的格子（可达 {RoomConnectivity.ReachableCount(room)} / 可站 {RoomConnectivity.StandableCount(room)}）");
            }

            // 4) 门必须两两配对，且目标按方位对应
            int doorCells = 0;
            foreach (var kv in w.rooms)
            {
                RoomData room = kv.Value;
                for (int y = 0; y < room.height; y++)
                    for (int x = 0; x < room.width; x++)
                    {
                        if (room.tiles[x, y] != (int)TileCode.Door) continue;
                        doorCells++;
                        Vector2Int cell = new Vector2Int(x, y);
                        if (w.PortalAt(kv.Key, cell) == null)
                            errs.Add($"{room.Key} 门格({x},{y}) 没有对应的传送连接");
                    }
            }
            if (doorCells != w.portals.Count * 2)
                errs.Add($"门格总数 {doorCells} != 连接数 {w.portals.Count} * 2");

            foreach (RoomPortal prt in w.portals)
            {
                Vector2Int delta = prt.roomB - prt.roomA;
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
                    errs.Add($"门连接 {prt.Describe()}：两个房间在 I 类矩阵上不相邻");
                else if (DirUtil.Step(prt.dirFromA) != delta)
                    errs.Add($"门连接 {prt.Describe()}：方位与实际相邻关系不一致");

                RoomData ra = w.Room(prt.roomA), rb = w.Room(prt.roomB);
                if (ra == null || rb == null) { errs.Add($"门连接 {prt.Describe()}：房间不存在"); continue; }
                if (ra.Tile(prt.cellA.x, prt.cellA.y) != (int)TileCode.Door)
                    errs.Add($"门连接 {prt.Describe()}：A 侧格子不是门");
                if (rb.Tile(prt.cellB.x, prt.cellB.y) != (int)TileCode.Door)
                    errs.Add($"门连接 {prt.Describe()}：B 侧格子不是门");

                Vector2Int tr, tc;
                if (!w.TryGetPortalTarget(prt.roomA, prt.cellA, out tr, out tc) || tr != prt.roomB || tc != prt.cellB)
                    errs.Add($"门连接 {prt.Describe()}：从 A 侧查不到 B 侧");
                if (!w.TryGetPortalTarget(prt.roomB, prt.cellB, out tr, out tc) || tr != prt.roomA || tc != prt.cellA)
                    errs.Add($"门连接 {prt.Describe()}：从 B 侧查不到 A 侧");
            }

            // 5) 整个世界里 II=3 的梯子恰好一个，且就是 worldPortalCell
            int ladders = 0;
            foreach (var kv in w.rooms)
            {
                RoomData room = kv.Value;
                for (int y = 0; y < room.height; y++)
                    for (int x = 0; x < room.width; x++)
                        if (room.tiles[x, y] == (int)TileCode.Ladder)
                        {
                            ladders++;
                            if (kv.Key != w.worldPortalRoom || x != w.worldPortalCell.x || y != w.worldPortalCell.y)
                                errs.Add($"多余的梯子：房间{room.Key} 格({x},{y})");
                        }
            }
            if (ladders != 1) errs.Add($"整个世界 II=3 的梯子必须恰好 1 个，实际 {ladders} 个");

            // 6) 出生格
            RoomData spawnRoom = w.Room(w.spawnRoom);
            if (spawnRoom == null) errs.Add($"出生房间 {w.spawnRoom} 不存在");
            else
            {
                if (!spawnRoom.CanStand(w.spawnCell.x, w.spawnCell.y))
                    errs.Add($"出生格 {w.spawnRoom}/{w.spawnCell} 是 III=0 不可达格");
                if (spawnRoom.Tile(w.spawnCell.x, w.spawnCell.y) == (int)TileCode.Door)
                    errs.Add($"出生格 {w.spawnRoom}/{w.spawnCell} 落在了门格上");
                if (spawnRoom.Tile(w.spawnCell.x, w.spawnCell.y) == (int)TileCode.Ladder)
                    errs.Add($"出生格 {w.spawnRoom}/{w.spawnCell} 落在了跨世界梯子格上（出生格要避开梯子）");
            }

            // 7) 房间图连通
            var dist = RoomDistanceField.Distances(w, w.spawnRoom);
            if (dist.Count != w.RoomCount)
            {
                foreach (var kv in w.rooms)
                    if (!dist.ContainsKey(kv.Key)) errs.Add($"房间{kv.Key} 从出生房间走不到（房间图不连通）");
            }

            return errs;
        }

        public static void Validate(WorldData w)
        {
            List<string> errs = Run(w);
            if (errs.Count > 0)
                throw new System.Exception($"[WorldValidator] {w.KindName} 校验失败（{errs.Count} 条）：\n  - " + string.Join("\n  - ", errs));
        }
    }
}