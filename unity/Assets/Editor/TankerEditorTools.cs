using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tanker
{
    /// MCP RunCommand 스니펫에서 호출하는 에디터 유틸 모음.
    public static class EditorTools
    {
        /// 스탠드얼론 빌드. 레포는 씬을 커밋하지 않으므로(규약) 빌드 시점에 빈 씬을 생성해 쓴다 —
        /// 모든 오브젝트는 BattleBootstrap이 런타임 생성하니 빈 씬이면 충분하다.
        public static string BuildGame(string outputPath, BuildTarget target)
        {
            const string scenePath = "Assets/Scenes/Build.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);

            var report = BuildPipeline.BuildPlayer(new[] { scenePath }, outputPath, target, BuildOptions.None);
            var s = report.summary;
            return "빌드 " + s.result + " | 크기 " + (s.totalSize / (1024 * 1024)) + "MB | 에러 " + s.totalErrors
                 + " | 경고 " + s.totalWarnings + " | 출력 " + s.outputPath;
        }
        /// 게임 뷰를 1080x1920 세로 고정 해상도로 전환한다.
        public static string SetPortraitGameView() => SetGameViewSize(1080, 1920, "TankerPortrait");

        /// 게임 뷰를 임의 고정 해상도로 전환한다 (비율 내성 테스트용 — 예: 1080x2340 = 19.5:9).
        public static string SetGameViewSize(int w, int h, string name)
        {
            var asm = typeof(EditorWindow).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleType.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { 0 });

            var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].Contains(name)) { index = i; break; }

            if (index < 0)
            {
                var gvsType = asm.GetType("UnityEditor.GameViewSize");
                var gvstType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = gvsType.GetConstructor(new[] { gvstType, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.Parse(gvstType, "FixedResolution"), w, h, name });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = ((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null)).Length - 1;
            }

            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType);
            var prop = gvType.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            prop.SetValue(gv, index);
            gv.Repaint();
            return "게임 뷰 " + name + "(" + w + "x" + h + ") 전환 완료, index=" + index;
        }
    }
}
