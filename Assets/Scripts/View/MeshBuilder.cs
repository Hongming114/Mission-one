using System.Collections.Generic;
using UnityEngine;

namespace DungeonCity.View
{
    /// <summary>
    /// 把大量小方块/地板面合并成一个 Mesh：一个房间最多只有几种材质 -> 只有几个对象。
    /// 注意顶点绕序：三角面朝上的顺序是 (-,-) (-,+) (+,+) (+,-)。
    /// </summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> norms = new List<Vector3>();
        private readonly List<int> tris = new List<int>();

        public bool IsEmpty => tris.Count == 0;
        public int VertexCount => verts.Count;

        // ---- 调试4（遮挡）：把有高度的顶点沿相机视线方向压平（算法在 Core\ViewShear.cs）----
        public static bool FlattenDepth { get { return DungeonCity.Core.ViewShear.Enabled; } set { DungeonCity.Core.ViewShear.Enabled = value; } }
        public static float ViewPitchDeg { get { return DungeonCity.Core.ViewShear.PitchDeg; } set { DungeonCity.Core.ViewShear.PitchDeg = value; } }

        public static Vector3 Flatten(Vector3 v) { return DungeonCity.Core.ViewShear.Flatten(v); }

        /// <summary>朝上的地板面（1x1 格）。</summary>
        public void AddFloor(Vector3 cellCenter, float y = 0f, float size = 1f)
        {
            float h = size * 0.5f;
            Vector3 c = new Vector3(cellCenter.x, y, cellCenter.z);
            Vector3 a = c + new Vector3(-h, 0f, -h);
            Vector3 b = c + new Vector3(-h, 0f, h);
            Vector3 d = c + new Vector3(h, 0f, h);
            Vector3 e = c + new Vector3(h, 0f, -h);
            AddQuad(a, b, d, e, Vector3.up);
        }

        public void AddBox(Vector3 center, Vector3 size)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            Vector3 c = center;
            // 上
            AddQuad(c + new Vector3(-hx, hy, -hz), c + new Vector3(-hx, hy, hz), c + new Vector3(hx, hy, hz), c + new Vector3(hx, hy, -hz), Vector3.up);
            // 下
            AddQuad(c + new Vector3(-hx, -hy, -hz), c + new Vector3(hx, -hy, -hz), c + new Vector3(hx, -hy, hz), c + new Vector3(-hx, -hy, hz), Vector3.down);
            // 前 (+z)
            AddQuad(c + new Vector3(-hx, -hy, hz), c + new Vector3(hx, -hy, hz), c + new Vector3(hx, hy, hz), c + new Vector3(-hx, hy, hz), Vector3.forward);
            // 后 (-z)
            AddQuad(c + new Vector3(hx, -hy, -hz), c + new Vector3(-hx, -hy, -hz), c + new Vector3(-hx, hy, -hz), c + new Vector3(hx, hy, -hz), Vector3.back);
            // 右 (+x)
            AddQuad(c + new Vector3(hx, -hy, hz), c + new Vector3(hx, -hy, -hz), c + new Vector3(hx, hy, -hz), c + new Vector3(hx, hy, hz), Vector3.right);
            // 左 (-x)
            AddQuad(c + new Vector3(-hx, -hy, -hz), c + new Vector3(-hx, -hy, hz), c + new Vector3(-hx, hy, hz), c + new Vector3(-hx, hy, -hz), Vector3.left);
        }

        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            a = Flatten(a); b = Flatten(b); c = Flatten(c); d = Flatten(d);
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            norms.Add(normal); norms.Add(normal); norms.Add(normal); norms.Add(normal);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
