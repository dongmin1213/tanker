using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 전 UI를 코드로 생성. 상태는 매 프레임 BattleManager에서 읽는다.
    public class BattleUI : MonoBehaviour
    {
        BattleManager mgr;
        Font font;
        RectTransform root;
        readonly Dictionary<Unit, UnitView> views = new();
        readonly Dictionary<Unit, RectTransform> lines = new();
        readonly Dictionary<Unit, Text> intentLabels = new();
        readonly Dictionary<Unit, Vector2> posOf = new();
        Text turnText, savedText, logText;
        Button tauntBtn, coverBtn, braceBtn, goBtn;
        GameObject resultPanel;
        Text resultText;

        class UnitView
        {
            public Unit Unit;
            public RectTransform Rect;
            public Image Body;
            public Outline Outline;
            public Text Name, Status;
            public RectTransform HpFill;
            public Color BaseColor;
            public Vector2 Impulse;   // 런지/넉백 잔여 오프셋 (감쇠)
            public float FlashT;      // 피격 플래시 잔여 시간 비율
            public float BobPhase;    // 숨쉬기 위상
        }

        RectTransform stage;          // 전장 컨테이너 — 화면 흔들림용
        static readonly Vector2 StageHome = new Vector2(540, 960);

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        void Start()
        {
            mgr = GetComponent<BattleManager>();
            mgr.Popup += ShowPopup;
            mgr.Strike += OnStrike;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            BuildLayout();
        }

        // ---------- 구축 ----------

        void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root = go.GetComponent<RectTransform>();
        }

        // center=true: 부모 중앙 기준(자식 요소용), false: 캔버스 좌하단 좌표계(최상위 배치용)
        RectTransform Rt(string name, RectTransform parent, Vector2 pos, Vector2 size, bool center = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = center ? new Vector2(0.5f, 0.5f) : Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Image Panel(string name, RectTransform parent, Vector2 pos, Vector2 size, Color c, bool center = false)
        {
            var rt = Rt(name, parent, pos, size, center);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        Text Label(string name, RectTransform parent, Vector2 pos, Vector2 size, string text,
                   int fontSize, Color c, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false, bool center = false)
        {
            var rt = Rt(name, parent, pos, size, center);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.text = text; t.fontSize = fontSize; t.color = c;
            t.alignment = anchor; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Button BtnCentered(string name, RectTransform parent, Vector2 pos, Vector2 size, string text, System.Action onClick)
        {
            var btn = Btn(name, parent, pos, size, text, onClick);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            return btn;
        }

        Button Btn(string name, RectTransform parent, Vector2 pos, Vector2 size, string text, System.Action onClick)
        {
            var img = Panel(name, parent, pos, size, Hex("3a3153"));
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            Label(name + "Txt", img.rectTransform, Vector2.zero, size, text, 40, Color.white, center: true);
            return btn;
        }

        void BuildLayout()
        {
            stage = Rt("stage", root, StageHome, new Vector2(1080, 1920));
            Panel("bgTop", stage, new Vector2(540, 1330), new Vector2(1080, 1180), Hex("1a1626"));
            Panel("bgBottom", root, new Vector2(540, 370), new Vector2(1080, 740), Hex("241d33"));

            turnText = Label("turn", root, new Vector2(540, 1850), new Vector2(1000, 50), "", 40, Color.white, bold: true);
            savedText = Label("saved", root, new Vector2(540, 1790), new Vector2(1000, 40), "", 30, Hex("ffd75e"));
            logText = Label("log", root, new Vector2(540, 770), new Vector2(1020, 60), "", 34, Hex("cfc8e8"));

            posOf[mgr.Tank] = new Vector2(400, 1120);
            posOf[mgr.Dps] = new Vector2(240, 1300);
            posOf[mgr.Healer] = new Vector2(155, 1480);
            posOf[mgr.GoblinA] = new Vector2(730, 1480);
            posOf[mgr.GoblinB] = new Vector2(890, 1300);
            posOf[mgr.Brute] = new Vector2(770, 1090);

            // 인텐트 라인을 유닛보다 먼저 만들어 뒤에 깔리게 한다
            foreach (var e in mgr.Enemies)
            {
                var line = Rt("line_" + e.Name, stage, Vector2.zero, new Vector2(0, 6));
                line.pivot = new Vector2(0, 0.5f);
                line.gameObject.AddComponent<Image>().color = Hex("ff6b6b");
                line.GetComponent<Image>().raycastTarget = false;
                lines[e] = line;
                intentLabels[e] = Label("intent_" + e.Name, stage, posOf[e] + new Vector2(0, 130),
                                        new Vector2(320, 40), "", 30, Hex("ff6b6b"), bold: true);
            }

            MakeUnitView(mgr.Tank, Hex("5878b4"), new Vector2(170, 190));
            MakeUnitView(mgr.Dps, Hex("c04858"), new Vector2(140, 160));
            MakeUnitView(mgr.Healer, Hex("78b478"), new Vector2(140, 160));
            MakeUnitView(mgr.GoblinA, Hex("6a8f4f"), new Vector2(140, 160));
            MakeUnitView(mgr.GoblinB, Hex("6a8f4f"), new Vector2(140, 160));
            MakeUnitView(mgr.Brute, Hex("7a4f8f"), new Vector2(190, 210));

            tauntBtn = Btn("taunt", root, new Vector2(165, 560), new Vector2(230, 120), "도발", () => mgr.PressSkill(SkillType.Taunt));
            coverBtn = Btn("cover", root, new Vector2(415, 560), new Vector2(230, 120), "엄호", () => mgr.PressSkill(SkillType.Cover));
            braceBtn = Btn("brace", root, new Vector2(665, 560), new Vector2(230, 120), "버티기", () => mgr.PressSkill(SkillType.Brace));
            goBtn = Btn("go", root, new Vector2(915, 560), new Vector2(230, 120), "진행 ▶", () => mgr.EndTurn());

            Label("hintTaunt", root, new Vector2(165, 470), new Vector2(240, 40), "적 1명을 2턴간\n나에게 고정", 22, Hex("8f86ad"));
            Label("hintCover", root, new Vector2(415, 470), new Vector2(240, 40), "아군 1명 대신\n내가 맞기", 22, Hex("8f86ad"));
            Label("hintBrace", root, new Vector2(665, 470), new Vector2(240, 40), "받는 피해 절반\n+3 회복", 22, Hex("8f86ad"));

            resultPanel = Panel("result", root, new Vector2(540, 960), new Vector2(1080, 1920), new Color(0, 0, 0, 0.72f)).gameObject;
            resultText = Label("resultTxt", resultPanel.GetComponent<RectTransform>(), new Vector2(0, 60), new Vector2(900, 200), "", 64, Color.white, bold: true, center: true);
            var restart = BtnCentered("restart", resultPanel.GetComponent<RectTransform>(), new Vector2(0, -120), new Vector2(360, 120), "다시 도전", () => mgr.Restart());
            restart.GetComponent<Image>().color = Hex("5b4f86");
            resultPanel.SetActive(false);
        }

        void MakeUnitView(Unit u, Color c, Vector2 size)
        {
            var body = Panel("unit_" + u.Name, stage, posOf[u], size, c);
            var outline = body.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(5, 5);
            outline.enabled = false;
            var btn = body.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => mgr.ClickUnit(u));

            var name = Label("name", body.rectTransform, new Vector2(0, -size.y / 2 - 28), new Vector2(220, 36), u.Name, 30, Color.white, bold: true, center: true);
            var hpBg = Panel("hpBg", body.rectTransform, new Vector2(0, -size.y / 2 - 62), new Vector2(150, 16), Hex("120e1c"), center: true);
            var fill = Rt("hpFill", hpBg.rectTransform, Vector2.zero, new Vector2(150, 16));
            fill.anchorMin = fill.anchorMax = new Vector2(0, 0.5f);
            fill.pivot = new Vector2(0, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = Hex("62d96a");
            fillImg.raycastTarget = false;
            var status = Label("status", body.rectTransform, new Vector2(0, -size.y / 2 - 96), new Vector2(240, 32), "", 26, Hex("ffb0e0"), center: true);

            views[u] = new UnitView { Unit = u, Rect = body.rectTransform, Body = body, Outline = outline, Name = name, Status = status, HpFill = fill, BaseColor = c, BobPhase = views.Count * 1.3f };
        }

        // ---------- 매 프레임 갱신 ----------

        void Update()
        {
            if (mgr == null || views.Count == 0) return;

            turnText.text = mgr.Turn + "턴  |  탱커의 의무: 아무도 죽게 두지 않는다";
            savedText.text = "막아낸 피해 누적: " + mgr.TotalSaved;
            logText.text = mgr.Log;

            foreach (var v in views.Values) RefreshUnit(v);
            foreach (var e in mgr.Enemies) RefreshIntent(e);

            bool taunt = mgr.TauntReady, skill = mgr.CanUseSkill;
            tauntBtn.interactable = taunt;
            coverBtn.interactable = skill;
            braceBtn.interactable = skill;
            goBtn.interactable = mgr.Phase == Phase.Player;
            tauntBtn.GetComponentInChildren<Text>().text = mgr.TauntCooldown > 0 ? "도발 (쿨 " + mgr.TauntCooldown + ")" : "도발";
            tauntBtn.GetComponent<Image>().color = mgr.Pending == SkillType.Taunt ? Hex("5b4f86") : Hex("3a3153");
            coverBtn.GetComponent<Image>().color = mgr.Pending == SkillType.Cover ? Hex("5b4f86") : Hex("3a3153");

            bool over = mgr.Phase == Phase.Won || mgr.Phase == Phase.Lost;
            if (resultPanel.activeSelf != over)
            {
                resultPanel.SetActive(over);
                if (over) resultText.text = mgr.Phase == Phase.Won
                    ? "던전 클리어!\n막아낸 피해 " + mgr.TotalSaved
                    : "팀을 지키지 못했다...";
            }
        }

        void RefreshUnit(UnitView v)
        {
            var u = v.Unit;
            float pct = Mathf.Clamp01(u.Hp / (float)u.MaxHp);
            v.HpFill.sizeDelta = new Vector2(150 * pct, 16);
            v.Name.text = u.Name + "  " + u.Hp + "/" + u.MaxHp;

            // 숨쉬기 + 런지/넉백 잔향 + 피격 플래시
            float bob = u.Alive ? Mathf.Sin(Time.time * 2.2f + v.BobPhase) * 5f : 0f;
            v.Rect.anchoredPosition = posOf[u] + new Vector2(0, bob) + v.Impulse;
            v.Impulse = Vector2.Lerp(v.Impulse, Vector2.zero, Time.deltaTime * 10f);
            v.FlashT = Mathf.Max(0, v.FlashT - Time.deltaTime * 3.5f);
            var baseColor = u.Alive ? v.BaseColor : new Color(0.25f, 0.25f, 0.28f, 0.6f);
            v.Body.color = Color.Lerp(baseColor, Color.white, v.FlashT);

            if (!u.Alive) v.Status.text = "사망";
            else if (u.Team == Team.Enemy && u.Enraged) v.Status.text = u.TauntTurns > 0 ? "격노·도발됨 " + u.TauntTurns : "격노";
            else if (u.Team == Team.Enemy && u.TauntTurns > 0) v.Status.text = "도발됨 " + u.TauntTurns;
            else if (u.Shaken) v.Status.text = "위축";
            else if (mgr.CoverTarget == u) v.Status.text = "엄호받는 중";
            else v.Status.text = "";

            bool clickable = mgr.Phase == Phase.Player && u.Alive &&
                ((mgr.Pending == SkillType.Taunt && u.Team == Team.Enemy) ||
                 (mgr.Pending == SkillType.Cover && u.Team == Team.Ally && !u.IsTank));
            v.Outline.enabled = clickable;
        }

        void RefreshIntent(Unit e)
        {
            var line = lines[e];
            var label = intentLabels[e];
            if (!e.Alive) { line.gameObject.SetActive(false); label.text = ""; return; }
            if (e.Charging)
            {
                line.gameObject.SetActive(false);
                label.text = "힘 모으는 중...";
                label.color = Hex("d8a5ff");
                return;
            }
            var target = mgr.EffectiveTarget(e);
            if (target == null || mgr.Phase != Phase.Player) { line.gameObject.SetActive(false); label.text = e.Charging ? label.text : ""; return; }

            bool redirected = target.IsTank && e.Intent != null && !e.Intent.IsTank;
            var c = redirected ? Hex("ffd75e") : Hex("ff6b6b");
            label.text = "▶ " + target.Name + "에게 " + e.Power;
            label.color = c;

            line.gameObject.SetActive(true);
            var from = posOf[e];
            var to = posOf[target];
            var d = to - from;
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(d.magnitude - 90, 6);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0.65f);
        }

        // ---------- 연출 ----------

        void OnStrike(Unit attacker, Unit victim)
        {
            if (views.TryGetValue(attacker, out var av) && posOf.ContainsKey(victim))
            {
                var dir = (posOf[victim] - posOf[attacker]).normalized;
                av.Impulse = dir * 42f;                       // 공격자 런지
            }
            if (views.TryGetValue(victim, out var vv) && posOf.ContainsKey(attacker))
            {
                vv.FlashT = attacker == mgr.Healer ? 0.5f : 1f;
                if (attacker != mgr.Healer)
                {
                    vv.Impulse = (posOf[victim] - posOf[attacker]).normalized * 20f; // 피격 넉백
                    if (victim.IsTank) StartCoroutine(StageShake());                 // 탱커 피격 = 화면 흔들림
                }
            }
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

        // ---------- 팝업 ----------

        void ShowPopup(Unit u, string text, Color c)
        {
            if (!posOf.ContainsKey(u)) return;
            var t = Label("popup", root, posOf[u] + new Vector2(0, 80), new Vector2(400, 60), text, 44, c, bold: true);
            StartCoroutine(FloatAway(t));
        }

        IEnumerator FloatAway(Text t)
        {
            var rt = t.rectTransform;
            float dur = 0.8f;
            for (float el = 0; el < dur; el += Time.deltaTime)
            {
                rt.anchoredPosition += new Vector2(0, 130 * Time.deltaTime);
                t.color = new Color(t.color.r, t.color.g, t.color.b, 1f - el / dur * 0.9f);
                yield return null;
            }
            Destroy(t.gameObject);
        }
    }
}
