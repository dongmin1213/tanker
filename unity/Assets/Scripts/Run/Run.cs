using System.Collections.Generic;

namespace Tanker
{
    public enum NodeType { Battle, Event, Choice, Rest, Boss }

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
        public int Node;
        public int Gold;
        public int TankHp;
        public List<ClassId> Party = new();   // 동료 (탱커 제외)
        public List<int> PartyHp = new();
        public List<CardType> Deck = new();
        public bool DpsShakenNext;            // 이벤트 (c) — 다음 전투에서 최강 공격수 위축 시작
        public int TauntGuard, CoverReduce, BraceBonus;
        public int TotalRedirected, TotalMitigated;
        public int BattlesWon;
        public int BattleIndex;               // 전투 순번 (스케일링 스테이지)

        public static int TankMax => Balance.I.tankHp;

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
        public string TitleKey;
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

    /// 런 콘텐츠 — 클래스/적 풀, 시드 기반 인카운터 생성, 스테이지 스케일링. 수치는 Balance, 문자열은 Loc.
    public static class RunData
    {
        public static readonly NodeType[] Nodes =
        {
            NodeType.Battle, NodeType.Event, NodeType.Battle, NodeType.Choice,
            NodeType.Battle, NodeType.Rest, NodeType.Boss,
        };

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

        public static string NodeTitle(int i)
        {
            switch (i)
            {
                case 0: case 2: return Loc.T("node.battleRandom");
                case 1: return Loc.T("node.event");
                case 3: return Loc.T("node.choice");
                case 4: return Loc.T("node.elite");
                case 5: return Loc.T("node.rest");
                default: return Loc.F("node.boss", Loc.T("enc.warlord"));
            }
        }

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

        static int ScaledHp(int hp, int stage) =>
            (int)System.Math.Round(hp * (1f + Balance.I.scaleHpPct * stage));

        static int ScaledPower(int power, int stage) =>
            power <= 0 ? power : power + stage / Balance.I.scalePowerStages;

        /// 시드·스테이지 기반 인카운터 생성 (보스 노드는 고정 구성 + 스케일링)
        public static EncounterDef GetEncounter(RunState run)
        {
            var b = Balance.I;
            int stage = run.BattleIndex;
            var rng = new System.Random(run.Seed * 977 + run.Node * 131);

            if (Nodes[run.Node] == NodeType.Boss)
            {
                return new EncounterDef
                {
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
            int budget = b.encounterBudgetBase + stage * b.encounterBudgetPerStage + (attackers - 2);

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
                TitleKey = Nodes[run.Node] == NodeType.Battle && run.Node == 4 ? "enc.elite" : "enc.random",
                Gold = run.Node == 4 ? b.eliteGold : b.battleGold,
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
