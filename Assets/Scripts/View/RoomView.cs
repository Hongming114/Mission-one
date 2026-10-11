using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;

namespace DungeonCity.View
{
    /// <summary>
    /// 把单个房间的 II 类矩阵画出来（地板/墙/门/梯子）。视觉只依赖 II 类矩阵与房间类型参数。
    /// </summary>
    public class RoomView
    {
        public RoomData data;
        public GameObject root;

        public static RoomView Build(RoomData room, Transform parent, MaterialLibrary mats, RoomProfile p)
        {
            var view = new RoomView { data = room };
            view.root = new GameObject("房间" + room.Key + "_" + room.typeName);
            view.root.transform.SetParent(parent, false);
            view.root.transform.position = room.worldOrigin;

            // 按材质分组累加网格：地板(两种色调) / 墙 / 室内障碍 / 门 / 梯子
            var floor = new MeshBuilder();
            var floorAlt = new MeshBuilder();
            var wall = new MeshBuilder();
            var rock = new MeshBuilder();
            var door = new MeshBuilder();
            var ladder = new MeshBuilder();
            bool hasAlt = p.floorAltChance > 0.0001f;

            for (int y = 0; y < room.height; y++)
                for (int x = 0; x < room.width; x++)
                {
                    Vector3 c = new Vector3(x + 0.5f, 0f, y + 0.5f);
                    int tile = room.tiles[x, y];
                    bool border = x == 0 || y == 0 || x == room.width - 1 || y == room.height - 1;

                    // 每个格子都有地板（墙下面也有，避免出现空洞）
                    bool alt = hasAlt && !border && Hash(x, y, room) < p.floorAltChance;
                    (alt ? floorAlt : floor).AddFloor(c);

                    switch ((TileCode)tile)
                    {
                        case TileCode.Wall:
                            if (border)
                                wall.AddBox(new Vector3(c.x, p.wallHeight * 0.5f, c.z), new Vector3(1f, p.wallHeight, 1f));
                            else
                                rock.AddBox(new Vector3(c.x, p.wallHeight * 0.34f, c.z), new Vector3(0.86f, p.wallHeight * 0.68f, 0.86f));
                            break;
                        case TileCode.Door:
                            BuildDoor(door, c, x, y, room, p);
                            break;
                        case TileCode.Ladder:
                            BuildLadder(ladder, c, p);
                            break;
                    }
                }

            AddGroup(view.root.transform, "地板", floor, mats.Floor(p.floorColor));
            if (!floorAlt.IsEmpty) AddGroup(view.root.transform, "地板色斑", floorAlt, mats.Floor(p.floorAltColor));
            AddGroup(view.root.transform, "墙", wall, mats.Wall(p.wallColor));
            if (!rock.IsEmpty) AddGroup(view.root.transform, "碎石", rock, mats.Wall(p.wallColor * 0.82f));
            AddGroup(view.root.transform, "门", door, mats.Door(p.doorColor));
            AddGroup(view.root.transform, "梯子", ladder, mats.Ladder(p.ladderColor));
            return view;
        }

        private static void BuildDoor(MeshBuilder b, Vector3 c, int x, int y, RoomData room, RoomProfile p)
        {
            bool vertical = (x == 0 || x == room.width - 1);   // 门开在左右墙上 -> 墙沿 z 方向
            float postH = p.wallHeight * 0.92f;
            // 门槛板
            b.AddBox(new Vector3(c.x, 0.03f, c.z), new Vector3(0.96f, 0.06f, 0.96f));
            if (vertical)
            {
                b.AddBox(new Vector3(c.x, postH * 0.5f, c.z - 0.45f), new Vector3(0.5f, postH, 0.14f));
                b.AddBox(new Vector3(c.x, postH * 0.5f, c.z + 0.45f), new Vector3(0.5f, postH, 0.14f));
            }
            else
            {
                b.AddBox(new Vector3(c.x - 0.45f, postH * 0.5f, c.z), new Vector3(0.14f, postH, 0.5f));
                b.AddBox(new Vector3(c.x + 0.45f, postH * 0.5f, c.z), new Vector3(0.14f, postH, 0.5f));
            }
        }

        private static void BuildLadder(MeshBuilder b, Vector3 c, RoomProfile p)
        {
            // 暗色井口 + 几级台阶，作为 III=2.2 跨世界传送的可视标记
            b.AddBox(new Vector3(c.x, -0.02f, c.z), new Vector3(0.92f, 0.05f, 0.92f));
            for (int i = 0; i < 4; i++)
                b.AddBox(new Vector3(c.x, 0.05f + i * 0.09f, c.z - 0.3f + i * 0.2f), new Vector3(0.7f, 0.09f, 0.2f));
        }

        private static void AddGroup(Transform parent, string name, MeshBuilder b, Material m)
        {
            if (b.IsEmpty) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = b.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static float Hash(int x, int y, RoomData room)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ room.grid.x * 83492791 ^ room.grid.y * 2654435761.GetHashCode();
                return (Mathf.Abs(h) % 1000) / 1000f;
            }
        }
    }
}
