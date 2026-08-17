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
        Bomber,           // 카운트다운 후 전원 광역 자폭 — 죽이면 해제 (v0.7)
        Thief,            // 공격 대신 골드를 훔침 — 처치하면 이자 붙여 회수 (v0.7)
        LichBoss,         // 2막 최종 보스: ①차징 ②사령 폭풍(탱커 포함 전원) ③강타 — 격노 시 폭풍 반복 (v0.8)
    }

    /// 아군 클래스 고유 특성 — 겹치지 않는 매커니즘 (풀 확장 규칙)
    public enum Trait
    {
        None,
        TankHealOnHit,    // 성기사: 공격할 때마다 탱커 회복
        Frenzy,           // 광전사: HP 절반 이하면 공격력 2배
        Cleanse,          // 음유시인: 공격 후 위축된 아군 1명 해제
        FullHpDouble,     // 문지기: 자기 HP가 최대면 공격력 2배 (풀피 유지 보상)
        KillChain,        // 그림자: 적을 처치하면 즉시 한 번 더 공격
        Momentum,         // 전사: 같은 대상을 연속 공격할 때마다 공격력 +1 누적
        GoldOnKill,       // 도적: 적을 처치하면 골드 획득
        ArcaneNova,       // 마법사: N턴마다 모든 적에게 절반 피해 광역
        Sniper,           // 궁수: 킬각 무시, 항상 공격력이 가장 높은 적을 조준
        FirstStrike,      // 암살자: 전투 첫 턴 공격력 2배 (선제 기습)
        Devour,           // 수인: 적을 처치하면 자신을 회복 (포식)
    }

    public class Unit
    {
        public string Name;
        public string NameKey;   // 언어 전환 시 재번역용 Loc 키
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
        public int ShieldCharges; // 철벽 방패: 남은 무효 횟수 (강화 시 2)
        public int Step;
        public int ChargeOffset;
        public bool SmashToDps = true;
        public Trait Trait;      // 아군 전용 특성
        public int Thorns;       // 적 전용: 맞을 때 공격자에게 반사 피해 (0 = 없음)
        public bool Lifesteal;   // 적 전용: 준 피해만큼 회복
        public Unit HealIntent;  // EnemyHealer 전용: 회복 대상
        public bool Aura;        // 고블린 대장: 살아있는 동안 다른 적 공격 +1 (v0.7)
        public int BombTimer;    // Bomber 전용: 남은 턴 (0이 되는 턴에 자폭)
        public int StolenGold;   // Thief 전용: 훔친 골드 누적
        public int Momentum;     // 전사 전용: 연속 공격 누적
        public Unit LastTarget;  // 전사 전용: 직전 공격 대상
        public bool SelfShield;  // 해골 방패병: 매턴 방패 1회 리필 (v0.8)
        public bool Pack;        // 늑대: 살아있는 다른 늑대 수만큼 공격 + (v0.8)
        public int Row;          // 아군 진형 — 0=전열 1=후열 (v0.9, 탱커는 항상 0)
        public bool RangedClass; // 아군 원거리 클래스 — 진형 공격 보정 미적용
        public bool Leap;        // 적 도약형(거미·박쥐·늑대) — 진형 무시하고 아무나 노린다
        public int BountyGold;   // 미믹: 처치 시 골드 드랍 (v0.8)
        public int DamageCap;    // 저주 갑옷: 받는 한 방 피해 상한 (0 = 없음) (v0.8)

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
