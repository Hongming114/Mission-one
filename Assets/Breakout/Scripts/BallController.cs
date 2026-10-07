using UnityEngine;

namespace Breakout
{
    /// <summary>
    /// 球。它是个普通的三维刚体，只是被限制了：
    ///   - 关掉重力（useGravity = false）
    ///   - 冻结 Y 轴位置和所有旋转（constraints），所以它永远在 y = 0.5 这一层飞
    ///   - 每物理帧把速度重新归一化成固定大小，保证球速恒定、不会越弹越慢
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BallController : MonoBehaviour
    {
        public float speed = 9f;
        public float speedUpPerBrick = 0.15f;
        public float maxSpeed = 18f;

        Rigidbody _rb;
        Transform _anchor;
        bool _launched;

        public bool IsLaunched { get { return _launched; } }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>回到"贴在挡板上等发球"的状态。</summary>
        public void ParkOn(Transform paddle)
        {
            _launched = false;
            _anchor = paddle;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            if (paddle != null) transform.position = paddle.position + new Vector3(0f, 0f, 0.9f);
        }

        public void Launch()
        {
            if (_launched) return;
            _anchor = null;
            _rb.isKinematic = false;
            _launched = true;
            float angle = Random.Range(-30f, 30f);   // 出发角度稍微随机一点，不然每局一模一样
            _rb.linearVelocity = (Quaternion.Euler(0f, angle, 0f) * Vector3.forward).normalized * speed;
        }

        public void Boost() { speed = Mathf.Min(speed + speedUpPerBrick, maxSpeed); }

        public void ResetSpeed() { speed = 9f; }

        void Update()
        {
            if (_anchor != null) transform.position = _anchor.position + new Vector3(0f, 0f, 0.9f);
        }

        void FixedUpdate()
        {
            if (!_launched) return;

            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude < 0.0001f) v = Vector3.forward * speed;
            v = v.normalized * speed;

            // 角度太平的话球会在左右墙之间来回横跳半天，硬掰一点出来
            if (v.z > -speed * 0.25f && v.z < speed * 0.25f)
            {
                v.z = (v.z >= 0f ? 1f : -1f) * speed * 0.35f;
                v = v.normalized * speed;
            }

            _rb.linearVelocity = v;
        }

        void OnCollisionEnter(Collision c)
        {
            Brick brick = c.collider.GetComponentInParent<Brick>();
            if (brick != null) brick.OnHit();

            PaddleController paddle = c.collider.GetComponentInParent<PaddleController>();
            if (paddle != null)
            {
                // 打在挡板上的位置越靠边，反弹角度越斜 —— 这样才有"控制感"
                float halfW = Mathf.Max(0.1f, paddle.transform.lossyScale.x * 0.5f);
                float t = Mathf.Clamp((transform.position.x - paddle.transform.position.x) / halfW, -1f, 1f);
                _rb.linearVelocity = (Quaternion.Euler(0f, t * 60f, 0f) * Vector3.forward).normalized * speed;
            }
        }
    }
}