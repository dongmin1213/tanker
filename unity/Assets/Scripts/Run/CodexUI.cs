using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 도감 — 아군 클래스 / 몬스터 / 유물 전체 열람 (v0.8).
    /// 랜덤 풀 게임의 "뭐가 있는지"를 전부 공개한다 — 타이틀에서 진입.
    public static class CodexUI
    {
        static GameObject openCanvas;
        static int tab;   // 0=아군 1=몬스터 2=유물
        static int page;
        const int PerPage = 6;

        public static void Open()
        {
            if (openCanvas != null) return;
            tab = 0; page = 0;
            Build();
        }

        static void Close()
        {
            var go = openCanvas;
            openCanvas = null;
            if (go != null) Object.Destroy(go);
        }

        static void Build()
        {
            // Destroy는 프레임 끝 처리 — 즉시 비활성화로 같은 프레임 멀티탭의 고아 캔버스 방지
            if (openCanvas != null) { openCanvas.SetActive(false); Object.Destroy(openCanvas); }
            // 적 목록은 페이지 빌드당 1회만 생성 (행마다 재생성 금지)
            var enemies = tab == 1 ? RunData.EnemyCodex() : null;
            var frame = UiKit.MakeCanvas("CodexCanvas", 95);
            openCanvas = frame.parent.gameObject;

            var dim = UiKit.Panel("dim", frame, Vector2.zero, new Vector2(1500, 2600), new Color(0, 0, 0, 0.82f), center: true);
            dim.raycastTarget = true;

            UiKit.FramedPanel("panel", frame, Vector2.zero, new Vector2(980, 1420), center: true);
            UiKit.Label("h1", frame, new Vector2(0, 610), new Vector2(800, 60),
                Loc.T("codex.h1"), 44, Color.white, bold: true, center: true);

            // 탭 3개
            string[] tabs = { "codex.allies", "codex.enemies", "codex.relics" };
            for (int t = 0; t < 3; t++)
            {
                int ti = t;
                var btn = UiKit.Btn("tab" + t, frame, new Vector2(-300 + t * 300, 520), new Vector2(280, 84),
                    Loc.T(tabs[t]), () => { tab = ti; page = 0; Build(); }, 28, center: true);
                UiKit.SetSelected(btn, t == tab);
            }

            int total = tab == 0 ? System.Enum.GetValues(typeof(ClassId)).Length
                : tab == 1 ? enemies.Count
                : System.Enum.GetValues(typeof(RelicId)).Length;
            int maxPage = (total - 1) / PerPage;
            page = Mathf.Clamp(page, 0, maxPage);
            float top = 420;

            for (int i = 0; i < PerPage; i++)
            {
                int idx = page * PerPage + i;
                if (idx >= total) break;
                float y = top - i * 155 - 70;
                if (tab == 0) DrawClassRow(frame, (ClassId)idx, y);
                else if (tab == 1) DrawEnemyRow(frame, enemies[idx], y);
                else DrawRelicRow(frame, (RelicId)idx, y);
            }

            UiKit.Label("pageNum", frame, new Vector2(0, -570), new Vector2(300, 40),
                (page + 1) + " / " + (maxPage + 1), 26, UiKit.Hex("8f86ad"), center: true);
            if (page > 0)
                UiKit.Btn("prev", frame, new Vector2(-300, -570), new Vector2(170, 88), "◀", () => { page--; Build(); }, 34, center: true);
            if (page < maxPage)
                UiKit.Btn("next", frame, new Vector2(300, -570), new Vector2(170, 88), "▶", () => { page++; Build(); }, 34, center: true);

            UiKit.Btn("close", frame, new Vector2(0, -675), new Vector2(360, 92), Loc.T("set.close"), () => Close(), 30, center: true);
        }

        static void RowIcon(RectTransform frame, string sheet, float y)
        {
            var frames = UiKit.LoadSheet(sheet);
            var icon = UiKit.Panel("ci_" + sheet + y, frame, new Vector2(-370, y), new Vector2(120, 135), Color.white, center: true);
            if (frames != null) { icon.sprite = frames[0]; icon.preserveAspect = true; }
            else icon.color = UiKit.Hex("3a3153");
            icon.raycastTarget = false;
        }

        static void DrawClassRow(RectTransform frame, ClassId id, float y)
        {
            var cd = RunData.Class(id);
            RowIcon(frame, cd.Sheet + "-idle", y);
            string stat = (cd.IsHealer ? Loc.F("codex.healStat", cd.Hp, cd.Power) : Loc.F("codex.atkStat", cd.Hp, cd.Power))
                        + " · " + Loc.T(cd.Ranged ? "codex.ranged" : "codex.melee");
            string trait = cd.IsHealer ? Loc.T("codex.healerTrait")
                : cd.Trait != Trait.None
                    ? (cd.Trait == Trait.TankHealOnHit ? Loc.F("trait." + cd.Trait, Balance.I.paladinTankHeal) : Loc.T("trait." + cd.Trait))
                    : "";
            UiKit.Label("cr_" + id, frame, new Vector2(60, y), new Vector2(680, 145),
                "<b>" + Loc.T(cd.LocKey) + "</b>  <size=24><color=#ffd75e>" + stat + "</color></size>\n<size=23><color=#9a92b8>"
                + trait + "</color></size>", 30, Color.white, TextAnchor.MiddleLeft, center: true);
        }

        static void DrawEnemyRow(RectTransform frame, RunData.CodexEnemy e, float y)
        {
            RowIcon(frame, e.Sheet + "-idle", y);
            string stat = Loc.F("codex.atkStat", e.Hp, e.Power);
            UiKit.Label("er_" + e.Sheet, frame, new Vector2(60, y), new Vector2(680, 145),
                "<b>" + Loc.T(e.Key) + "</b>  <size=24><color=#ffd75e>" + stat + "</color></size>\n<size=23><color=#9a92b8>"
                + Loc.T("cdx." + e.Sheet) + "</color></size>", 30, Color.white, TextAnchor.MiddleLeft, center: true);
        }

        static void DrawRelicRow(RectTransform frame, RelicId r, float y)
        {
            UiKit.Label("rr_" + r, frame, new Vector2(0, y), new Vector2(820, 145),
                "<b>" + Loc.T("relic." + r) + "</b>\n<size=24><color=#9a92b8>" + GameFlow.RelicDesc(r) + "</color></size>",
                30, Color.white, TextAnchor.MiddleLeft, center: true);
        }
    }
}
