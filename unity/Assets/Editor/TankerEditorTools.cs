using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tanker
{
    /// MCP RunCommand 스니펫에서 호출하는 에디터 유틸 모음.
    public static class EditorTools
    {
        /// 게임 뷰를 1080x1920 세로 고정 해상도로 전환한다.
        public static string SetPortraitGameView()
        {
            var asm = typeof(EditorWindow).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleType.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { 0 });

            var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].Contains("TankerPortrait")) { index = i; break; }

            if (index < 0)
            {
                var gvsType = asm.GetType("UnityEditor.GameViewSize");
                var gvstType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = gvsType.GetConstructor(new[] { gvstType, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.Parse(gvstType, "FixedResolution"), 1080, 1920, "TankerPortrait" });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = ((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null)).Length - 1;
            }

            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType);
            var prop = gvType.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            prop.SetValue(gv, index);
            gv.Repaint();
            return "게임 뷰 TankerPortrait(1080x1920) 전환 완료, index=" + index;
        }
    }
}
