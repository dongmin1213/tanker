using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 게임 방법 오버레이 — 4페이지 (전투 / 카드 순환 / 파티 / 던전).
    /// 첫 원정 자동 1회 + 타이틀·설정에서 언제든 열람 (친절성 검사 반영).
    public static class HelpUI
    {
        static GameObject openCanvas;
        static int page;

        public static void Open()
        {
            if (openCanvas != null) return;
            page = 0;
            Build();
        }

        static void Build()
        {
            // Destroy는 프레임 끝 처리 — 즉시 비활성화로 같은 프레임 멀티탭의 고아 캔버스 방지
            if (openCanvas != null) { openCanvas.SetActive(false); Object.Destroy(openCanvas); }
            var frame = UiKit.MakeCanvas("HelpCanvas", 95);
            openCanvas = frame.parent.gameObject;

            var dim = UiKit.Panel("dim", frame, Vector2.zero, new Vector2(1500, 2600), new Color(0, 0, 0, 0.8f), center: true);
            dim.raycastTarget = true;

            UiKit.FramedPanel("panel", frame, Vector2.zero, new Vector2(920, 1000), center: true);
            UiKit.Label("h1", frame, new Vector2(0, 400), new Vector2(800, 64),
                Loc.T("help.t" + page), 46, UiKit.Hex("ffd75e"), bold: true, center: true);
            var body = UiKit.Label("body", frame, new Vector2(0, 40), new Vector2(780, 600),
                Loc.T("help.b" + page), 30, Color.white, TextAnchor.UpperLeft, center: true);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            UiKit.Label("pageNum", frame, new Vector2(0, -350), new Vector2(300, 40),
                (page + 1) + " / 4", 26, UiKit.Hex("8f86ad"), center: true);
            if (page > 0)
                UiKit.Btn("prev", frame, new Vector2(-300, -350), new Vector2(180, 92), "◀", () => { page--; Build(); }, 36, center: true);
            if (page < 3)
                UiKit.Btn("next", frame, new Vector2(300, -350), new Vector2(180, 92), "▶", () => { page++; Build(); }, 36, center: true);

            UiKit.Btn("close", frame, new Vector2(0, -560), new Vector2(380, 96), Loc.T("set.close"), () =>
            {
                var go = openCanvas;
                openCanvas = null;
                Object.Destroy(go);
            }, 32, center: true);
        }
    }
}
