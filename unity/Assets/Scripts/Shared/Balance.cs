using UnityEngine;

namespace Tanker
{
    /// 모든 밸런스 수치의 유일한 출처 — Resources/balance.json에서 로드한다 (규약: 수치 하드코딩 금지).
    /// 필드 기본값은 json이 없거나 키가 빠졌을 때의 안전망일 뿐, 진짜 값은 json이다.
    /// 수치를 바꾸면 balance.json과 docs/design.md 표를 함께 갱신할 것.
    [System.Serializable]
    public class Balance
    {
        public int tankHp = 40;
        public int dpsHp = 18;
        public int dpsPower = 6;
        public int healerHp = 14;
        public int healerPower = 5;

        public int tauntDuration = 2;
        public int tauntCooldown = 3;
        public int braceHeal = 3;

        public int goblinHp = 12;
        public int goblinPower = 4;
        public int archerHp = 10;
        public int archerPower = 5;
        public int bruteHp = 22;
        public int brutePower = 8;
        public int bruteEnrageHp = 12;
        public int bruteEnragePower = 6;
        public int eliteBruteHp = 18;
        public int bossHp = 45;
        public int bossPower = 10;
        public int bossAoePower = 5;
        public int bossEnrageHp = 24;

        public int battleGold = 40;
        public int eliteGold = 40;
        public float restRatio = 0.3f;

        // v0.9 진형·AI
        public int rowFrontBonus = 1;    // 근접 전열 공격 +
        public int rowBackPenalty = 1;   // 근접 후열 공격 -
        public int threatAuraBonus = 50; // 아군 킬각 우선순위: 지휘 오라
        public int threatBombBonus = 120;// 아군 킬각 우선순위: 폭발 임박
        public int trapTankCost = 6;
        public int trapToll = 20;
        public int trapDpsCost = 4;

        // v0.4 — 클래스 (도적=dps 시트, 클레릭=healer 시트)
        public int warriorHp = 22; public int warriorPower = 5;
        public int rogueHp = 15; public int roguePower = 7;
        public int mageHp = 12; public int magePower = 8;
        public int rangerHp = 14; public int rangerPower = 6;
        public int assassinHp = 11; public int assassinPower = 9;
        public int beastkinHp = 20; public int beastkinPower = 6;
        public int clericHp = 14; public int clericPower = 5;
        public int partyMinCompanions = 2;
        public int partyMaxCompanions = 4;

        // v0.4 — 신규 적
        public int slimeHp = 14; public int slimePower = 3;
        public int orcHp = 16; public int orcPower = 6;
        public int shamanHp = 10;
        public int spiderHp = 12; public int spiderPower = 3;

        // v0.4 — 인카운터 생성·스케일링
        public int encounterBudgetBase = 5;
        public int encounterBudgetPerStage = 2;
        public float scaleHpPct = 0.08f;  // v0.6.2: 층 기반 스케일로 전환하며 보정 (0~7층)
        public int scalePowerStages = 3;

        // v0.4 — 카드
        public int devotionAmount = 5;
        public int phalanxReduce = 2;
        public int oathReduce = 2;
        public int oathTurns = 2;
        public int cardRemovePrice = 30;

        // v0.5 — 신규 클래스 (특성: 성기사=공격 시 탱커 회복, 광전사=반피 2배, 음유시인=위축 해제)
        public int paladinHp = 18; public int paladinPower = 4; public int paladinTankHeal = 1;
        public int berserkerHp = 17; public int berserkerPower = 5;
        public int bardHp = 13; public int bardPower = 4;

        // v0.5 — 신규 적 (골렘=반사, 박쥐=흡혈, 네크로맨서=적 회복)
        public int golemHp = 24; public int golemPower = 4; public int golemThorns = 1;
        public int batHp = 8; public int batPower = 2;
        public int necroHp = 12; public int necroHeal = 3;

        // v0.5 — 신규 카드
        public int ironWillCap = 3;
        public int respiteHeal = 4;

        // v0.6 — 분기 맵 (StS식)
        public int mapFloors = 10;
        public int mapNodesMin = 2;       // 중간층 방 수 범위
        public int mapNodesMax = 3;
        public int mapElites = 2;
        public int mapShops = 2;
        public int mapTreasures = 1;
        public int mapRests = 1;          // 중간 휴식 (보스 전 휴식은 별도 고정)
        public int mapEvents = 2;
        public int eliteMinFloor = 3;
        public int shopMinFloor = 1;
        public int treasureMinFloor = 1;
        public int restMidMinFloor = 2;
        public int eventMinFloor = 1;
        public int eliteBudgetBonus = 3;
        public int treasureGold = 25;

        // v0.6 — 전투 핸드
        public int handSize = 4;

        // v0.7 — 카드 강화 (+) 수치
        public int tauntDurationPlus = 3;
        public int coverPlusReduce = 2;
        public int braceHealPlus = 5;
        public int shieldPlusCharges = 2;
        public int devotionAmountPlus = 7;
        public int rallyPlusHeal = 2;
        public int phalanxReducePlus = 3;
        public int oathTurnsPlus = 3;
        public int ironWillCapPlus = 2;
        public int markPlusReduce = 1;
        public int respiteHealPlus = 6;

        // v0.7 — 신규 카드
        public int thornStanceDmg = 2; public int thornStanceDmgPlus = 3;
        public int firstAidHeal = 3; public int firstAidHealPlus = 5;
        public int warCryTurns = 1; public int warCryPlusHeal = 2;

        // v0.7 — 유물 (획득처: 엘리트 확정, 보물 택1, 상점)
        public int relicPrice = 45;
        public int relicThornShield = 1;   // 가시 방패: 탱커 직격 반사
        public int relicWarBanner = 1;     // 군기: 첫 턴 아군 공격 +
        public int relicGoldMagnet = 15;   // 금화 자석: 전투 보상 +G
        public float relicWaterSkin = 0.15f; // 물주머니: 휴식 회복 +비율
        public int relicVictoryMeal = 3;   // 승전 축배: 승리 시 탱커 회복
        public int relicIronHeart = 6;     // 강철 심장: 탱커 최대 HP +
        public float relicMerchantSeal = 0.2f; // 상인 인장: 상점 할인
        public int relicOldStandard = 1;   // 낡은 깃발: 도발 지속 +턴
        public int relicGuardCharm = 1;    // 수호 부적: 낙인 분담 -

        // v0.7 — 신규 적
        public int chiefHp = 14; public int chiefPower = 3; public int chiefAura = 1;
        public int bomberHp = 10; public int bomberFuse = 3; public int bomberBlast = 6;
        public int thiefHp = 11; public int thiefSteal = 5;

        // v0.7 — 신규 클래스
        public int wardenHp = 21; public int wardenPower = 5;
        public int shadowHp = 12; public int shadowPower = 6;

        // v0.7 — 클래스 고유 특성 수치 (전 클래스 개성화)
        public int momentumStep = 1;       // 전사: 같은 대상 연속 공격 시 +누적
        public int killGold = 3;           // 도적: 처치 시 골드
        public int novaEvery = 3;          // 마법사: N턴마다 전체 광역 (각 절반 피해)
        public int devourHeal = 3;         // 수인: 처치 시 자가 회복
        

        // v0.7 — 이벤트·기타
        public int altarHpCost = 6;        // 수상한 제단: 탱커 HP를 바치고 유물
        public int relicDupGold = 30;      // 유물이 다 떨어졌을 때 대체 골드

        // v0.8 — 신규 적 4종
        public int skeletonHp = 12; public int skeletonPower = 4;  // 매턴 방패 1회
        public int wolfHp = 9; public int wolfPower = 3; public int packBonus = 1; // 무리당 +
        public int mimicHp = 13; public int mimicPower = 5; public int mimicBounty = 12; // 처치 골드
        public int armorHp = 18; public int armorPower = 4; public int armorDamageCap = 3; // 받는 피해 상한

        // v0.8 — 2막·최종 보스 리치 왕
        public int actStageBonus = 10;      // 2막 스케일 = 층 + 보너스
        public float descendHeal = 0.25f;  // 심층 진입 시 전원 회복 비율
        public int lichHp = 60;
        public int lichPower = 12;
        public int lichAoePower = 4;       // 사령 폭풍 — 탱커 포함 전원
        public int lichEnrageHp = 30;

        public int guardPrice = 40;
        public int guardValue = 2;
        public int coverPrice = 50;
        public int coverValue = 2;
        public int bracePrice = 40;
        public int braceValue = 2;
        public int potionPrice = 30;
        public int potionHeal = 20;

        static Balance i;
        public static Balance I => i ?? (i = Load());

        static Balance Load()
        {
            var b = new Balance();
            var ta = Resources.Load<TextAsset>("balance");
            if (ta != null) JsonUtility.FromJsonOverwrite(ta.text, b);
            else Debug.LogWarning("[Balance] Resources/balance.json 없음 — 코드 기본값 사용");
            return b;
        }

        /// 에디터 검증 도구용 — json 수정 후 강제 재로드
        public static void Reload() { i = null; }
    }
}
