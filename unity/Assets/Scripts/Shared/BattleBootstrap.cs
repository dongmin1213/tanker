using UnityEngine;
using UnityEngine.EventSystems;

namespace Tanker
{
    /// 어떤 씬에서든 재생만 누르면 게임이 뜨도록, 필요한 오브젝트를 코드로 생성한다.
    public static class BattleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Boot()
        {
            Application.targetFrameRate = 60; // 발열 방지 — 에디터/모바일 공통 프레임 캡 (최적화 규약)
            if (Object.FindFirstObjectByType<GameFlow>() != null) return;

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.12f);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            new GameObject("Game").AddComponent<GameFlow>();
        }
    }
}
