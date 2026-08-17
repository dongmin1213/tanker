using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 런 전체의 흐름 (v0.5): 타이틀 → 파티 소개 → 맵(7노드) → 전투/이벤트/상점/휴식 → 카드 보상 → 보스 → 엔딩.
    /// 런 규칙(시드·파티·덱·골드·회복)은 여기, 전투 규칙은 BattleManager에 있다.
    /// 모든 화면은 우상단 설정(볼륨·언어·타이틀 복귀)을 단다 — currentScreen이 언어 변경 후 재그리기 콜백.
    public class GameFlow : MonoBehaviour
    {
        public RunState run;              // 시뮬레이션·디버그 접근용
        List<ShopItem> shop;
        RelicId? shopRelic;               // 이번 상점의 유물 매물 (v0.7)
        bool shopRelicSold;
        RectTransform root;
        RectTransform screen;
        GameObject battleGo;
        Image animImg;                    // 화면 장식 스프라이트 애니메이션 (타이틀 히어로 등)
        Sprite[] animFrames;
        float animT;
        System.Action currentScreen;      // 언어 변경 후 현재 화면 재그리기

        void Start()
        {
            root = UiKit.MakeCanvas("FlowCanvas", 10);
            AudioKit.PlayBgm();
            ShowTitle();
        }

        void Update()
        {
            if (animImg != null && animFrames != null)
            {
                animT += Time.deltaTime;
                animImg.sprite = animFrames[(int)(animT * 5f) % animFrames.Length];
            }
        }

        void Clear()
        {
            DeckViewUI.Close(); // 이전 화면에서 열린 오버레이가 새 화면 위에 남지 않게
            StatusUI.Close();
            animImg = null; animFrames = null;
            if (screen != null)
            {
                // Destroy는 프레임 끝 처리 — 즉시 비활성화해 같은 프레임 이중 클릭(보상 중복·노드 건너뜀)을 차단
                screen.gameObject.SetActive(false);
                Destroy(screen.gameObject);
            }
            screen = UiKit.Rt("screen", root, new Vector2(540, 960), new Vector2(1080, 1920));
        }

        static Sprite bgCache;

        void Background(float dim)
        {
            if (bgCache == null)
            {
                // Resources.Load는 캐시가 없을 때 1회만 — 화면 전환마다 반복 로드 금지 규약
                var tex = Resources.Load<Texture2D>("Art/bg-dungeon");
                if (tex != null)
                    bgCache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 128f);
            }
            if (bgCache != null)
            {
                var bg = UiKit.Panel("bg", screen, Vector2.zero, new Vector2(1350, 2400), Color.white, center: true);
                bg.sprite = bgCache; bg.raycastTarget = false;
            }
            else UiKit.Panel("bg", screen, Vector2.zero, new Vector2(1350, 2400), UiKit.Hex("161221"), center: true);
            UiKit.Panel("dim", screen, Vector2.zero, new Vector2(1350, 2400), new Color(0, 0, 0, dim), center: true)
                 .raycastTarget = false;
        }

        /// 설정 기어 — 타이틀 화면에선 복귀 버튼 없음
        void Gear(bool onTitle = false)
        {
            SettingsUI.AttachGear(screen, () => currentScreen?.Invoke(),
                onTitle ? (System.Action)null : AbortBattleToTitle);
        }

        /// 배너 헤더 — 프레임 패널 위 제목
        void Banner(string text, float y, float w = 760)
        {
            var p = UiKit.FramedPanel("banner", screen, new Vector2(0, y), new Vector2(w, 120), center: true);
            p.raycastTarget = false;
            UiKit.Label("h1", screen, new Vector2(0, y), new Vector2(w, 90), text, 56, Color.white, bold: true, center: true);
        }

        /// 화면 장식 스프라이트 (필요 시 애니메이션 등록)
        Image Deco(string sheet, Vector2 pos, Vector2 size, bool animate = false)
        {
            var frames = UiKit.LoadSheet(sheet);
            if (frames == null) return null;
            var img = UiKit.Panel("deco_" + sheet, screen, pos, size, Color.white, center: true);
            img.sprite = frames[0]; img.preserveAspect = true; img.raycastTarget = false;
            if (animate) { animImg = img; animFrames = frames; }
            return img;
        }

        /// 전투 중이든 아니든 안전하게 타이틀로
        public void AbortBattleToTitle()
        {
            if (battleGo != null) { Destroy(battleGo); battleGo = null; }
            ShowTitle();
        }

        // ---------- 타이틀 ----------

        public void ShowTitle()
        {
            currentScreen = ShowTitle;
            Clear();
            Background(0.35f);
            Gear(onTitle: true);
            UiKit.Label("h1", screen, new Vector2(0, 560), new Vector2(1000, 90), Loc.T("title.name"), 84, Color.white, bold: true, center: true);
            UiKit.Label("sub", screen, new Vector2(0, 455), new Vector2(1000, 50),
                Loc.T("title.sub"), 36, UiKit.Hex("cfc8e8"), center: true);

            Deco("tank-idle", new Vector2(0, 40), new Vector2(420, 470), animate: true);

            bool canContinue = RunSave.Has();
            UiKit.Btn("start", screen, new Vector2(0, canContinue ? -400 : -480), new Vector2(520, 130),
                Loc.T("title.start"), () => StartRun(), 44, center: true);
            if (canContinue)
                UiKit.Btn("continue", screen, new Vector2(0, -560), new Vector2(520, 120),
                    Loc.T("title.continue"), () => ContinueRun(), 38, center: true);
            float rowY = canContinue ? -710 : -640;
            UiKit.Btn("help", screen, new Vector2(-190, rowY), new Vector2(350, 96),
                Loc.T("title.help"), () => HelpUI.Open(), 30, center: true);
            UiKit.Btn("codex", screen, new Vector2(190, rowY), new Vector2(350, 96),
                Loc.T("codex.h1"), () => CodexUI.Open(), 30, center: true);
            UiKit.Label("ver", screen, new Vector2(0, -860), new Vector2(800, 40), Loc.T("title.ver"), 26, UiKit.Hex("8f86ad"), center: true);
        }

        public void StartRun()
        {
            int seed = System.Environment.TickCount & 0x7fffffff;
            bool firstRun = PlayerPrefs.GetInt("runs.started", 0) == 0;
            PlayerPrefs.SetInt("runs.started", PlayerPrefs.GetInt("runs.started", 0) + 1);
            RunSave.Clear();
            run = new RunState(seed, firstRun);
            shop = RunData.MakeShop();
            Debug.Log("[Flow] 런 시작 — 시드 " + seed + ", 파티 " + run.Party.Count + "명" + (firstRun ? " (첫 원정 고정)" : ""));
            if (firstRun) HelpUI.Open(); // 첫 원정 — 게임 방법 자동 안내
            ShowParty();
        }

        public void ContinueRun()
        {
            var loaded = RunSave.Load();
            if (loaded == null) { ShowTitle(); return; }
            run = loaded;
            shop = RunData.MakeShop();
            Debug.Log("[Flow] 이어하기 — 시드 " + run.Seed + ", 층 " + run.FloorReached);
            switch (run.Pending)
            {
                case 1: ShowCardRewardInner(run.PendingTreasure, run.PendingGold); break; // 미수령 카드 보상부터
                case 2: ShowRelicGain(); break;                                           // 엘리트 유물부터
                case 3: ShowDescend(); break;                                             // 심층 선택부터
                default: ShowMap(); break;
            }
        }

        // ---------- 파티 소개 ----------

        public void ShowParty()
        {
            currentScreen = ShowParty;
            Clear();
            Background(0.55f);
            Gear();
            Banner(Loc.T("party.h1"), 740);
            // 구성 규칙을 숨기지 않는다 — "왜 이 파티인가"에 대한 답 (친절성 검사)
            bool hasHealer = false;
            foreach (var id in run.Party) if (RunData.Class(id).IsHealer) hasHealer = true;
            UiKit.Label("desc", screen, new Vector2(0, 640), new Vector2(980, 44),
                Loc.F("party.desc2", run.Party.Count, System.Enum.GetValues(typeof(ClassId)).Length), 27, UiKit.Hex("cfc8e8"), center: true);
            if (!hasHealer)
                UiKit.Label("noheal", screen, new Vector2(0, 596), new Vector2(980, 40),
                    Loc.T("party.noHealer"), 26, UiKit.Hex("e8895e"), center: true);

            float y = 450;
            DrawPartyRow(Loc.T("unit.tank"), "tank", RunState.TankMax, 0, true, y, Trait.None);
            for (int i = 0; i < run.Party.Count; i++)
            {
                y -= 195;
                var cd = RunData.Class(run.Party[i]);
                DrawPartyRow(Loc.T(cd.LocKey), cd.Sheet, cd.Hp, cd.Power, cd.IsHealer, y, cd.Trait);
            }

            UiKit.Btn("go", screen, new Vector2(0, -700), new Vector2(520, 130), Loc.T("party.go"), () => ShowMap(), 44, center: true);
        }

        void DrawPartyRow(string name, string sheet, int hp, int power, bool healer, float y, Trait trait)
        {
            var row = UiKit.FramedPanel("row_" + name, screen, new Vector2(0, y), new Vector2(940, 180), center: true);
            row.raycastTarget = false;
            var frames = UiKit.LoadSheet(sheet + "-idle");
            var icon = UiKit.Panel("p_" + name, screen, new Vector2(-350, y - 5), new Vector2(140, 155), Color.white, center: true);
            if (frames != null) { icon.sprite = frames[0]; icon.preserveAspect = true; }
            else icon.color = UiKit.Hex("3a3153");
            icon.raycastTarget = false;
            string stat = healer && power > 0 ? Loc.F("party.heal", hp, power)
                        : power > 0 ? Loc.F("party.attack", hp, power)
                        : Loc.F("party.tank", hp);
            // 고유 특성은 소개 화면에서 반드시 설명한다 (매커니즘이 보이지 않으면 없는 것과 같다)
            if (trait != Trait.None)
                stat += "\n<color=#ffd75e>" + (trait == Trait.TankHealOnHit
                    ? Loc.F("trait." + trait, Balance.I.paladinTankHeal) : Loc.T("trait." + trait)) + "</color>";
            UiKit.Label("pn_" + name, screen, new Vector2(90, y), new Vector2(620, 170),
                name + "\n<size=24>" + stat + "</size>", 36, Color.white, TextAnchor.MiddleLeft, true, center: true);
        }

        // ---------- 맵 ----------

        public void ShowMap()
        {
            currentScreen = ShowMap;
            Clear();
            Background(0.55f);
            Gear();
            Banner(Loc.T("map.h1"), 790);

            // 파티 현황 스트립 — 미니 초상 + HP. 탭하면 내 상태(유물·덱) 열람 (v0.8)
            var strip = UiKit.FramedPanel("strip", screen, new Vector2(0, 622), new Vector2(1000, 190), center: true);
            strip.raycastTarget = true;
            var stripBtn = strip.gameObject.AddComponent<Button>();
            stripBtn.transition = Selectable.Transition.None;
            stripBtn.onClick.AddListener(() => { AudioKit.Click(); StatusUI.Open(run); });
            UiKit.Label("stripHint", screen, new Vector2(388, 690), new Vector2(190, 30),
                Loc.T("status.tap"), 19, UiKit.Hex("8f86ad"), TextAnchor.MiddleRight, center: true);
            int count = 1 + run.Party.Count;
            float step = Mathf.Min(190f, 880f / count);
            float x0 = -(count - 1) * step / 2f;
            DrawMiniAlly(x0, 638, "tank", Loc.T("unit.tank"), run.TankHp, run.TankMaxHp); // 강철 심장 반영 최대치
            for (int i = 0; i < run.Party.Count; i++)
            {
                var cd = RunData.Class(run.Party[i]);
                DrawMiniAlly(x0 + (i + 1) * step, 638, cd.Sheet, Loc.T(cd.LocKey), run.PartyHp[i], cd.Hp);
            }
            UiKit.Label("gold", screen, new Vector2(0, 495), new Vector2(940, 36),
                run.Gold + "G   ·   " + Loc.F("map.deck", run.Deck.Count), 26, UiKit.Hex("ffd75e"), center: true);

            // 유물 칩 행 (v0.7)
            if (run.Relics.Count > 0)
            {
                string chips = "";
                foreach (var r in run.Relics) chips += (chips == "" ? "" : "  ·  ") + Loc.T("relic." + r);
                UiKit.Label("relics", screen, new Vector2(0, 458), new Vector2(1000, 32),
                    "<color=#c9a44a>" + chips + "</color>", 21, Color.white, center: true);
            }

            // 분기 그래프 — 연결선 먼저(현재 방에서 나가는 길은 금색), 그 위에 방
            var reach = run.Reachable();
            foreach (var n in run.Map)
                foreach (var nx in n.Next)
                    PathLine(NodePos(n), NodePos(run.Map[nx]), hot: run.Cur == n.Id);
            foreach (var n in run.Map)
                DrawMapNode(n, reach.Contains(n.Id));

            UiKit.Label("pick", screen, new Vector2(0, -700), new Vector2(1000, 40),
                Loc.T("map.pick"), 26, UiKit.Hex("ffd75e"), center: true);
        }

        static Vector2 NodePos(MapNode n) => new Vector2(n.X, -570 + n.Floor * 132);

        void DrawMiniAlly(float x, float y, string sheet, string name, int hp, int maxHp)
        {
            var frames = UiKit.LoadSheet(sheet + "-idle");
            var icon = UiKit.Panel("m_" + name + x, screen, new Vector2(x, y + 10), new Vector2(92, 100), Color.white, center: true);
            if (frames != null) { icon.sprite = frames[0]; icon.preserveAspect = true; }
            else icon.color = UiKit.Hex("3a3153");
            icon.raycastTarget = false;
            float pct = Mathf.Clamp01(hp / (float)maxHp);
            UiKit.Panel("mhbg_" + name + x, screen, new Vector2(x, y - 50), new Vector2(96, 11), UiKit.Hex("241d33"), center: true).raycastTarget = false;
            var fill = UiKit.Panel("mhp_" + name + x, screen, new Vector2(x - 48 + 48 * pct, y - 50), new Vector2(96 * pct, 11),
                pct > 0.5f ? UiKit.Hex("8fd4a8") : UiKit.Hex("e8895e"), center: true);
            fill.raycastTarget = false;
            UiKit.Label("mn_" + name + x, screen, new Vector2(x, y - 74), new Vector2(150, 26), name + " " + hp, 19, UiKit.Hex("cfc8e8"), center: true);
        }

        void PathLine(Vector2 a, Vector2 b, bool hot)
        {
            var mid = (a + b) / 2f; var d = b - a;
            var line = UiKit.Panel("line", screen, mid, new Vector2(d.magnitude - 80, hot ? 9 : 6),
                hot ? UiKit.Hex("c9a44a") : UiKit.Hex("4a4468"), center: true);
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.raycastTarget = false;
        }

        /// 전용 아이콘 우선, 도착 전엔 유닛 시트 폴백 (전용 아이콘 = 종류 즉시 식별 — 크리틱 반영)
        static string[] NodeSheets(NodeType t, int act) => t switch
        {
            NodeType.Battle => new[] { "icon-battle", "goblin-idle" },
            NodeType.Elite => new[] { "icon-elite", "brute-idle" },
            NodeType.Boss => new[] { act >= 1 ? "lich-idle" : "boss-idle" }, // 2막 보스는 리치 왕

            NodeType.Event => new[] { "icon-trap", "spider-idle" },
            NodeType.Rest => new[] { "icon-rest", "fx-heal" },
            NodeType.Shop => new[] { "icon-shop" },
            NodeType.Treasure => new[] { "icon-chest" },
            _ => new string[0],
        };

        static string NodeGlyph(NodeType t) => t == NodeType.Shop ? "$" : t == NodeType.Treasure ? "G" : "?";

        void DrawMapNode(MapNode n, bool canGo)
        {
            var p = NodePos(n);
            bool done = run.Visited.Contains(n.Id) && run.Cur != n.Id;
            bool now = run.Cur == n.Id;
            float s = now || canGo ? 130 : 114; // 모바일 터치 타겟 확대 (크리틱 반영)
            var frame = UiKit.FramedPanel("node" + n.Id, screen, p, new Vector2(s, s), center: true);
            frame.raycastTarget = canGo;
            if (now) frame.color = new Color(1.25f, 1.2f, 0.85f);
            else if (canGo) frame.color = new Color(1.45f, 1.3f, 0.75f); // 갈 수 있는 방 — 확실한 금빛
            else if (done) frame.color = new Color(0.35f, 0.4f, 0.35f);
            else frame.color = new Color(0.38f, 0.36f, 0.48f);           // 잠긴 방 — 뚜렷하게 어둡게

            if (canGo)
            {
                var btn = frame.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int id = n.Id;
                btn.onClick.AddListener(() => { AudioKit.Click(); EnterNode(id); });
                // 틴트만으론 어두운 배경에서 안 보인다 — 금색 아웃라인으로 확실하게
                var glow = frame.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(1f, 0.84f, 0.37f, 0.9f);
                glow.effectDistance = new Vector2(5, 5);
            }

            float iconAlpha = done ? 0.3f : canGo || now ? 1f : 0.55f;
            Sprite[] frames = null;
            foreach (var key in NodeSheets(n.Type, run.Act))
                if ((frames = UiKit.LoadSheet(key)) != null) break;
            if (frames != null)
            {
                var icon = UiKit.Panel("ni" + n.Id, screen, p + new Vector2(0, 10), new Vector2(s - 48, s - 54), Color.white, center: true);
                icon.sprite = frames[0]; icon.preserveAspect = true; icon.raycastTarget = false;
                icon.color = new Color(1, 1, 1, iconAlpha);
            }
            else
                UiKit.Label("nq" + n.Id, screen, p + new Vector2(0, 10), new Vector2(80, 60), NodeGlyph(n.Type), 44,
                    new Color(1f, 0.84f, 0.37f, iconAlpha), bold: true, center: true);

            UiKit.Label("nl" + n.Id, screen, p + new Vector2(0, -s / 2 + 18), new Vector2(s + 40, 24),
                RunData.NodeTitle(n.Type), 19,
                done ? UiKit.Hex("6a6288") : canGo || now ? Color.white : UiKit.Hex("9a92b8"), center: true);

            if (done)
                UiKit.Label("nd" + n.Id, screen, p + new Vector2(0, 10), new Vector2(90, 90), "✓", 52, UiKit.Hex("8fd4a8"), bold: true, center: true);

            // 현재 위치 — 탱커 말 배지 (내가 어디 있는지 즉시 보이게, 크리틱 반영)
            if (now)
            {
                var tk = UiKit.LoadSheet("tank-idle");
                if (tk != null)
                {
                    var marker = UiKit.Panel("nm" + n.Id, screen, p + new Vector2(-s / 2 + 6, s / 2 - 2), new Vector2(52, 58), Color.white, center: true);
                    marker.sprite = tk[0]; marker.preserveAspect = true; marker.raycastTarget = false;
                }
            }
        }

        int lastEnterFrame = -1;

        public void EnterNode(int id)
        {
            if (!run.Reachable().Contains(id)) return; // 연결된 방만 (UI 밖 호출 안전망)
            if (battleGo != null) return;              // 전투 중 상태 오염 방지
            if (Time.frameCount == lastEnterFrame) return; // 멀티터치 동시 탭 — 같은 프레임 이중 진입 차단
            lastEnterFrame = Time.frameCount;
            run.Cur = id;
            run.Visited.Add(id);
            switch (run.Map[id].Type)
            {
                case NodeType.Battle:
                case NodeType.Elite:
                case NodeType.Boss:
                    StartBattle(RunData.GetEncounter(run));
                    break;
                case NodeType.Event:
                    // 이벤트 3종 — 시드 결정: 가시 함정 / 수상한 제단 / 떠돌이 학자 (v0.7 탐험 다양화)
                    switch (new System.Random(run.Seed * 389 + run.Act * 4099 + run.Cur * 53).Next(3)) // 막마다 다른 스트림
                    {
                        case 0: ShowEvent(); break;
                        case 1: ShowAltar(); break;
                        default: ShowScholar(); break;
                    }
                    break;
                case NodeType.Rest: ShowRest(); break;
                case NodeType.Shop:
                    shop = RunData.MakeShop(); // 상점마다 새 재고 — 할인은 표시/구매 시점에 계산 (인장 즉시 반영)
                    shopRelic = RandomNewRelic();
                    shopRelicSold = false;
                    ShowShop();
                    break;
                case NodeType.Treasure: ShowTreasure(); break;
            }
        }

        void Advance()
        {
            run.Pending = 0; run.PendingTreasure = false; run.PendingGold = 0; // 보상 수령 완료
            RunSave.Save(run); // 방 완료마다 자동 저장 — 중단해도 이어하기 (모바일 필수)
            ShowMap();
        }

        // ---------- 보물 상자 (v0.6) ----------

        public void ShowTreasure()
        {
            int got = Balance.I.treasureGold;
            run.Gold += got;
            // 골드 지급 즉시 체크포인트 — 보상 화면에서 중단해도 골드 소실·중복 지급 없음
            run.Pending = 1; run.PendingTreasure = true; run.PendingGold = got;
            RunSave.Save(run);
            ShowCardRewardInner(true, got);
        }

        // ---------- 심층 선택 (v0.8 — 2막) ----------

        /// 워로드 격파 후: 여기서 끝내면 승리, 더 내려가면 2막 (플레이타임 15분/30분 조절 장치)
        public void ShowDescend()
        {
            currentScreen = ShowDescend;
            Clear();
            Background(0.5f);
            Gear();
            Banner(Loc.T("descend.h1"), 640);
            Deco("tank-idle", new Vector2(0, 380), new Vector2(300, 340), animate: true);
            var descP = UiKit.FramedPanel("descP", screen, new Vector2(0, 120), new Vector2(940, 180), center: true);
            descP.raycastTarget = false;
            UiKit.Label("desc", screen, new Vector2(0, 120), new Vector2(880, 160),
                Loc.T("descend.desc"), 30, UiKit.Hex("cfc8e8"), center: true);

            UiKit.Btn("descend", screen, new Vector2(0, -90), new Vector2(860, 130), Loc.T("descend.go"), () =>
            {
                run.Act = 1;
                run.RestAll(Balance.I.descendHeal, camp: false); // 심층 진입 재정비 — 물주머니 미적용
                run.Map = RunData.GenerateMap(new System.Random(run.Seed * 777 + 13));
                run.Cur = -1;
                run.Visited.Clear();
                run.Pending = 0;
                RunSave.Save(run);
                ShowMap();
            }, 33, center: true);

            UiKit.Btn("return", screen, new Vector2(0, -260), new Vector2(860, 120), Loc.T("descend.return"), () => ShowEnding(true), 33, center: true);
        }

        // ---------- 유물 (v0.7) ----------

        RelicId? RandomNewRelic()
        {
            var pool = new List<RelicId>();
            foreach (RelicId r in System.Enum.GetValues(typeof(RelicId)))
                if (!run.Has(r)) pool.Add(r);
            if (pool.Count == 0) return null;
            var rng = new System.Random(run.Seed * 263 + run.Act * 5407 + run.Cur * 31); // 막마다 다른 스트림
            return pool[rng.Next(pool.Count)];
        }

        /// 엘리트 승리 보상 — 미보유 유물 확정 획득 (전부 보유 시 골드 대체)
        public void ShowRelicGain()
        {
            var pick = RandomNewRelic();
            if (pick == null) { run.Gold += Balance.I.relicDupGold; run.Pending = 1; RunSave.Save(run); ShowCardReward(); return; }
            run.AddRelic(pick.Value);
            // 획득 즉시 체크포인트 — 유물 화면에서 중단해도 같은 유물이 두 번 나오지 않는다
            run.Pending = 1;
            RunSave.Save(run);
            ShowRelicScreen(pick.Value, () => ShowCardReward());
        }

        void ShowRelicScreen(RelicId r, System.Action next)
        {
            currentScreen = () => ShowRelicScreen(r, next);
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("relic.h1"), 620);
            var chip = UiKit.FramedPanel("relicChip", screen, new Vector2(0, 260), new Vector2(700, 340), center: true);
            chip.raycastTarget = false;
            UiKit.Label("rname", screen, new Vector2(0, 350), new Vector2(620, 60),
                Loc.T("relic." + r), 44, UiKit.Hex("ffd75e"), bold: true, center: true);
            UiKit.Label("rdesc", screen, new Vector2(0, 230), new Vector2(600, 160),
                RelicDesc(r), 30, Color.white, center: true);
            UiKit.Label("rhint", screen, new Vector2(0, 40), new Vector2(900, 40),
                Loc.T("relic.hint"), 24, UiKit.Hex("8f86ad"), center: true);
            UiKit.Btn("go", screen, new Vector2(0, -240), new Vector2(460, 125), Loc.T("result.continue"), () => next(), 40, center: true);
        }

        public static string RelicDesc(RelicId r)
        {
            var b = Balance.I;
            switch (r)
            {
                case RelicId.ThornShield: return Loc.F("relic.ThornShield.desc", b.relicThornShield);
                case RelicId.WarBanner: return Loc.F("relic.WarBanner.desc", b.relicWarBanner);
                case RelicId.GoldMagnet: return Loc.F("relic.GoldMagnet.desc", b.relicGoldMagnet);
                case RelicId.WaterSkin: return Loc.F("relic.WaterSkin.desc", (int)(b.relicWaterSkin * 100));
                case RelicId.VictoryMeal: return Loc.F("relic.VictoryMeal.desc", b.relicVictoryMeal);
                case RelicId.IronHeart: return Loc.F("relic.IronHeart.desc", b.relicIronHeart);
                case RelicId.MerchantSeal: return Loc.F("relic.MerchantSeal.desc", (int)(b.relicMerchantSeal * 100));
                case RelicId.OldStandard: return Loc.F("relic.OldStandard.desc", b.relicOldStandard);
                case RelicId.GuardCharm: return Loc.F("relic.GuardCharm.desc", b.relicGuardCharm);
                default: return Loc.T("relic." + r + ".desc");
            }
        }

        // ---------- 전투 ----------

        public void StartBattle(EncounterDef def)
        {
            if (battleGo != null) return; // 진입 연타 → 전투 이중 생성 방지
            Clear();
            battleGo = new GameObject("Battle");
            var mgr = battleGo.AddComponent<BattleManager>();
            battleGo.AddComponent<BattleUI>();
            mgr.Init(def, run);
            mgr.Finished += won => OnBattleFinished(mgr, won);
        }

        void OnBattleFinished(BattleManager mgr, bool won)
        {
            if (battleGo == null) return; // 연타 중복 가드
            run.TotalRedirected += mgr.RedirectedSaved;
            run.TotalMitigated += mgr.MitigatedSaved;
            if (won)
            {
                run.BattlesWon++;
                run.Gold += mgr.RewardGold;
                run.TankHp = mgr.Tank.Hp;
                for (int i = 0; i < run.Party.Count && i + 1 < mgr.Allies.Count; i++)
                    run.PartyHp[i] = mgr.Allies[i + 1].Hp;
                run.BattleIndex++;
            }
            bool wasBoss = run.CurNode.Type == NodeType.Boss;
            bool wasElite = run.CurNode.Type == NodeType.Elite;
            Destroy(battleGo);
            battleGo = null;
            if (!won) { ShowEnding(false); return; }
            // 승리 즉시 체크포인트 — 보상 화면에서 중단해도 전투 재플레이 없이 보상부터 이어한다
            if (wasBoss && run.Act == 0) run.Pending = 3;
            else if (wasBoss) run.Pending = 0;               // 진엔딩 직행 — 엔딩이 저장을 지운다
            else run.Pending = wasElite ? 2 : 1;
            run.PendingTreasure = false; run.PendingGold = 0;
            if (!wasBoss || run.Act == 0) RunSave.Save(run);
            if (wasBoss && run.Act == 0) ShowDescend();      // 1막 보스 격파 — 귀환/심층 선택 (v0.8)
            else if (wasBoss) ShowEnding(true);              // 리치 격파 — 진엔딩
            else if (wasElite) ShowRelicGain();              // 엘리트 — 유물 확정 보상 후 카드 보상
            else ShowCardReward();
        }

        // ---------- 카드 보상 ----------

        public void ShowCardReward() => ShowCardRewardInner(false, 0);

        /// treasure/gold를 캡처 — 문자열이 아닌 인자를 캡처해야 언어 변경 재그리기가 새 언어로 나온다
        void ShowCardRewardInner(bool treasure, int gold)
        {
            currentScreen = () => ShowCardRewardInner(treasure, gold);
            Clear();
            Background(0.6f);
            Gear();
            Banner(treasure ? Loc.T("treasure.h1") : Loc.T("reward.h1"), 640);
            UiKit.Label("desc", screen, new Vector2(0, 530), new Vector2(920, 50),
                treasure ? Loc.F("treasure.desc", gold) : Loc.T("reward.desc"), 30, UiKit.Hex("cfc8e8"), center: true);

            var rng = new System.Random(run.Seed * 397 + run.Act * 6421 + run.Cur * 71); // 막마다 다른 스트림
            var offered = new List<CardType>();
            var pool = new List<CardType>(Cards.Pool);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                var pick = pool[rng.Next(pool.Count)];
                pool.Remove(pick);
                offered.Add(pick);
            }

            // 세로 대형 카드 3장 나란히 — 전투 핸드와 같은 인상
            for (int i = 0; i < offered.Count; i++)
            {
                var card = new Card(offered[i]);
                float x = (i - (offered.Count - 1) / 2f) * 350f;
                var btn = UiKit.Btn("reward" + i, screen, new Vector2(x, 130), new Vector2(320, 460),
                    Cards.NameOf(card) + "\n\n<size=24>" + Cards.DescOf(card) + "</size>", () =>
                    {
                        run.Deck.Add(card);
                        Advance();
                    }, 34, center: true);
                if (UiKit.CardSprite != null) btn.GetComponent<Image>().sprite = UiKit.CardSprite;
                var txt = btn.GetComponentInChildren<Text>();
                txt.horizontalOverflow = HorizontalWrapMode.Wrap; // 긴 설명은 카드 안에서 줄바꿈
                txt.rectTransform.sizeDelta = new Vector2(272, 420);
            }

            // 보물 상자 — 카드 대신 유물 선택지 (v0.7)
            if (treasure && RandomNewRelic() != null)
                UiKit.Btn("relicOpt", screen, new Vector2(0, -240), new Vector2(560, 110), Loc.T("treasure.relic"), () =>
                {
                    var pick = RandomNewRelic();
                    if (pick == null) { Advance(); return; }
                    run.AddRelic(pick.Value);
                    // 선택 즉시 보상 소진 저장 — 유물 화면에서 중단 후 재선택으로 유물 파밍 방지
                    run.Pending = 0; run.PendingTreasure = false; run.PendingGold = 0;
                    RunSave.Save(run);
                    ShowRelicScreen(pick.Value, () => Advance());
                }, 32, center: true);

            UiKit.Btn("skip", screen, new Vector2(0, -380), new Vector2(420, 110), Loc.T("reward.skip"), () => Advance(), 34, center: true);
        }

        // ---------- 이벤트: 가시 함정 복도 ----------

        public void ShowEvent()
        {
            currentScreen = ShowEvent;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("ev.h1"), 700);
            Deco("spider-idle", new Vector2(0, 500), new Vector2(280, 220));
            var descP = UiKit.FramedPanel("descP", screen, new Vector2(0, 300), new Vector2(940, 170), center: true);
            descP.raycastTarget = false;
            UiKit.Label("desc", screen, new Vector2(0, 300), new Vector2(880, 150),
                Loc.T("ev.desc"), 32, UiKit.Hex("cfc8e8"), center: true);

            UiKit.Btn("a", screen, new Vector2(0, 110), new Vector2(880, 120), Loc.F("ev.a", Balance.I.trapTankCost), () =>
            {
                run.TankHp -= Balance.I.trapTankCost;
                if (run.TankHp <= 0) { ShowEnding(false); return; }
                Advance();
            }, 36, center: true);

            var payBtn = UiKit.Btn("b", screen, new Vector2(0, -40), new Vector2(880, 120), Loc.F("ev.b", Balance.I.trapToll), () =>
            {
                run.Gold -= Balance.I.trapToll;
                Advance();
            }, 36, center: true);
            payBtn.interactable = run.Gold >= Balance.I.trapToll;

            UiKit.Btn("c", screen, new Vector2(0, -190), new Vector2(880, 120), Loc.F("ev.c", Balance.I.trapDpsCost), () =>
            {
                // 최강 공격수가 대가를 치른다
                int idx = -1; int bestPower = -1;
                for (int i = 0; i < run.Party.Count; i++)
                {
                    var cd = RunData.Class(run.Party[i]);
                    if (!cd.IsHealer && run.PartyHp[i] > 0 && cd.Power > bestPower) { bestPower = cd.Power; idx = i; }
                }
                if (idx >= 0)
                {
                    run.PartyHp[idx] -= Balance.I.trapDpsCost;
                    if (run.PartyHp[idx] <= 0) { ShowEnding(false); return; }
                    run.DpsShakenNext = true;
                }
                else run.TankHp = Mathf.Max(1, run.TankHp - Balance.I.trapDpsCost);
                Advance();
            }, 32, center: true);

            UiKit.Label("hint", screen, new Vector2(0, -350), new Vector2(900, 40),
                Loc.T("ev.hint"), 26, UiKit.Hex("8f86ad"), center: true);
        }

        // ---------- 이벤트 2·3: 수상한 제단 / 떠돌이 학자 (v0.7) ----------

        public void ShowAltar()
        {
            currentScreen = ShowAltar;
            Clear();
            Background(0.65f);
            Gear();
            Banner(Loc.T("altar.h1"), 660);
            Deco("necro-idle", new Vector2(0, 440), new Vector2(240, 260));
            var descP = UiKit.FramedPanel("descP", screen, new Vector2(0, 220), new Vector2(940, 170), center: true);
            descP.raycastTarget = false;
            UiKit.Label("desc", screen, new Vector2(0, 220), new Vector2(880, 150),
                Loc.F("altar.desc", Balance.I.altarHpCost), 31, UiKit.Hex("cfc8e8"), center: true);

            var offer = UiKit.Btn("offer", screen, new Vector2(0, 20), new Vector2(860, 122),
                Loc.F("altar.offer", Balance.I.altarHpCost), () =>
                {
                    run.TankHp -= Balance.I.altarHpCost;
                    if (run.TankHp <= 0) { ShowEnding(false); return; }
                    var pick = RandomNewRelic();
                    if (pick == null) { run.Gold += Balance.I.relicDupGold; Advance(); return; }
                    run.AddRelic(pick.Value);
                    ShowRelicScreen(pick.Value, () => Advance());
                }, 33, center: true);
            offer.interactable = run.TankHp > Balance.I.altarHpCost;

            UiKit.Btn("ignore", screen, new Vector2(0, -140), new Vector2(860, 115), Loc.T("altar.ignore"), () => Advance(), 33, center: true);
        }

        public void ShowScholar()
        {
            currentScreen = ShowScholar;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("scholar.h1"), 660);
            Deco("bard-idle", new Vector2(0, 440), new Vector2(220, 260));
            var descP = UiKit.FramedPanel("descP", screen, new Vector2(0, 220), new Vector2(940, 170), center: true);
            descP.raycastTarget = false;
            UiKit.Label("desc", screen, new Vector2(0, 220), new Vector2(880, 150),
                Loc.T("scholar.desc"), 31, UiKit.Hex("cfc8e8"), center: true);

            var learnBtn = UiKit.Btn("learn", screen, new Vector2(0, 20), new Vector2(860, 122),
                Loc.T("scholar.remove"), () => ShowScholarRemove(), 33, center: true);
            learnBtn.interactable = run.Deck.Count > 1;
            UiKit.Btn("ignore", screen, new Vector2(0, -140), new Vector2(860, 115), Loc.T("altar.ignore"), () => Advance(), 33, center: true);
        }

        void ShowScholarRemove()
        {
            currentScreen = ShowScholarRemove;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("scholar.pick"), 700);
            for (int i = 0; i < run.Deck.Count && i < 10; i++)
            {
                int idx = i;
                float y = 500 - (i / 2) * 130;
                float x = i % 2 == 0 ? -230 : 230;
                UiKit.Btn("sc" + i, screen, new Vector2(x, y), new Vector2(420, 110),
                    Cards.NameOf(run.Deck[i]), () =>
                    {
                        if (run.Deck.Count <= 1) return;
                        run.Deck.RemoveAt(idx);
                        Advance();
                    }, 30, center: true);
            }
            UiKit.Btn("back", screen, new Vector2(0, -600), new Vector2(420, 105), Loc.T("remove.back"), () => ShowScholar(), 32, center: true);
        }

        // ---------- 휴식 / 상점 ----------

        /// 휴식 — 회복 또는 카드 강화 택1 (StS 캠프파이어, v0.7 깊이의 핵)
        public void ShowRest()
        {
            currentScreen = ShowRest;
            Clear();
            Background(0.5f);
            Gear();
            Banner(Loc.T("rest.h1"), 620);
            Deco("tank-idle", new Vector2(-120, 330), new Vector2(280, 320), animate: true);
            Deco("icon-rest", new Vector2(130, 300), new Vector2(190, 190));
            UiKit.Label("desc", screen, new Vector2(0, 90), new Vector2(920, 50),
                Loc.T("rest.choose"), 30, UiKit.Hex("cfc8e8"), center: true);

            int pct = (int)((Balance.I.restRatio + (run.Has(RelicId.WaterSkin) ? Balance.I.relicWaterSkin : 0)) * 100);
            UiKit.Btn("heal", screen, new Vector2(0, -60), new Vector2(720, 125), Loc.F("rest.heal", pct), () =>
            {
                int before = run.TankHp;
                run.RestAll(Balance.I.restRatio);
                ShowRestScreen(run.TankHp - before);
            }, 34, center: true);

            bool upgradable = false;
            foreach (var c in run.Deck) if (!c.Plus) upgradable = true;
            var upBtn = UiKit.Btn("upgrade", screen, new Vector2(0, -220), new Vector2(720, 125), Loc.T("rest.upgrade"), () => ShowUpgradePick(), 34, center: true);
            upBtn.interactable = upgradable;
        }

        /// 강화할 카드 선택 — 미강화 카드만 나열
        public void ShowUpgradePick()
        {
            currentScreen = ShowUpgradePick;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("up.h1"), 700);
            UiKit.Label("desc", screen, new Vector2(0, 590), new Vector2(940, 44),
                Loc.T("up.desc"), 27, UiKit.Hex("cfc8e8"), center: true);

            int shown = 0;
            for (int i = 0; i < run.Deck.Count && shown < 10; i++)
            {
                if (run.Deck[i].Plus) continue;
                int idx = i;
                float y = 460 - (shown / 2) * 135;
                float x = shown % 2 == 0 ? -235 : 235;
                var c = run.Deck[i];
                UiKit.Btn("up" + i, screen, new Vector2(x, y), new Vector2(430, 118),
                    Cards.NameOf(c) + "\n<size=20>" + Cards.ShortDesc(new Card(c.Type, true)) + "</size>", () =>
                    {
                        run.Deck[idx] = new Card(run.Deck[idx].Type, true);
                        AudioKit.Heal();
                        ShowUpgradeDone(run.Deck[idx]);
                    }, 28, center: true);
                shown++;
            }

            UiKit.Btn("back", screen, new Vector2(0, -600), new Vector2(420, 105), Loc.T("remove.back"), () => ShowRest(), 32, center: true);
        }

        void ShowUpgradeDone(Card c)
        {
            currentScreen = () => ShowUpgradeDone(c);
            Clear();
            Background(0.55f);
            Gear();
            Banner(Loc.T("up.done"), 560);
            var chip = UiKit.FramedPanel("upChip", screen, new Vector2(0, 180), new Vector2(560, 420), center: true);
            chip.raycastTarget = false;
            UiKit.Label("upName", screen, new Vector2(0, 300), new Vector2(520, 60), Cards.NameOf(c), 46, Color.white, bold: true, center: true);
            UiKit.Label("upDesc", screen, new Vector2(0, 140), new Vector2(480, 200), Cards.DescOf(c), 30, UiKit.Hex("8fd4a8"), center: true);
            UiKit.Btn("go", screen, new Vector2(0, -260), new Vector2(460, 125), Loc.T("rest.go"), () => Advance(), 40, center: true);
        }

        void ShowRestScreen(int healed)
        {
            currentScreen = () => ShowRestScreen(healed);
            Clear();
            Background(0.5f);
            Gear();
            Banner(Loc.T("rest.h1"), 560);
            Deco("tank-idle", new Vector2(-110, 210), new Vector2(300, 340), animate: true);
            Deco("fx-heal", new Vector2(140, 160), new Vector2(210, 210));
            UiKit.Label("desc", screen, new Vector2(0, -100), new Vector2(920, 120),
                Loc.F("rest.desc2", healed), 36, UiKit.Hex("8fd4a8"), center: true);
            UiKit.Btn("go", screen, new Vector2(0, -330), new Vector2(520, 130), Loc.T("rest.go"), () => Advance(), 44, center: true);
        }

        /// 모든 상점 가격의 단일 계산점 — 상인 인장은 매물·카드 제거·유물 전부, 구매 직후에도 즉시 적용
        int ShopPrice(int basePrice) => Mathf.RoundToInt(basePrice
            * (run.Has(RelicId.MerchantSeal) ? 1f - Balance.I.relicMerchantSeal : 1f));

        public void ShowShop()
        {
            currentScreen = ShowShop;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("shop.h1"), 700);
            var goldChip = UiKit.FramedPanel("goldChip", screen, new Vector2(0, 585), new Vector2(300, 90), center: true);
            goldChip.raycastTarget = false;
            UiKit.Label("gold", screen, new Vector2(0, 585), new Vector2(280, 60), run.Gold + "G", 40, UiKit.Hex("ffd75e"), bold: true, center: true);

            for (int i = 0; i < shop.Count; i++)
            {
                var item = shop[i];
                int price = ShopPrice(item.Price);
                float y = 452 - i * 130;
                string label = item.Bought ? Loc.F("shop.soldout", item.Name)
                    : Loc.F("shop.item", item.Name, price, item.Desc);
                var btn = UiKit.Btn("item" + i, screen, new Vector2(0, y), new Vector2(880, 118), label, () =>
                {
                    if (item.Bought || run.Gold < price) return;
                    run.Gold -= price;
                    item.Bought = true;
                    item.Apply(run);
                    ShowShop();
                }, 30, center: true);
                btn.interactable = !item.Bought && run.Gold >= price;
            }

            // 유물 매물 (v0.7)
            if (shopRelic != null)
            {
                int rPrice = ShopPrice(Balance.I.relicPrice);
                string rLabel = shopRelicSold ? Loc.F("shop.soldout", Loc.T("relic." + shopRelic.Value))
                    : Loc.F("shop.relic", Loc.T("relic." + shopRelic.Value), rPrice, RelicDesc(shopRelic.Value));
                var rBtn = UiKit.Btn("relicItem", screen, new Vector2(0, -60), new Vector2(880, 118), rLabel, () =>
                {
                    if (shopRelicSold || run.Gold < rPrice || shopRelic == null) return;
                    run.Gold -= rPrice;
                    shopRelicSold = true;
                    run.AddRelic(shopRelic.Value);
                    ShowShop();
                }, 27, center: true);
                rBtn.interactable = !shopRelicSold && run.Gold >= rPrice;
            }

            var removeBtn = UiKit.Btn("removeCard", screen, new Vector2(0, -200), new Vector2(880, 110),
                Loc.F("shop.remove", ShopPrice(Balance.I.cardRemovePrice)), () => ShowRemoveCard(), 30, center: true);
            removeBtn.interactable = run.Gold >= ShopPrice(Balance.I.cardRemovePrice) && run.Deck.Count > 1;

            UiKit.Btn("leave", screen, new Vector2(0, -400), new Vector2(520, 110), Loc.T("shop.leave"), () => Advance(), 36, center: true);
        }

        // ---------- 카드 제거 ----------

        public void ShowRemoveCard()
        {
            currentScreen = ShowRemoveCard;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("remove.h1"), 700);
            UiKit.Label("desc", screen, new Vector2(0, 590), new Vector2(920, 44),
                Loc.F("remove.desc", ShopPrice(Balance.I.cardRemovePrice)), 28, UiKit.Hex("cfc8e8"), center: true);

            for (int i = 0; i < run.Deck.Count && i < 10; i++)
            {
                int idx = i;
                float y = 460 - (i / 2) * 130;
                float x = i % 2 == 0 ? -230 : 230;
                UiKit.Btn("deck" + i, screen, new Vector2(x, y), new Vector2(420, 110),
                    Cards.NameOf(run.Deck[i]), () =>
                    {
                        int price = ShopPrice(Balance.I.cardRemovePrice);
                        if (run.Gold < price || run.Deck.Count <= 1) return;
                        run.Gold -= price;
                        run.Deck.RemoveAt(idx);
                        ShowShop();
                    }, 30, center: true);
            }

            UiKit.Btn("back", screen, new Vector2(0, -560), new Vector2(420, 110), Loc.T("remove.back"), () => ShowShop(), 34, center: true);
        }

        // ---------- 엔딩 ----------

        public void ShowEnding(bool won)
        {
            RunSave.Clear(); // 런 종료 — 이어하기 소멸
            currentScreen = () => ShowEnding(won);
            Clear();
            Background(won ? 0.3f : 0.78f);
            Gear(onTitle: true);
            bool trueEnd = won && run.Act >= 1; // 리치까지 격파한 진엔딩
            Banner(Loc.T(trueEnd ? "end.true.h1" : won ? "end.win.h1" : "end.lose.h1"), 640);
            UiKit.Label("desc", screen, new Vector2(0, 520), new Vector2(940, 100),
                Loc.T(trueEnd ? "end.true.desc" : won ? "end.win.desc" : "end.lose.desc"),
                36, won ? UiKit.Hex("ffd75e") : UiKit.Hex("cfc8e8"), center: true);

            var hero = Deco("tank-idle", new Vector2(0, 280), new Vector2(340, 380), animate: won);
            if (hero != null && !won) hero.color = new Color(0.45f, 0.4f, 0.5f);

            var statsP = UiKit.FramedPanel("statsP", screen, new Vector2(0, -180), new Vector2(940, 420), center: true);
            statsP.raycastTarget = false;
            UiKit.Label("stats", screen, new Vector2(0, -170), new Vector2(860, 380),
                Loc.F("end.act", run.Act + 1) + "\n" +
                Loc.F("end.stats", run.FloorReached, Balance.I.mapFloors, run.BattlesWon,
                      run.TotalRedirected, run.TotalMitigated, run.Gold)
                + "\n<size=24>" + Loc.F("end.seed", run.Seed) + "</size>", 34, Color.white, center: true);

            UiKit.Btn("retry", screen, new Vector2(0, -520), new Vector2(520, 125), Loc.T("end.retry"), () => StartRun(), 40, center: true);
            UiKit.Btn("title", screen, new Vector2(0, -680), new Vector2(520, 110), Loc.T("end.title"), () => ShowTitle(), 38, center: true);
        }
    }
}
