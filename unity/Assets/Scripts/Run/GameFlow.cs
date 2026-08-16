using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 런 전체의 흐름 (v0.4): 타이틀 → 파티 소개 → 맵(7노드) → 전투/이벤트/상점/휴식 → 카드 보상 → 보스 → 엔딩.
    /// 런 규칙(시드·파티·덱·골드·회복)은 여기, 전투 규칙은 BattleManager에 있다.
    public class GameFlow : MonoBehaviour
    {
        public RunState run;              // 시뮬레이션·디버그 접근용
        List<ShopItem> shop;
        RectTransform root;
        RectTransform screen;
        GameObject battleGo;
        Image titleTank;
        Sprite[] titleFrames;
        float titleT;

        void Start()
        {
            root = UiKit.MakeCanvas("FlowCanvas", 10);
            ShowTitle();
        }

        void Update()
        {
            if (titleTank != null && titleFrames != null)
            {
                titleT += Time.deltaTime;
                titleTank.sprite = titleFrames[(int)(titleT * 5f) % titleFrames.Length];
            }
        }

        void Clear()
        {
            titleTank = null; titleFrames = null;
            if (screen != null) Destroy(screen.gameObject);
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

        Text H1(string text, float y) =>
            UiKit.Label("h1", screen, new Vector2(0, y), new Vector2(1000, 80), text, 72, Color.white, bold: true, center: true);

        // ---------- 타이틀 ----------

        public void ShowTitle()
        {
            Clear();
            Background(0.35f);
            H1(Loc.T("title.name"), 560);
            UiKit.Label("sub", screen, new Vector2(0, 460), new Vector2(1000, 50),
                Loc.T("title.sub"), 36, UiKit.Hex("cfc8e8"), center: true);

            titleFrames = UiKit.LoadSheet("tank-idle");
            if (titleFrames != null)
            {
                var img = UiKit.Panel("hero", screen, new Vector2(0, 40), new Vector2(420, 470), Color.white, center: true);
                img.sprite = titleFrames[0]; img.preserveAspect = true; img.raycastTarget = false;
                titleTank = img;
            }

            UiKit.Btn("start", screen, new Vector2(0, -480), new Vector2(520, 130), Loc.T("title.start"), () => StartRun(), 44, center: true);
            UiKit.Label("ver", screen, new Vector2(0, -840), new Vector2(800, 40), Loc.T("title.ver"), 26, UiKit.Hex("8f86ad"), center: true);
            UiKit.Btn("lang", screen, new Vector2(0, -660), new Vector2(300, 90), Loc.T("title.lang"), () =>
            {
                Loc.SetLang(Loc.Lang == Loc.KO ? Loc.EN : Loc.KO);
                ShowTitle();
            }, 30, center: true);
        }

        public void StartRun()
        {
            int seed = System.Environment.TickCount & 0x7fffffff;
            run = new RunState(seed);
            shop = RunData.MakeShop();
            Debug.Log("[Flow] 런 시작 — 시드 " + seed + ", 파티 " + run.Party.Count + "명");
            ShowParty();
        }

        // ---------- 파티 소개 (신규) ----------

        public void ShowParty()
        {
            Clear();
            Background(0.55f);
            H1(Loc.T("party.h1"), 700);
            UiKit.Label("desc", screen, new Vector2(0, 610), new Vector2(960, 50),
                Loc.T("party.desc"), 30, UiKit.Hex("cfc8e8"), center: true);

            // 탱커 + 동료들 나열
            float y = 430;
            DrawPartyRow(Loc.T("unit.tank"), "tank", RunState.TankMax, 0, true, y);
            for (int i = 0; i < run.Party.Count; i++)
            {
                y -= 190;
                var cd = RunData.Class(run.Party[i]);
                DrawPartyRow(Loc.T(cd.LocKey), cd.Sheet, cd.Hp, cd.Power, cd.IsHealer, y);
            }

            UiKit.Btn("go", screen, new Vector2(0, -700), new Vector2(520, 130), Loc.T("party.go"), () => ShowMap(), 44, center: true);
        }

        void DrawPartyRow(string name, string sheet, int hp, int power, bool healer, float y)
        {
            var frames = UiKit.LoadSheet(sheet + "-idle");
            var icon = UiKit.Panel("p_" + name, screen, new Vector2(-330, y), new Vector2(150, 170), Color.white, center: true);
            if (frames != null) { icon.sprite = frames[0]; icon.preserveAspect = true; }
            else icon.color = UiKit.Hex("3a3153");
            icon.raycastTarget = false;
            string stat = healer && power > 0 ? Loc.F("party.heal", hp, power)
                        : power > 0 ? Loc.F("party.attack", hp, power)
                        : Loc.F("party.tank", hp);
            UiKit.Label("pn_" + name, screen, new Vector2(80, y), new Vector2(600, 160),
                name + "\n<size=26>" + stat + "</size>", 38, Color.white, TextAnchor.MiddleLeft, true, center: true);
        }

        // ---------- 맵 ----------

        public void ShowMap()
        {
            Clear();
            Background(0.55f);
            H1(Loc.T("map.h1"), 760);

            string party = Loc.T("unit.tank") + " " + run.TankHp + "/" + RunState.TankMax;
            for (int i = 0; i < run.Party.Count; i++)
            {
                var cd = RunData.Class(run.Party[i]);
                party += "   " + Loc.T(cd.LocKey) + " " + run.PartyHp[i] + "/" + cd.Hp;
            }
            UiKit.Label("party", screen, new Vector2(0, 670), new Vector2(1040, 40), party, 26, UiKit.Hex("ffd75e"), center: true);
            UiKit.Label("gold", screen, new Vector2(0, 620), new Vector2(1000, 36),
                run.Gold + "G   ·   " + Loc.F("map.deck", run.Deck.Count), 26, UiKit.Hex("8fd4a8"), center: true);

            for (int i = 0; i < RunData.Nodes.Length; i++)
            {
                float y = 490 - i * 105;
                string marker = i < run.Node ? Loc.T("map.done") + "  " : i == run.Node ? "▶  " : "-  ";
                var color = i < run.Node ? UiKit.Hex("6f9a6f") : i == run.Node ? Color.white : UiKit.Hex("6a6288");
                UiKit.Label("node" + i, screen, new Vector2(0, y), new Vector2(860, 60),
                    marker + (i + 1) + ". " + RunData.NodeTitle(i), 34, color, TextAnchor.MiddleLeft, i == run.Node, center: true);
            }

            UiKit.Btn("enter", screen, new Vector2(0, -560), new Vector2(520, 130), Loc.T("map.enter"), () => EnterNode(), 44, center: true);
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

        // ---------- 카드 보상 (신규) ----------

        public void ShowCardReward()
        {
            Clear();
            Background(0.6f);
            H1(Loc.T("reward.h1"), 620);
            UiKit.Label("desc", screen, new Vector2(0, 520), new Vector2(920, 50),
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

            for (int i = 0; i < offered.Count; i++)
            {
                var card = offered[i];
                UiKit.Btn("reward" + i, screen, new Vector2(0, 320 - i * 190), new Vector2(760, 160),
                    Cards.NameOf(card) + "\n<size=24>" + Cards.DescOf(card) + "</size>", () =>
                    {
                        run.Deck.Add(card);
                        Advance();
                    }, 34, center: true);
            }

            UiKit.Btn("skip", screen, new Vector2(0, -420), new Vector2(420, 110), Loc.T("reward.skip"), () => Advance(), 34, center: true);
        }

        // ---------- 이벤트: 가시 함정 복도 ----------

        public void ShowEvent()
        {
            Clear();
            Background(0.6f);
            H1(Loc.T("ev.h1"), 620);
            UiKit.Label("desc", screen, new Vector2(0, 470), new Vector2(920, 160),
                Loc.T("ev.desc"), 34, UiKit.Hex("cfc8e8"), center: true);

            UiKit.Btn("a", screen, new Vector2(0, 240), new Vector2(860, 120), Loc.F("ev.a", Balance.I.trapTankCost), () =>
            {
                run.TankHp -= Balance.I.trapTankCost;
                if (run.TankHp <= 0) { ShowEnding(false); return; }
                Advance();
            }, 36, center: true);

            var payBtn = UiKit.Btn("b", screen, new Vector2(0, 90), new Vector2(860, 120), Loc.F("ev.b", Balance.I.trapToll), () =>
            {
                run.Gold -= Balance.I.trapToll;
                Advance();
            }, 36, center: true);
            payBtn.interactable = run.Gold >= Balance.I.trapToll;

            UiKit.Btn("c", screen, new Vector2(0, -60), new Vector2(860, 120), Loc.F("ev.c", Balance.I.trapDpsCost), () =>
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

            UiKit.Label("hint", screen, new Vector2(0, -220), new Vector2(900, 40),
                Loc.T("ev.hint"), 26, UiKit.Hex("8f86ad"), center: true);
        }

        // ---------- 갈림길 / 휴식 / 상점 ----------

        public void ShowChoice()
        {
            Clear();
            Background(0.6f);
            H1(Loc.T("ch.h1"), 560);
            UiKit.Label("desc", screen, new Vector2(0, 430), new Vector2(920, 100),
                Loc.T("ch.desc"), 32, UiKit.Hex("cfc8e8"), center: true);
            UiKit.Btn("rest", screen, new Vector2(0, 200), new Vector2(720, 130), Loc.F("ch.rest", (int)(Balance.I.restRatio * 100)), () => ShowRest(), 36, center: true);
            UiKit.Btn("shop", screen, new Vector2(0, 30), new Vector2(720, 130), Loc.T("ch.shop"), () => ShowShop(), 36, center: true);
        }

        public void ShowRest()
        {
            int beforeTank = run.TankHp;
            run.RestAll(Balance.I.restRatio);
            Clear();
            Background(0.5f);
            H1(Loc.T("rest.h1"), 480);
            UiKit.Label("desc", screen, new Vector2(0, 320), new Vector2(920, 160),
                Loc.F("rest.desc2", run.TankHp - beforeTank), 36, UiKit.Hex("8fd4a8"), center: true);
            UiKit.Btn("go", screen, new Vector2(0, -160), new Vector2(520, 130), Loc.T("rest.go"), () => Advance(), 44, center: true);
        }

        public void ShowShop()
        {
            Clear();
            Background(0.6f);
            H1(Loc.T("shop.h1"), 660);
            UiKit.Label("gold", screen, new Vector2(0, 565), new Vector2(900, 44), run.Gold + "G", 40, UiKit.Hex("ffd75e"), center: true);

            for (int i = 0; i < shop.Count; i++)
            {
                var item = shop[i];
                float y = 420 - i * 145;
                string label = item.Bought ? Loc.F("shop.soldout", item.Name)
                    : Loc.F("shop.item", item.Name, item.Price, item.Desc);
                var btn = UiKit.Btn("item" + i, screen, new Vector2(0, y), new Vector2(860, 125), label, () =>
                {
                    if (item.Bought || run.Gold < item.Price) return;
                    run.Gold -= item.Price;
                    item.Bought = true;
                    item.Apply(run);
                    ShowShop();
                }, 30, center: true);
                btn.interactable = !item.Bought && run.Gold >= item.Price;
            }

            var removeBtn = UiKit.Btn("removeCard", screen, new Vector2(0, -180), new Vector2(860, 110),
                Loc.F("shop.remove", Balance.I.cardRemovePrice), () => ShowRemoveCard(), 30, center: true);
            removeBtn.interactable = run.Gold >= Balance.I.cardRemovePrice && run.Deck.Count > 1;

            UiKit.Btn("leave", screen, new Vector2(0, -380), new Vector2(520, 110), Loc.T("shop.leave"), () => Advance(), 36, center: true);
        }

        // ---------- 카드 제거 (신규) ----------

        public void ShowRemoveCard()
        {
            Clear();
            Background(0.6f);
            H1(Loc.T("remove.h1"), 660);
            UiKit.Label("desc", screen, new Vector2(0, 570), new Vector2(920, 44),
                Loc.F("remove.desc", Balance.I.cardRemovePrice), 28, UiKit.Hex("cfc8e8"), center: true);

            for (int i = 0; i < run.Deck.Count && i < 10; i++)
            {
                int idx = i;
                float y = 440 - (i / 2) * 130;
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
            Clear();
            Background(won ? 0.35f : 0.75f);
            H1(Loc.T(won ? "end.win.h1" : "end.lose.h1"), 500);
            UiKit.Label("desc", screen, new Vector2(0, 360), new Vector2(940, 120),
                Loc.T(won ? "end.win.desc" : "end.lose.desc"),
                38, UiKit.Hex("cfc8e8"), center: true);

            UiKit.Label("stats", screen, new Vector2(0, 110), new Vector2(940, 260),
                Loc.F("end.stats", run.Node + 1, RunData.Nodes.Length, run.BattlesWon,
                      run.TotalRedirected, run.TotalMitigated, run.Gold)
                + "\n" + Loc.F("end.seed", run.Seed), 34, Color.white, center: true);

            UiKit.Btn("title", screen, new Vector2(0, -280), new Vector2(520, 130), Loc.T("end.title"), () => ShowTitle(), 44, center: true);
        }
    }
}
