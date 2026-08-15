using UnityEngine;
using UnityEditor;

// 재생 모드에서 게임 뷰(UI 포함)를 unity/captures/shot.png로 저장한다.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.LogWarning("재생 모드가 아님 — 게임 뷰 캡처는 재생 중에만 유효"); }
        System.IO.Directory.CreateDirectory("captures");
        ScreenCapture.CaptureScreenshot("captures/shot.png");
        result.Log("captures/shot.png 저장 요청 (다음 프레임에 기록됨)");
    }
}
