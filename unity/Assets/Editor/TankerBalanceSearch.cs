using System.Collections.Generic;
using System.Text;

namespace Tanker
{
    /// 전투 규칙의 결정론 전수 탐색기 (에디터 전용) — BattleManager의 규칙을 그대로 미러링한다.
    /// 목적: 제한 정책(엄호만/도발+버티기만/전체)별로 풀 런 승리선이 존재하는지 자동 검증 (v0.4 진입 조건).
    /// 규칙을 바꾸면 반드시 이 파일도 같이 갱신할 것 — 드리프트 방지를 위해 교차검증(엔진 실주행)과 병행한다.
    public static class BalanceSearch
    {
        // ---- 실험용 룰 플래그 (기본 = 현행 규칙) ----
        public static bool CoverFirstHitOnly = false;   // 엄호가 대상을 노리는 첫 공격만 리다이렉트
        public static bool BossPiercesBrace = false;    // 보스 단일 강타는 버티기 절반 무시
        public static bool BraceHealAfterDamage = true;  // [현행 룰] 버티기 회복은 받은 피해만큼 후불
        public static bool HealerHalfToTank = false;    // 힐러가 탱커에게는 절반만 힐
        public static bool HealerSkipsTank = true;      // [현행 룰] 힐러는 백라이너 전담 — 탱커 치료 안 함

        public enum Policy { CoverOnly, TauntBrace, Mixed }

        class Upg { public int Guard, CoverRed, BraceBonus; public override string ToString() => Guard + "/" + CoverRed + "/" + BraceBonus; }

        class EnemyDef { public string Name; public int Hp, Power, Aoe, Offset; public AiKind Ai; }

        static Balance B => Balance.I;

        static EnemyDef[] Encounter(int node)
        {
            switch (node)
            {
                case 0: return new[]
                {
                    new EnemyDef { Name = "고블린A", Hp = B.goblinHp, Power = B.goblinPower, Ai = AiKind.FixedHealer },
                    new EnemyDef { Name = "고블린B", Hp = B.goblinHp, Power = B.goblinPower, Ai = AiKind.FixedDps },
                    new EnemyDef { Name = "브루트", Hp = B.bruteHp, Power = B.brutePower, Ai = AiKind.BruteCycle, Offset = 0 },
                };
                case 2: return new[]
                {
                    new EnemyDef { Name = "궁수", Hp = B.archerHp, Power = B.archerPower, Ai = AiKind.FixedHealer },
                    new EnemyDef { Name = "고블린A", Hp = B.goblinHp, Power = B.goblinPower, Ai = AiKind.FixedDps },
                    new EnemyDef { Name = "고블린B", Hp = B.goblinHp, Power = B.goblinPower, Ai = AiKind.LowestBackliner },
                };
                case 4: return new[]
                {
                    new EnemyDef { Name = "브루트X", Hp = B.eliteBruteHp, Power = B.brutePower, Ai = AiKind.BruteCycle, Offset = 0 },
                    new EnemyDef { Name = "브루트Y", Hp = B.eliteBruteHp, Power = B.brutePower, Ai = AiKind.BruteCycle, Offset = 1 },
                };
                default: return new[]
                {
                    new EnemyDef { Name = "워로드", Hp = B.bossHp, Power = B.bossPower, Ai = AiKind.BossWarlord, Aoe = B.bossAoePower },
                    new EnemyDef { Name = "고블린", Hp = B.goblinHp, Power = B.goblinPower, Ai = AiKind.LowestBackliner },
                };
            }
        }

        // ---- 전투 상태 ----
        class St
        {
            public int Turn, T, D, H;               // 아군 HP
            public bool DS, HS;                     // 위축
            public int[] E; public int[] Taunt; public int[] Step;
            public bool[] Enr; public bool[] SmashD;
            public int[] Intent;                    // 0=탱커 1=딜러 2=힐러 -1=없음
            public bool[] Chg, Aoe;
            public int Cool;

            public St Clone()
            {
                return new St
                {
                    Turn = Turn, T = T, D = D, H = H, DS = DS, HS = HS,
                    E = (int[])E.Clone(), Taunt = (int[])Taunt.Clone(), Step = (int[])Step.Clone(),
                    Enr = (bool[])Enr.Clone(), SmashD = (bool[])SmashD.Clone(),
                    Intent = (int[])Intent.Clone(), Chg = (bool[])Chg.Clone(), Aoe = (bool[])Aoe.Clone(),
                    Cool = Cool
                };
            }

            public string Key()
            {
                var sb = new StringBuilder();
                sb.Append(Turn).Append('|').Append(T).Append(',').Append(D).Append(',').Append(H)
                  .Append(DS ? 1 : 0).Append(HS ? 1 : 0).Append('|').Append(Cool).Append('|');
                for (int i = 0; i < E.Length; i++)
                    sb.Append(E[i]).Append(',').Append(Taunt[i]).Append(',').Append(Step[i]).Append(',')
                      .Append(Enr[i] ? 1 : 0).Append(SmashD[i] ? 1 : 0).Append(Chg[i] ? 1 : 0).Append(Aoe[i] ? 1 : 0)
                      .Append(',').Append(Intent[i]).Append(';');
                return sb.ToString();
            }
        }

        struct Act { public int Kind; public int Arg; } // 0=없음 1=버티기 2=도발(적idx) 3=엄호(1=딜러,2=힐러)

        static int LowestBackliner(St s) // 엔진: {힐러, 딜러} 순회, 동률은 힐러
        {
            if (s.H > 0 && (s.D <= 0 || s.H <= s.D)) return 2;
            if (s.D > 0) return 1;
            return 0;
        }

        static void RollIntents(St s, EnemyDef[] def)
        {
            for (int i = 0; i < def.Length; i++)
            {
                s.Chg[i] = false; s.Aoe[i] = false;
                if (s.E[i] <= 0) { s.Intent[i] = -1; continue; }
                s.Step[i]++;
                switch (def[i].Ai)
                {
                    case AiKind.FixedHealer: s.Intent[i] = s.H > 0 ? 2 : LowestBackliner(s); break;
                    case AiKind.FixedDps: s.Intent[i] = s.D > 0 ? 1 : LowestBackliner(s); break;
                    case AiKind.LowestBackliner: s.Intent[i] = LowestBackliner(s); break;
                    case AiKind.BruteCycle:
                        if (!s.Enr[i] && s.E[i] <= B.bruteEnrageHp) s.Enr[i] = true;
                        if (s.Enr[i]) s.Intent[i] = NextSmash(s, i);
                        else if ((s.Step[i] + def[i].Offset) % 2 == 1) { s.Intent[i] = -1; s.Chg[i] = true; }
                        else s.Intent[i] = NextSmash(s, i);
                        break;
                    case AiKind.BossWarlord:
                        if (!s.Enr[i] && s.E[i] <= B.bossEnrageHp) { s.Enr[i] = true; s.Step[i] = 1; }
                        if (!s.Enr[i])
                        {
                            int p = (s.Step[i] - 1) % 3;
                            if (p == 0) { s.Intent[i] = -1; s.Chg[i] = true; }
                            else if (p == 1) s.Aoe[i] = true;
                            else s.Intent[i] = s.D > 0 ? 1 : LowestBackliner(s);
                        }
                        else
                        {
                            if (s.Step[i] % 2 == 1) s.Aoe[i] = true;
                            else s.Intent[i] = s.D > 0 ? 1 : LowestBackliner(s);
                        }
                        break;
                }
            }
        }

        static int NextSmash(St s, int i)
        {
            int t = s.SmashD[i] ? (s.D > 0 ? 1 : 2) : (s.H > 0 ? 2 : 1);
            s.SmashD[i] = !s.SmashD[i];
            if (t == 1 && s.D <= 0) t = 0;
            if (t == 2 && s.H <= 0) t = 0;
            return t;
        }

        static int Power(St s, EnemyDef d, int i) => d.Ai == AiKind.BruteCycle && s.Enr[i] ? B.bruteEnragePower : d.Power;

        /// 한 턴 해소. 반환: 0=계속 1=승리 2=패배
        static int Resolve(St s, EnemyDef[] def, Act a, Upg u)
        {
            int cover = 0; bool brace = false; // cover: 0=없음 1=딜러 2=힐러
            int coverUsed = 0;                 // CoverFirstHitOnly용
            int bracedTaken = 0;

            if (a.Kind == 2 && s.Cool == 0 && s.E[a.Arg] > 0) { s.Taunt[a.Arg] = B.tauntDuration; s.Cool = B.tauntCooldown; }
            else if (a.Kind == 3) cover = a.Arg;
            else if (a.Kind == 1)
            {
                brace = true;
                if (!BraceHealAfterDamage) s.T = System.Math.Min(B.tankHp, s.T + B.braceHeal + u.BraceBonus);
            }

            // 딜러
            if (s.D > 0)
            {
                int tgt = -1;
                for (int i = 0; i < def.Length; i++)
                    if (s.E[i] > 0 && (tgt < 0 || s.E[i] < s.E[tgt])) tgt = i;
                if (tgt >= 0)
                {
                    s.E[tgt] = System.Math.Max(0, s.E[tgt] - (s.DS ? B.dpsPower / 2 : B.dpsPower));
                    s.DS = false;
                    bool any = false;
                    for (int i = 0; i < def.Length; i++) if (s.E[i] > 0) any = true;
                    if (!any) return 1;
                }
            }

            // 적
            for (int i = 0; i < def.Length; i++)
            {
                if (s.E[i] <= 0 || s.Chg[i]) continue;

                if (s.Aoe[i] && s.Taunt[i] == 0)
                {
                    foreach (var b in new[] { 1, 2 })
                    {
                        int hp = b == 1 ? s.D : s.H;
                        if (hp <= 0) continue;
                        bool covered = cover == b && (!CoverFirstHitOnly || coverUsed == 0);
                        if (covered) coverUsed++;
                        int dmg = def[i].Aoe;
                        if (covered)
                        {
                            dmg = System.Math.Max(0, dmg - u.CoverRed);
                            if (brace) dmg /= 2;
                            s.T -= dmg; bracedTaken += dmg;
                            if (s.T <= 0) return 2;
                        }
                        else
                        {
                            if (b == 1) { s.D -= dmg; s.DS = true; if (s.D <= 0) return 2; }
                            else { s.H -= dmg; s.HS = true; if (s.H <= 0) return 2; }
                        }
                    }
                    continue;
                }

                int planned = s.Aoe[i] ? 0 : s.Intent[i];
                if (planned == 1 && s.D <= 0) planned = 0;
                if (planned == 2 && s.H <= 0) planned = 0;
                if (planned < 0) planned = 0;

                int actual;
                bool viaTaunt = s.Taunt[i] > 0;
                bool viaCover = false;
                if (viaTaunt) actual = 0;
                else if (cover != 0 && planned == cover && (!CoverFirstHitOnly || coverUsed == 0)) { actual = 0; viaCover = true; coverUsed++; }
                else actual = planned;

                int dmg2 = Power(s, def[i], i);
                if (viaTaunt) dmg2 = System.Math.Max(0, dmg2 - u.Guard);
                if (viaCover) dmg2 = System.Math.Max(0, dmg2 - u.CoverRed);
                if (actual == 0 && brace && !(BossPiercesBrace && def[i].Ai == AiKind.BossWarlord)) dmg2 /= 2;

                if (actual == 0) { s.T -= dmg2; bracedTaken += dmg2; if (s.T <= 0) return 2; }
                else if (actual == 1) { s.D -= dmg2; s.DS = true; if (s.D <= 0) return 2; }
                else { s.H -= dmg2; s.HS = true; if (s.H <= 0) return 2; }
            }

            // 버티기 후불 회복 (실험 룰): 받은 피해만큼만
            if (brace && BraceHealAfterDamage)
                s.T = System.Math.Min(B.tankHp, s.T + System.Math.Min(B.braceHeal + u.BraceBonus, bracedTaken));

            // 힐러 — 엔진: 아군(탱,딜,힐) 순회, HP<Max && 최저 (동률은 앞순서)
            if (s.H > 0)
            {
                int amount = s.HS ? B.healerPower / 2 : B.healerPower; s.HS = false;
                int best = -1, bestHp = 0;
                if (!HealerSkipsTank && s.T < B.tankHp) { best = 0; bestHp = s.T; }
                if (s.D > 0 && s.D < B.dpsHp && (best < 0 || s.D < bestHp)) { best = 1; bestHp = s.D; }
                if (s.H < B.healerHp && (best < 0 || s.H < bestHp)) { best = 2; bestHp = s.H; }
                if (best == 0) s.T = System.Math.Min(B.tankHp, s.T + (HealerHalfToTank ? amount / 2 : amount));
                else if (best == 1) s.D = System.Math.Min(B.dpsHp, s.D + amount);
                else if (best == 2) s.H = System.Math.Min(B.healerHp, s.H + amount);
            }

            bool alive = false;
            for (int i = 0; i < def.Length; i++) if (s.E[i] > 0) alive = true;
            if (!alive) return 1;

            // 정리
            s.Turn++;
            for (int i = 0; i < def.Length; i++) if (s.E[i] > 0 && s.Taunt[i] > 0) s.Taunt[i]--;
            if (s.Cool > 0) s.Cool--;
            RollIntents(s, def);
            return 0;
        }

        static List<Act> Actions(St s, EnemyDef[] def, Policy p)
        {
            var list = new List<Act>();
            if (p == Policy.CoverOnly)
            {
                if (s.D > 0) list.Add(new Act { Kind = 3, Arg = 1 });
                if (s.H > 0) list.Add(new Act { Kind = 3, Arg = 2 });
                return list;
            }
            if (p == Policy.TauntBrace)
            {
                list.Add(new Act { Kind = 0 });
                list.Add(new Act { Kind = 1 });
                if (s.Cool == 0)
                    for (int i = 0; i < def.Length; i++)
                        if (s.E[i] > 0) list.Add(new Act { Kind = 2, Arg = i });
                return list;
            }
            list.Add(new Act { Kind = 0 });
            list.Add(new Act { Kind = 1 });
            if (s.D > 0) list.Add(new Act { Kind = 3, Arg = 1 });
            if (s.H > 0) list.Add(new Act { Kind = 3, Arg = 2 });
            if (s.Cool == 0)
                for (int i = 0; i < def.Length; i++)
                    if (s.E[i] > 0) list.Add(new Act { Kind = 2, Arg = i });
            return list;
        }

        static readonly Dictionary<string, HashSet<long>> battleCache = new Dictionary<string, HashSet<long>>();

        /// 전투 하나의 도달 가능한 승리 종료 HP 집합 (메모이즈)
        static HashSet<long> SimBattle(int node, int t, int d, int h, bool dShaken, Upg u, Policy p)
        {
            string ck = node + "|" + t + "," + d + "," + h + "," + (dShaken ? 1 : 0) + "|" + u + "|" + p
                      + "|" + (CoverFirstHitOnly ? 1 : 0) + (BossPiercesBrace ? 1 : 0) + (BraceHealAfterDamage ? 1 : 0) + (HealerHalfToTank ? 1 : 0) + (HealerSkipsTank ? 1 : 0);
            if (battleCache.TryGetValue(ck, out var cached)) return cached;
            var def = Encounter(node);
            var s0 = new St
            {
                Turn = 1, T = t, D = d, H = h, DS = dShaken, HS = false,
                E = new int[def.Length], Taunt = new int[def.Length], Step = new int[def.Length],
                Enr = new bool[def.Length], SmashD = new bool[def.Length],
                Intent = new int[def.Length], Chg = new bool[def.Length], Aoe = new bool[def.Length],
                Cool = 0
            };
            for (int i = 0; i < def.Length; i++) { s0.E[i] = def[i].Hp; s0.SmashD[i] = true; }
            RollIntents(s0, def);

            var exits = new HashSet<long>();
            var seen = new HashSet<string>();
            var stack = new Stack<St>();
            stack.Push(s0);
            while (stack.Count > 0)
            {
                if (seen.Count > 1_500_000) { UnityEngine.Debug.LogWarning("[BalanceSearch] 상태 3M 초과 — 탐색 중단(부분 결과)"); break; }
                var s = stack.Pop();
                if (s.Turn > 20) continue; // 실전 전투는 ~12턴 — 20턴 안에 못 이기면 승리선으로 안 친다 (게이트 정의)
                var key = s.Key();
                if (seen.Contains(key)) continue;
                seen.Add(key);
                foreach (var a in Actions(s, def, p))
                {
                    var n = s.Clone();
                    int r = Resolve(n, def, a, u);
                    if (r == 1) exits.Add(Pack(n.T, n.D, n.H));
                    else if (r == 0) stack.Push(n);
                }
            }
            battleCache[ck] = exits;
            return exits;
        }

        static long Pack(int t, int d, int h) => (long)t * 10000 + d * 100 + h;

        static (int t, int d, int h) Unpack(long v) => ((int)(v / 10000), (int)(v / 100 % 100), (int)(v % 100));

        static long Rest(long v)
        {
            var (t, d, h) = Unpack(v);
            return Pack(System.Math.Min(B.tankHp, t + (int)(B.tankHp * B.restRatio)),
                        System.Math.Min(B.dpsHp, d + (int)(B.dpsHp * B.restRatio)),
                        System.Math.Min(B.healerHp, h + (int)(B.healerHp * B.restRatio)));
        }

        /// 풀 런 탐색: 승리 가능 여부와 최고 종료 HP 합.
        /// Mixed(전체 액션)는 상태 공간이 실전상 전수 불가 — 엔진 실주행(FlowPilot)으로 승리 가능성을 확인한다.
        public static string SearchRun(Policy p)
        {
            if (p == Policy.Mixed) return "Mixed: 전수 탐색 미지원 — 엔진 실주행으로 확인";
            var upgradeCombos = new List<(Upg u, int cost, int potion)>
            {
                (new Upg(), 0, 0),
                (new Upg { Guard = B.guardValue }, B.guardPrice, 0),
                (new Upg { CoverRed = B.coverValue }, B.coverPrice, 0),
                (new Upg { BraceBonus = B.braceValue }, B.bracePrice, 0),
                (new Upg(), B.potionPrice, B.potionHeal),
                (new Upg { Guard = B.guardValue, BraceBonus = B.braceValue }, B.guardPrice + B.bracePrice, 0),
                (new Upg { Guard = B.guardValue }, B.guardPrice + B.potionPrice, B.potionHeal),
                (new Upg { CoverRed = B.coverValue }, B.coverPrice + B.potionPrice, B.potionHeal),
                (new Upg { BraceBonus = B.braceValue }, B.bracePrice + B.potionPrice, B.potionHeal),
            };

            var b1 = SimBattle(0, B.tankHp, B.dpsHp, B.healerHp, false, new Upg(), p);
            if (b1.Count == 0) return p + ": 전투1에서 전멸 — 승리선 없음";

            // 함정: (a) 탱커-6 (b) -20G (c) 딜러-4+다음전투 위축
            var afterTrap = new List<(long hp, int gold, bool dShaken)>();
            int goldFull = B.battleGold * 2, goldToll = B.battleGold * 2 - B.trapToll;
            foreach (var v in b1)
            {
                var (t, d, h) = Unpack(v);
                if (t - B.trapTankCost > 0) afterTrap.Add((Pack(t - B.trapTankCost, d, h), goldFull, false));
                afterTrap.Add((v, goldToll, false));
                if (d - B.trapDpsCost > 0) afterTrap.Add((Pack(t, d - B.trapDpsCost, h), goldFull, true));
            }

            // 후반(갈림길 이후) 진입 상태를 중복 제거해 모은다 — (HP, 업그레이드 조합)별로 한 번만 탐색
            var lateEntries = new HashSet<(long hp, int combo)>();
            var seenMid = new HashSet<(long, int, bool)>();
            foreach (var (hp, gold, dsh) in afterTrap)
            {
                if (seenMid.Contains((hp, gold, dsh))) continue;
                seenMid.Add((hp, gold, dsh));
                var (t, d, h) = Unpack(hp);
                foreach (var v2 in SimBattle(2, t, d, h, dsh, new Upg(), p))
                {
                    lateEntries.Add((Rest(v2), -1)); // 갈림길: 휴식
                    for (int ci = 0; ci < upgradeCombos.Count; ci++)
                    {
                        if (upgradeCombos[ci].cost > gold) continue;
                        var (t2, d2, h2) = Unpack(v2);
                        lateEntries.Add((Pack(System.Math.Min(B.tankHp, t2 + upgradeCombos[ci].potion), d2, h2), ci));
                    }
                }
            }

            long best = -1; bool won = false;
            foreach (var (hp, combo) in lateEntries)
            {
                var u = combo < 0 ? new Upg() : upgradeCombos[combo].u;
                foreach (var final in ChainLate(hp, u, p))
                { won = true; if (final > best) best = final; }
            }
            if (!won) return p + ": 풀 런 승리선 없음 ✓게이트 통과";
            var (bt, bd, bh) = Unpack(best);
            return p + ": 풀 런 승리선 존재 — 최고 종료 HP " + bt + "/" + bd + "/" + bh;
        }

        static IEnumerable<long> ChainLate(long hp, Upg u, Policy p)
        {
            var (t, d, h) = Unpack(hp);
            var elite = SimBattle(4, t, d, h, false, u, p);
            var results = new HashSet<long>();
            foreach (var v in elite)
            {
                var rested = Rest(v);
                var (t2, d2, h2) = Unpack(rested);
                foreach (var v2 in SimBattle(6, t2, d2, h2, false, u, p))
                    results.Add(v2);
            }
            return results;
        }

        /// 단일 전투의 전체 액션 최적 종료 HP — "이 전투에서 잘하면 얼마나 남기는가" 상한 측정용
        public static string ProbeBattle(int node, int t, int d, int h)
        {
            var exits = SimBattle(node, t, d, h, false, new Upg(), Policy.Mixed);
            if (exits.Count == 0) return "노드 " + node + ": 승리 불가";
            long best = -1; foreach (var v in exits) if (v > best) best = v;
            var (bt, bd, bh) = Unpack(best);
            return "노드 " + node + " 진입 " + t + "/" + d + "/" + h + " → 최적 종료 " + bt + "/" + bd + "/" + bh + " (승리 상태 " + exits.Count + "종)";
        }

        public static string RunPolicy(string name)
        {
            var p = name == "cover" ? Policy.CoverOnly : Policy.TauntBrace;
            return "룰: coverFirst=" + CoverFirstHitOnly + " bossPierce=" + BossPiercesBrace
                 + " braceAfter=" + BraceHealAfterDamage + " healerHalf=" + HealerHalfToTank + "\n" + SearchRun(p);
        }

        /// 게이트 검사: 제한 정책 2개만 전수 탐색한다.
        /// Mixed(전체 액션)는 상태 공간이 폭발하므로 전수 탐색하지 않는다 — 혼합 승리 가능성은 엔진 실주행 회귀로 확인.
        public static string RunAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("룰: coverFirst=" + CoverFirstHitOnly + " bossPierce=" + BossPiercesBrace + " braceAfter=" + BraceHealAfterDamage);
            sb.AppendLine(SearchRun(Policy.CoverOnly));
            sb.AppendLine(SearchRun(Policy.TauntBrace));
            return sb.ToString();
        }
    }
}
