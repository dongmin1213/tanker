using UnityEngine;
using UnityEngine.UI;

namespace Tanker
{
    /// 전 화면 공용 설정 오버레이 — 기어 버튼 부착 + 볼륨/언어/타이틀 복귀.
    /// 오버레이는 열 때 전용 캔버스로 생성, 닫으면 파기 (화면 구조와 독립).
    public static class SettingsUI
    {
        /// 화면 우상단에 설정 버튼을 붙인다.
        /// onRedraw: 언어 변경 후 현재 화면을 다시 그리는 콜백 (null이면 생략).
        /// onReturnTitle: 타이틀 복귀 실행 (null이면 버튼 숨김 — 타이틀 화면 자신).
        public static void AttachGear(RectTransform frame, System.Action onRedraw, System.Action onReturnTitle)
        {
            UiKit.Btn("gear", frame, new Vector2(455, 870), new Vector2(140, 76),
                Loc.T("set.open"), () => Open(onRedraw, onReturnTitle), 28, center: true);
        }

        static GameObject openCanvas; // 연타로 겹쳐 열리는 것 방지

        public static void Open(System.Action onRedraw, System.Action onReturnTitle)
        {
            if (openCanvas != null) return;
            var frame = UiKit.MakeCanvas("SettingsCanvas", 90);
            var canvasGo = frame.parent.gameObject;
            openCanvas = canvasGo;
            bool confirming = false;

            // 배경 딤 — 탭 차단
            var dim = UiKit.Panel("dim", frame, Vector2.zero, new Vector2(1400, 2500), new Color(0, 0, 0, 0.72f), center: true);
            dim.raycastTarget = true;

            UiKit.FramedPanel("panel", frame, Vector2.zero, new Vector2(860, 1050), center: true);
            UiKit.Label("h1", frame, new Vector2(0, 420), new Vector2(700, 70), Loc.T("set.title"), 52, Color.white, bold: true, center: true);

            // 볼륨 슬라이더 2종
            UiKit.Label("bgmL", frame, new Vector2(-230, 280), new Vector2(280, 50), Loc.T("set.bgm"), 34, UiKit.Hex("cfc8e8"), TextAnchor.MiddleLeft, center: true);
            UiKit.MakeSlider("bgmS", frame, new Vector2(110, 280), new Vector2(420, 30), AudioKit.BgmVolume, v => AudioKit.BgmVolume = v);
            UiKit.Label("sfxL", frame, new Vector2(-230, 160), new Vector2(280, 50), Loc.T("set.sfx"), 34, UiKit.Hex("cfc8e8"), TextAnchor.MiddleLeft, center: true);
            UiKit.MakeSlider("sfxS", frame, new Vector2(110, 160), new Vector2(420, 30), AudioKit.SfxVolume, v => AudioKit.SfxVolume = v);

            // 전투 속도 (v0.9) — 해소 연출 대기 단축, 다음 전투부터 적용
            bool fast = PlayerPrefs.GetInt("speed.fast", 0) == 1;
            UiKit.Btn("speed", frame, new Vector2(0, 60), new Vector2(680, 90),
                Loc.T(fast ? "set.speedFast" : "set.speedNormal"), () =>
                {
                    PlayerPrefs.SetInt("speed.fast", fast ? 0 : 1);
                    PlayerPrefs.Save();
                    Object.Destroy(canvasGo);
                    openCanvas = null;
                    Open(onRedraw, onReturnTitle);
                }, 30, center: true);

            // 언어 전환
            UiKit.Btn("lang", frame, new Vector2(0, -50), new Vector2(680, 90), Loc.T("set.lang"), () =>
            {
                Loc.SetLang(Loc.Lang == Loc.KO ? Loc.EN : Loc.KO);
                Object.Destroy(canvasGo);
                openCanvas = null; // Destroy는 프레임 끝 처리 — 즉시 재오픈 허용
                onRedraw?.Invoke();
                Open(onRedraw, onReturnTitle); // 새 언어로 다시 연다
            }, 32, center: true);

            // 게임 방법 (v0.7 온보딩)
            UiKit.Btn("howto", frame, new Vector2(0, -160), new Vector2(680, 90), Loc.T("title.help"),
                () => HelpUI.Open(), 32, center: true);

            // 타이틀 복귀 (2단 확인)
            if (onReturnTitle != null)
            {
                var titleBtn = UiKit.Btn("toTitle", frame, new Vector2(0, -270), new Vector2(680, 90), Loc.T("set.toTitle"), null, 32, center: true);
                var titleTxt = titleBtn.GetComponentInChildren<Text>();
                titleBtn.onClick.AddListener(() =>
                {
                    if (!confirming)
                    {
                        confirming = true;
                        titleTxt.text = Loc.T("set.confirmTitle");
                        titleTxt.fontSize = 26;
                        UiKit.SetSelected(titleBtn, true);
                        return;
                    }
                    Object.Destroy(canvasGo);
                    onReturnTitle();
                });
            }

            UiKit.Btn("close", frame, new Vector2(0, -400), new Vector2(420, 100), Loc.T("set.close"),
                () => Object.Destroy(canvasGo), 36, center: true);
        }
    }
}
