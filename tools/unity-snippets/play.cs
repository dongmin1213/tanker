using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorApplication.EnterPlaymode();
        result.Log("재생 모드 진입 요청");
    }
}
