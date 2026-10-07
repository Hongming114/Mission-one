using UnityEngine;

namespace Breakout
{
    public enum GameState { Ready, Playing, Paused, Won, Lost }

    /// <summary>
    /// 全局规则与状态机。场景里只应该有一个。
    /// Awake 里把自己登记到 Instance，其它脚本就能用 GameManager.Instance 拿到它。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("规则")]
        public int startingLives = 3;
        public float deathZ = -8f;          // 球掉到这个 z 之后就算丢了

        [Header("状态（运行时只读）")]
        public GameState state = GameState.Ready;
        public int score;
        public int lives;
        public int bricksLeft;

        [Header("引用（由 BreakoutBuilder 自动填）")]
        public PaddleController paddle;
        public BallController ball;
        public BreakoutHud hud;
        public Transform brickRoot;

        void Awake()
        {
            Instance = this;
            lives = startingLives;
        }

        public void Configure(PaddleController p, BallController b, BreakoutHud h, Transform bricks, int brickCount)
        {
            paddle = p;
            ball = b;
            hud = h;
            brickRoot = bricks;
            bricksLeft = brickCount;
            score = 0;
            lives = startingLives;
            ResetBall();
        }

        void Update()
        {
            if (InputHelper.RestartDown()) { Restart(); return; }

            switch (state)
            {
                case GameState.Ready:
                    if (InputHelper.SpaceDown()) LaunchBall();
                    break;

                case GameState.Playing:
                    if (InputHelper.PauseDown()) { state = GameState.Paused; break; }
                    if (ball != null && ball.transform.position.z < deathZ) OnBallLost();
                    break;

                case GameState.Paused:
                    if (InputHelper.PauseDown()) state = GameState.Playing;
                    break;
            }
        }

        public void LaunchBall()
        {
            if (state != GameState.Ready || ball == null) return;
            state = GameState.Playing;
            ball.Launch();
        }

        public void ResetBall()
        {
            state = GameState.Ready;
            if (ball == null) return;
            ball.gameObject.SetActive(true);
            ball.ParkOn(paddle != null ? paddle.transform : null);
        }

        public void OnBallLost()
        {
            if (state != GameState.Playing) return;
            lives--;
            if (lives <= 0)
            {
                lives = 0;
                state = GameState.Lost;
                ball.gameObject.SetActive(false);
            }
            else
            {
                ResetBall();
            }
        }

        public void OnBrickDestroyed(Brick brick)
        {
            score += brick.points;
            bricksLeft = Mathf.Max(0, bricksLeft - 1);
            if (ball != null) ball.Boost();
            if (hud != null) hud.Pop(brick.transform.position, "+" + brick.points);
            if (bricksLeft == 0)
            {
                state = GameState.Won;
                if (ball != null) ball.gameObject.SetActive(false);
            }
        }

        /// <summary>重开一局：把砖块全部重建，分数和命数回到初始值。</summary>
        public void Restart()
        {
            if (brickRoot != null)
            {
                for (int i = brickRoot.childCount - 1; i >= 0; i--)
                {
                    Transform c = brickRoot.GetChild(i);
                    c.SetParent(null, false);      // 先摘出去，Destroy 是延迟到帧末才生效的
                    Destroy(c.gameObject);
                }
            }
            score = 0;
            lives = startingLives;
            bricksLeft = BreakoutBuilder.BuildBricks(this, brickRoot);
            if (ball != null)
            {
                ball.ResetSpeed();
                ball.gameObject.SetActive(true);
            }
            ResetBall();
        }

        /// <summary>暂停 / 结算时挡板也应该停下，所以单独开一个属性给 PaddleController 查。</summary>
        public bool AcceptsGameplayInput
        {
            get { return state == GameState.Ready || state == GameState.Playing; }
        }
    }
}