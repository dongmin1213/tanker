namespace Tanker
{
    public enum Team { Ally, Enemy }
    public enum Role { Tank, Attacker, Healer }

    /// 적 행동 규칙 — 모든 랜덤은 사전 공개, 해소는 결정론.
    public enum AiKind
    {
        None,             // 아군
        FixedHealer,      // 힐러 고정 (없으면 최저 HP 비탱커)
        FixedDps,         // 최강 공격수 고정 (없으면 최저 HP 비탱커)
        LowestBackliner,  // HP 최저 비탱커
        BruteCycle,       // 차징-강타 교대 (ChargeOffset 위상), 강타는 백라이너 교대. 격노 시 매턴 공격
        BossWarlord,      // ①차징 ②광역(비탱커 전원) ③강타(최강 공격수) 사이클. 격노 시 ②③ 교대
        ShamanCurse,      // 공격 대신 최강 공격수에게 위축 저주. 도발되면 행동 낭비
        SpiderDouble,     // 같은 대상(최저 HP 비탱커)을 연타 2회 — 엄호는 첫 타만 리다이렉트
        EnemyHealer,      // 공격 대신 가장 다친 다른 적을 회복. 도발되면 행동 낭비
    }

    /// 아군 클래스 고유 특성 — 겹치지 않는 매커니즘 (풀 확장 규칙)
    public enum Trait
    {
        None,
        TankHealOnHit,    // 성기사: 공격할 때마다 탱커 회복
        Frenzy,           // 광전사: HP 절반 이하면 공격력 2배
        Cleanse,          // 음유시인: 공격 후 위축된 아군 1명 해제
    }

    public class Unit
    {
        public string Name;
        public string Sheet;     // 스프라이트 시트 키
        public Team Team;
        public Role Role;
        public AiKind Ai;
        public int MaxHp;
        public int Hp;
        public int Power;        // 공격수: 공격력, 힐러: 힐량, 보스: 강타
        public int AoePower;     // 보스 광역 대상당 피해 (0 = 없음)
        public bool IsTank;
        public bool Shaken;      // 위축: 다음 행동 효과 절반
        public int TauntTurns;   // 적 전용
        public Unit Intent;      // 적 전용: 단일 공격 대상 (null = 차징/광역/저주)
        public Unit CurseIntent; // 샤먼 전용: 저주 대상
        public bool AoeIntent;
        public bool Charging;
        public bool Enraged;
        public bool Stunned;     // 밀쳐내기: 이번 턴 행동 취소
        public bool Shielded;    // 철벽 방패: 이번 턴 첫 피해 무효
        public int Step;
        public int ChargeOffset;
        public bool SmashToDps = true;
        public Trait Trait;      // 아군 전용 특성
        public int Thorns;       // 적 전용: 맞을 때 공격자에게 반사 피해 (0 = 없음)
        public bool Lifesteal;   // 적 전용: 준 피해만큼 회복
        public Unit HealIntent;  // EnemyHealer 전용: 회복 대상

        public bool Alive => Hp > 0;

        public static Unit Make(string name, Team team, int hp, int power, bool tank = false,
                                string sheet = null, AiKind ai = AiKind.None, int chargeOffset = 0,
                                int aoePower = 0, Role role = Role.Attacker)
        {
            return new Unit
            {
                Name = name, Team = team, MaxHp = hp, Hp = hp, Power = power, IsTank = tank,
                Sheet = sheet, Ai = ai, ChargeOffset = chargeOffset, AoePower = aoePower,
                Role = tank ? Role.Tank : role
            };
        }
    }
}
