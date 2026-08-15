using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var gvType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gv = EditorWindow.GetWindow(gvType);
        gv.Show();
        gv.Focus();
        Application.runInBackground = true;
        result.Log("게임 뷰 포커스 + runInBackground=true, frameCount=" + Time.frameCount);
    }
}
