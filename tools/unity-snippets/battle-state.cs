using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var m = Object.FindFirstObjectByType<Tanker.BattleManager>();
        if (m == null) { result.Log("전투 없음 (맵/타이틀 화면일 수 있음)"); return; }
        var s = "[" + m.EncounterTitle + "] 턴:" + m.Turn + " 페이즈:" + m.Phase + " | ";
        foreach (var a in m.Allies) s += a.Name + ":" + a.Hp + " ";
        s += "| ";
        foreach (var e in m.Enemies) s += e.Name + ":" + e.Hp + (e.Charging ? "(차징)" : "") + (e.Enraged ? "(격노)" : "") + " ";
        s += "| 대신맞음:" + m.RedirectedSaved + " 경감:" + m.MitigatedSaved + " | " + m.Log;
        result.Log(s);
    }
}
