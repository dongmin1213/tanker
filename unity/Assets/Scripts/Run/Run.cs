using System.Collections.Generic;

namespace Tanker
{
    public enum NodeType { Battle, Elite, Event, Rest, Shop, Treasure, Boss }

    /// 분기 맵의 방 하나 — Next는 다음 층에서 이동 가능한 방 Id (StS식 DAG)
    [System.Serializable]
    public class MapNode
    {
        public int Id, Floor;
        public float X;             // 맵 화면 가로 슬롯 좌표
        public NodeType Type;
        public List<int> Next = new();
    }

    public enum ClassId { Warrior, Rogue, Mage, Ranger, Assassin, Beastkin, Cleric, Paladin, Berserker, Bard, Warden, Shadow }

    /// 런 전체 패시브 유물 (v0.7) — 획득: 엘리트 확정 / 보물 택1 / 상점
    public enum RelicId { ThornShield, VeteranHelm, WarBanner, GoldMagnet, WaterSkin, VictoryMeal, IronHeart, MerchantSeal, OldStandard, GuardCharm }

    public class ClassDef
    {
        public ClassId Id; public string LocKey, Sheet; public int Hp, Power; public bool IsHealer;
        public Trait Trait;
        public bool Ranged;  // 원거리 클래스 — 진형(전열/후열) 공격 보정을 받지 않는다 (v0.9)
    }

    /// 던전 런 한 판의 상태 — 시드가 파티·인카운터·덱 셔플을 결정한다 (모든 랜덤은 사전 공개).
    /// JsonUtility로 통째 저장/복원 (노드마다 자동 저장 — 모바일 중단 대응).
    [System.Serializable]
    public class RunState
    {
        public int Seed;
        public int Act;                       // 0 = 1막, 1 = 2막(심층) — v0.8
        public int Cur = -1;                  // 현재 방 Id (-1 = 아직 입장 전 — 1층에서 고른다)
        public int Gold;
        public int TankHp;
        public List<MapNode> Map;             // 시드 생성 분기 맵
        public List<int> Visited = new();
        public List<ClassId> Party = new();   // 동료 (탱커 제외)
        public List<int> PartyHp = new();
        public List<int> Rows = new();        // 동료 배치 — 0=전열 1=후열 (v0.9 진형, 탱커는 항상 전열)
        public List<Card> Deck = new();
        public List<RelicId> Relics = new();  // v0.7 유물
        public bool DpsShakenNext;            // 이벤트 (c) — 다음 전투에서 최강 공격수 위축 시작
        public int TauntGuard, CoverReduce, BraceBonus;
        public int TotalRedirected, TotalMitigated;
        public int BattlesWon;
        public int BattleIndex;               // 전투 순번 (통계)
        public float PlaySeconds;             // 실플레이 시간 계측 — "10~30분 세션" 검증용 (v0.9)

        // 승리 직후 미수령 보상 체크포인트 — 보상 화면에서 앱이 죽어도 전투를 다시 시키지 않는다
        public int Pending;                   // 0=없음 1=카드 보상 2=엘리트 유물 3=심층 선택
        public int PendingGold;               // 보물방 골드 (보상 화면 문구용)
        public bool PendingTreasure;          // 보물방 보상 여부

        public static int TankMax => Balance.I.tankHp; // 기본 최대 (유물 미반영 — 표시는 TankMaxHp 사용)
        public int TankMaxHp => Balance.I.tankHp + (Has(RelicId.IronHeart) ? Balance.I.relicIronHeart : 0);

        public bool Has(RelicId r) => Relics.Contains(r);

        public void AddRelic(RelicId r)
        {
            if (Has(r)) return;
            Relics.Add(r);
            if (r == RelicId.IronHeart) TankHp += Balance.I.relicIronHeart; // 최대와 함께 현재 HP도 상승
        }

        public MapNode CurNode => Cur >= 0 ? Map[Cur] : null;
        public int FloorReached => Cur >= 0 ? Map[Cur].Floor + 1 : 0;

        /// 지금 이동 가능한 방 Id들 — 입장 전엔 1층 전체, 이후엔 현재 방의 연결선
        public List<int> Reachable()
        {
            var r = new List<int>();
            if (Cur < 0) { foreach (var n in Map) if (n.Floor == 0) r.Add(n.Id); }
            else r.AddRange(Map[Cur].Next);
            return r;
        }

        public RunState() { } // JsonUtility 복원용

        /// firstRun: 첫 원정은 탱커+전사+클레릭 고정 — 규칙을 배우는 안전한 구성 (온보딩)
        public RunState(int seed, bool firstRun = false)
        {
            Seed = seed;
            TankHp = Balance.I.tankHp;
            if (firstRun)
            {
                Party.Add(ClassId.Warrior);
                Party.Add(ClassId.Cleric);
            }
            else
            {
                var rng = new System.Random(seed);
                int n = Balance.I.partyMinCompanions
                      + rng.Next(Balance.I.partyMaxCompanions - Balance.I.partyMinCompanions + 1);
                var pool = new List<ClassId>((ClassId[])System.Enum.GetValues(typeof(ClassId)));
                for (int i = 0; i < n && pool.Count > 0; i++)
                {
                    var pick = pool[rng.Next(pool.Count)];
                    pool.Remove(pick);
                    Party.Add(pick);
                }
            }
            foreach (var id in Party) PartyHp.Add(RunData.Class(id).Hp);
            EnsureRows();
            Deck = Cards.StarterDeck();
            // 맵은 독립 서브시드 — 파티 생성 RNG 소비량이 바뀌어도 같은 시드의 맵은 유지된다
            Map = RunData.GenerateMap(new System.Random(seed * 613 + 101));
        }

        /// 배치 기본값 채우기 — 근접=전열, 원거리/힐러=후열. 구버전 저장 이관도 겸한다 (v0.9)
        public void EnsureRows()
        {
            if (Rows == null) Rows = new List<int>();
            while (Rows.Count < Party.Count)
                Rows.Add(RunData.Class(Party[Rows.Count]).Ranged ? 1 : 0);
            while (Rows.Count > Party.Count) Rows.RemoveAt(Rows.Count - 1);
        }

        /// camp=true(휴식 방)일 때만 물주머니 보너스 — 심층 진입 재정비 등엔 미적용
        public void RestAll(float ratio, bool camp = true)
        {
            if (camp && Has(RelicId.WaterSkin)) ratio += Balance.I.relicWaterSkin;
            TankHp = System.Math.Min(TankMaxHp, TankHp + (int)(TankMaxHp * ratio));
            for (int i = 0; i < Party.Count; i++)
            {
                if (PartyHp[i] <= 0) continue;
                int max = RunData.Class(Party[i]).Hp;
                PartyHp[i] = System.Math.Min(max, PartyHp[i] + (int)(max * ratio));
            }
        }
    }

    /// 런 자동 저장 — 노드 완료마다 저장, 타이틀 "이어하기" (모바일 중단 대응)
    /// 저장·삭제는 즉시 디스크 플러시 — 모바일은 정상 종료 콜백을 보장하지 않는다.
    public static class RunSave
    {
        const string Key = "run.save";

        public static void Save(RunState run)
        {
            UnityEngine.PlayerPrefs.SetString(Key, UnityEngine.JsonUtility.ToJson(run));
            UnityEngine.PlayerPrefs.Save();
        }

        public static bool Has() => UnityEngine.PlayerPrefs.HasKey(Key);

        public static RunState Load()
        {
            try
            {
                var run = UnityEngine.JsonUtility.FromJson<RunState>(UnityEngine.PlayerPrefs.GetString(Key));
                return Valid(run) ? run : null;
            }
            catch { return null; }
        }

        /// 잘린/구버전 저장이 인덱스 예외로 터지지 않게 — 무결성이 깨졌으면 이어하기를 막는다
        static bool Valid(RunState r)
        {
            if (r == null || r.Map == null || r.Map.Count == 0) return false;
            if (r.Cur < -1 || r.Cur >= r.Map.Count) return false;
            if (r.Party == null || r.PartyHp == null || r.Party.Count != r.PartyHp.Count) return false;
            if (r.Deck == null || r.Deck.Count == 0 || r.Relics == null || r.Visited == null) return false;
            if (r.Act < 0 || r.TankHp <= 0) return false;
            foreach (var n in r.Map)
                if (n == null || n.Next == null) return false;
            return true;
        }

        public static void Clear()
        {
            UnityEngine.PlayerPrefs.DeleteKey(Key);
            UnityEngine.PlayerPrefs.Save();
        }
    }

    public class UnitDef
    {
        public string NameKey, Sheet;
        public int Hp, Power, AoePower, ChargeOffset, Thorns, BountyGold, DamageCap;
        public AiKind Ai;
        public bool Lifesteal, Aura, SelfShield, Pack, Leap;

        public UnitDef(string nameKey, string sheet, int hp, int power, AiKind ai, int chargeOffset = 0, int aoePower = 0,
                       int thorns = 0, bool lifesteal = false, bool aura = false,
                       bool selfShield = false, bool pack = false, int bountyGold = 0, int damageCap = 0,
                       bool leap = false)
        { NameKey = nameKey; Sheet = sheet; Hp = hp; Power = power; Ai = ai; ChargeOffset = chargeOffset; AoePower = aoePower;
          Thorns = thorns; Lifesteal = lifesteal; Aura = aura;
          SelfShield = selfShield; Pack = pack; BountyGold = bountyGold; DamageCap = damageCap; Leap = leap; }
    }

    public class EncounterDef
    {
        public string Title;      // 적 구성에서 생성한 전투 이름 ("고블린 무리", "오크의 습격" 등)
        public string TitleKey;   // 언어 전환 시 재번역용 레시피 — 포맷 키 (+선택 유닛 키)
        public string TitleArgKey;
        public int Gold;
        public UnitDef[] Units;
    }

    /// 상점 매물 — 키만 저장하고 표시 시점에 번역한다 (언어 전환·영어 모드 대응)
    public class ShopItem
    {
        public string NameKey, DescKey;
        public int DescArg;
        public int Price;
        public System.Action<RunState> Apply;
        public bool Bought;
        public string Name => Loc.T(NameKey);
        public string Desc => DescArg != 0 ? Loc.F(DescKey, DescArg) : Loc.T(DescKey);
    }

    /// 런 콘텐츠 — 분기 맵 생성, 클래스/적 풀, 시드 기반 인카운터, 스테이지 스케일링. 수치는 Balance, 문자열은 Loc.
    public static class RunData
    {
        /// StS식 분기 맵 생성 — 1층 전투 시작, 마지막 전 층 휴식 보장, 최상층 보스.
        /// 층간 연결은 비례 구간 매핑이라 교차 없이 전 노드가 시작~보스에 연결된다.
        public static List<MapNode> GenerateMap(System.Random rng)
        {
            var b = Balance.I;
            int floors = b.mapFloors;
            var map = new List<MapNode>();
            var byFloor = new List<MapNode>[floors];

            for (int f = 0; f < floors; f++)
            {
                int count = f == 0 || f >= floors - 2
                    ? 1 // 시작·보스 전 휴식·보스층은 1개
                    : b.mapNodesMin + rng.Next(b.mapNodesMax - b.mapNodesMin + 1);
                byFloor[f] = new List<MapNode>();
                for (int i = 0; i < count; i++)
                {
                    float span = count == 2 ? 280f : 460f;
                    float x = count == 1 ? 0 : -span / 2f + span / (count - 1) * i;
                    var node = new MapNode { Id = map.Count, Floor = f, X = x, Type = NodeType.Battle };
                    map.Add(node);
                    byFloor[f].Add(node);
                }
            }

            // 연결 — 비례 구간 매핑 (교차 없음, 양방향 커버 보장)
            for (int f = 0; f + 1 < floors; f++)
            {
                int na = byFloor[f].Count, nb = byFloor[f + 1].Count;
                for (int a = 0; a < na; a++)
                {
                    int lo = a * nb / na, hi = ((a + 1) * nb - 1) / na;
                    for (int t = lo; t <= hi; t++) byFloor[f][a].Next.Add(byFloor[f + 1][t].Id);
                }
            }

            // 타입 배치 — 고정: 마지막 전 층 휴식, 최상층 보스. 중간 층 노드에 특수 방 배분, 나머지는 전투.
            byFloor[floors - 2][0].Type = NodeType.Rest;
            byFloor[floors - 1][0].Type = NodeType.Boss;
            var mid = new List<MapNode>();
            for (int f = 1; f < floors - 2; f++) mid.AddRange(byFloor[f]);

            void Assign(NodeType type, int count, int minFloor)
            {
                for (int c = 0; c < count; c++)
                {
                    for (int guard = 0; guard < 60; guard++)
                    {
                        var n = mid[rng.Next(mid.Count)];
                        if (n.Type != NodeType.Battle || n.Floor < minFloor) continue;
                        // 같은 층에 같은 특수 방 중복 금지
                        bool dup = false;
                        foreach (var o in byFloor[n.Floor]) if (o != n && o.Type == type) dup = true;
                        if (dup) continue;
                        n.Type = type;
                        break;
                    }
                }
            }

            Assign(NodeType.Elite, b.mapElites, b.eliteMinFloor);
            Assign(NodeType.Shop, b.mapShops, b.shopMinFloor);
            Assign(NodeType.Treasure, b.mapTreasures, b.treasureMinFloor);
            Assign(NodeType.Rest, b.mapRests, b.restMidMinFloor);
            Assign(NodeType.Event, b.mapEvents, b.eventMinFloor);
            return map;
        }

        public static ClassDef Class(ClassId id)
        {
            var b = Balance.I;
            switch (id)
            {
                case ClassId.Warrior: return new ClassDef { Id = id, LocKey = "class.warrior", Sheet = "warrior", Hp = b.warriorHp, Power = b.warriorPower, Trait = Trait.Momentum };
                case ClassId.Rogue: return new ClassDef { Id = id, LocKey = "class.rogue", Sheet = "dps", Hp = b.rogueHp, Power = b.roguePower, Trait = Trait.GoldOnKill };
                case ClassId.Mage: return new ClassDef { Id = id, LocKey = "class.mage", Sheet = "mage", Hp = b.mageHp, Power = b.magePower, Trait = Trait.ArcaneNova, Ranged = true };
                case ClassId.Ranger: return new ClassDef { Id = id, LocKey = "class.ranger", Sheet = "ranger", Hp = b.rangerHp, Power = b.rangerPower, Trait = Trait.Sniper, Ranged = true };
                case ClassId.Assassin: return new ClassDef { Id = id, LocKey = "class.assassin", Sheet = "assassin", Hp = b.assassinHp, Power = b.assassinPower, Trait = Trait.FirstStrike };
                case ClassId.Beastkin: return new ClassDef { Id = id, LocKey = "class.beastkin", Sheet = "beastkin", Hp = b.beastkinHp, Power = b.beastkinPower, Trait = Trait.Devour };
                case ClassId.Warden: return new ClassDef { Id = id, LocKey = "class.warden", Sheet = "warden", Hp = b.wardenHp, Power = b.wardenPower, Trait = Trait.FullHpDouble };
                case ClassId.Shadow: return new ClassDef { Id = id, LocKey = "class.shadow", Sheet = "shadow", Hp = b.shadowHp, Power = b.shadowPower, Trait = Trait.KillChain };
                case ClassId.Paladin: return new ClassDef { Id = id, LocKey = "class.paladin", Sheet = "paladin", Hp = b.paladinHp, Power = b.paladinPower, Trait = Trait.TankHealOnHit };
                case ClassId.Berserker: return new ClassDef { Id = id, LocKey = "class.berserker", Sheet = "berserker", Hp = b.berserkerHp, Power = b.berserkerPower, Trait = Trait.Frenzy };
                case ClassId.Bard: return new ClassDef { Id = id, LocKey = "class.bard", Sheet = "bard", Hp = b.bardHp, Power = b.bardPower, Trait = Trait.Cleanse, Ranged = true };
                default: return new ClassDef { Id = id, LocKey = "class.cleric", Sheet = "healer", Hp = b.clericHp, Power = b.clericPower, IsHealer = true, Ranged = true };
            }
        }

        public static string NodeTitle(NodeType t) => Loc.T("nodeT." + t);

        // 적 풀: (락키, 시트, hp, power, ai, cost, aoe)
        class EnemyPick
        {
            public string Key, Sheet; public int Hp, Power, Cost; public AiKind Ai;
            public int Thorns, BountyGold, DamageCap; public bool Lifesteal, Aura, SelfShield, Pack, Leap;
        }

        static List<EnemyPick> EnemyPool()
        {
            var b = Balance.I;
            return new List<EnemyPick>
            {
                new EnemyPick { Key = "unit.goblin", Sheet = "goblin", Hp = b.goblinHp, Power = b.goblinPower, Ai = AiKind.FixedHealer, Cost = 2 },
                new EnemyPick { Key = "unit.goblin", Sheet = "goblin", Hp = b.goblinHp, Power = b.goblinPower, Ai = AiKind.FixedDps, Cost = 2 },
                new EnemyPick { Key = "unit.goblin", Sheet = "goblin", Hp = b.goblinHp, Power = b.goblinPower, Ai = AiKind.LowestBackliner, Cost = 2 },
                new EnemyPick { Key = "unit.archer", Sheet = "archer", Hp = b.archerHp, Power = b.archerPower, Ai = AiKind.FixedHealer, Cost = 2 },
                new EnemyPick { Key = "unit.slime", Sheet = "slime", Hp = b.slimeHp, Power = b.slimePower, Ai = AiKind.LowestBackliner, Cost = 1 },
                new EnemyPick { Key = "unit.orc", Sheet = "orc", Hp = b.orcHp, Power = b.orcPower, Ai = AiKind.FixedDps, Cost = 3 },
                new EnemyPick { Key = "unit.shaman", Sheet = "shaman", Hp = b.shamanHp, Power = 0, Ai = AiKind.ShamanCurse, Cost = 3 },
                new EnemyPick { Key = "unit.spider", Sheet = "spider", Hp = b.spiderHp, Power = b.spiderPower, Ai = AiKind.SpiderDouble, Cost = 3, Leap = true },
                new EnemyPick { Key = "unit.brute", Sheet = "brute", Hp = b.bruteHp, Power = b.brutePower, Ai = AiKind.BruteCycle, Cost = 4 },
                new EnemyPick { Key = "unit.golem", Sheet = "golem", Hp = b.golemHp, Power = b.golemPower, Ai = AiKind.LowestBackliner, Cost = 3, Thorns = b.golemThorns },
                new EnemyPick { Key = "unit.bat", Sheet = "bat", Hp = b.batHp, Power = b.batPower, Ai = AiKind.LowestBackliner, Cost = 1, Lifesteal = true, Leap = true },
                new EnemyPick { Key = "unit.necro", Sheet = "necro", Hp = b.necroHp, Power = 0, Ai = AiKind.EnemyHealer, Cost = 3 },
                new EnemyPick { Key = "unit.chief", Sheet = "chief", Hp = b.chiefHp, Power = b.chiefPower, Ai = AiKind.LowestBackliner, Cost = 3, Aura = true },
                new EnemyPick { Key = "unit.bomber", Sheet = "bomber", Hp = b.bomberHp, Power = 0, Ai = AiKind.Bomber, Cost = 3 },
                new EnemyPick { Key = "unit.thief", Sheet = "thief", Hp = b.thiefHp, Power = 0, Ai = AiKind.Thief, Cost = 2 },
                new EnemyPick { Key = "unit.skeleton", Sheet = "skeleton", Hp = b.skeletonHp, Power = b.skeletonPower, Ai = AiKind.LowestBackliner, Cost = 2, SelfShield = true },
                new EnemyPick { Key = "unit.wolf", Sheet = "wolf", Hp = b.wolfHp, Power = b.wolfPower, Ai = AiKind.LowestBackliner, Cost = 2, Pack = true, Leap = true },
                new EnemyPick { Key = "unit.mimic", Sheet = "mimic", Hp = b.mimicHp, Power = b.mimicPower, Ai = AiKind.FixedDps, Cost = 2, BountyGold = b.mimicBounty },
                new EnemyPick { Key = "unit.armor", Sheet = "armor", Hp = b.armorHp, Power = b.armorPower, Ai = AiKind.LowestBackliner, Cost = 3, DamageCap = b.armorDamageCap },
            };
        }

        /// 적 구성에서 전투 이름 레시피 — "고블린 무리"(같은 종 다수) / "오크의 습격"(최고 코스트 대표) / 정예 접두.
        /// 키를 반환해 언어 전환 시 재번역 가능하게 한다.
        static void EncounterTitleKeys(List<EnemyPick> picked, bool elite, out string key, out string arg)
        {
            var lead = picked[0];
            bool allSame = true;
            foreach (var p in picked)
            {
                if (p.Cost > lead.Cost) lead = p;
                if (p.Key != picked[0].Key) allSame = false;
            }
            if (elite) { key = "enc.eliteAmbush"; arg = lead.Key; }
            else if (allSame && picked.Count > 1) { key = "enc.pack"; arg = picked[0].Key; }
            else { key = "enc.ambush"; arg = lead.Key; }
        }

        /// 도감용 적 목록 — 풀 전체 + 보스 2종, 시트 기준 중복 제거 (v0.8)
        public struct CodexEnemy { public string Key, Sheet; public int Hp, Power; }

        public static List<CodexEnemy> EnemyCodex()
        {
            var b = Balance.I;
            var list = new List<CodexEnemy>();
            var seen = new HashSet<string>();
            foreach (var p in EnemyPool())
            {
                if (!seen.Add(p.Sheet)) continue;
                list.Add(new CodexEnemy { Key = p.Key, Sheet = p.Sheet, Hp = p.Hp, Power = p.Power });
            }
            list.Add(new CodexEnemy { Key = "unit.warlord", Sheet = "boss", Hp = b.bossHp, Power = b.bossPower });
            list.Add(new CodexEnemy { Key = "unit.lich", Sheet = "lich", Hp = b.lichHp, Power = b.lichPower });
            return list;
        }

        static int ScaledHp(int hp, int stage) =>
            (int)System.Math.Round(hp * (1f + Balance.I.scaleHpPct * stage));

        static int ScaledPower(int power, int stage) =>
            power <= 0 ? power : power + stage / Balance.I.scalePowerStages;

        /// 시드·스테이지 기반 인카운터 생성 (보스 방은 고정 구성 + 스케일링, 엘리트는 예산 보너스)
        public static EncounterDef GetEncounter(RunState run)
        {
            var b = Balance.I;
            // 스케일링은 층 기반 — 전투를 피해 달려도 깊이만큼 강해진다. 2막은 층 + 보너스 (v0.8)
            int stage = run.CurNode.Floor + run.Act * b.actStageBonus;
            bool elite = run.CurNode.Type == NodeType.Elite;
            var rng = new System.Random(run.Seed * 977 + (run.Act + 1) * 419 + run.Cur * 131);

            if (run.CurNode.Type == NodeType.Boss)
            {
                if (run.Act >= 1)
                    return new EncounterDef
                    {
                        Title = Loc.T("enc.lich"),
                        TitleKey = "enc.lich",
                        Gold = 0,
                        Units = new[]
                        {
                            new UnitDef("unit.lich", "lich", b.lichHp, b.lichPower,
                                        AiKind.LichBoss, aoePower: b.lichAoePower),
                            new UnitDef("unit.necro", "necro", ScaledHp(b.necroHp, stage), 0, AiKind.EnemyHealer),
                        },
                    };
                return new EncounterDef
                {
                    Title = Loc.T("enc.warlord"),
                    TitleKey = "enc.warlord",
                    Gold = 0,
                    Units = new[]
                    {
                        new UnitDef("unit.warlord", "boss", ScaledHp(b.bossHp, stage), ScaledPower(b.bossPower, stage),
                                    AiKind.BossWarlord, aoePower: ScaledPower(b.bossAoePower, stage)),
                        new UnitDef("unit.goblin", "goblin", ScaledHp(b.goblinHp, stage), ScaledPower(b.goblinPower, stage),
                                    AiKind.LowestBackliner),
                    },
                };
            }

            int attackers = 0;
            foreach (var id in run.Party) if (!Class(id).IsHealer) attackers++;
            int budget = b.encounterBudgetBase + stage * b.encounterBudgetPerStage + (attackers - 2)
                       + (elite ? b.eliteBudgetBonus : 0);

            var pool = EnemyPool();
            var picked = new List<EnemyPick>();
            int guard = 0;
            while (budget > 0 && picked.Count < 4 && guard++ < 50)
            {
                var cand = pool[rng.Next(pool.Count)];
                if (cand.Cost > budget && picked.Count >= 2) break;
                if (cand.Cost > budget) continue;
                picked.Add(cand);
                budget -= cand.Cost;
            }
            while (picked.Count < 2) { picked.Add(pool[4]); } // 최소 2마리 (슬라임 보충)

            var units = new UnitDef[picked.Count];
            int bruteOffset = 0;
            for (int i = 0; i < picked.Count; i++)
            {
                var p = picked[i];
                units[i] = new UnitDef(p.Key, p.Sheet, ScaledHp(p.Hp, stage), ScaledPower(p.Power, stage),
                                       p.Ai, chargeOffset: p.Ai == AiKind.BruteCycle ? (bruteOffset++ % 2) : 0,
                                       thorns: p.Thorns, lifesteal: p.Lifesteal, aura: p.Aura,
                                       selfShield: p.SelfShield, pack: p.Pack, bountyGold: p.BountyGold, damageCap: p.DamageCap, leap: p.Leap);
            }
            EncounterTitleKeys(picked, elite, out var tKey, out var tArg);
            return new EncounterDef
            {
                Title = Loc.F(tKey, Loc.T(tArg)),
                TitleKey = tKey,
                TitleArgKey = tArg,
                Gold = elite ? b.eliteGold : b.battleGold,
                Units = units,
            };
        }

        public static List<ShopItem> MakeShop()
        {
            var b = Balance.I;
            return new List<ShopItem>
            {
                new ShopItem { NameKey = "item.guard", DescKey = "item.guard.desc", DescArg = b.guardValue, Price = b.guardPrice, Apply = r => r.TauntGuard += b.guardValue },
                new ShopItem { NameKey = "item.cover", DescKey = "item.cover.desc", DescArg = b.coverValue, Price = b.coverPrice, Apply = r => r.CoverReduce += b.coverValue },
                new ShopItem { NameKey = "item.brace", DescKey = "item.brace.desc", DescArg = b.braceValue, Price = b.bracePrice, Apply = r => r.BraceBonus += b.braceValue },
                new ShopItem { NameKey = "item.potion", DescKey = "item.potion.desc", DescArg = b.potionHeal, Price = b.potionPrice, Apply = r => r.TankHp = System.Math.Min(r.TankMaxHp, r.TankHp + b.potionHeal) },
            };
        }
    }
}
