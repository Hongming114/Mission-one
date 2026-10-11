using System.Collections.Generic;
using DungeonCity.Core;
using DungeonCity.Game;
using DungeonCity.Gen;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCity.UI
{
    /// <summary>
    /// 调试2-5：小地图。
    /// 站在玩家视角推演：视野被限制在「当前房间 + 门邻居 + 相机看得到的房间」之后，
    /// 玩家会迷路——不知道去过哪些房间、不知道梯子在哪个方向、也不知道还剩多少没探索。
    /// 所以按 I 类矩阵铺格子，只画去过的房间：给一张会逐渐点亮的探索图，而不是剧透整张地图。
    /// 点击可放大，放大后能看清图例、自己的位置。
    /// 配色即图例：蓝灰=去过　绿=出生房间　紫=梯子房间（换世界）　黄=当前房间　亮点=玩家。
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        public const float CellSize = 26f;
        public const float Pad = 10f;
        public const float Margin = 16f;
        public const float ZoomScale = 1.7f;

        GameSession session;
        RectTransform panel;
        RectTransform cellsRoot;
        Text title;
        Image playerDot;
        readonly List<Image> cells = new List<Image>();
        readonly List<Vector2Int> cellKeys = new List<Vector2Int>();

        bool zoomed;
        float refreshTimer;
        float panelW = 220f;
        float panelH = 170f;
        WorldKind lastKind = WorldKind.City;
        int lastGridW = -1;
        int lastGridH = -1;
        int lastVisitedCount = -1;
        float lastCellSize = -1f;

        public bool Zoomed { get { return zoomed; } }

        /// <summary>鼠标是否落在小地图面板上：落在上面时左键用来放大/还原，不挥剑。</summary>
        public bool PointerInside
        {
            get
            {
                if (panel == null) return false;
                Vector3 m = Input.mousePosition;
                float x1 = Screen.width - Margin;
                float x0 = x1 - panelW;
                float y1 = Screen.height - Margin;
                float y0 = y1 - panelH;
                return m.x >= x0 && m.x <= x1 && m.y >= y0 && m.y <= y1;
            }
        }

        public static MinimapUI Create(GameSession session)
        {
            var go = new GameObject("小地图");
            var ui = go.AddComponent<MinimapUI>();
            ui.session = session;
            ui.Build();
            return ui;
        }

        void Build()
        {
            UiFactory.EnsureEventSystem();
            Canvas canvas = UiFactory.CreateCanvas("小地图画布", transform, 55);
            panel = UiFactory.Panel("小地图面板", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 0.9f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margin, -Margin), new Vector2(panelW, panelH));

            title = UiFactory.NewText("标题", panel, "小地图", 13, TextAnchor.UpperLeft, new Color(0.86f, 0.9f, 1f));
            UiFactory.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Pad, -Pad), new Vector2(panelW - Pad * 2f, 20f));

            cellsRoot = UiFactory.NewRect("格子层", panel);
            UiFactory.Place(cellsRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Pad, -Pad - 24f), new Vector2(0f, 0f));

            playerDot = UiFactory.NewImage("玩家点", panel, new Color(1f, 1f, 0.45f));
            playerDot.raycastTarget = false;
            UiFactory.Place((RectTransform)playerDot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(7f, 7f));

            Refresh(true);
        }

        /// <summary>调试2-5：点击小地图放大 / 还原（放大后能看清图例与自己的位置）。</summary>
        public void ToggleZoom()
        {
            zoomed = !zoomed;
            Refresh(true);
        }

        void LateUpdate()
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = 0.15f;
            Refresh(false);
        }

        void Refresh(bool rebuildNow)
        {
            if (session == null) return;
            WorldData w = session.World;
            if (w == null) return;

            float cell = CellSize * (zoomed ? ZoomScale : 1f);
            panelW = w.iWidth * cell + Pad * 2f;
            panelH = w.iHeight * cell + Pad * 2f + (zoomed ? 68f : 24f);
            if (panel != null) panel.sizeDelta = new Vector2(panelW, panelH);

            int visitedCount = session.VisitedCount(session.currentKind);
            bool relayout = lastKind != w.kind || lastGridW != w.iWidth || lastGridH != w.iHeight
                || Mathf.Abs(lastCellSize - cell) > 0.01f || lastVisitedCount != visitedCount;
            if (rebuildNow || relayout) RebuildCells(w, cell, visitedCount);

            Vector2Int current = session.playerRoom;
            Vector2Int ladder = w.worldPortalRoom;
            Vector2Int spawn = w.spawnRoom;
            for (int i = 0; i < cells.Count && i < cellKeys.Count; i++)
                cells[i].color = CellColor(cellKeys[i], current, ladder, spawn);

            RoomData cur = w.HasRoom(current) ? w.Room(current) : null;
            if (cur != null)
            {
                Vector2Int lc = session.playerCell;
                float fx = Clamp01((lc.x + 0.5f) / Mathf.Max(1f, cur.width));
                float fy = Clamp01((lc.y + 0.5f) / Mathf.Max(1f, cur.height));
                Vector2 topLeft = CellPos(current, w.iHeight, cell);
                playerDot.gameObject.SetActive(true);
                ((RectTransform)playerDot.transform).anchoredPosition =
                    new Vector2(topLeft.x + fx * cell, topLeft.y - (1f - fy) * cell);
            }
            else
            {
                playerDot.gameObject.SetActive(false);
            }

            if (title != null)
            {
                string mode = zoomed ? "[点击还原]" : "[点击放大]";
                string text = "小地图 · " + w.KindName + "　已探索 " + visitedCount + "/" + w.RoomCount + "　" + mode;
                if (zoomed) text += "\n蓝=去过　绿=出生　紫=梯子（换世界）　黄=当前\n" + session.RoomText;
                title.text = text;
                ((RectTransform)title.transform).sizeDelta = new Vector2(panelW - Pad * 2f, zoomed ? 64f : 20f);
            }
        }

        void RebuildCells(WorldData w, float cell, int visitedCount)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null) Destroy(cells[i].gameObject);
            }
            cells.Clear();
            cellKeys.Clear();
            if (cellsRoot != null) ((RectTransform)cellsRoot).sizeDelta = new Vector2(w.iWidth * cell, w.iHeight * cell);

            for (int x = 0; x < w.iWidth; x++)
            {
                for (int y = 0; y < w.iHeight; y++)
                {
                    var key = new Vector2Int(x, y);
                    bool visited = session.IsVisited(session.currentKind, key);
                    bool hasRoom = w.HasRoom(key);
                    Image img = UiFactory.NewImage("格" + x + "_" + y, cellsRoot, new Color(0.16f, 0.17f, 0.21f));
                    img.raycastTarget = false;
                    img.gameObject.SetActive(visited || hasRoom);
                    UiFactory.Place((RectTransform)img.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        CellPos(key, w.iHeight, cell), new Vector2(cell - 2f, cell - 2f));
                    cells.Add(img);
                    cellKeys.Add(key);
                }
            }

            lastKind = w.kind;
            lastGridW = w.iWidth;
            lastGridH = w.iHeight;
            lastCellSize = cell;
            lastVisitedCount = visitedCount;
        }

        /// <summary>I 类矩阵格在面板上的左上角锚点坐标（世界北 = 屏幕上方）。</summary>
        Vector2 CellPos(Vector2Int key, int gridH, float cell)
        {
            return new Vector2(key.x * cell, -(gridH - 1 - key.y) * cell);
        }

        /// <summary>配色即图例：黄=当前　紫=梯子（换世界）　绿=出生房间　蓝灰=去过　深灰=没去过。</summary>
        Color CellColor(Vector2Int key, Vector2Int current, Vector2Int ladder, Vector2Int spawn)
        {
            bool visited = session.IsVisited(session.currentKind, key);
            if (visited && key == current) return new Color(1f, 0.85f, 0.25f);
            if (visited && key == ladder) return new Color(0.65f, 0.45f, 0.95f);
            if (visited && key == spawn) return new Color(0.35f, 0.85f, 0.45f);
            if (visited) return new Color(0.36f, 0.42f, 0.55f);
            if (key == current) return new Color(0.5f, 0.45f, 0.2f);
            return new Color(0.16f, 0.17f, 0.21f);
        }

        static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
