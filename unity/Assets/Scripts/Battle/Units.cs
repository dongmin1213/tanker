namespace Tanker
{
    public enum Team { Ally, Enemy }

    public class Unit
    {
        public string Name;
        public Team Team;
        public int MaxHp;
        public int Hp;
        public int Power;        // 적/딜러: 공격력, 힐러: 힐량
        public bool IsTank;
        public bool Shaken;      // 위축: 다음 행동 효과 절반
        public int TauntTurns;   // 적 전용: 남은 도발 지속 턴
        public Unit Intent;      // 적 전용: 이번 턴 공격 대상 (null = 힘 모으기)
        public bool Charging;    // 브루트 전용: 이번 턴은 준비 턴
        public bool Enraged;     // 브루트 전용: 저체력 격노 — 매 턴 공격

        public bool Alive => Hp > 0;

        public static Unit Make(string name, Team team, int hp, int power, bool tank = false)
        {
            return new Unit { Name = name, Team = team, MaxHp = hp, Hp = hp, Power = power, IsTank = tank };
        }
    }
}
