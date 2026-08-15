using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log(Tanker.EditorTools.SetPortraitGameView());
    }
}
