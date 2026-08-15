using UnityEngine;
using UnityEditor;

// 에디터/재생 상태 진단. frameCount가 늘지 않으면 게임 뷰 숨김(동결) — focus-gameview.cs 실행.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("isPlaying=" + EditorApplication.isPlaying
                   + " isCompiling=" + EditorApplication.isCompiling
                   + " isPaused=" + EditorApplication.isPaused
                   + " timeScale=" + Time.timeScale
                   + " frameCount=" + Time.frameCount);
    }
}
