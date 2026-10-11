using UnityEngine;

namespace DungeonCity.View
{
    /// <summary>
    /// 侧俯视 2.5D 正交相机：pitch 52°、yaw 45°，跟随玩家；也支持"框住整个世界"用于总览/截图。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public Transform target;
        public float pitch = 52f;
        public float yaw = 0f;                       // 调试2-1：整个视角顺时针转 45°（原 45），按 W（世界 +Z）在屏幕上就是正上方
        public float orthoSize = 11f / 1.75f;
        public float baseOrthoSize = 11f / 1.75f;        // 缩放基准（= 100% 的 orthographicSize）
        public float zoomPercent = DungeonCity.Core.CameraZoom.DefaultPercent;   // 调试2-2：滚轮缩放，50%~200%   // 反馈 1：整体视角放大到 175%（原 11 → 约 6.286）
        public float distance = 40f;
        public float followSharpness = 8f;
        public Vector3 targetOffset = new Vector3(0f, 0.6f, 0f);
        public bool follow = true;

        public static CameraRig Create(string name)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;

            var rig = go.AddComponent<CameraRig>();
            rig.cam = cam;
            go.tag = "MainCamera";
            rig.ApplyZoom();
            return rig;
        }

        public void Apply()
        {
            if (cam == null) cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        public float ZoomPercent { get { return zoomPercent; } }

        /// <summary>调试2-2：滚轮调换视角（上滑放大到最多 200%，下滑缩小到最少 50%，100% = 当前默认视角）。</summary>
        public void ZoomByScroll(float scrollDelta)
        {
            float next = DungeonCity.Core.CameraZoom.ApplyScroll(zoomPercent, scrollDelta);
            if (Mathf.Abs(next - zoomPercent) < 0.0001f) return;
            zoomPercent = next;
            ApplyZoom();
        }

        /// <summary>按 zoomPercent 重新计算 orthographicSize 并应用。</summary>
        public void ApplyZoom()
        {
            orthoSize = DungeonCity.Core.CameraZoom.OrthoSize(baseOrthoSize, zoomPercent);
            Apply();
        }

        public void SetTarget(Transform t, bool snap = true)
        {
            target = t;
            follow = true;
            if (snap) SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + targetOffset - transform.forward * distance;
        }

        void LateUpdate()
        {
            if (!follow || target == null) return;
            Vector3 focus = target.position + targetOffset;
            Vector3 want = focus - transform.forward * distance;
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
        }

        /// <summary>不再跟随，直接把整个 bounds 框进画面（总览/截图）。</summary>
        public void FrameBounds(Bounds b, float margin = 1.15f)
        {
            follow = false;
            Vector3 focus = b.center;
            float vertical = b.size.z * Mathf.Sin(pitch * Mathf.Deg2Rad) + b.size.y * Mathf.Cos(pitch * Mathf.Deg2Rad);
            float needV = 0.5f * Mathf.Max(b.size.z, vertical) * margin;
            float needH = 0.5f * b.size.x / Mathf.Max(cam.aspect, 0.2f) * margin;
            orthoSize = Mathf.Max(needV, needH, 2f);
            baseOrthoSize = orthoSize;
            zoomPercent = DungeonCity.Core.CameraZoom.DefaultPercent;
            cam.orthographicSize = orthoSize;
            transform.position = focus - transform.forward * distance;
        }
    }
}