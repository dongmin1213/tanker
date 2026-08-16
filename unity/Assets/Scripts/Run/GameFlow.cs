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

            // 파티 현황 스트립 — 미니 초상 + HP
            var strip = UiKit.FramedPanel("strip", screen, new Vector2(0, 600), new Vector2(1000, 210), center: true);
            strip.raycastTarget = false;
            int count = 1 + run.Party.Count;
            float step = Mathf.Min(190f, 880f / count);
            float x0 = -(count - 1) * step / 2f;
            DrawMiniAlly(x0, 625, "tank", Loc.T("unit.tank"), run.TankHp, RunState.TankMax);
            for (int i = 0; i < run.Party.Count; i++)
            {
                var cd = RunData.Class(run.Party[i]);
                DrawMiniAlly(x0 + (i + 1) * step, 625, cd.Sheet, Loc.T(cd.LocKey), run.PartyHp[i], cd.Hp);
            }
            UiKit.Label("gold", screen, new Vector2(0, 512), new Vector2(940, 36),
                run.Gold + "G   ·   " + Loc.F("map.deck", run.Deck.Count), 26, UiKit.Hex("ffd75e"), center: true);

            // 지그재그 경로 + 노드 아이콘
            var pos = new Vector2[RunData.Nodes.Length];
            for (int i = 0; i < RunData.Nodes.Length; i++)
            {
                float ny = 360 - i * 135;
                float nx = (i % 4) switch { 0 => -190f, 1 => 0f, 2 => 190f, _ => 0f };
                pos[i] = new Vector2(nx, ny);
            }
            for (int i = 0; i + 1 < pos.Length; i++) PathLine(pos[i], pos[i + 1]);
            for (int i = 0; i < pos.Length; i++) DrawNode(i, pos[i]);

            UiKit.Btn("enter", screen, new Vector2(0, -700), new Vector2(520, 130),
                Loc.T("map.enter") + "  —  " + RunData.NodeTitle(run.Node), () => EnterNode(), 32, center: true);
        }

        void DrawMiniAlly(float x, float y, string sheet, string name, int hp, int maxHp)
        {
            var frames = UiKit.LoadSheet(sheet + "-idle");
            var icon = UiKit.Panel("m_" + name + x, screen, new Vector2(x, y + 15), new Vector2(105, 115), Color.white, center: true);
            if (frames != null) { icon.sprite = frames[0]; icon.preserveAspect = true; }
            else icon.color = UiKit.Hex("3a3153");
            icon.raycastTarget = false;
            float pct = Mathf.Clamp01(hp / (float)maxHp);
            UiKit.Panel("mhbg_" + name + x, screen, new Vector2(x, y - 58), new Vector2(96, 12), UiKit.Hex("241d33"), center: true).raycastTarget = false;
            var fill = UiKit.Panel("mhp_" + name + x, screen, new Vector2(x - 48 + 48 * pct, y - 58), new Vector2(96 * pct, 12),
                pct > 0.5f ? UiKit.Hex("8fd4a8") : UiKit.Hex("e8895e"), center: true);
            fill.raycastTarget = false;
            UiKit.Label("mn_" + name + x, screen, new Vector2(x, y - 85), new Vector2(150, 28), name + " " + hp, 20, UiKit.Hex("cfc8e8"), center: true);
        }

        void PathLine(Vector2 a, Vector2 b)
        {
            var mid = (a + b) / 2f; var d = b - a;
            var line = UiKit.Panel("line", screen, mid, new Vector2(d.magnitude - 90, 7), UiKit.Hex("5b4f86"), center: true);
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.raycastTarget = false;
        }

        static string NodeSheet(NodeType t) => t switch
        {
            NodeType.Battle => "goblin-idle",
            NodeType.Boss => "boss-idle",
            NodeType.Event => "spider-idle",
            NodeType.Rest => "fx-heal",
            _ => null, // Choice — 물음표 라벨
        };

        void DrawNode(int i, Vector2 p)
        {
            bool done = i < run.Node, now = i == run.Node;
            float s = now ? 150 : 124;
            var frame = UiKit.FramedPanel("node" + i, screen, p, new Vector2(s, s), center: true);
            frame.raycastTarget = false;
            if (done) frame.color = new Color(0.45f, 0.5f, 0.45f);
            else if (now) frame.color = new Color(1.25f, 1.2f, 0.85f);

            var sheet = NodeSheet(RunData.Nodes[i]);
            var frames = UiKit.LoadSheet(sheet);
            if (frames != null)
            {
                var icon = UiKit.Panel("ni" + i, screen, p + new Vector2(0, 4), new Vector2(s - 52, s - 52), Color.white, center: true);
                icon.sprite = frames[0]; icon.preserveAspect = true; icon.raycastTarget = false;
                if (done) icon.color = new Color(1, 1, 1, 0.35f);
            }
            else
                UiKit.Label("nq" + i, screen, p, new Vector2(90, 90), "?", 56, done ? UiKit.Hex("6a6288") : UiKit.Hex("ffd75e"), bold: true, center: true);

            if (done)
                UiKit.Label("nd" + i, screen, p, new Vector2(90, 90), "✓", 60, UiKit.Hex("8fd4a8"), bold: true, center: true);
            // 현재 노드 강조는 확대+금색 틴트+굵은 라벨로 충분 — ▼ 마커는 위 노드를 침범해 제거 (전수검사)

            // 라벨은 좌우 번갈아 바깥쪽에
            float lx = p.x <= -100 ? p.x + s / 2 + 165 : p.x - s / 2 - 165;
            var anchor = p.x <= -100 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            UiKit.Label("nl" + i, screen, new Vector2(lx, p.y), new Vector2(300, 60),
                RunData.NodeTitle(i), 24, done ? UiKit.Hex("6a6288") : now ? Color.white : UiKit.Hex("9a92b8"),
                anchor, now, center: true);
        }

        public void EnterNode()
        {
            switch (RunData.Nodes[run.Node])
            {
                case NodeType.Battle:
                case NodeType.Boss:
                    StartBattle(RunData.GetEncounter(run));
                    break;
                case NodeType.Event: ShowEvent(); break;
                case NodeType.Choice: ShowChoice(); break;
                case NodeType.Rest: ShowRest(); break;
            }
        }

        void Advance()
        {
            run.Node++;
            ShowMap();
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
            bool wasBoss = RunData.Nodes[run.Node] == NodeType.Boss;
            Destroy(battleGo);
            battleGo = null;
            if (!won) ShowEnding(false);
            else if (wasBoss) ShowEnding(true);
            else ShowCardReward();
        }

        // ---------- 카드 보상 ----------

        public void ShowCardReward()
        {
            currentScreen = ShowCardReward;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("reward.h1"), 640);
            UiKit.Label("desc", screen, new Vector2(0, 530), new Vector2(920, 50),
                Loc.T("reward.desc"), 30, UiKit.Hex("cfc8e8"), center: true);

            var rng = new System.Random(run.Seed * 397 + run.Node * 71);
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

        // ---------- 갈림길 / 휴식 / 상점 ----------

        public void ShowChoice()
        {
            currentScreen = ShowChoice;
            Clear();
            Background(0.6f);
            Gear();
            Banner(Loc.T("ch.h1"), 620);
            UiKit.Label("desc", screen, new Vector2(0, 490), new Vector2(920, 100),
                Loc.T("ch.desc"), 32, UiKit.Hex("cfc8e8"), center: true);
            Deco("fx-heal", new Vector2(-260, 260), new Vector2(190, 190));
            Deco("dps-idle", new Vector2(260, 260), new Vector2(170, 190));
            UiKit.Btn("rest", screen, new Vector2(0, 60), new Vector2(720, 130), Loc.F("ch.rest", (int)(Balance.I.restRatio * 100)), () => ShowRest(), 36, center: true);
            UiKit.Btn("shop", screen, new Vector2(0, -110), new Vector2(720, 130), Loc.T("ch.shop"), () => ShowShop(), 36, center: true);
        }

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
                Loc.F("end.stats", run.Node + 1, RunData.Nodes.Length, run.BattlesWon,
                      run.TotalRedirected, run.TotalMitigated, run.Gold)
                + "\n<size=24>" + Loc.F("end.seed", run.Seed) + "</size>", 34, Color.white, center: true);

            UiKit.Btn("retry", screen, new Vector2(0, -520), new Vector2(520, 125), Loc.T("end.retry"), () => StartRun(), 40, center: true);
            UiKit.Btn("title", screen, new Vector2(0, -680), new Vector2(520, 110), Loc.T("end.title"), () => ShowTitle(), 38, center: true);
        }
    }
}
