using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tanker
{
    /// v1.0 밸런스 시뮬레이터 — **실제 BattleManager를 그대로 돌린다**.
    /// 별도 규칙 사본을 두지 않으므로 검증기와 실게임이 어긋날 수 없다 (v0.3 탐색기의 드리프트 문제 해결).
    /// 배치모드: -executeMethod Tanker.BalanceSim.BatchRun  (환경변수 TANKER_SIM_RUNS로 횟수 조절)
    public static class BalanceSim
    {
        /// 진형별 독립 측정용 — -1이면 무작위
        public static int ForcedFormation = -1;

        // ---- 한 런의 결과 ----
        class RunResult
        {
            public int Seed;
            public bool Won;            // 1막 이상 클리어(귀환) 여부
            public int ActReached;      // 도달한 막 (0/1/2)
            public int FloorReached;
            public int Battles;
            public int Turns;
            public int TankHpEnd;
            public int Gold;
            public int NoLossBattles;   // 전투 종료 HP ≥ 진입 HP (무손실 엔진 탐지)
            public int LateBattles, LateNoLoss; // 5층 이상 전투 — 후반 무손실이 진짜 엔진 신호
            public int WipeTurns;       // 패배까지 걸린 턴
            public int CardsPlayed;     // 시뮬 정책이 실제로 낸 카드 수 (0이면 정책 결함)
            public string DeathCause = "-";
            public List<ClassId> Party = new();
            public int Formation;
        }

        /// 카드 선택 휴리스틱 — "합리적인 플레이어" 대역.
        /// 위협을 읽고(IncomingPreview) 가장 급한 곳을 막는다. 특정 빌드에 유리하지 않게 일반적으로만.
        static void ChooseCard(BattleManager bm)
        {
            if (bm.Phase != Phase.Player || bm.Hand.Count == 0) return;

            // 이번 턴 가장 큰 피해를 받을 아군 / 탱커 예상 피해
            Unit worst = null; int worstDmg = 0;
            foreach (var a in bm.Allies)
            {
                if (!a.Alive || a.IsTank) continue;
                int inc = bm.IncomingPreview(a);
                if (inc > worstDmg) { worstDmg = inc; worst = a; }
            }
            int tankInc = bm.IncomingPreview(bm.Tank);
            bool lethal = worst != null && worstDmg >= worst.Hp;      // 동료가 죽는다
            bool tankDanger = tankInc * 3 >= bm.Tank.Hp;              // 탱커가 크게 맞는다

            // 우선순위: 동료 즉사 방지 > 탱커 위기 > 화력/자원
            var order = new List<CardType>();
            if (lethal) order.AddRange(new[] { CardType.Bless, CardType.Shield, CardType.Cover, CardType.Taunt, CardType.WarCry,
                                              CardType.Rearguard, CardType.Phalanx, CardType.Barricade, CardType.Shove, CardType.Feint });
            if (tankDanger) order.AddRange(new[] { CardType.LastStand, CardType.Fortress, CardType.Brace, CardType.IronWill, CardType.Aegis, CardType.Respite, CardType.Undying });
            order.AddRange(new[]
            {
                CardType.Taunt, CardType.WarCry, CardType.Shove, CardType.Feint, CardType.Silence,
                CardType.Warsong, CardType.Inspire, CardType.Vanguard, CardType.Counter, CardType.ThornStance,
                CardType.GuardianMark, CardType.Oath, CardType.Vow, CardType.Study, CardType.Grudge,
                CardType.FirstAid, CardType.Purge, CardType.Antidote, CardType.Sacrifice, CardType.Devotion,
                CardType.Scout, CardType.Discipline, CardType.Awaken, CardType.Regroup, CardType.Reposition, CardType.Barter,
            });

            for (int oi = 0; oi < order.Count; oi++)
            {
                for (int i = 0; i < bm.Hand.Count; i++)
                {
                    if (bm.Hand[i].Type != order[oi] || !bm.CardPlayable(i)) continue;
                    bm.PressCard(i);
                    var need = Cards.TargetOf(bm.Hand[i].Type);
                    if (need == CardTarget.None) return;
                    var t = need == CardTarget.Enemy ? PickEnemy(bm) : PickAlly(bm, worst);
                    if (t == null) { bm.PressCard(i); continue; }   // 대상 없음 — 선택 취소하고 다음 후보
                    bm.ClickUnit(t);
                    if (bm.PlannedCard >= 0) return;
                }
            }
        }

        static Unit PickEnemy(BattleManager bm)
        {
            Unit best = null;
            foreach (var e in bm.Enemies)
                if (e.Alive && !e.Hidden && (best == null || e.Power > best.Power)) best = e;
            return best;
        }

        static Unit PickAlly(BattleManager bm, Unit worst)
        {
            if (worst != null && worst.Alive) return worst;
            Unit low = null;
            foreach (var a in bm.Allies)
                if (a.Alive && !a.IsTank && (low == null || a.Hp < low.Hp)) low = a;
            return low;
        }

        /// 전투 하나를 끝까지 — 반환: (승리, 턴 수)
        static (bool won, int turns) SimBattle(RunState run, EncounterDef def, RunResult res, bool late = false)
        {
            var go = new GameObject("SimBattle");
            var bm = go.AddComponent<BattleManager>();
            bm.Init(def, run);   // 종료는 Phase로 판정 (Finished는 결과 화면 버튼에서만 발생)

            int entryTank = bm.Tank.Hp;
            int turns = 0;
            while (bm.Phase != Phase.Won && bm.Phase != Phase.Lost && turns < 60)
            {
                turns++;
                ChooseCard(bm);
                if (bm.PlannedCard >= 0) res.CardsPlayed++;
                bm.EndTurnHeadless();
            }
            if (bm.Phase == Phase.Lost)
            {
                foreach (var a in bm.Allies)
                    if (!a.Alive) { res.DeathCause = a.Name + "(" + (a.IsTank ? "탱커" : "동료") + ") @" + turns + "턴, 적 " + def.Units.Length; break; }
            }
            bool won = bm.Phase == Phase.Won;
            if (won)
            {
                if (bm.Tank.Hp >= entryTank) res.NoLossBattles++;   // 무손실 엔진 신호
                if (late) { res.LateBattles++; if (bm.Tank.Hp >= entryTank) res.LateNoLoss++; }
                else res.LateBattles += 0;
                run.TankHp = bm.Tank.Hp;
                for (int i = 0; i < run.Party.Count && i + 1 < bm.Allies.Count; i++)
                    run.PartyHp[i] = bm.Allies[i + 1].Hp;
                run.Gold += bm.RewardGold + bm.BonusGold;
            }
            Object.DestroyImmediate(go);
            return (won, turns);
        }

        /// 런 하나 — 맵을 따라가며 전투/휴식/보상을 단순 정책으로 처리
        static RunResult SimRun(int seed)
        {
            var res = new RunResult { Seed = seed };
            var run = new RunState(seed);
            var rng = new System.Random(seed * 31 + 7);
            run.Formation = ForcedFormation >= 0 ? ForcedFormation : rng.Next(4);
            run.EnsureRows();
            res.Party.AddRange(run.Party);
            res.Formation = run.Formation;

            for (int guard = 0; guard < 80; guard++)
            {
                // 다음 방 선택 — 다치면 휴식, 아니면 보상 큰 방
                var reach = run.Reachable();
                if (reach.Count == 0) break;
                int pick = reach[0]; int bestScore = int.MinValue;
                bool hurt = run.TankHp * 2 <= run.TankMaxHp;
                foreach (var id in reach)
                {
                    var t = run.Map[id].Type;
                    int score = t switch
                    {
                        NodeType.Rest => hurt ? 100 : 20,
                        NodeType.Treasure => 60,
                        NodeType.Shop => 40,
                        NodeType.Elite => hurt ? 5 : 50,
                        NodeType.Event => 30,
                        NodeType.Battle => 35,
                        _ => 45,
                    };
                    if (score > bestScore) { bestScore = score; pick = id; }
                }
                run.Cur = pick;
                run.Visited.Add(pick);
                var node = run.Map[pick];

                if (node.Type == NodeType.Battle || node.Type == NodeType.Elite || node.Type == NodeType.Boss)
                {
                    var def = RunData.GetEncounter(run);
                    var (w, t2) = SimBattle(run, def, res, node.Floor >= 4 || run.Act > 0);
                    res.Battles++; res.Turns += t2;
                    if (!w)
                    {
                        res.WipeTurns = t2;
                        res.FloorReached = Mathf.Max(res.FloorReached, node.Floor + 1);
                        res.ActReached = Mathf.Max(res.ActReached, run.Act);
                        res.TankHpEnd = run.TankHp;
                        res.Gold = run.Gold;
                        return res;
                    }
                    // 보상: 카드 1장 추가 (덱 성장) — 무작위 대표 플레이
                    run.Deck.Add(new Card(Cards.Pool[rng.Next(Cards.Pool.Length)]));
                    if (node.Type == NodeType.Elite)
                    {
                        foreach (RelicId r in System.Enum.GetValues(typeof(RelicId)))
                            if (!run.Has(r)) { run.AddRelic(r); break; }
                    }
                    if (node.Type == NodeType.Boss)
                    {
                        res.Won = true;
                        if (run.Act >= 2) break;                 // 3막 클리어 — 진엔딩
                        run.Act++;                               // 계속 내려간다
                        run.RestAll(Balance.I.descendHeal, camp: false);
                        run.Map = RunData.GenerateMap(new System.Random(run.Seed * 777 + 13 * run.Act));
                        run.Cur = -1; run.Visited.Clear();
                        continue;
                    }
                }
                else if (node.Type == NodeType.Rest) run.RestAll(Balance.I.restRatio);
                else if (node.Type == NodeType.Treasure) run.Gold += Balance.I.treasureGold;
                else if (node.Type == NodeType.Event) run.TankHp = Mathf.Max(1, run.TankHp - Balance.I.trapTankCost / 2);

                res.FloorReached = Mathf.Max(res.FloorReached, node.Floor + 1);
                res.ActReached = Mathf.Max(res.ActReached, run.Act);
            }
            res.TankHpEnd = run.TankHp;
            res.Gold = run.Gold;
            return res;
        }

        /// N회 배치 실행 후 리포트
        public static string Run(int runs)
        {
            AudioKit.Silent = true;
            var results = new List<RunResult>();
            for (int i = 0; i < runs; i++) results.Add(SimRun(1000 + i * 977));

            int wins = 0, act2 = 0, act3 = 0, noLoss = 0, battles = 0, turns = 0, lateB = 0, lateNo = 0;
            var floorSum = 0;
            foreach (var r in results)
            {
                if (r.Won) wins++;
                if (r.ActReached >= 1) act2++;
                if (r.ActReached >= 2) act3++;
                noLoss += r.NoLossBattles; lateB += r.LateBattles; lateNo += r.LateNoLoss;
                battles += r.Battles;
                turns += r.Turns;
                floorSum += r.FloorReached;
            }
            var sb = new StringBuilder();
            sb.AppendLine("=== 밸런스 시뮬 " + runs + "런 (실제 전투 코드 사용) ===");
            sb.AppendLine("1막 보스 격파율: " + (100 * wins / runs) + "%   2막 도달: " + (100 * act2 / runs) + "%   3막 도달: " + (100 * act3 / runs) + "%");
            sb.AppendLine("평균 도달 층: " + (floorSum / (float)runs).ToString("F1") + "   평균 전투 수: " + (battles / (float)runs).ToString("F1")
                        + "   전투당 평균 턴: " + (battles > 0 ? (turns / (float)battles).ToString("F1") : "-"));
            sb.AppendLine("무손실 전투(종료 HP ≥ 진입 HP): " + noLoss + "건 / 전체 " + battles + "전투 ("
                        + (battles > 0 ? 100 * noLoss / battles : 0) + "%)");
            sb.AppendLine("  후반(5층+/2막) 무손실: " + lateNo + " / " + lateB + " ("
                        + (lateB > 0 ? 100 * lateNo / lateB : 0) + "%) — 20% 초과면 무손실 엔진 의심");

            // 진형별 성적
            var byForm = new int[4]; var byFormWin = new int[4];
            foreach (var r in results) { byForm[r.Formation]++; if (r.Won) byFormWin[r.Formation]++; }
            // 진단 — 앞 8런의 사망 원인·카드 사용 (정책 결함 vs 난이도 구분)
            sb.AppendLine("--- 진단(앞 8런) ---");
            for (int i = 0; i < results.Count && i < 8; i++)
            {
                var r = results[i];
                sb.AppendLine("  층 " + r.FloorReached + " / 전투 " + r.Battles + " / 카드 " + r.CardsPlayed
                            + " / 막 " + (r.ActReached + 1) + " / 사망 " + r.DeathCause + " / 파티 " + r.Party.Count + "명");
            }
            sb.Append("진형별 격파율: ");
            for (int f = 0; f < 4; f++)
                sb.Append(Loc.T("form." + f) + " " + (byForm[f] > 0 ? (100 * byFormWin[f] / byForm[f]) + "%" : "-") + "  ");
            sb.AppendLine();
            return sb.ToString();
        }

        [MenuItem("Tanker/밸런스 시뮬 30런")]
        public static void Menu30() => Debug.Log(Run(30));

        /// 진형별 비교 — 같은 시드 집합을 4개 진형으로 각각 돌린다 (편차의 원인 분리)
        public static void BatchFormations()
        {
            int n = 20;
            var env = System.Environment.GetEnvironmentVariable("TANKER_SIM_RUNS");
            if (!string.IsNullOrEmpty(env)) int.TryParse(env, out n);
            var sb = new StringBuilder();
            for (int f = 0; f < 4; f++)
            {
                ForcedFormation = f;
                sb.AppendLine("### " + Loc.T("form." + f));
                sb.AppendLine(Run(n));
            }
            ForcedFormation = -1;
            Debug.Log("[BalanceSim-Form]\n" + sb);
        }

        /// 배치모드 진입점
        public static void BatchRun()
        {
            int n = 40;
            var env = System.Environment.GetEnvironmentVariable("TANKER_SIM_RUNS");
            if (!string.IsNullOrEmpty(env)) int.TryParse(env, out n);
            Debug.Log("[BalanceSim]\n" + Run(n));
        }
    }
}
