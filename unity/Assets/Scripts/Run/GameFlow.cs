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
            DeckViewUI.Close(); // 이전 화면에서 열린 덱 열람이 새 화면 위에 남지 않게
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
            var tex = Resources.Load<Texture2D>("Art/bg-dungeon");
            if (tex != null)
            {
                if (bgCache == null)
                    bgCache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 128f);
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

            UiKit.Btn("start", screen, new Vector2(0, -480), new Vector2(520, 130), Loc.T("title.start"), () => StartRun(), 44, center: true);
            UiKit.Label("ver", screen, new Vector2(0, -840), new Vector2(800, 40), Loc.T("title.ver"), 26, UiKit.Hex("8f86ad"), center: true);
        }

        public void StartRun()
        {
            int seed = System.Environment.TickCount & 0x7fffffff;
            run = new RunState(seed);
            shop = RunData.MakeShop();
            Debug.Log("[Flow] 런 시작 — 시드 " + seed + ", 파티 " + run.Party.Count + "명");
            ShowParty();
        }

        // ---------- 파티 소개 ----------

        public void ShowParty()
        {
            currentScreen = ShowParty;
            Clear();
            Background(0.55f);
            Gear();
            Banner(Loc.T("party.h1"), 740);
            UiKit.Label("desc", screen, new Vector2(0, 630), new Vector2(960, 50),
                Loc.T("party.desc"), 30, UiKit.Hex("cfc8e8"), center: true);

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

            // 파티 현황 스트립 — 미니 초상 + HP (맵이 주인공 — 스트립은 낮게)
            var strip = UiKit.FramedPanel("strip", screen, new Vector2(0, 622), new Vector2(1000, 190), center: true);
            strip.raycastTarget = false;
            int count = 1 + run.Party.Count;
            float step = Mathf.Min(190f, 880f / count);
            float x0 = -(count - 1) * step / 2f;
            DrawMiniAlly(x0, 638, "tank", Loc.T("unit.tank"), run.TankHp, RunState.TankMax);
            for (int i = 0; i < run.Party.Count; i++)
            {
                var cd = RunData.Class(run.Party[i]);
                DrawMiniAlly(x0 + (i + 1) * step, 638, cd.Sheet, Loc.T(cd.LocKey), run.PartyHp[i], cd.Hp);
            }
            UiKit.Label("gold", screen, new Vector2(0, 495), new Vector2(940, 36),
                run.Gold + "G   ·   " + Loc.F("map.deck", run.Deck.Count), 26, UiKit.Hex("ffd75e"), center: true);

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
        static string[] NodeSheets(NodeType t) => t switch
        {
            NodeType.Battle => new[] { "icon-battle", "goblin-idle" },
            NodeType.Elite => new[] { "icon-elite", "brute-idle" },
            NodeType.Boss => new[] { "boss-idle" },
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
            foreach (var key in NodeSheets(n.Type))
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
                case NodeType.Event: ShowEvent(); break;
                case NodeType.Rest: ShowRest(); break;
                case NodeType.Shop: shop = RunData.MakeShop(); ShowShop(); break; // 상점마다 새 재고
                case NodeType.Treasure: ShowTreasure(); break;
            }
        }

        void Advance() => ShowMap();

        // ---------- 보물 상자 (v0.6) ----------

        public void ShowTreasure()
        {
            int got = Balance.I.treasureGold;
            run.Gold += got;
            ShowCardRewardInner(true, got);
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
            Destroy(battleGo);
            battleGo = null;
            if (!won) ShowEnding(false);
            else if (wasBoss) ShowEnding(true);
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

            var rng = new System.Random(run.Seed * 397 + run.Cur * 71);
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
                var card = offered[i];
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

            UiKit.Btn("skip", screen, new Vector2(0, -360), new Vector2(420, 110), Loc.T("reward.skip"), () => Advance(), 34, center: true);
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

        // ---------- 휴식 / 상점 ----------

        public void ShowRest()
        {
            int beforeTank = run.TankHp;
            run.RestAll(Balance.I.restRatio);
            currentScreen = () => ShowRestScreen(run.TankHp - beforeTank);
            ShowRestScreen(run.TankHp - beforeTank);
        }

        void ShowRestScreen(int healed)
        {
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
                float y = 440 - i * 145;
                string label = item.Bought ? Loc.F("shop.soldout", item.Name)
                    : Loc.F("shop.item", item.Name, item.Price, item.Desc);
                var btn = UiKit.Btn("item" + i, screen, new Vector2(0, y), new Vector2(880, 128), label, () =>
                {
                    if (item.Bought || run.Gold < item.Price) return;
                    run.Gold -= item.Price;
                    item.Bought = true;
                    item.Apply(run);
                    ShowShop();
                }, 30, center: true);
                btn.interactable = !item.Bought && run.Gold >= item.Price;
            }

            var removeBtn = UiKit.Btn("removeCard", screen, new Vector2(0, -180), new Vector2(880, 110),
                Loc.F("shop.remove", Balance.I.cardRemovePrice), () => ShowRemoveCard(), 30, center: true);
            removeBtn.interactable = run.Gold >= Balance.I.cardRemovePrice && run.Deck.Count > 1;

            UiKit.Btn("leave", screen, new Vector2(0, -380), new Vector2(520, 110), Loc.T("shop.leave"), () => Advance(), 36, center: true);
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
                Loc.F("remove.desc", Balance.I.cardRemovePrice), 28, UiKit.Hex("cfc8e8"), center: true);

            for (int i = 0; i < run.Deck.Count && i < 10; i++)
            {
                int idx = i;
                float y = 460 - (i / 2) * 130;
                float x = i % 2 == 0 ? -230 : 230;
                UiKit.Btn("deck" + i, screen, new Vector2(x, y), new Vector2(420, 110),
                    Cards.NameOf(run.Deck[i]), () =>
                    {
                        if (run.Gold < Balance.I.cardRemovePrice || run.Deck.Count <= 1) return;
                        run.Gold -= Balance.I.cardRemovePrice;
                        run.Deck.RemoveAt(idx);
                        ShowShop();
                    }, 30, center: true);
            }

            UiKit.Btn("back", screen, new Vector2(0, -560), new Vector2(420, 110), Loc.T("remove.back"), () => ShowShop(), 34, center: true);
        }

        // ---------- 엔딩 ----------

        public void ShowEnding(bool won)
        {
            currentScreen = () => ShowEnding(won);
            Clear();
            Background(won ? 0.3f : 0.78f);
            Gear(onTitle: true);
            Banner(Loc.T(won ? "end.win.h1" : "end.lose.h1"), 640);
            UiKit.Label("desc", screen, new Vector2(0, 520), new Vector2(940, 100),
                Loc.T(won ? "end.win.desc" : "end.lose.desc"),
                36, won ? UiKit.Hex("ffd75e") : UiKit.Hex("cfc8e8"), center: true);

            var hero = Deco("tank-idle", new Vector2(0, 280), new Vector2(340, 380), animate: won);
            if (hero != null && !won) hero.color = new Color(0.45f, 0.4f, 0.5f);

            var statsP = UiKit.FramedPanel("statsP", screen, new Vector2(0, -180), new Vector2(940, 420), center: true);
            statsP.raycastTarget = false;
            UiKit.Label("stats", screen, new Vector2(0, -170), new Vector2(860, 380),
                Loc.F("end.stats", run.FloorReached, Balance.I.mapFloors, run.BattlesWon,
                      run.TotalRedirected, run.TotalMitigated, run.Gold)
                + "\n<size=24>" + Loc.F("end.seed", run.Seed) + "</size>", 34, Color.white, center: true);

            UiKit.Btn("retry", screen, new Vector2(0, -520), new Vector2(520, 125), Loc.T("end.retry"), () => StartRun(), 40, center: true);
            UiKit.Btn("title", screen, new Vector2(0, -680), new Vector2(520, 110), Loc.T("end.title"), () => ShowTitle(), 38, center: true);
        }
    }
}
