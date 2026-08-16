using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 전투 UI 전부를 코드로 생성 (v0.4: 가변 파티 + 카드 핸드). 상태는 BattleManager에서 읽고 입력만 전달.
    /// 최적화: 텍스트류는 10Hz로만 갱신, 애니메이션만 매 프레임.
    public class BattleUI : MonoBehaviour
    {
        const float IdleFps = 5f, ActionFps = 10f, FxFps = 12f;

        BattleManager mgr;
        RectTransform root;
        readonly Dictionary<Unit, UnitView> views = new();
        readonly Dictionary<Unit, RectTransform[]> lines = new();
        readonly Dictionary<Unit, Text> intentLabels = new();
        readonly Dictionary<Unit, int> previewCache = new();
        int previewVersion = -1;
        readonly Dictionary<Unit, Vector2> posOf = new();
        Text turnText, savedText, logText, deckText;
        readonly Button[] cardBtns = new Button[3];
        readonly Text[] cardTexts = new Text[3];
        Button goBtn;
        GameObject resultPanel;
        Text resultText, resultBtnText;
        static Sprite sharedGlow;
        Sprite glowSprite;
        Sprite[] fxHit, fxHeal;
        float uiTick;

        class UnitView
        {
            public Unit Unit;
            public RectTransform Rect;
            public Image Body;
            public Image Sprite;
            public Image Glow;
            public Sprite[] Idle, Action, Alt;
            public float ActionT;
            public Outline Outline;
            public Text Name, Status, Incoming;
            public RectTransform HpFill;
            public Color BaseColor;
            public Vector2 Impulse;
            public float FlashT;
            public float BobPhase;
        }

        RectTransform stage;
        static readonly Vector2 StageHome = new Vector2(540, 960);

        static Color Hex(string h) => UiKit.Hex(h);

        void Start()
        {
            mgr = GetComponent<BattleManager>();
            mgr.Popup += ShowPopup;
            mgr.Strike += OnStrike;
            glowSprite = sharedGlow != null ? sharedGlow : sharedGlow = MakeGlowSprite();
            fxHit = LoadSheet("fx-hit");
            fxHeal = LoadSheet("fx-heal");
            root = UiKit.MakeCanvas("BattleCanvas", 20);
            BuildLayout();
        }

        void OnDestroy()
        {
            if (mgr != null) { mgr.Popup -= ShowPopup; mgr.Strike -= OnStrike; }
            if (root != null) Destroy(root.transform.root.gameObject);
        }

        static Sprite[] LoadSheet(string name) => UiKit.LoadSheet(name);

        static Sprite MakeGlowSprite()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }

        static Vector2 SizeOf(Unit u)
        {
            switch (u.Sheet)
            {
                case "tank": return new Vector2(170, 190);
                case "brute": case "orc": return new Vector2(180, 200);
                case "boss": return new Vector2(240, 280);
                case "warrior": case "beastkin": case "paladin": case "berserker": return new Vector2(150, 170);
                case "slime": return new Vector2(130, 120);
                case "spider": return new Vector2(170, 140);
                case "golem": return new Vector2(190, 210);
                case "bat": return new Vector2(120, 100);
                default: return new Vector2(140, 160);
            }
        }

        static Color FallbackColor(Unit u)
        {
            switch (u.Sheet)
            {
                case "tank": return Hex("5878b4");
                case "dps": return Hex("c04858");
                case "healer": return Hex("78b478");
                case "warrior": return Hex("b47a3c");
                case "mage": return Hex("8a5fc0");
                case "ranger": return Hex("4f9a58");
                case "assassin": return Hex("5a3f66");
                case "beastkin": return Hex("8f7a5f");
                case "brute": return Hex("7a4f8f");
                case "boss": return Hex("8f3f3f");
                case "archer": return Hex("9a8f6a");
                case "slime": return Hex("3fa5a0");
                case "orc": return Hex("6f7a3f");
                case "shaman": return Hex("7a55c0");
                case "spider": return Hex("8f3f55");
                case "paladin": return Hex("c0a848");
                case "berserker": return Hex("b43f3f");
                case "bard": return Hex("4f7ab4");
                case "golem": return Hex("6f6f7a");
                case "bat": return Hex("55415f");
                case "necro": return Hex("3f5548");
                default: return Hex("6a8f4f");
            }
        }

        RectTransform Rt(string name, RectTransform parent, Vector2 pos, Vector2 size, bool center = false)
            => UiKit.Rt(name, parent, pos, size, center);

        Image Panel(string name, RectTransform parent, Vector2 pos, Vector2 size, Color c, bool center = false)
            => UiKit.Panel(name, parent, pos, size, c, center);

        Text Label(string name, RectTransform parent, Vector2 pos, Vector2 size, string text,
                   int fontSize, Color c, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false, bool center = false)
            => UiKit.Label(name, parent, pos, size, text, fontSize, c, anchor, bold, center);

        void BuildLayout()
        {
            stage = Rt("stage", root, StageHome, new Vector2(1080, 1920));
            var bgSprite = Resources.Load<Sprite>("Art/bg-dungeon");
            if (bgSprite == null)
            {
                var bgTex = Resources.Load<Texture2D>("Art/bg-dungeon");
                if (bgTex != null)
                    bgSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f), 128f);
            }
            if (bgSprite != null)
            {
                var bg = Panel("bg", stage, new Vector2(540, 960), new Vector2(1350, 2400), Color.white);
                bg.sprite = bgSprite;
                bg.raycastTarget = false;
            }
            else Panel("bgTop", stage, new Vector2(540, 1330), new Vector2(1080, 1180), Hex("1a1626"));
            var bottom = UiKit.FramedPanel("bgBottom", root, new Vector2(540, 160), new Vector2(1120, 1180));
            bottom.raycastTarget = false;

            turnText = Label("turn", root, new Vector2(540, 1850), new Vector2(1000, 50), "", 40, Color.white, bold: true);
            savedText = Label("saved", root, new Vector2(540, 1790), new Vector2(1000, 40), "", 30, Hex("ffd75e"));
            logText = Label("log", root, new Vector2(540, 770), new Vector2(1020, 60), "", 34, Hex("cfc8e8"));
            deckText = Label("deck", root, new Vector2(540, 705), new Vector2(1000, 36), "", 24, Hex("8f86ad"));

            // 아군 슬롯 — 탱커 고정 + 동료 최대 4
            posOf[mgr.Tank] = new Vector2(400, 1120);
            var allySlots = new[]
            {
                new Vector2(265, 1310), new Vector2(105, 1360),
                new Vector2(290, 1530), new Vector2(120, 1600),
            };
            for (int i = 1; i < mgr.Allies.Count && i - 1 < allySlots.Length; i++)
                posOf[mgr.Allies[i]] = allySlots[i - 1];

            // 적 슬롯 — 큰 놈이 앞
            var slots = mgr.Enemies.Count switch
            {
                1 => new[] { new Vector2(770, 1160) },
                2 => new[] { new Vector2(760, 1130), new Vector2(885, 1500) },
                3 => new[] { new Vector2(770, 1100), new Vector2(890, 1310), new Vector2(730, 1490) },
                _ => new[] { new Vector2(770, 1090), new Vector2(920, 1280), new Vector2(730, 1450), new Vector2(930, 1560) },
            };
            var ordered = new List<Unit>(mgr.Enemies);
            ordered.Sort((a, b) => (SizeOf(b).x * SizeOf(b).y).CompareTo(SizeOf(a).x * SizeOf(a).y));
            for (int i = 0; i < ordered.Count; i++) posOf[ordered[i]] = slots[Mathf.Min(i, slots.Length - 1)];

            foreach (var e in mgr.Enemies)
            {
                var pair = new RectTransform[4]; // 광역 최대 4명 + 거미 엄호 둘째 타까지 전부 시각화
                for (int i = 0; i < pair.Length; i++)
                {
                    var line = Rt("line_" + e.Name + i, stage, Vector2.zero, new Vector2(0, 6));
                    line.pivot = new Vector2(0, 0.5f);
                    var li = line.gameObject.AddComponent<Image>();
                    li.color = Hex("ff6b6b"); li.raycastTarget = false;
                    line.gameObject.SetActive(false);
                    pair[i] = line;
                }
                lines[e] = pair;
                var size = SizeOf(e);
                intentLabels[e] = Label("intent_" + e.Name, stage, posOf[e] + new Vector2(0, size.y * 0.75f + 40),
                                        new Vector2(360, 40), "", 28, Hex("ff6b6b"), bold: true);
            }

            foreach (var a in mgr.Allies) MakeUnitView(a);
            foreach (var e in mgr.Enemies) MakeUnitView(e);

            // 핸드 카드 3장(세로 대형 — 모바일 탭 타겟) + 전폭 진행 바
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                cardBtns[i] = UiKit.Btn("card" + i, root, new Vector2(190 + i * 350, 480), new Vector2(300, 400), "", () => mgr.PressCard(idx), 34);
                cardTexts[i] = cardBtns[i].GetComponentInChildren<Text>();
            }
            goBtn = UiKit.Btn("go", root, new Vector2(540, 155), new Vector2(980, 130), Loc.T("skill.go"), () => mgr.EndTurn(), 42);
            Label("hintGo", root, new Vector2(540, 45), new Vector2(1000, 40), Loc.T("hint.cards"), 22, Hex("8f86ad"));

            var flow = Object.FindFirstObjectByType<GameFlow>();
            SettingsUI.AttachGear(root, null, flow != null ? (System.Action)flow.AbortBattleToTitle : null);

            resultPanel = Panel("result", root, new Vector2(540, 960), new Vector2(1080, 1920), new Color(0, 0, 0, 0.72f)).gameObject;
            var resultRt = resultPanel.GetComponent<RectTransform>();
            resultText = Label("resultTxt", resultRt, new Vector2(0, 60), new Vector2(900, 200), "", 60, Color.white, bold: true, center: true);
            var contBtn = UiKit.Btn("continue", resultRt, new Vector2(0, -140), new Vector2(420, 120), "", () => mgr.PressContinue(), center: true);
            resultBtnText = contBtn.GetComponentInChildren<Text>();
            resultPanel.SetActive(false);
        }

        void MakeUnitView(Unit u)
        {
            var size = SizeOf(u);
            var c = FallbackColor(u);
            var idle = LoadSheet(u.Sheet + "-idle");
            var body = Panel("unit_" + u.Name, stage, posOf[u], size, idle != null ? Color.clear : c);
            var outline = body.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(5, 5);
            outline.enabled = false;
            var btn = body.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => mgr.ClickUnit(u));

            Image spriteImg = null, glow = null;
            if (idle != null)
            {
                glow = Panel("glow", body.rectTransform, new Vector2(0, size.y * 0.08f), size * 1.7f, Color.clear, center: true);
                glow.sprite = glowSprite; glow.raycastTarget = false;
                var shadow = Panel("shadow", body.rectTransform, new Vector2(0, -size.y * 0.52f), new Vector2(size.x * 1.05f, size.y * 0.22f), new Color(0, 0, 0, 0.35f), center: true);
                shadow.sprite = glowSprite; shadow.raycastTarget = false;
                var spRt = Rt("sprite", body.rectTransform, new Vector2(0, size.y * 0.08f), size * 1.5f, center: true);
                spriteImg = spRt.gameObject.AddComponent<Image>();
                spriteImg.preserveAspect = true; spriteImg.raycastTarget = false;
                spriteImg.sprite = idle[0];
                if (u.Team == Team.Enemy) spRt.localScale = new Vector3(-1, 1, 1);
            }

            var name = Label("name", body.rectTransform, new Vector2(0, -size.y / 2 - 28), new Vector2(180, 34), u.Name, 25, Color.white, bold: true, center: true);
            var hpBg = Panel("hpBg", body.rectTransform, new Vector2(0, -size.y / 2 - 60), new Vector2(150, 14), Hex("120e1c"), center: true);
            var fill = Rt("hpFill", hpBg.rectTransform, Vector2.zero, new Vector2(150, 14));
            fill.anchorMin = fill.anchorMax = new Vector2(0, 0.5f);
            fill.pivot = new Vector2(0, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = Hex("62d96a");
            fillImg.raycastTarget = false;
            var status = Label("status", body.rectTransform, new Vector2(0, -size.y / 2 - 92), new Vector2(240, 30), "", 24, Hex("ffb0e0"), center: true);
            Text incoming = null;
            if (u.Team == Team.Ally)
                incoming = Label("incoming", body.rectTransform, new Vector2(0, -size.y / 2 - 122), new Vector2(240, 28), "", 22, Hex("ff8f7a"), center: true);

            var alt = u.IsTank ? LoadSheet("tank-brace") : LoadSheet(u.Sheet + "-charge");
            var action = LoadSheet(u.Sheet + "-attack") ?? LoadSheet(u.Sheet + "-cast");
            views[u] = new UnitView
            {
                Unit = u, Rect = body.rectTransform, Body = body, Sprite = spriteImg, Glow = glow,
                Idle = idle, Action = action, Alt = alt,
                Outline = outline, Name = name, Status = status, Incoming = incoming, HpFill = fill, BaseColor = c,
                BobPhase = views.Count * 1.3f
            };
        }

        // ---------- 갱신 ----------

        void Update()
        {
            if (mgr == null || views.Count == 0) return;

            uiTick -= Time.deltaTime;
            bool tick = uiTick <= 0f;
            if (tick) uiTick = 0.1f; // 텍스트류 10Hz — GC·발열 최적화

            if (tick)
            {
                turnText.text = Loc.F("bt.header", mgr.Turn, mgr.EncounterTitle);
                savedText.text = Loc.F("bt.score", mgr.RedirectedSaved, mgr.MitigatedSaved);
                logText.text = mgr.Log;
                deckText.text = Loc.F("bt.deck", mgr.Hand.Count, mgr.TotalDeckInfo());
                RefreshCards();
                foreach (var v in views.Values) RefreshUnitText(v);
                foreach (var e in mgr.Enemies) RefreshIntent(e);
            }

            foreach (var v in views.Values) RefreshUnitVisual(v);

            bool over = mgr.Phase == Phase.Won || mgr.Phase == Phase.Lost;
            if (resultPanel.activeSelf != over)
            {
                resultPanel.SetActive(over);
                if (over)
                {
                    if (mgr.Phase == Phase.Won)
                    {
                        resultText.text = Loc.F("result.win", mgr.EncounterTitle, mgr.RedirectedSaved, mgr.MitigatedSaved)
                            + (mgr.RewardGold > 0 ? "\n" + Loc.F("result.loot", mgr.RewardGold) : "");
                        resultBtnText.text = Loc.T("result.continue");
                    }
                    else
                    {
                        resultText.text = Loc.T("result.lose");
                        resultBtnText.text = Loc.T("result.view");
                    }
                }
            }
        }

        void RefreshCards()
        {
            bool player = mgr.Phase == Phase.Player;
            for (int i = 0; i < 3; i++)
            {
                bool has = i < mgr.Hand.Count;
                cardBtns[i].gameObject.SetActive(has);
                if (!has) continue;
                var card = mgr.Hand[i];
                cardTexts[i].text = Cards.NameOf(card) + "\n\n<size=26>" + Loc.T("card." + card + ".s") + "</size>";
                cardBtns[i].interactable = player && mgr.CardPlayable(i);
                UiKit.SetSelected(cardBtns[i], mgr.PendingCard == i || mgr.PlannedCard == i);
            }
            goBtn.interactable = player && mgr.PendingCard < 0;
        }

        void RefreshUnitText(UnitView v)
        {
            var u = v.Unit;
            float pct = Mathf.Clamp01(u.Hp / (float)u.MaxHp);
            v.HpFill.sizeDelta = new Vector2(150 * pct, 14);
            v.Name.text = u.Name + "  " + u.Hp + "/" + u.MaxHp;

            if (!u.Alive) v.Status.text = Loc.T("st.dead");
            else if (u.Team == Team.Enemy && u.Enraged) v.Status.text = u.TauntTurns > 0 ? Loc.F("st.enragedTaunt", u.TauntTurns) : Loc.T("st.enraged");
            else if (u.Team == Team.Enemy && u.TauntTurns > 0) v.Status.text = Loc.F("st.taunted", u.TauntTurns);
            else if (u.Team == Team.Enemy && u.Stunned) v.Status.text = Loc.T("st.stunned");
            else if (mgr.PlannedTarget == u && mgr.PlannedCard >= 0) v.Status.text = Loc.F("st.cardPlanned", Cards.NameOf(mgr.Hand[mgr.PlannedCard]));
            else if (u.Shielded) v.Status.text = Loc.T("st.shielded");
            else if (u.IsTank && mgr.Bracing) v.Status.text = Loc.T("st.bracing");
            else if (u.Shaken) v.Status.text = Loc.T("st.shaken");
            else if (mgr.CoverTarget == u) v.Status.text = Loc.T("st.covered");
            else v.Status.text = "";

            if (v.Incoming != null)
            {
                int inc = 0;
                if (u.Alive && mgr.Phase == Phase.Player)
                {
                    // 플레이어 페이즈는 입력에만 상태가 변하므로 StateVersion 캐시로 시뮬 재계산(할당)을 막는다
                    if (previewVersion != mgr.StateVersion) { previewCache.Clear(); previewVersion = mgr.StateVersion; }
                    if (!previewCache.TryGetValue(u, out inc)) previewCache[u] = inc = mgr.IncomingPreview(u);
                }
                v.Incoming.text = inc > 0 ? Loc.F("st.incoming", inc) : "";
            }
        }

        void RefreshUnitVisual(UnitView v)
        {
            var u = v.Unit;
            bool clickable = false;
            if (mgr.Phase == Phase.Player && u.Alive && mgr.PendingCard >= 0)
            {
                var need = Cards.TargetOf(mgr.Hand[mgr.PendingCard]);
                clickable = (need == CardTarget.Enemy && u.Team == Team.Enemy)
                         || (need == CardTarget.Ally && u.Team == Team.Ally && !u.IsTank);
            }

            float bob = v.Idle == null && u.Alive ? Mathf.Sin(Time.time * 2.2f + v.BobPhase) * 5f : 0f;
            v.Rect.anchoredPosition = posOf[u] + new Vector2(0, bob) + v.Impulse;
            v.Impulse = Vector2.Lerp(v.Impulse, Vector2.zero, Time.deltaTime * 10f);
            v.FlashT = Mathf.Max(0, v.FlashT - Time.deltaTime * 3.5f);

            if (v.Sprite != null)
            {
                v.Sprite.sprite = CurrentFrame(v);
                v.Sprite.color = u.Alive ? Color.white : new Color(0.45f, 0.42f, 0.5f, 0.7f);
                float sel = clickable ? 0.22f + Mathf.Sin(Time.time * 6f) * 0.08f : 0f;
                v.Glow.color = v.FlashT > 0.01f
                    ? new Color(1f, 1f, 1f, v.FlashT * 0.6f)
                    : new Color(1f, 0.85f, 0.35f, sel);
                v.Outline.enabled = false;
            }
            else
            {
                var baseColor = u.Alive ? v.BaseColor : new Color(0.25f, 0.25f, 0.28f, 0.6f);
                v.Body.color = Color.Lerp(baseColor, Color.white, v.FlashT);
                v.Outline.enabled = clickable;
            }
        }

        Sprite CurrentFrame(UnitView v)
        {
            var u = v.Unit;
            if (!u.Alive) return v.Idle[0];
            if (v.ActionT > 0f && v.Action != null)
            {
                float dur = v.Action.Length / ActionFps;
                int i = Mathf.Min(v.Action.Length - 1, (int)((dur - v.ActionT) * ActionFps));
                v.ActionT -= Time.deltaTime;
                return v.Action[i];
            }
            Sprite[] seq = v.Idle;
            if (u.IsTank && v.Alt != null && mgr.Bracing) seq = v.Alt;
            else if (u.Charging && v.Alt != null) seq = v.Alt;
            return seq[(int)(Time.time * IdleFps + v.BobPhase * 3f) % seq.Length];
        }

        void DrawLine(RectTransform line, Vector2 from, Vector2 to, Color c)
        {
            line.gameObject.SetActive(true);
            var d = to - from;
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(Mathf.Max(0, d.magnitude - 90), 6);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0.65f);
        }

        void RefreshIntent(Unit e)
        {
            var pair = lines[e];
            var label = intentLabels[e];
            foreach (var line in pair) line.gameObject.SetActive(false);

            if (!e.Alive || mgr.Phase != Phase.Player) { label.text = ""; return; }
            if (mgr.IsStunnedNow(e)) { label.text = Loc.T("intent.stunned"); label.color = Hex("ffcc80"); return; }
            if (e.Charging) { label.text = Loc.T("intent.charge"); label.color = Hex("d8a5ff"); return; }

            if (e.Ai == AiKind.ShamanCurse)
            {
                if (mgr.IsTauntedNow(e)) { label.text = Loc.T("intent.curseWasted"); label.color = Hex("d8a5ff"); return; }
                var ct = e.CurseIntent;
                if (ct == null || !ct.Alive) { label.text = ""; return; }
                label.text = Loc.F("intent.curse", ct.Name);
                label.color = Hex("c08aff");
                DrawLine(pair[0], posOf[e], posOf[ct], Hex("c08aff"));
                return;
            }

            if (e.Ai == AiKind.EnemyHealer)
            {
                if (mgr.IsTauntedNow(e)) { label.text = Loc.T("intent.curseWasted"); label.color = Hex("a5ffd8"); return; }
                var ht = e.HealIntent;
                if (ht == null || !ht.Alive) { label.text = ""; return; }
                label.text = Loc.F("intent.heal", ht.Name);
                label.color = Hex("8fd4a8");
                DrawLine(pair[0], posOf[e], posOf[ht], Hex("8fd4a8"));
                return;
            }

            if (mgr.AoeActive(e))
            {
                int i = 0, extra = 0;
                string parts = "";
                foreach (var b in mgr.Allies)
                {
                    if (b.IsTank || !b.Alive) continue;
                    bool covered = mgr.CoverPreview == b;
                    var recv = covered ? mgr.Tank : b;
                    if (i < pair.Length)
                        DrawLine(pair[i], posOf[e], posOf[recv], covered ? Hex("ffd75e") : Hex("ff6b6b"));
                    // 라벨은 2명까지 상세, 이후는 "외 N" — 개별 수치는 각 유닛의 예상 표시가 담당
                    if (i < 2) parts += (parts == "" ? "" : " · ") + recv.Name + " " + mgr.EffectiveAoeDamage(e, b);
                    else extra++;
                    i++;
                }
                if (extra > 0) parts += Loc.F("intent.aoeMore", extra);
                label.text = Loc.F("intent.aoe", parts);
                label.color = Hex("ff6b6b");
                return;
            }

            var target = mgr.EffectiveTarget(e);
            if (target == null) { label.text = ""; return; }
            bool redirected = target.IsTank && (e.AoeIntent || (e.Intent != null && !e.Intent.IsTank));
            var c = redirected ? Hex("ffd75e") : Hex("ff6b6b");
            label.text = Loc.F("intent.single", target.Name, mgr.EffectiveDamage(e));
            label.color = c;
            DrawLine(pair[0], posOf[e], posOf[target], c);
            // 거미 연타 + 엄호: 둘째 타는 원 대상에게 — 그 공격선도 숨기지 않는다 (전수검사)
            if (e.Ai == AiKind.SpiderDouble && redirected && !mgr.IsTauntedNow(e)
                && e.Intent != null && e.Intent.Alive)
                DrawLine(pair[1], posOf[e], posOf[e.Intent], Hex("ff6b6b"));
        }

        // ---------- 연출 ----------

        void OnStrike(Unit attacker, Unit victim)
        {
            if (views.TryGetValue(attacker, out var av) && posOf.ContainsKey(victim))
            {
                var dir = (posOf[victim] - posOf[attacker]).normalized;
                av.Impulse = dir * 42f;
                if (av.Action != null) av.ActionT = av.Action.Length / ActionFps;
            }
            if (views.TryGetValue(victim, out var vv) && posOf.ContainsKey(attacker))
            {
                bool heal = (attacker.Team == Team.Ally && attacker.Role == Role.Healer)
                            || attacker.Ai == AiKind.EnemyHealer; // 네크로 회복도 힐 연출

                vv.FlashT = heal ? 0.5f : 1f;
                SpawnFx(heal ? fxHeal : fxHit, posOf[victim]);
                if (!heal)
                {
                    vv.Impulse = (posOf[victim] - posOf[attacker]).normalized * 20f;
                    if (victim.IsTank) StartCoroutine(StageShake());
                }
            }
        }

        void SpawnFx(Sprite[] frames, Vector2 pos)
        {
            if (frames == null || root == null) return;
            var rt = Rt("fx", stage, pos + new Vector2(0, 20), new Vector2(230, 230));
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = frames[0];
            StartCoroutine(PlayFx(img, frames));
        }

        IEnumerator PlayFx(Image img, Sprite[] frames)
        {
            float dur = frames.Length / FxFps;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (img == null) yield break;
                img.sprite = frames[Mathf.Min(frames.Length - 1, (int)(t * FxFps))];
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        IEnumerator StageShake()
        {
            const float dur = 0.28f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                stage.anchoredPosition = StageHome + Random.insideUnitCircle * 16f * (1f - t / dur);
                yield return null;
            }
            stage.anchoredPosition = StageHome;
        }

        void ShowPopup(Unit u, string text, Color c)
        {
            if (!posOf.ContainsKey(u) || root == null) return;
            var t = Label("popup", root, posOf[u] + new Vector2(0, 80), new Vector2(400, 60), text, 44, c, bold: true);
            StartCoroutine(FloatAway(t));
        }

        IEnumerator FloatAway(Text t)
        {
            var rt = t.rectTransform;
            float dur = 0.8f;
            for (float el = 0; el < dur; el += Time.deltaTime)
            {
                if (t == null) yield break;
                rt.anchoredPosition += new Vector2(0, 130 * Time.deltaTime);
                t.color = new Color(t.color.r, t.color.g, t.color.b, 1f - el / dur * 0.9f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}
