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
        // v1.0 풀 확장 — 전부 고유 행동 축
        Sealer,           // 주술 방해자: 공격 대신 손패 1장 봉인 (그 턴 사용 불가)
        Gazer,            // 응시자: 공격 대신 아군 1명 석화 (그 아군의 이번 턴 행동 취소)
        Dispeller,        // 방패 파괴자: 공격 대신 탱커의 방패·맹세·방벽 해제
        Rotmancer,        // 부패 술사: 공격 대신 이번 턴 아군 회복을 무효화
        Burrower,         // 굴착 벌레: 홀수 턴 잠복(무행동·무적) / 짝수 턴 강타
        Sentinel,         // 동면 골렘: N턴 각성 카운트 후 초강타 1회, 이후 매턴 강타
        TwinBlade,        // 쌍둥이 검사: 짝이 죽으면 공격 2배
        BroodBoss,        // 1막 보스 B — 거미 여왕: ①산란(새끼 소환) ②연타 ③광역
        ColossusBoss,     // 2막 보스 B — 강철 파괴자: 방패·엄호 관통 강타 + 격노 시 2회 행동
        WyrmBoss,         // 3막 최종 보스 — 심연의 용: ①화염 브레스(전원) ②꼬리 강타 ③비행(회피)
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
        // v1.0 풀 확장 — 전부 고유 축 (진형·상태·플레이어 선택과 결합)
        LongReach,        // 창병: 장창 — 후열에서도 진형 페널티 없음 + 전열 아군 1명 받는 피해 -1
        Poison,           // 독술사: 공격 시 독 중첩 — 적 페이즈 종료마다 중첩당 피해
        GroveHeal,        // 드루이드: 힐러 — 전열 아군 전원을 소량 회복 (클레릭은 단일 집중)
        Retaliate,        // 거인: 피격당할 때마다 다음 공격 누적 (기세는 공격 기반)
        Volley,           // 노병 사수: 두 적을 각각 절반 피해로 동시 사격
        Focus,            // 수도승: 이번 턴 카드를 쓰지 않았으면 공격 2배 (플레이어 선택 연동)
        Foresight,        // 점술사: 매 턴 카드 1장 추가 드로우
        Bulwark,          // 종사: 살아있는 동안 탱커가 받는 피해 -1
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
        // v1.0 상태·플래그
        public int PoisonIncoming; // 이번 턴 부여 예정 독 (프리뷰 집계용)
        public int Poison;       // 독 중첩 — 적 페이즈 종료마다 중첩당 1 피해 (적·아군 공용)
        public bool Petrified;   // 석화 — 이번 턴 행동 취소 (아군)
        public int Retaliation;  // 거인 피격 누적
        public bool Pierce;      // 적: 방패·엄호를 무시하고 직격
        public bool Reflector;   // 적: 받은 피해 절반을 최고 HP 아군에게 전가
        public bool HealBlock;   // 적: 살아있는 동안 아군 회복량 절반
        public bool Splitter;    // 적: 죽으면 다음 턴에 절반 분열체 2마리로 일어난다
        public bool Dormant;     // 분열 대기체 — 아직 등장하지 않은 유닛
        public bool SplitPending;// 이번 턴 죽은 분열체 — 다음 RollIntents에서 깨어난다
        public bool DeathBuff;   // 광신도: 죽을 때 다른 적 전원 공격 +
        public bool Env;         // 가시 덩굴: 도발·엄호 무시 환경 피해
        public bool CounterOnce; // 거울 정령: 다음 아군 공격 1회를 무효화하고 반사
        public bool PoisonHit;   // 역병 쥐: 공격 시 아군에게 독 부여
        public int PowerBonus;   // 일시 공격 증가 (교란 반동·광신도 유언)
        public bool Feinted;     // 교란 — 이번 턴 허공 공격 (보스에게도 통함)
        public bool Hidden;      // 굴착 벌레 잠복 — 이번 턴 무행동·피해 무효
        public int WakeTimer;    // 동면 골렘 각성 카운트
        public int FeintBonus;   // 교란 반동 — 다음 행동 1회 공격 증가
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
