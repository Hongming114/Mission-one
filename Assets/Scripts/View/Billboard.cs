using UnityEngine;

namespace DungeonCity.View
{
    /// <summary>
    /// 2.5D 侧俯视：所有角色/特效都是"面向相机的广告牌"，不随地形倾斜。
    /// 逻辑层用 (x, y) 表示地面坐标，这里统一换算成世界的 (x, 0, z)：y -> z。
    /// 另外提供"世界方向 -> 屏幕平面角度"的换算，用来摆放剑刃、刀光这类贴图。
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        public Transform faceTarget;

        public static Billboard Attach(GameObject go, Camera cam)
        {
            var b = go.AddComponent<Billboard>();
            b.faceTarget = cam != null ? cam.transform : null;
            return b;
        }

        void LateUpdate()
        {
            if (faceTarget == null)
            {
                Camera c = Camera.main;
                if (c == null) return;
                faceTarget = c.transform;
            }
            transform.rotation = faceTarget.rotation;
        }

        public static Vector3 ToWorld(Vector2 ground)
        {
            return new Vector3(ground.x, 0f, ground.y);
        }

        public static Vector3 ToWorld(Vector2 ground, float y)
        {
            return new Vector3(ground.x, y, ground.y);
        }

        public static Vector2 ToGround(Vector3 world)
        {
            return new Vector2(world.x, world.z);
        }

        /// <summary>地面方向在世界里的朝向（(x,y) -> (x,0,y)）。</summary>
        public static Vector3 GroundDir(Vector2 groundDir)
        {
            return new Vector3(groundDir.x, 0f, groundDir.y);
        }

        /// <summary>把世界方向投影到相机的屏幕平面，返回角度（度，绕相机视线轴，逆时针为正）。</summary>
        public static float PlaneAngleDeg(Camera cam, Vector3 worldDir)
        {
            if (cam == null || worldDir.sqrMagnitude < 1e-8f) return 0f;
            Vector3 d = worldDir.normalized;
            float x = Vector3.Dot(d, cam.transform.right);
            float y = Vector3.Dot(d, cam.transform.up);
            return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        }

        public static float PlaneAngleDeg(Camera cam, Vector2 groundDir)
        {
            return PlaneAngleDeg(cam, GroundDir(groundDir));
        }

        public static Quaternion PlaneRotation(Camera cam, Vector2 groundDir)
        {
            return Quaternion.Euler(0f, 0f, PlaneAngleDeg(cam, groundDir));
        }

        /// <summary>每世界单位（格）在屏幕上占多少像素。</summary>
        public static float PixelsPerUnit(Camera cam)
        {
            if (cam == null) return 16f;
            return Screen.height / (2f * Mathf.Max(0.01f, cam.orthographicSize));
        }
    }
}