using UnityEngine;

namespace Breakout
{
    /// <summary>一块砖：被球碰到就加分并销毁。</summary>
    public class Brick : MonoBehaviour
    {
        public int points = 10;

        GameManager _game;
        Color _baseColor;
        float _flashUntil;

        public void Init(GameManager game, int points, Color color)
        {
            _game = game;
            this.points = points;
            _baseColor = color;
            MaterialFactory.SetColor(GetComponent<Renderer>(), color);
        }

        void Update()
        {
            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                MaterialFactory.SetColor(GetComponent<Renderer>(), _baseColor);
            }
        }

        /// <summary>被球击中。现在是一碰就碎，想做成"打两下"就加 hitPoints。</summary>
        public void OnHit()
        {
            _flashUntil = Time.time + 0.06f;
            MaterialFactory.SetColor(GetComponent<Renderer>(), Color.white);
            if (_game != null) _game.OnBrickDestroyed(this);
            Destroy(gameObject);
        }
    }
}