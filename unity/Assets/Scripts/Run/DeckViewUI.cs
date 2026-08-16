using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 내 덱 전체 열람 오버레이 — 종류별로 묶어 장수·짧은 설명을 보여준다.
    /// 전투·맵 어디서든 호출 가능 (전용 캔버스, 닫으면 파기).
    public static class DeckViewUI
    {
        static GameObject openCanvas;

        /// hand/draw/discard에 -1을 주면 구성 부제를 생략한다 (전투 밖 열람용)
        public static void Open(List<CardType> deck, int hand = -1, int draw = -1, int discard = -1)
        {
            if (openCanvas != null) return;
            var frame = UiKit.MakeCanvas("DeckViewCanvas", 85);
            var canvasGo = frame.parent.gameObject;
            openCanvas = canvasGo;

            var dim = UiKit.Panel("dim", frame, Vector2.zero, new Vector2(1400, 2500), new Color(0, 0, 0, 0.72f), center: true);
            dim.raycastTarget = true;

            // 종류별 집계 (덱 순서 무관 — 셔플은 전투에서만 의미)
            var counts = new Dictionary<CardType, int>();
            foreach (var c in deck) counts[c] = counts.TryGetValue(c, out var n) ? n + 1 : 1;

            int rows = (counts.Count + 1) / 2;
            float panelH = Mathf.Max(560, 330 + rows * 230);
            UiKit.FramedPanel("panel", frame, Vector2.zero, new Vector2(980, panelH), center: true);
            UiKit.Label("h1", frame, new Vector2(0, panelH / 2 - 85), new Vector2(860, 60),
                Loc.F("deck.title", deck.Count), 44, Color.white, bold: true, center: true);
            if (hand >= 0)
                UiKit.Label("sub", frame, new Vector2(0, panelH / 2 - 145), new Vector2(860, 40),
                    Loc.F("deck.sub", hand, draw, discard), 26, UiKit.Hex("9a92b8"), center: true);

            // 2열 미니 카드 그리드 — 전투 카드와 같은 프레임으로 시각 연결 (크리틱 반영)
            float top = panelH / 2 - 200;
            int i = 0;
            foreach (var kv in counts)
            {
                float x = i % 2 == 0 ? -225 : 225;
                float y = top - i / 2 * 230 - 100;
                var card = UiKit.Panel("card_" + kv.Key, frame, new Vector2(x, y), new Vector2(400, 215), Color.white, center: true);
                if (UiKit.CardSprite != null) { card.sprite = UiKit.CardSprite; card.type = Image.Type.Sliced; }
                else card.color = UiKit.Hex("241d33");
                card.raycastTarget = false;
                UiKit.Label("ct_" + kv.Key, card.rectTransform, Vector2.zero, new Vector2(340, 190),
                    "<b>" + Cards.NameOf(kv.Key) + "  ×" + kv.Value + "</b>\n<size=22><color=#9a92b8>"
                    + Cards.DescOf(kv.Key) + "</color></size>", 28, Color.white, center: true);
                i++;
            }

            UiKit.Btn("close", frame, new Vector2(0, -panelH / 2 + 85), new Vector2(380, 100),
                Loc.T("set.close"), () => Object.Destroy(canvasGo), 34, center: true);
        }
    }
}
