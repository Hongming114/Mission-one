using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 一个房间的全部生成数据：I 层坐标 + 尺寸 + II 类矩阵 + III 类矩阵。
    /// 房间内部坐标 [0, width-1] x [0, height-1]，与 I 类矩阵坐标同为"x 向右、y 向上"。
    /// </summary>
    public class RoomData
    {
        public Vector2Int grid;          // 在 I 类矩阵中的坐标
        public int typeId;               // I 类矩阵的数值（房间类型）
        public string typeName = "";
        public int width, height;        // a, b
        public int[,] tiles;             // II 类矩阵（TileCode）
        public float[,] func;            // III 类矩阵（FuncCode）
        public DoorDir doors;            // 本房间开了门的方位
        public Vector3 worldOrigin;      // 格子 (0,0) 在世界空间的左下角

        public string Key => "(" + grid.x + "," + grid.y + ")";

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        /// <summary>越界按墙处理，省掉大量边界判断。</summary>
        public int Tile(int x, int y) => InBounds(x, y) ? tiles[x, y] : (int)TileCode.Wall;
        public float Func(int x, int y) => InBounds(x, y) ? func[x, y] : FuncCode.Blocked;

        /// <summary>角色能否站在这格（III 类矩阵非 0）。</summary>
        public bool CanStand(int x, int y) => FuncCode.CanStand(Func(x, y));

        public Vector3 CellCenter(int x, int y) => worldOrigin + new Vector3(x + 0.5f, 0f, y + 0.5f);
        public Vector3 CellCenter(Vector2Int c) => CellCenter(c.x, c.y);

        public bool IsDoorCell(int x, int y) => (TileCode)Tile(x, y) == TileCode.Door;

        public override string ToString() => $"房间{Key} {typeName} {width}x{height} 门[{DoorDirUtil.Describe(doors)}]";
    }
}