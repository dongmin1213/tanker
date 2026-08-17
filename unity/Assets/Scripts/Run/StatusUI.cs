using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 내 상태 오버레이 — 골드·진행, 파티 HP, 보유 유물(이름+효과), 덱 요약 (v0.8).
    /// 맵(파티 스트립 탭)·전투(상태 버튼) 어디서든 열람.
    public static class StatusUI
    {
        static GameObject openCanvas;

        public static void Close()
        {
            if (openCanvas != null) Object.Destroy(openCanvas);
            openCanvas = null;
        }

        /// battle을 주면 파티 HP는 진행 중 전투의 실시간 값 — RunState는 승리 시점에만 갱신되므로
        public static void Open(RunState run, BattleManager battle = null)
        {
            if (openCanvas != null || run == null) return;
            var frame = UiKit.MakeCanvas("StatusCanvas", 85);
            var canvasGo = frame.parent.gameObject;
            openCanvas = canvasGo;

            var dim = UiKit.Panel("dim", frame, Vector2.zero, new Vector2(1500, 2600), new Color(0, 0, 0, 0.78f), center: true);
            dim.raycastTarget = true;

            float relicH = Mathf.Max(1, run.Relics.Count) * 86;
            float panelH = Mathf.Min(1700, 640 + relicH + 130);
            UiKit.FramedPanel("panel", frame, Vector2.zero, new Vector2(960, panelH), center: true);
            float top = panelH / 2;

            UiKit.Label("h1", frame, new Vector2(0, top - 80), new Vector2(860, 60),
                Loc.T("status.h1"), 46, Color.white, bold: true, center: true);
            UiKit.Label("meta", frame, new Vector2(0, top - 145), new Vector2(860, 40),
                Loc.F("status.meta", run.Act + 1, run.FloorReached, Balance.I.mapFloors, run.Gold)
                + "  ·  " + Loc.F("status.time", (int)(run.PlaySeconds / 60)), 28, UiKit.Hex("ffd75e"), center: true);

            // 파티 — 미니 초상 + HP
            int count = 1 + run.Party.Count;
            float step = Mathf.Min(180f, 840f / count);
            float x0 = -(count - 1) * step / 2f;
            int tankHp = battle != null ? battle.Tank.Hp : run.TankHp;
            DrawMini(frame, x0, top - 280, "tank", Loc.T("unit.tank"), tankHp, run.TankMaxHp);
            for (int i = 0; i < run.Party.Count; i++)
            {
                var cd = RunData.Class(run.Party[i]);
                int hp = battle != null && i + 1 < battle.Allies.Count ? battle.Allies[i + 1].Hp : run.PartyHp[i];
                DrawMini(frame, x0 + (i + 1) * step, top - 280, cd.Sheet, Loc.T(cd.LocKey), hp, cd.Hp);
            }

            // 유물 목록
            UiKit.Label("relicH", frame, new Vector2(0, top - 430), new Vector2(860, 40),
                Loc.F("status.relics", run.Relics.Count), 30, UiKit.Hex("c9a44a"), bold: true, center: true);
            if (run.Relics.Count == 0)
                UiKit.Label("noRelic", frame, new Vector2(0, top - 500), new Vector2(820, 40),
                    Loc.T("status.noRelic"), 26, UiKit.Hex("8f86ad"), center: true);
            for (int i = 0; i < run.Relics.Count; i++)
            {
                var r = run.Relics[i];
                UiKit.Label("relic" + i, frame, new Vector2(0, top - 500 - i * 86), new Vector2(820, 80),
                    "<b>" + Loc.T("relic." + r) + "</b>  <size=24><color=#9a92b8>" + GameFlow.RelicDesc(r) + "</color></size>",
                    28, Color.white, TextAnchor.MiddleLeft, center: true);
            }

            float bottom = -panelH / 2;
            UiKit.Btn("deck", frame, new Vector2(-190, bottom + 95), new Vector2(330, 100),
                Loc.F("bt.deckView", run.Deck.Count), () => { Close(); DeckViewUI.Open(run.Deck); }, 30, center: true);
            UiKit.Btn("close", frame, new Vector2(190, bottom + 95), new Vector2(330, 100),
                Loc.T("set.close"), () => Close(), 30, center: true);
        }

        static void DrawMini(RectTransform frame, float x, float y, string sheet, string name, int hp, int maxHp)
        {
            var frames = UiKit.LoadSheet(sheet + "-idle");
            var icon = UiKit.Panel("s_" + name + x, frame, new Vector2(x, y + 12), new Vector2(90, 100), Color.white, center: true);
            if (frames != null) { icon.sprite = frames[0]; icon.preserveAspect = true; }
            else icon.color = UiKit.Hex("3a3153");
            icon.raycastTarget = false;
            float pct = Mathf.Clamp01(hp / (float)maxHp);
            UiKit.Panel("sb_" + name + x, frame, new Vector2(x, y - 48), new Vector2(90, 10), UiKit.Hex("241d33"), center: true).raycastTarget = false;
            UiKit.Panel("sf_" + name + x, frame, new Vector2(x - 45 + 45 * pct, y - 48), new Vector2(90 * pct, 10),
                pct > 0.5f ? UiKit.Hex("8fd4a8") : UiKit.Hex("e8895e"), center: true).raycastTarget = false;
            UiKit.Label("sn_" + name + x, frame, new Vector2(x, y - 72), new Vector2(150, 26),
                name + " " + hp + "/" + maxHp, 18, UiKit.Hex("cfc8e8"), center: true);
        }
    }
}
