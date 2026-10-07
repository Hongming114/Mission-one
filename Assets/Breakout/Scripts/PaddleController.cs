using UnityEngine;

namespace Breakout
{
    /// <summary>
    /// 挡板：只在一条直线上左右移动。
    /// 用 kinematic Rigidbody + MovePosition 而不是直接改 transform.position，
    /// 这样物理引擎能正确地算碰撞（直接改 transform 会"穿"过球）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PaddleController : MonoBehaviour
    {
        public float speed = 16f;
        public float minX = -5.6f;
        public float maxX = 5.6f;

        Rigidbody _rb;
        float _dir;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            _dir = 0f;
            GameManager g = GameManager.Instance;
            if (g != null && !g.AcceptsGameplayInput) return;
            _dir = InputHelper.Horizontal();
        }

        void FixedUpdate()
        {
            if (_rb == null || _dir == 0f) return;
            Vector3 p = _rb.position;
            p.x = Mathf.Clamp(p.x + _dir * speed * Time.fixedDeltaTime, minX, maxX);
            _rb.MovePosition(p);
        }
    }
}