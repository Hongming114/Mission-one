using UnityEngine;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 八向移动指令：把 WASD / 方向键的按下状态解析成方向向量 (x, y)，x 向右、y 向上（世界 +X / +Z）。
    /// 文档规则：
    ///   * 同时按两个方向键 -> 走斜向（左上/左下/右上/右下）；
    ///   * 同时按三个键 -> 相对的一对互相抵消，按剩下的单键方向走（W+S+A -> 向左）；
    ///   * 同时按四个键 -> 两两抵消，不动。
    /// dx、dy 分别相减天然满足这三条规则。
    /// </summary>
    public static class MoveInput
    {
        public static readonly Vector2Int None = new Vector2Int(0, 0);

        public static Vector2Int FromKeys(bool up, bool down, bool left, bool right)
        {
            int dx = (right ? 1 : 0) - (left ? 1 : 0);
            int dy = (up ? 1 : 0) - (down ? 1 : 0);
            return new Vector2Int(dx, dy);
        }

        public static bool IsNone(Vector2Int dir) { return dir.x == 0 && dir.y == 0; }

        /// <summary>方向向量 -> 精灵方向序号：0 下、1 左下、2 左、3 左上、4 上、5 右上、6 右、7 右下。</summary>
        public static int SpriteIndex(Vector2Int dir)
        {
            if (dir.y < 0) return dir.x < 0 ? 1 : (dir.x > 0 ? 7 : 0);
            if (dir.y > 0) return dir.x < 0 ? 3 : (dir.x > 0 ? 5 : 4);
            return dir.x < 0 ? 2 : 6;
        }

        /// <summary>方向向量 -> 单位向量（斜向归一化，保证斜着走不比直着快）。</summary>
        public static Vector2 ToVector(Vector2Int dir)
        {
            Vector2 v = new Vector2(dir.x, dir.y);
            return v.sqrMagnitude > 1.0001f ? v.normalized : v;
        }

        /// <summary>把一个方向压成 -1/0/1 的格子方向（用于"朝主角"这类连续方向）。</summary>
        public static Vector2Int AxisDir(Vector2 v)
        {
            int x = Mathf.Abs(v.x) < 0.15f ? 0 : (v.x > 0f ? 1 : -1);
            int y = Mathf.Abs(v.y) < 0.15f ? 0 : (v.y > 0f ? 1 : -1);
            return new Vector2Int(x, y);
        }

        /// <summary>把向量旋转 angleDeg（度，正数 = 逆时针，在 (x, z) 平面上）。</summary>
        public static Vector2 Rotate(Vector2 v, float angleDeg)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static string Describe(Vector2Int dir)
        {
            if (IsNone(dir)) return "静止";
            string v = dir.y > 0 ? "上" : (dir.y < 0 ? "下" : "");
            string h = dir.x < 0 ? "左" : (dir.x > 0 ? "右" : "");
            return h + v;
        }
    }
}