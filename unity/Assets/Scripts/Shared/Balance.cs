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
        public float restRatio = 0.25f;
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
        public float scaleHpPct = 0.12f;
        public int scalePowerStages = 2;

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
        public int mapFloors = 8;
        public int mapElites = 2;
        public int mapShops = 2;
        public int mapTreasures = 1;
        public int mapRests = 1;          // 중간 휴식 (보스 전 휴식은 별도 고정)
        public int mapEvents = 2;
        public int eliteMinFloor = 3;
        public int eliteBudgetBonus = 3;
        public int treasureGold = 25;

        // v0.6 — 전투 핸드
        public int handSize = 4;

        public int guardPrice = 40;
        public int guardValue = 2;
        public int coverPrice = 50;
        public int coverValue = 2;
        public int bracePrice = 40;
        public int braceValue = 2;
        public int potionPrice = 30;
        public int potionHeal = 15;

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
