using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorApplication.ExitPlaymode();
        result.Log("재생 종료 요청");
    }
}
