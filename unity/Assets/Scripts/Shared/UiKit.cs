using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 코드 생성 UI 공용 헬퍼 — 폰트·UI 프레임 스프라이트(9-slice)·기본 위젯.
    /// 에셋(ui-panel/ui-button)이 없으면 단색 폴백.
    public static class UiKit
    {
        static Font font;
        static Sprite panelSprite, buttonSprite, cardSprite;
        static bool loaded;

        public static Font Font { get { EnsureLoaded(); return font; } }
        public static Sprite PanelSprite { get { EnsureLoaded(); return panelSprite; } }
        public static Sprite ButtonSprite { get { EnsureLoaded(); return buttonSprite; } }
        public static Sprite CardSprite { get { EnsureLoaded(); return cardSprite; } }

        public static Color PanelColor = Hex("241d33");
        public static Color ButtonColor = Hex("3a3153");
        public static Color ButtonSelected = Hex("5b4f86");

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelSprite = LoadNineSlice("Art/ui-panel");
            buttonSprite = LoadNineSlice("Art/ui-button");
            cardSprite = LoadNineSlice("Art/ui-card", borderRatio: 0.16f); // 카드 프레임 — 테두리 얇음
        }

        static Sprite LoadNineSlice(string path, float borderRatio = 0.28f)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            float b = Mathf.Min(tex.width, tex.height) * borderRatio; // 프레임 테두리 두께 비율
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                                 64f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite[]> sheetCache = new();

        /// Resources/Art의 2x2 시트를 4프레임(좌상→우상→좌하→우하)으로 자른다. 없으면 null.
        /// 런타임 Sprite는 파기되지 않으므로 캐시해 재도전 시 누적을 막는다.
        public static Sprite[] LoadSheet(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (sheetCache.TryGetValue(name, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>("Art/" + name);
            if (tex == null) { sheetCache[name] = null; return null; }
            int w = tex.width / 2, h = tex.height / 2;
            var frames = new Sprite[4];
            for (int i = 0; i < 4; i++)
                frames[i] = Sprite.Create(tex, new Rect(i % 2 * w, (1 - i / 2) * h, w, h),
                                          new Vector2(0.5f, 0.5f), 128f);
            sheetCache[name] = frames;
            return frames;
        }

        /// 캔버스를 만들고 그 안의 1080×1920 중앙 프레임을 반환한다.
        /// 폭 기준 스케일(match 0)이라 19.5:9 같은 긴 화면에서도 가로가 잘리지 않고,
        /// 남는 세로 공간은 프레임 위아래 여백이 된다 (배경은 각 화면에서 블리드로 채움).
        public static RectTransform MakeCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f; // 폭 고정 — 가로 잘림 방지
            var frame = Rt("frame", go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1080, 1920), center: true);
            return frame;
        }

        public static RectTransform Rt(string name, RectTransform parent, Vector2 pos, Vector2 size, bool center = false)
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

        public static Image Panel(string name, RectTransform parent, Vector2 pos, Vector2 size, Color c, bool center = false)
        {
            var img = Rt(name, parent, pos, size, center).gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        /// ui-panel 스프라이트가 있으면 9-slice 프레임, 없으면 단색 패널
        public static Image FramedPanel(string name, RectTransform parent, Vector2 pos, Vector2 size, bool center = false)
        {
            var img = Panel(name, parent, pos, size, Color.white, center);
            if (PanelSprite != null) { img.sprite = PanelSprite; img.type = Image.Type.Sliced; }
            else img.color = PanelColor;
            return img;
        }

        public static Text Label(string name, RectTransform parent, Vector2 pos, Vector2 size, string text,
                                 int fontSize, Color c, TextAnchor anchor = TextAnchor.MiddleCenter,
                                 bool bold = false, bool center = false)
        {
            var t = Rt(name, parent, pos, size, center).gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = fontSize; t.color = c;
            t.alignment = anchor; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(string name, RectTransform parent, Vector2 pos, Vector2 size, string text,
                                 System.Action onClick, int fontSize = 40, bool center = false)
        {
            var img = Panel(name, parent, pos, size, Color.white, center);
            if (ButtonSprite != null) { img.sprite = ButtonSprite; img.type = Image.Type.Sliced; }
            else img.color = ButtonColor;
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => { AudioKit.Click(); onClick?.Invoke(); });
            Label(name + "Txt", img.rectTransform, Vector2.zero, size, text, fontSize, Color.white, center: true);
            return btn;
        }

        /// 코드 생성 슬라이더 (0~1) — 배경 바 + 채움 + 핸들
        public static Slider MakeSlider(string name, RectTransform parent, Vector2 pos, Vector2 size,
                                        float value, System.Action<float> onChanged)
        {
            var rt = Rt(name, parent, pos, size, center: true);
            var bg = Panel("bg", rt, Vector2.zero, size, Hex("241d33"), center: true);
            bg.raycastTarget = true;

            var fillArea = Rt("fillArea", rt, Vector2.zero, Vector2.zero, center: true);
            fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero; fillArea.offsetMax = Vector2.zero;
            var fill = Panel("fill", fillArea, Vector2.zero, Vector2.zero, Hex("8fd4a8"));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
            fill.raycastTarget = false;

            var handleArea = Rt("handleArea", rt, Vector2.zero, Vector2.zero, center: true);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(17, 0); handleArea.offsetMax = new Vector2(-17, 0);
            var handle = Panel("handle", handleArea, Vector2.zero, new Vector2(34, size.y + 26), Hex("ffd75e"), center: true);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        /// 버튼 강조 (선택 상태) — 스프라이트 모드에선 틴트, 폴백에선 색 교체
        public static void SetSelected(Button btn, bool selected)
        {
            var img = btn.GetComponent<Image>();
            if (ButtonSprite != null) img.color = selected ? new Color(1.25f, 1.2f, 0.9f) : Color.white;
            else img.color = selected ? ButtonSelected : ButtonColor;
        }
    }
}
