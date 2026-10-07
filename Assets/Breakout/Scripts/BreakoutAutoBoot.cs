using UnityEngine;

namespace Breakout
{
    /// <summary>
    /// 兜底装置：不管你当前打开的是哪个场景，只要按 Play 时场景里找不到 GameManager，
    /// 就自动用代码搭一局出来。所以哪怕你新建了个空场景直接按 Play，也能立刻玩。
    ///
    /// 不想要这种行为？把下面 Boot 方法上的 [RuntimeInitializeOnLoadMethod] 那一行删掉即可
    /// （删掉之后就必须依赖 Assets/Breakout/Scenes/Breakout.unity 这个场景了）。
    /// </summary>
    public static class BreakoutAutoBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            BreakoutBuilder.Build();
            Debug.Log("[打砖块] 当前场景里没有 GameManager，已用代码自动搭好一局。");
        }
    }
}