using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var m = Object.FindFirstObjectByType<Tanker.BattleManager>();
        result.Log("턴:" + m.Turn + " 페이즈:" + m.Phase + " | 나:" + m.Tank.Hp + " 딜러:" + m.Dps.Hp
            + " 힐러:" + m.Healer.Hp + " | 고A:" + m.GoblinA.Hp + " 고B:" + m.GoblinB.Hp + " 브루트:" + m.Brute.Hp
            + " | 막은피해:" + m.TotalSaved + " | " + m.Log);
    }
}
