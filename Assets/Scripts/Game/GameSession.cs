using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;
using DungeonCity.Sim;
using DungeonCity.View;

namespace DungeonCity.Game
{
    /// <summary>
    /// 一局游戏的全部状态（连续移动版）：
    ///   * 两个世界（城市 = M1、地牢 = M2）由 WorldGenerator 生成，生成即 WorldValidator 自检；
    ///   * 主角是 PlayerSim（纯逻辑），视图是 PlayerView（像素人 + 剑 + 残影），位置由逻辑层连续坐标驱动；
    ///   * 门（III=2.1）踩上去自动传送到配对门格内侧；跨世界梯子（III=2.2）在城市/地牢之间切换；
    ///   * 地牢按刷怪规则判定，成功后真的生成 SlimeSim + SlimeView（视野 3 格、蠕动、扑击、受击击退）；
    ///   * 主角挥剑扫到史莱姆就掉血（每只怪一次挥剑只挨一下），怪物模型与主角用 ActorBody.Separate 保证不重合；
    ///   * 文档 L31~L33：空格跳跃（浮空 1.5 格，空中仍可水平移动 = 跳跃 + 行走的合运动）；
    ///   * 文档 L34/L43/L44：史莱姆死亡掉落「史莱姆清」（0.6 格），主角碰撞后按拾取次序存入背包（50 格、单格上限 99）。
    ///   * 主角血量归零 -> 回出生点并回满血（本轮自己定的规则，可改）。
    /// 视图层只读状态，逻辑全在这里和 Sim 层。
    /// </summary>
    public sealed class GameSession
    {
        public readonly Dictionary<WorldKind, WorldData> worlds = new Dictionary<WorldKind, WorldData>();
        public readonly Dictionary<WorldKind, WorldView> views = new Dictionary<WorldKind, WorldView>();
        public readonly Dictionary<WorldKind, WalkMap> maps = new Dictionary<WorldKind, WalkMap>();
        public readonly MaterialLibrary materials = new MaterialLibrary();
        public readonly SpriteLibrary sprites = new SpriteLibrary();
        public readonly MonsterSpawnDirector monsterDirector = new MonsterSpawnDirector(WorldCatalog.Dungeon());
        public readonly PlayerSim player = new PlayerSim();
        public readonly List<SlimeSim> slimes = new List<SlimeSim>();
        public readonly List<SlimeView> slimeViews = new List<SlimeView>();
        public readonly Inventory inventory = new Inventory();
        public readonly List<SlimeDrop> drops = new List<SlimeDrop>();
        public readonly List<SlimeDropView> dropViews = new List<SlimeDropView>();

        public Transform root;
        public Camera cam;
        public WorldKind currentKind = WorldKind.City;
        public PlayerView playerView;
        public MonsterSpawnPlan lastJudge;
        public string lastEvent = "";
        public int moveCount, killCount, hitCount, respawnCount;
        public float playTime;
        public Vector2Int lastDungeonPlayerRoom = new Vector2Int(-1, -1);

        /// <summary>调试2-4：本帧显示的房间集合（玩家房间 + 门相连的邻居 + 相机地面视野内的房间）。</summary>
        readonly HashSet<Vector2Int> visibleRooms = new HashSet<Vector2Int>();
        /// <summary>调试2-5：小地图用——城市 / 地牢各自记录去过的房间（只显示去过的房间）。</summary>
        public readonly Dictionary<WorldKind, HashSet<Vector2Int>> visited = new Dictionary<WorldKind, HashSet<Vector2Int>>
        {
            { WorldKind.City, new HashSet<Vector2Int>() },
            { WorldKind.Dungeon, new HashSet<Vector2Int>() },
        };
        public bool IsVisited(WorldKind kind, Vector2Int key)
        {
            HashSet<Vector2Int> set;
            return visited.TryGetValue(kind, out set) && set.Contains(key);
        }
        public int VisitedCount(WorldKind kind)
        {
            HashSet<Vector2Int> set;
            return visited.TryGetValue(kind, out set) ? set.Count : 0;
        }

        float ladderCooldown;
        bool ladderArmed = true;
        int nextSlimeId = 1;

        /// <summary>反馈 6：调试模式（Alt+T 切换）。默认 false＝普通模式，调试界面与调试键都不生效。</summary>
        public bool debugMode;
        Vector2Int shownRoom = new Vector2Int(int.MinValue, int.MinValue);

        public void SetDebugMode(bool on)
        {
            debugMode = on;
            Note(on ? "进入调试模式（Alt+T 退出）" : "回到普通模式（调试界面与按键已隐藏/失效）");
        }

        public WorldData World { get { return worlds[currentKind]; } }
        public WorldView View { get { return views[currentKind]; } }
        public WalkMap Map { get { return maps[currentKind]; } }
        public string WorldNameCn { get { return currentKind == WorldKind.City ? "城市" : "地牢"; } }
        public Vector2Int PlayerWorldCell { get { return WalkMap.CellOf(player.pos); } }
        public bool InRoom { get { RoomData r; Vector2Int l; return Map.TryRoomOf(player.pos, out r, out l); } }
        public Vector2Int PlayerRoomKey
        {
            get { RoomData r; Vector2Int l; return Map.TryRoomOf(player.pos, out r, out l) ? r.grid : new Vector2Int(-1, -1); }
        }
        public Vector2Int PlayerLocalCell
        {
            get { RoomData r; Vector2Int l; return Map.TryRoomOf(player.pos, out r, out l) ? l : new Vector2Int(-1, -1); }
        }
        public Vector2Int playerRoom { get { return PlayerRoomKey; } }
        public Vector2Int playerCell { get { return PlayerLocalCell; } }
        public string facing { get { return MoveInput.Describe(player.facing); } }
        public RoomData PlayerRoom { get { RoomData r; Vector2Int l; return Map.TryRoomOf(player.pos, out r, out l) ? r : null; } }
        public float FuncAtPlayer { get { return Map.FuncAt(PlayerWorldCell); } }
        public int TileAtPlayer { get { return Map.TileAt(PlayerWorldCell); } }
        public int MonsterCap { get { return WorldCatalog.Dungeon().monsterCapRoomsFactor * worlds[WorldKind.Dungeon].RoomCount; } }
        public string RoomText { get { return $"房间({playerRoom.x},{playerRoom.y}) 格({playerCell.x},{playerCell.y})"; } }

        // ============================================================ 构建

        public void Build(Transform root, Camera cam)
        {
            this.root = root;
            this.cam = cam;
            foreach (WorldKind k in new[] { WorldKind.City, WorldKind.Dungeon })
            {
                WorldGenConfig cfg = WorldCatalog.For(k);
                WorldData data = WorldGenerator.Generate(cfg, cfg.seed);
                WorldValidator.Validate(data);           // 生成即自检：不合法就直接抛异常
                worlds[k] = data;
                maps[k] = new WalkMap(data, cfg.spacing);
                Debug.Log($"[WorldGen] 生成 {data.Describe()}\n{cfg.DumpIMatrix()}");
            }

            views[WorldKind.City] = WorldView.Build(worlds[WorldKind.City], materials, root);
            views[WorldKind.Dungeon] = WorldView.Build(worlds[WorldKind.Dungeon], materials, root);
            views[WorldKind.Dungeon].SetVisible(false);
            currentKind = WorldKind.City;
            lastDungeonPlayerRoom = worlds[WorldKind.Dungeon].spawnRoom;

            WorldData city = worlds[WorldKind.City];
            player.pos = ArrivalPos(WorldKind.City, city.spawnRoom, city.spawnCell);
            player.facing = new Vector2Int(0, -1);
            player.RestoreFull();

            playerView = PlayerView.Create("玩家", player, sprites, cam, root);
            playerView.ApplyVisualImmediate();
            Note($"出生在城市的房间({city.spawnRoom.x},{city.spawnRoom.y}) 格({city.spawnCell.x},{city.spawnCell.y})");
        }

        public void EnsureView(WorldKind kind)
        {
            if (!views.ContainsKey(kind)) views[kind] = WorldView.Build(worlds[kind], materials, root);
        }

        /// <summary>格子中心 + 朝房间中心退 0.6 格（避免落在门格/梯子格里反复触发）。</summary>
        public Vector2 ArrivalPos(WorldKind kind, Vector2Int roomKey, Vector2Int cell)
        {
            RoomData room = worlds[kind].Room(roomKey);
            if (room == null) return Vector2.zero;
            Vector3 c = room.CellCenter(cell.x, cell.y);
            Vector2 p = new Vector2(c.x, c.z);
            Vector3 rc = room.worldOrigin + new Vector3(room.width * 0.5f, 0f, room.height * 0.5f);
            Vector2 inward = new Vector2(rc.x - p.x, rc.z - p.y);
            if (inward.sqrMagnitude > 1e-4f) p += inward.normalized * 0.6f;
            return p;
        }

        // ============================================================ 每帧

        public void Tick(float deltaTime, Vector2Int inputDir, bool dashRequested, bool attackRequested, bool jumpRequested = false)
        {
            playTime += deltaTime;
            if (ladderCooldown > 0f) ladderCooldown = Mathf.Max(0f, ladderCooldown - deltaTime);

            WalkMap map = Map;
            player.Tick(deltaTime, map, inputDir, dashRequested, attackRequested, jumpRequested);
            if (player.moved.sqrMagnitude > 1e-6f) moveCount++;

            if (player.ExitPortalIfAny(map, true)) { Note($"穿过门（III=2.1）-> {RoomText}"); SyncRoomVisibility(); }
            HandleLadder(map);
            ResolveSwordHits();
            TickSlimes(deltaTime);
            ResolveCollisions();
            TickDrops(deltaTime);   // 文档 L44：拾取判定（碰撞即触发背包存放指令）
            TickSpawner(deltaTime);
            if (player.Dead) Respawn();

            // 反馈 3：只显示玩家当前所在的房间，以及同一个房间里的史莱姆。
            SyncRoomVisibility();
        }

        /// <summary>反馈 3：玩家只能看到自己所在的房间；不在同一房间（或不在同一世界）的史莱姆也隐藏。</summary>
        void SyncRoomVisibility()
        {
            Vector2Int key = PlayerRoomKey;
            if (key.x < 0 || key.y < 0) return;
            shownRoom = key;
            visited[currentKind].Add(key);   // 调试2-5：小地图只显示去过的房间

            WorldData w = World;
            WorldView view = View;
            if (view == null) return;
            view.SetVisible(true);

            // 调试2-4：可见房间 = 玩家房间 + 门相连的相邻房间 + 相机地面视野内的房间（缩小视角时能看到更多房间）
            visibleRooms.Clear();
            visibleRooms.Add(key);
            CollectDoorNeighbours(w, key);
            CollectRoomsInCameraView(w);
            view.ShowRooms(visibleRooms);

            for (int i = 0; i < slimes.Count && i < slimeViews.Count; i++)
            {
                SlimeView sv = slimeViews[i];
                if (sv == null) continue;
                bool same = false;
                if (currentKind == WorldKind.Dungeon && !slimes[i].Dead && Map != null)
                {
                    RoomData sr; Vector2Int sl;
                    if (Map.TryRoomOf(slimes[i].pos, out sr, out sl)) same = visibleRooms.Contains(sr.grid);
                }
                sv.SetVisible(same);
            }
        }

        /// <summary>调试2-4：门相连的邻居房间也一起显示（站在门口能看到对面房间）。</summary>
        void CollectDoorNeighbours(WorldData w, Vector2Int key)
        {
            if (w == null) return;
            for (int i = 0; i < w.portals.Count; i++)
            {
                RoomPortal p = w.portals[i];
                if (p.roomA == key) visibleRooms.Add(p.roomB);
                if (p.roomB == key) visibleRooms.Add(p.roomA);
            }
        }

        /// <summary>调试2-4：相机地面视野矩形覆盖到的房间都显示（缩放越小看到越多，不然缩放没意义）。</summary>
        void CollectRoomsInCameraView(WorldData w)
        {
            if (w == null || cam == null || player == null) return;
            float halfV = cam.orthographicSize;
            float halfW = halfV * Mathf.Max(cam.aspect, 0.2f);
            float sinPitch = Mathf.Max(0.3f, -cam.transform.forward.y);
            float halfDepth = halfV / sinPitch;
            float margin = 1.25f;
            float x0 = player.pos.x - halfW - margin, x1 = player.pos.x + halfW + margin;
            float z0 = player.pos.y - halfDepth - margin, z1 = player.pos.y + halfDepth + margin;
            foreach (Vector2Int k in WorldGenerator.SortedKeys(w))
            {
                RoomData r = w.rooms[k];
                float rx0 = r.worldOrigin.x, rx1 = r.worldOrigin.x + r.width;
                float rz0 = r.worldOrigin.z, rz1 = r.worldOrigin.z + r.height;
                if (rx1 < x0 || rx0 > x1 || rz1 < z0 || rz0 > z1) continue;
                visibleRooms.Add(k);
            }
        }

        void HandleLadder(WalkMap map)
        {
            bool onLadder = FuncCode.IsWorldPortal(map.FuncAt(PlayerWorldCell));
            if (!onLadder) { ladderArmed = true; return; }
            if (!ladderArmed || ladderCooldown > 0f) return;
            ladderArmed = false;
            ladderCooldown = 0.8f;
            WorldKind to = currentKind == WorldKind.City ? WorldKind.Dungeon : WorldKind.City;
            WorldData target = worlds[to];
            SwitchWorld(to, target.worldPortalRoom, target.worldPortalCell);
            Note($"踩到梯子（III=2.2）-> 切换到{WorldNameCn}（M={(int)to}）");
        }

        /// <summary>挥剑的扇形命中判定（每只怪一次挥剑只挨一下，判定用剑刃扫过的角度区间）。</summary>
        void ResolveSwordHits()
        {
            if (!player.Attacking || slimes.Count == 0) return;
            for (int i = 0; i < slimes.Count; i++)
            {
                SlimeSim s = slimes[i];
                if (s.Dead || s.worldKind != currentKind) continue;
                if (!CombatSector.Contains(player.pos, player.facing, PlayerSim.SwordLength, PlayerSim.SwordHalfAngleDeg, s.pos, s.radius)) continue;
                // 自我审查第 3 条：隔着墙不能互殴（视线被挡就没有伤害）。
                if (!LineOfSight.Clear(Map, player.pos, s.pos, true)) continue;
                float rel = CombatSector.RelativeDeg(player.facing, player.pos, s.pos);
                if (!player.Swept(rel, rel)) continue;
                bool killed = s.TakeHit(player.atk, s.pos - player.pos);
                player.hitCount++;
                hitCount++;
                Note(killed
                    ? $"剑命中 {s.tag}：-{player.atk} HP -> 击杀（第 {killCount + 1} 只）"
                    : $"剑命中 {s.tag}：-{player.atk} HP（剩 {s.hp}/{SlimeSim.HpDefault}），已击退 0.5 格");
                if (killed) killCount++;
            }
        }

        void TickSlimes(float deltaTime)
        {
            WalkMap dungeonMap = maps[WorldKind.Dungeon];
            WorldData dungeon = worlds[WorldKind.Dungeon];
            // 反馈 5：主角不在这个世界时，怪也照常行动（跟着记忆走到主角离开的位置，再自己游荡 / 换房间）。
            bool playerInWorld = currentKind == WorldKind.Dungeon && !player.Dead;
            PlayerSim target = playerInWorld ? player : null;
            for (int i = slimes.Count - 1; i >= 0; i--)
            {
                SlimeSim s = slimes[i];
                s.Tick(deltaTime, dungeonMap, dungeon, target, playerInWorld);

                // 怪自己踩到门格就跟着传送进配对房间（forPlayer:false 让它绝不会坐梯子跨世界）。
                // 调试3-1：与主角同房间时禁止任何跨房间移动；不同房间时走到门前一格才允许穿门
                bool slimeSameRoom = playerInWorld && s.SameRoomAs(dungeonMap, player.pos);
                if (!s.Dead && !slimeSameRoom && s.ExitPortalIfAny(dungeonMap, false))
                {
                    s.portalCount++;
                    Vector2Int sc = WalkMap.CellOf(s.pos);
                    Note($"{s.tag} 跟着穿过门（III=2.1）-> 格({sc.x},{sc.y})");
                }

                if (target != null && s.state == SlimeState.Lunge && !s.lungeHit && s.GapTo(player) <= 0.12f)
                {
                    s.lungeHit = true;
                    s.hitPlayerCount++;
                    // 自我审查第 3 条：隔墙不算命中。
                    if (LineOfSight.Clear(dungeonMap, s.pos, player.pos, true) && player.Hurt(s.atk, s.tag))
                        Note($"{s.tag} 扑击命中：主角 -{s.atk} HP（{player.hp}/{player.hpMax}）");
                }
                if (s.state != SlimeState.Lunge) s.lungeHit = false;

                if (s.Dead)
                {
                    SpawnDrop(s.pos, s.tag);   // 文档 L43：死亡位置掉落史莱姆清
                    Note($"{s.tag} 被消灭");
                    slimes.RemoveAt(i);
                    slimeViews.RemoveAt(i);
                }
            }
        }

        /// <summary>模型碰撞体积：主角与怪、怪与怪都不相交重合。</summary>
        void ResolveCollisions()
        {
            if (currentKind != WorldKind.Dungeon || slimes.Count == 0) return;
            WalkMap map = Map;
            for (int i = 0; i < slimes.Count; i++)
            {
                ActorBody.Separate(player, slimes[i], map, true, false);
                for (int j = i + 1; j < slimes.Count; j++)
                    ActorBody.Separate(slimes[i], slimes[j], map, false, false);
            }
        }

        /// <summary>刷怪判定：到点就判定，成功就真的生成史莱姆（达到上限时判定照做但不生成）。</summary>
        void TickSpawner(float deltaTime)
        {
            WorldData dungeon = worlds[WorldKind.Dungeon];
            Vector2Int playerRoomInDungeon = currentKind == WorldKind.Dungeon ? PlayerRoomKey : lastDungeonPlayerRoom;
            if (currentKind == WorldKind.Dungeon) lastDungeonPlayerRoom = playerRoomInDungeon;
            if (currentKind == WorldKind.Dungeon) lastDungeonPlayerRoom = PlayerRoomKey;

            MonsterSpawnPlan plan = monsterDirector.Tick(deltaTime, dungeon, playerRoomInDungeon, slimes.Count);
            if (plan == null) return;
            lastJudge = plan;
            if (plan.MonsterCount == 0)
            {
                Note($"刷怪判定：抽样 {plan.sampledCells} 格，本次没有怪生成（场上 {slimes.Count}/{MonsterCap}）");
                return;
            }

            foreach (MonsterSpawnPoint pt in plan.points)
            {
                RoomData room = dungeon.Room(pt.room);
                if (room == null) continue;
                Vector3 c = room.CellCenter(pt.cell.x, pt.cell.y);
                var s = new SlimeSim(nextSlimeId++)
                {
                    worldKind = WorldKind.Dungeon,
                    pos = new Vector2(c.x, c.z)
                };
                s.Note($"在房间({pt.room.x},{pt.room.y})格({pt.cell.x},{pt.cell.y})生成（距玩家 {pt.roomDistance} 个房间）");
                slimes.Add(s);
                slimeViews.Add(SlimeView.Create(s.tag, s, sprites, cam, views[WorldKind.Dungeon].root.transform));
            }
            Note($"刷怪判定：生成 {plan.MonsterCount} 只（场上 {slimes.Count}/{MonsterCap}）");
        }

        /// <summary>文档 L43：史莱姆死亡后在其死亡位置留下一个"史莱姆清"（0.6 格，可拾取）。</summary>
        void SpawnDrop(Vector2 pos, string fromTag)
        {
            var d = new SlimeDrop();
            d.pos = pos;
            d.amount = 1;
            d.tag = SlimeDrop.ItemName;
            drops.Add(d);
            if (views.ContainsKey(WorldKind.Dungeon) && cam != null)
                dropViews.Add(SlimeDropView.Create($"{SlimeDrop.ItemName}#{drops.Count}", d, sprites, cam, views[WorldKind.Dungeon].root.transform));
            Note($"{fromTag} 掉落 {SlimeDrop.ItemName}（{SlimeDrop.SizeCells:0.0}×{SlimeDrop.SizeCells:0.0} 格）");
        }

        /// <summary>
        /// 文档 L44：主角模型与掉落物模型碰撞（圆心距 <= 0.6 = 两个半径之和）就触发背包的存放指令。
        /// 按拾取次序放进对应编号的格子；存不下（50 格已满 / 该物品到 99）就终止存放指令，模型留在原地。
        /// </summary>
        void TickDrops(float deltaTime)
        {
            for (int i = 0; i < drops.Count; i++)
            {
                SlimeDrop d = drops[i];
                if (d.picked) continue;
                if (d.retryTimer > 0f) d.retryTimer = Mathf.Max(0f, d.retryTimer - deltaTime);
                if (currentKind != WorldKind.Dungeon) continue;
                if (Vector2.Distance(player.pos, d.pos) > SlimeDrop.PickupDistance) continue;

                int slotBefore = inventory.SlotNumberOf(SlimeDrop.ItemName);
                int put = inventory.Add(SlimeDrop.ItemName, d.amount);
                if (put <= 0)
                {
                    if (d.retryTimer <= 0f)
                    {
                        d.retryTimer = SlimeDrop.RetrySeconds;
                        Note($"{SlimeDrop.ItemName} 存放失败（已用 {inventory.UsedSlots}/{Inventory.SlotCount} 格、单格上限 {Inventory.StackMax}）：模型留在原地");
                    }
                    continue;
                }

                d.amount -= put;
                int slot = inventory.SlotNumberOf(SlimeDrop.ItemName);
                if (slotBefore == 0) Note($"拾取 {SlimeDrop.ItemName} x{put} -> {slot} 号格子（共 {inventory.Count(SlimeDrop.ItemName)} 个）");
                else Note($"拾取 {SlimeDrop.ItemName} x{put}（{slot} 号格子，共 {inventory.Count(SlimeDrop.ItemName)} 个）");

                if (d.amount <= 0)
                {
                    d.picked = true;
                    drops.RemoveAt(i);
                    if (i < dropViews.Count)
                    {
                        SlimeDropView v = dropViews[i];
                        dropViews.RemoveAt(i);
                        if (v != null) Object.Destroy(v.gameObject);
                    }
                    i--;
                }
            }
        }

        void Respawn()
        {
            respawnCount++;
            Note($"主角血量归零：回到{WorldNameCn}出生点并回满血");
            player.RestoreFull();
            WorldData w = World;
            SwitchWorld(currentKind, w.spawnRoom, w.spawnCell);
        }

        public void SwitchWorld(WorldKind kind, Vector2Int room, Vector2Int cell)
        {
            EnsureView(kind);
            // 反馈 5：主角坐梯子离开地牢时，把所有怪的记忆设成"主角离开的那一格"，它们会跟到梯子旁边（但不坐梯子）。
            if (currentKind == WorldKind.Dungeon && kind != WorldKind.Dungeon)
            {
                Vector2Int leaveCell = WalkMap.CellOf(player.pos);
                for (int i = 0; i < slimes.Count; i++) slimes[i].SetMemoryTo(maps[WorldKind.Dungeon], worlds[WorldKind.Dungeon], leaveCell);
            }
            if (views.ContainsKey(currentKind)) views[currentKind].SetVisible(false);
            currentKind = kind;
            player.pos = ArrivalPos(kind, room, cell);
            player.portalCooldown = 0.3f;
            if (kind == WorldKind.Dungeon) lastDungeonPlayerRoom = room;
            views[kind].SetVisible(true);
            if (playerView != null) playerView.ApplyVisualImmediate();
            monsterDirector.ResetTimer();
            ladderArmed = false;
            ladderCooldown = 0.6f;
            shownRoom = new Vector2Int(int.MinValue, int.MinValue);
            SyncRoomVisibility();
        }

        /// <summary>回到城市出生点（R 键）。</summary>
        public void ResetToSpawn()
        {
            WorldData city = worlds[WorldKind.City];
            SwitchWorld(WorldKind.City, city.spawnRoom, city.spawnCell);
            player.RestoreFull();
            Note("调试：回到城市出生点");
        }

        /// <summary>调试：直接切到另一个世界的出生点（V 键）。</summary>
        public void ToggleWorldDebug()
        {
            WorldKind to = currentKind == WorldKind.City ? WorldKind.Dungeon : WorldKind.City;
            WorldData w = worlds[to];
            SwitchWorld(to, w.spawnRoom, w.spawnCell);
            Note($"调试：切换到{WorldNameCn}（M={(int)to}）的出生房间");
        }

        /// <summary>当前世界里"每个房间到玩家所在房间的距离"（刷怪规则用，HUD 展示）。</summary>
        public Dictionary<Vector2Int, int> PlayerRoomDistances()
        {
            return RoomDistanceField.Distances(World, PlayerRoomKey);
        }

        /// <summary>背包里的史莱姆清数量（HUD 用）。</summary>
        public int SlimeJellyCount { get { return inventory.Count(SlimeDrop.ItemName); } }

        /// <summary>背包摘要（HUD 用）：已用格子数 + 每格物品与数量。</summary>
        public string InventoryText
        {
            get
            {
                if (inventory.UsedSlots == 0) return $"背包：0/{Inventory.SlotCount} 格（空）";
                return $"背包：{inventory.UsedSlots}/{Inventory.SlotCount} 格　{inventory.Describe()}";
            }
        }

        /// <summary>场上还没被拾取的掉落物数量（HUD / 台架用）。</summary>
        public int DropCount { get { return drops.Count; } }

        public void Note(string s)
        {
            lastEvent = s;
        }
    }
}
