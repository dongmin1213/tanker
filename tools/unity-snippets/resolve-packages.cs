using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        UnityEditor.PackageManager.Client.Resolve();
        result.Log("패키지 Resolve 요청 완료");
    }
}
