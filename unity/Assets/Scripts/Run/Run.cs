using System.Collections.Generic;

namespace Tanker
{
    public enum NodeType { Battle, Elite, Event, Rest, Shop, Treasure, Boss }

    /// 분기 맵의 방 하나 — Next는 다음 층에서 이동 가능한 방 Id (StS식 DAG)
    public class MapNode
    {
        public int Id, Floor;
        public float X;             // 맵 화면 가로 슬롯 좌표
        public NodeType Type;
        public readonly List<int> Next = new();
    }

    public enum ClassId { Warrior, Rogue, Mage, Ranger, Assassin, Beastkin, Cleric, Paladin, Berserker, Bard }

    public class ClassDef
    {
        public ClassId Id; public string LocKey, Sheet; public int Hp, Power; public bool IsHealer;
        public Trait Trait;
    }

    /// 던전 런 한 판의 상태 — 시드가 파티·인카운터·덱 셔플을 결정한다 (모든 랜덤은 사전 공개).
    public class RunState
    {
        public int Seed;
        public int Cur = -1;                  // 현재 방 Id (-1 = 아직 입장 전 — 1층에서 고른다)
        public int Gold;
        public int TankHp;
        public List<MapNode> Map;             // 시드 생성 분기 맵
        public readonly HashSet<int> Visited = new();
        public List<ClassId> Party = new();   // 동료 (탱커 제외)
        public List<int> PartyHp = new();
        public List<CardType> Deck = new();
        public bool DpsShakenNext;            // 이벤트 (c) — 다음 전투에서 최강 공격수 위축 시작
        public int TauntGuard, CoverReduce, BraceBonus;
        public int TotalRedirected, TotalMitigated;
        public int BattlesWon;
        public int BattleIndex;               // 전투 순번 (스케일링 스테이지)

        public static int TankMax => Balance.I.tankHp;

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

        public RunState(int seed)
        {
            Seed = seed;
            TankHp = Balance.I.tankHp;
            var rng = new System.Random(seed);
            int n = Balance.I.partyMinCompanions
                  + rng.Next(Balance.I.partyMaxCompanions - Balance.I.partyMinCompanions + 1);
            var pool = new List<ClassId>((ClassId[])System.Enum.GetValues(typeof(ClassId)));
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                var pick = pool[rng.Next(pool.Count)];
                pool.Remove(pick);
                Party.Add(pick);
                PartyHp.Add(RunData.Class(pick).Hp);
            }
            Deck = Cards.StarterDeck();
            // 맵은 독립 서브시드 — 파티 생성 RNG 소비량이 바뀌어도 같은 시드의 맵은 유지된다
            Map = RunData.GenerateMap(new System.Random(seed * 613 + 101));
        }

        public void RestAll(float ratio)
        {
            TankHp = System.Math.Min(TankMax, TankHp + (int)(TankMax * ratio));
            for (int i = 0; i < Party.Count; i++)
            {
                if (PartyHp[i] <= 0) continue;
                int max = RunData.Class(Party[i]).Hp;
                PartyHp[i] = System.Math.Min(max, PartyHp[i] + (int)(max * ratio));
            }
        }
    }

    public class UnitDef
    {
        public string NameKey, Sheet;
        public int Hp, Power, AoePower, ChargeOffset, Thorns;
        public AiKind Ai;
        public bool Lifesteal;

        public UnitDef(string nameKey, string sheet, int hp, int power, AiKind ai, int chargeOffset = 0, int aoePower = 0,
                       int thorns = 0, bool lifesteal = false)
        { NameKey = nameKey; Sheet = sheet; Hp = hp; Power = power; Ai = ai; ChargeOffset = chargeOffset; AoePower = aoePower;
          Thorns = thorns; Lifesteal = lifesteal; }
    }

    public class EncounterDef
    {
        public string Title;   // 적 구성에서 생성한 전투 이름 ("고블린 무리", "오크의 습격" 등)
        public int Gold;
        public UnitDef[] Units;
    }

    public class ShopItem
    {
        public string Name, Desc;
        public int Price;
        public System.Action<RunState> Apply;
        public bool Bought;
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
                case ClassId.Warrior: return new ClassDef { Id = id, LocKey = "class.warrior", Sheet = "warrior", Hp = b.warriorHp, Power = b.warriorPower };
                case ClassId.Rogue: return new ClassDef { Id = id, LocKey = "class.rogue", Sheet = "dps", Hp = b.rogueHp, Power = b.roguePower };
                case ClassId.Mage: return new ClassDef { Id = id, LocKey = "class.mage", Sheet = "mage", Hp = b.mageHp, Power = b.magePower };
                case ClassId.Ranger: return new ClassDef { Id = id, LocKey = "class.ranger", Sheet = "ranger", Hp = b.rangerHp, Power = b.rangerPower };
                case ClassId.Assassin: return new ClassDef { Id = id, LocKey = "class.assassin", Sheet = "assassin", Hp = b.assassinHp, Power = b.assassinPower };
                case ClassId.Beastkin: return new ClassDef { Id = id, LocKey = "class.beastkin", Sheet = "beastkin", Hp = b.beastkinHp, Power = b.beastkinPower };
                case ClassId.Paladin: return new ClassDef { Id = id, LocKey = "class.paladin", Sheet = "paladin", Hp = b.paladinHp, Power = b.paladinPower, Trait = Trait.TankHealOnHit };
                case ClassId.Berserker: return new ClassDef { Id = id, LocKey = "class.berserker", Sheet = "berserker", Hp = b.berserkerHp, Power = b.berserkerPower, Trait = Trait.Frenzy };
                case ClassId.Bard: return new ClassDef { Id = id, LocKey = "class.bard", Sheet = "bard", Hp = b.bardHp, Power = b.bardPower, Trait = Trait.Cleanse };
                default: return new ClassDef { Id = id, LocKey = "class.cleric", Sheet = "healer", Hp = b.clericHp, Power = b.clericPower, IsHealer = true };
            }
        }

        public static string NodeTitle(NodeType t) => Loc.T("nodeT." + t);

        // 적 풀: (락키, 시트, hp, power, ai, cost, aoe)
        class EnemyPick
        {
            public string Key, Sheet; public int Hp, Power, Cost; public AiKind Ai;
            public int Thorns; public bool Lifesteal;
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
                new EnemyPick { Key = "unit.spider", Sheet = "spider", Hp = b.spiderHp, Power = b.spiderPower, Ai = AiKind.SpiderDouble, Cost = 3 },
                new EnemyPick { Key = "unit.brute", Sheet = "brute", Hp = b.bruteHp, Power = b.brutePower, Ai = AiKind.BruteCycle, Cost = 4 },
                new EnemyPick { Key = "unit.golem", Sheet = "golem", Hp = b.golemHp, Power = b.golemPower, Ai = AiKind.LowestBackliner, Cost = 3, Thorns = b.golemThorns },
                new EnemyPick { Key = "unit.bat", Sheet = "bat", Hp = b.batHp, Power = b.batPower, Ai = AiKind.LowestBackliner, Cost = 1, Lifesteal = true },
                new EnemyPick { Key = "unit.necro", Sheet = "necro", Hp = b.necroHp, Power = 0, Ai = AiKind.EnemyHealer, Cost = 3 },
            };
        }

        /// 적 구성에서 전투 이름 생성 — "고블린 무리"(같은 종 다수) / "오크의 습격"(최고 코스트 대표) / 정예 접두
        static string EncounterTitle(List<EnemyPick> picked, bool elite)
        {
            var lead = picked[0];
            bool allSame = true;
            foreach (var p in picked)
            {
                if (p.Cost > lead.Cost) lead = p;
                if (p.Key != picked[0].Key) allSame = false;
            }
            if (elite) return Loc.F("enc.eliteAmbush", Loc.T(lead.Key));
            if (allSame && picked.Count > 1) return Loc.F("enc.pack", Loc.T(picked[0].Key));
            return Loc.F("enc.ambush", Loc.T(lead.Key));
        }

        static int ScaledHp(int hp, int stage) =>
            (int)System.Math.Round(hp * (1f + Balance.I.scaleHpPct * stage));

        static int ScaledPower(int power, int stage) =>
            power <= 0 ? power : power + stage / Balance.I.scalePowerStages;

        /// 시드·스테이지 기반 인카운터 생성 (보스 방은 고정 구성 + 스케일링, 엘리트는 예산 보너스)
        public static EncounterDef GetEncounter(RunState run)
        {
            var b = Balance.I;
            // 스케일링은 층 기반 — 전투를 피해 달려도 깊이만큼 강해진다 (승전 수 기반은 회피 러시가 최적이 되는 구멍)
            int stage = run.CurNode.Floor;
            bool elite = run.CurNode.Type == NodeType.Elite;
            var rng = new System.Random(run.Seed * 977 + run.Cur * 131);

            if (run.CurNode.Type == NodeType.Boss)
            {
                return new EncounterDef
                {
                    Title = Loc.T("enc.warlord"),
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
                                       thorns: p.Thorns, lifesteal: p.Lifesteal);
            }
            return new EncounterDef
            {
                Title = EncounterTitle(picked, elite),
                Gold = elite ? b.eliteGold : b.battleGold,
                Units = units,
            };
        }

        public static List<ShopItem> MakeShop()
        {
            var b = Balance.I;
            return new List<ShopItem>
            {
                new ShopItem { Name = Loc.T("item.guard"), Desc = Loc.F("item.guard.desc", b.guardValue), Price = b.guardPrice, Apply = r => r.TauntGuard += b.guardValue },
                new ShopItem { Name = Loc.T("item.cover"), Desc = Loc.F("item.cover.desc", b.coverValue), Price = b.coverPrice, Apply = r => r.CoverReduce += b.coverValue },
                new ShopItem { Name = Loc.T("item.brace"), Desc = Loc.F("item.brace.desc", b.braceValue), Price = b.bracePrice, Apply = r => r.BraceBonus += b.braceValue },
                new ShopItem { Name = Loc.T("item.potion"), Desc = Loc.F("item.potion.desc", b.potionHeal), Price = b.potionPrice, Apply = r => r.TankHp = System.Math.Min(RunState.TankMax, r.TankHp + b.potionHeal) },
            };
        }
    }
}
