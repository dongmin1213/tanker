using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tanker
{
    public enum Phase { Player, Resolving, Won, Lost }

    /// 전투 규칙의 전부 (v0.4: 가변 파티 + 카드 핸드). UI는 상태를 읽고 입력만 전달한다.
    /// 카드는 '진행' 전까지 예약 — 자유 취소/교체, EndTurn이 유일한 확정점. 수치는 Balance, 문자열은 Loc.
    public class BattleManager : MonoBehaviour
    {
        public Unit Tank;
        public readonly List<Unit> Allies = new();     // [0]=탱커
        public readonly List<Unit> Enemies = new();

        public Phase Phase = Phase.Player;
        public int Turn = 1;
        public int TauntDuration = 2;
        public int TauntGuardAmt, CoverReduceAmt, BraceHeal = 3;

        // 카드 핸드
        public readonly List<CardType> Hand = new();
        readonly List<CardType> drawPile = new();
        readonly List<CardType> discardPile = new();
        System.Random cardRng;
        public int PendingCard = -1;                   // 대상 선택 대기 중 핸드 인덱스
        public int PlannedCard = -1;                   // 예약된 핸드 인덱스
        public Unit PlannedTarget;

        // 커밋된 효과 (해소 중)
        public Unit CoverTarget;
        public bool Bracing;
        public bool PhalanxActive;                     // 결사 방어: 이번 턴 아군 전원 -2
        public int OathTurns;                          // 반석의 맹세: 남은 턴 동안 탱커 -2
        public bool IronWillActive;                    // 철의 의지: 이번 턴 탱커가 받는 한 방 상한
        public Unit MarkTarget;                        // 수호 낙인: 그 아군 피해 절반을 탱커가 분담
        int bracedTaken;

        public int RedirectedSaved, MitigatedSaved;
        public int TotalSaved => RedirectedSaved + MitigatedSaved;
        public int RewardGold;
        public string EncounterTitle = "";
        public string Log = "";

        public event Action<Unit, string, Color> Popup;
        public event Action<Unit, Unit> Strike;
        public event Action<bool> Finished;

        public void Init(EncounterDef def, RunState run)
        {
            var b = Balance.I;
            Tank = Unit.Make(Loc.T("unit.tank"), Team.Ally, b.tankHp, 0, tank: true, sheet: "tank");
            Tank.Hp = Mathf.Clamp(run.TankHp, 1, b.tankHp);
            Allies.Clear(); Allies.Add(Tank);
            for (int i = 0; i < run.Party.Count; i++)
            {
                var cd = RunData.Class(run.Party[i]);
                var u = Unit.Make(Loc.T(cd.LocKey), Team.Ally, cd.Hp, cd.Power, sheet: cd.Sheet,
                                  role: cd.IsHealer ? Role.Healer : Role.Attacker);
                u.Hp = Mathf.Clamp(run.PartyHp[i], 1, cd.Hp);
                u.Trait = cd.Trait;
                Allies.Add(u);
            }
            if (run.DpsShakenNext)
            {
                var s = StrongestAttacker();
                if (s != null) s.Shaken = true;
                run.DpsShakenNext = false;
            }

            Enemies.Clear();
            foreach (var d in def.Units)
            {
                var e = Unit.Make(Loc.T(d.NameKey), Team.Enemy, d.Hp, d.Power,
                                  sheet: d.Sheet, ai: d.Ai, chargeOffset: d.ChargeOffset, aoePower: d.AoePower);
                e.Thorns = d.Thorns;
                e.Lifesteal = d.Lifesteal;
                Enemies.Add(e);
            }

            TauntDuration = b.tauntDuration;
            TauntGuardAmt = run.TauntGuard;
            CoverReduceAmt = run.CoverReduce;
            BraceHeal = b.braceHeal + run.BraceBonus;
            RewardGold = def.Gold;
            EncounterTitle = Loc.T(def.TitleKey);

            Phase = Phase.Player; Turn = 1;
            PendingCard = -1; PlannedCard = -1; PlannedTarget = null;
            CoverTarget = null; Bracing = false; PhalanxActive = false; OathTurns = 0;
            IronWillActive = false; MarkTarget = null;
            RedirectedSaved = 0; MitigatedSaved = 0;

            // 덱 — 시드 결정 셔플
            cardRng = new System.Random(run.Seed * 131 + run.Node * 17);
            drawPile.Clear(); discardPile.Clear(); Hand.Clear();
            drawPile.AddRange(run.Deck);
            ShufflePile(drawPile);
            DrawHand();

            RollIntents();
            Log = Loc.T("log.t1");
        }

        void ShufflePile(List<CardType> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = cardRng.Next(i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }

        public string TotalDeckInfo() => drawPile.Count + "+" + discardPile.Count;

        void DrawHand()
        {
            while (Hand.Count < 3)
            {
                if (drawPile.Count == 0)
                {
                    if (discardPile.Count == 0) break;
                    drawPile.AddRange(discardPile);
                    discardPile.Clear();
                    ShufflePile(drawPile);
                }
                Hand.Add(drawPile[drawPile.Count - 1]);
                drawPile.RemoveAt(drawPile.Count - 1);
            }
        }

        // ---- 아군 헬퍼 ----

        public Unit HealerUnit()
        {
            foreach (var a in Allies) if (a.Role == Role.Healer && a.Alive) return a;
            return null;
        }

        public Unit StrongestAttacker()
        {
            Unit best = null;
            foreach (var a in Allies)
                if (a.Alive && a.Role == Role.Attacker && (best == null || a.Power > best.Power)) best = a;
            return best;
        }

        Unit LowestNonTank()
        {
            Unit best = null;
            foreach (var a in Allies)
                if (a.Alive && !a.IsTank && (best == null || a.Hp < best.Hp)) best = a;
            return best ?? Tank;
        }

        /// 적 AI v2 — 한 방(연타 합)에 처치 가능한 백라이너가 있으면 그중 최고 위협(공격력)을 노리고,
        /// 없으면 HP 최저 백라이너 집중. 인텐트로 예고되므로 플레이어가 읽고 대응할 수 있다.
        Unit SmartBackliner(Unit e)
        {
            int dmg = e.Ai == AiKind.SpiderDouble ? e.Power * 2 : e.Power;
            Unit kill = null, low = null;
            foreach (var a in Allies)
            {
                if (!a.Alive || a.IsTank) continue;
                if (a.Hp <= dmg && (kill == null || a.Power > kill.Power)) kill = a;
                if (low == null || a.Hp < low.Hp) low = a;
            }
            return kill ?? low ?? Tank;
        }

        /// 아군 공격수 AI v2 — 이번 타로 처치 가능한 적 우선(활동 중 > 무력화 중, 그중 위협 큰 쪽),
        /// 킬각이 없으면 HP 최저 집중(오버킬 최소화). 예측(PredictKills)과 해소가 같은 함수를 쓴다.
        Unit PickAttackTarget(int dmg, System.Func<Unit, int> hpOf)
        {
            Unit kill = null, focus = null;
            foreach (var e in Enemies)
            {
                int h = hpOf(e);
                if (h <= 0) continue;
                if (h <= dmg && (kill == null || EnemyThreat(e) > EnemyThreat(kill)
                                  || (EnemyThreat(e) == EnemyThreat(kill) && h < hpOf(kill)))) kill = e;
                if (focus == null || h < hpOf(focus)) focus = e;
            }
            return kill ?? focus;
        }

        static int EnemyThreat(Unit e) => (e.Stunned || e.Charging ? 0 : 100) + e.Power;

        /// 아군 공격수의 이번 타 피해 — 위축 절반, 광전사는 HP 절반 이하에서 2배 (예측·해소 공용)
        static int AllyDamage(Unit a)
        {
            int d = a.Shaken ? a.Power / 2 : a.Power;
            if (a.Trait == Trait.Frenzy && a.Hp * 2 <= a.MaxHp) d *= 2;
            return d;
        }

        // ---- 인텐트 ----

        string RollIntents()
        {
            var b = Balance.I;
            string notice = null;
            foreach (var e in Enemies)
            {
                e.Charging = false; e.AoeIntent = false; e.CurseIntent = null; e.HealIntent = null;
                if (!e.Alive) { e.Intent = null; continue; }
                e.Step++;
                switch (e.Ai)
                {
                    case AiKind.EnemyHealer:
                        e.Intent = null;
                        Unit hurt = null;
                        foreach (var o in Enemies)
                            if (o != e && o.Alive && o.Hp < o.MaxHp
                                && (hurt == null || o.MaxHp - o.Hp > hurt.MaxHp - hurt.Hp)) hurt = o;
                        e.HealIntent = hurt; // null = 회복할 대상 없음 (대기)
                        break;
                    case AiKind.FixedHealer:
                        var h = HealerUnit();
                        e.Intent = h ?? LowestNonTank();
                        break;
                    case AiKind.FixedDps:
                        e.Intent = StrongestAttacker() ?? LowestNonTank();
                        break;
                    case AiKind.LowestBackliner:
                        e.Intent = SmartBackliner(e);
                        break;
                    case AiKind.SpiderDouble:
                        e.Intent = SmartBackliner(e);
                        break;
                    case AiKind.ShamanCurse:
                        e.Intent = null;
                        e.CurseIntent = StrongestAttacker() ?? LowestNonTank();
                        break;
                    case AiKind.BruteCycle:
                        notice = CheckEnrage(e, b.bruteEnrageHp, b.bruteEnragePower, Loc.F("log.enrageBrute", e.Name)) ?? notice;
                        if (e.Enraged) e.Intent = NextSmashTarget(e);
                        else if ((e.Step + e.ChargeOffset) % 2 == 1) { e.Intent = null; e.Charging = true; }
                        else e.Intent = NextSmashTarget(e);
                        break;
                    case AiKind.BossWarlord:
                        var bossNotice = CheckEnrage(e, b.bossEnrageHp, 0, Loc.F("log.enrageBoss", e.Name));
                        if (bossNotice != null) { notice = bossNotice; e.Step = 1; }
                        if (!e.Enraged)
                        {
                            int p = (e.Step - 1) % 3;
                            if (p == 0) { e.Intent = null; e.Charging = true; }
                            else if (p == 1) e.AoeIntent = true;
                            else e.Intent = StrongestAttacker() ?? LowestNonTank();
                        }
                        else
                        {
                            if (e.Step % 2 == 1) e.AoeIntent = true;
                            else e.Intent = StrongestAttacker() ?? LowestNonTank();
                        }
                        break;
                }
            }
            return notice;
        }

        string CheckEnrage(Unit e, int threshold, int enragePower, string msg)
        {
            if (e.Enraged || e.Hp > threshold) return null;
            e.Enraged = true;
            if (enragePower > 0) e.Power = enragePower;
            Popup?.Invoke(e, Loc.T("pop.enrage"), new Color(1f, 0.3f, 0.25f));
            return msg;
        }

        Unit NextSmashTarget(Unit e)
        {
            // 백라이너 교대: 최강 공격수 ↔ 힐러(없으면 최저 HP)
            var strong = StrongestAttacker();
            var healer = HealerUnit() ?? LowestNonTank();
            var t = e.SmashToDps ? (strong ?? healer) : (healer ?? strong);
            e.SmashToDps = !e.SmashToDps;
            return t ?? Tank;
        }

        // ---- 예측 (UI와 해소가 같은 계산) ----

        CardType? PlannedType => PlannedCard >= 0 && PlannedCard < Hand.Count ? Hand[PlannedCard] : (CardType?)null;

        public bool IsTauntedNow(Unit e) => e.TauntTurns > 0 || (PlannedType == CardType.Taunt && PlannedTarget == e);
        public bool IsStunnedNow(Unit e) => e.Stunned || (PlannedType == CardType.Shove && PlannedTarget == e);
        public bool AoeActive(Unit e) => e.AoeIntent && !IsTauntedNow(e) && !IsStunnedNow(e);
        public Unit CoverPreview => CoverTarget ?? (PlannedType == CardType.Cover ? PlannedTarget : null);
        public Unit ShieldPreview => PlannedType == CardType.Shield ? PlannedTarget : null;
        bool BracingNow => Bracing || PlannedType == CardType.Brace;
        bool PhalanxNow => PhalanxActive || PlannedType == CardType.Phalanx;
        bool OathNow => OathTurns > 0 || PlannedType == CardType.Oath;
        bool IronWillNow => IronWillActive || PlannedType == CardType.IronWill;
        public Unit MarkPreview => MarkTarget ?? (PlannedType == CardType.GuardianMark ? PlannedTarget : null);

        public Unit EffectiveTarget(Unit enemy)
        {
            if (enemy.Charging || IsStunnedNow(enemy)) return null;
            if (enemy.Ai == AiKind.ShamanCurse || enemy.Ai == AiKind.EnemyHealer) return null;
            if (IsTauntedNow(enemy)) return Tank;
            if (enemy.AoeIntent || enemy.Intent == null) return null;
            var cover = CoverPreview;
            if (cover != null && enemy.Intent == cover) return Tank;
            return enemy.Intent;
        }

        /// 통합 피해 계산 — 예측과 해소가 이 함수 하나를 쓴다.
        /// hitIndex: 연타에서 몇 번째 타인가 (엄호는 0타만 리다이렉트).
        int ComputeDamage(Unit enemy, Unit receiver, int baseDmg, bool viaTaunt, bool viaCover)
        {
            int dmg = baseDmg;
            if (viaTaunt) dmg = Mathf.Max(0, dmg - TauntGuardAmt);
            if (viaCover) dmg = Mathf.Max(0, dmg - CoverReduceAmt);
            if (PhalanxNow && receiver.Team == Team.Ally) dmg = Mathf.Max(0, dmg - Balance.I.phalanxReduce);
            if (OathNow && receiver.IsTank) dmg = Mathf.Max(0, dmg - Balance.I.oathReduce);
            if (receiver.IsTank && BracingNow) dmg /= 2;
            if (receiver.IsTank && IronWillNow) dmg = Mathf.Min(dmg, Balance.I.ironWillCap); // 철의 의지: 한 방 상한
            return dmg;
        }

        public int EffectiveDamage(Unit enemy)
        {
            var target = EffectiveTarget(enemy);
            if (target == null) return 0;
            bool taunted = IsTauntedNow(enemy);
            bool viaCover = !taunted && target.IsTank && CoverPreview != null && enemy.Intent == CoverPreview;
            int perHit = ComputeDamage(enemy, target, enemy.Power, taunted && target.IsTank, viaCover);
            int hits = enemy.Ai == AiKind.SpiderDouble ? 2 : 1;
            // 거미 2타: 도발이면 둘 다 탱커, 엄호면 첫 타만 — 라벨엔 수신자 기준 합계
            if (hits == 2 && viaCover) return perHit; // 엄호 수신(탱커) 몫은 첫 타만
            return perHit * hits;
        }

        public int EffectiveAoeDamage(Unit enemy, Unit backliner)
        {
            bool covered = CoverPreview == backliner;
            var receiver = covered ? Tank : backliner;
            return ComputeDamage(enemy, receiver, enemy.AoePower, false, covered);
        }

        /// 이번 턴 이 유닛이 받을 예상 총 피해 (예약·선행 처치·기절·방패·수호 낙인 분담 반영)
        public int IncomingPreview(Unit u)
        {
            var killed = PredictKills();
            bool shieldLeft = u.Shielded || ShieldPreview == u;
            var mark = MarkPreview;
            int sum = 0;

            // 타격 1건을 u 관점 합계에 반영 — 낙인 아군이면 절반만, 탱커면 낙인 분담분 가산
            void Add(Unit recv, int d)
            {
                if (d <= 0) return;
                int share = mark != null && recv == mark && !recv.IsTank && d > 1 ? d / 2 : 0;
                if (recv == u)
                {
                    if (shieldLeft) { shieldLeft = false; return; } // 방패는 그 한 방 전체 무효 (분담 포함)
                    sum += d - share;
                }
                else if (u.IsTank && share > 0) sum += share;
            }

            foreach (var e in Enemies)
            {
                if (!e.Alive || e.Charging || killed.Contains(e) || IsStunnedNow(e)) continue;
                if (e.Ai == AiKind.ShamanCurse || e.Ai == AiKind.EnemyHealer) continue;
                if (AoeActive(e))
                {
                    foreach (var b in Allies)
                    {
                        if (b.IsTank || !b.Alive) continue;
                        bool covered = CoverPreview == b;
                        Add(covered ? Tank : b, EffectiveAoeDamage(e, b));
                    }
                    continue;
                }
                var recvS = EffectiveTarget(e);
                if (recvS == null) continue;
                int hits = e.Ai == AiKind.SpiderDouble ? 2 : 1;
                bool taunted = IsTauntedNow(e);
                bool viaCover = !taunted && recvS.IsTank && CoverPreview != null && e.Intent == CoverPreview;
                for (int hi = 0; hi < hits; hi++)
                {
                    // 거미 2타 + 엄호: 둘째 타는 원 대상에게 — 탱커 몫은 첫 타만
                    if (hits == 2 && viaCover && hi == 1) { Add(e.Intent, ComputeDamage(e, e.Intent, e.Power, false, false)); break; }
                    Add(recvS, ComputeDamage(e, recvS, e.Power, taunted && recvS.IsTank, viaCover));
                }
            }
            return sum;
        }

        HashSet<Unit> PredictKills()
        {
            // 아군 공격 페이즈 시뮬 — 처치될 적의 공격은 프리뷰에서 제외
            var killed = new HashSet<Unit>();
            var hp = new Dictionary<Unit, int>();
            foreach (var e in Enemies) if (e.Alive) hp[e] = e.Hp;
            foreach (var a in Allies)
            {
                if (!a.Alive || a.Role != Role.Attacker) continue;
                int dmg = AllyDamage(a);
                var tgt = PickAttackTarget(dmg, e => hp.TryGetValue(e, out var v) && !killed.Contains(e) ? v : 0);
                if (tgt == null) break;
                hp[tgt] -= dmg;
                if (hp[tgt] <= 0) killed.Add(tgt);
            }
            return killed;
        }

        // ---- 플레이어 입력 ----

        public bool CanUseSkill => Phase == Phase.Player;

        public bool CardPlayable(int idx)
        {
            if (idx < 0 || idx >= Hand.Count) return false;
            if (Hand[idx] == CardType.Devotion) return Tank.Hp > Balance.I.devotionAmount;
            return true;
        }

        public void PressCard(int idx)
        {
            if (!CanUseSkill || !CardPlayable(idx)) return;
            if (PendingCard == idx) { PendingCard = -1; Log = Loc.T("log.selCancel"); return; }
            if (PlannedCard == idx)
            {
                PlannedCard = -1; PlannedTarget = null; PendingCard = -1;
                Log = Loc.T("log.planCancel");
                return;
            }
            PlannedCard = -1; PlannedTarget = null;
            var t = Cards.TargetOf(Hand[idx]);
            if (t == CardTarget.None)
            {
                PlannedCard = idx; PendingCard = -1;
                Log = Loc.F("log.planCard", Cards.NameOf(Hand[idx]));
                return;
            }
            PendingCard = idx;
            Log = t == CardTarget.Enemy ? Loc.T("log.pickEnemy") : Loc.T("log.pickAlly");
        }

        public void ClickUnit(Unit u)
        {
            if (Phase != Phase.Player || PendingCard < 0 || !u.Alive) return;
            var card = Hand[PendingCard];
            var need = Cards.TargetOf(card);
            if (need == CardTarget.Enemy && u.Team == Team.Enemy)
            {
                if (card == CardType.Shove && u.Ai == AiKind.BossWarlord) { Log = Loc.T("log.shoveBossImmune"); return; }
                PlannedCard = PendingCard; PlannedTarget = u; PendingCard = -1;
                Log = Loc.F("log.planCardTarget", Cards.NameOf(card), u.Name);
            }
            else if (need == CardTarget.Ally && u.Team == Team.Ally && !u.IsTank)
            {
                PlannedCard = PendingCard; PlannedTarget = u; PendingCard = -1;
                Log = Loc.F("log.planCardTarget", Cards.NameOf(card), u.Name);
            }
        }

        public void EndTurn()
        {
            if (Phase != Phase.Player) return;
            PendingCard = -1;
            CommitPlanned();
            StartCoroutine(Resolve());
        }

        void CommitPlanned()
        {
            if (PlannedCard >= 0 && PlannedCard < Hand.Count)
            {
                var card = Hand[PlannedCard];
                var b = Balance.I;
                switch (card)
                {
                    case CardType.Taunt:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        { PlannedTarget.TauntTurns = TauntDuration; Popup?.Invoke(PlannedTarget, Loc.T("pop.taunt"), new Color(1f, 0.55f, 0.35f)); }
                        break;
                    case CardType.Cover:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        { CoverTarget = PlannedTarget; Popup?.Invoke(PlannedTarget, Loc.T("pop.cover"), new Color(0.55f, 0.75f, 1f)); }
                        break;
                    case CardType.Brace:
                        Bracing = true; bracedTaken = 0;
                        break;
                    case CardType.Shield:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        { PlannedTarget.Shielded = true; Popup?.Invoke(PlannedTarget, Loc.T("pop.shield"), new Color(0.7f, 0.85f, 1f)); }
                        break;
                    case CardType.Shove:
                        if (PlannedTarget != null && PlannedTarget.Alive && PlannedTarget.Ai != AiKind.BossWarlord)
                        { PlannedTarget.Stunned = true; Popup?.Invoke(PlannedTarget, Loc.T("pop.shove"), new Color(1f, 0.8f, 0.5f)); }
                        break;
                    case CardType.Devotion:
                        if (PlannedTarget != null && PlannedTarget.Alive && Tank.Hp > b.devotionAmount)
                        {
                            Tank.Hp -= b.devotionAmount;
                            PlannedTarget.Hp = Mathf.Min(PlannedTarget.MaxHp, PlannedTarget.Hp + b.devotionAmount);
                            Popup?.Invoke(PlannedTarget, "+" + b.devotionAmount, new Color(0.55f, 1f, 0.55f));
                        }
                        break;
                    case CardType.Rally:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        { PlannedTarget.Shaken = false; Popup?.Invoke(PlannedTarget, Loc.T("pop.rally"), new Color(1f, 0.95f, 0.6f)); }
                        break;
                    case CardType.Phalanx:
                        PhalanxActive = true; Popup?.Invoke(Tank, Loc.T("pop.phalanx"), new Color(0.8f, 0.9f, 1f));
                        break;
                    case CardType.Oath:
                        OathTurns = b.oathTurns; Popup?.Invoke(Tank, Loc.T("pop.oath"), new Color(1f, 0.84f, 0.37f));
                        break;
                    case CardType.IronWill:
                        IronWillActive = true; Popup?.Invoke(Tank, Loc.T("pop.ironwill"), new Color(0.75f, 0.8f, 0.95f));
                        break;
                    case CardType.GuardianMark:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        { MarkTarget = PlannedTarget; Popup?.Invoke(PlannedTarget, Loc.T("pop.mark"), new Color(1f, 0.84f, 0.37f)); }
                        break;
                    case CardType.Respite:
                        Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + b.respiteHeal);
                        AudioKit.Heal();
                        Popup?.Invoke(Tank, "+" + b.respiteHeal, new Color(0.55f, 1f, 0.55f));
                        break;
                }
                AudioKit.Card();
                discardPile.Add(card);
                Hand.RemoveAt(PlannedCard);
            }
            PlannedCard = -1; PlannedTarget = null;
        }

        public void PressContinue()
        {
            if (Phase == Phase.Won) Finished?.Invoke(true);
            else if (Phase == Phase.Lost) Finished?.Invoke(false);
        }

        // ---- 해소 ----

        bool AnyAllyDead()
        {
            foreach (var a in Allies) if (!a.Alive) return true;
            return false;
        }

        bool AnyEnemyAlive()
        {
            foreach (var e in Enemies) if (e.Alive) return true;
            return false;
        }

        /// 단일 타격 적용 — 방패/감산/버티기까지. 실제 받은 피해 반환.
        int ApplyHit(Unit attacker, Unit planned, Unit receiver, int baseDmg, bool viaTaunt, bool viaCover)
        {
            if (receiver.Shielded)
            {
                receiver.Shielded = false;
                Strike?.Invoke(attacker, receiver);
                AudioKit.Guard();
                Popup?.Invoke(receiver, Loc.T("pop.blocked"), new Color(0.7f, 0.85f, 1f));
                Log = Loc.F("log.blocked", receiver.Name);
                MitigatedSaved += baseDmg;
                return 0;
            }
            int dmg = ComputeDamage(attacker, receiver, baseDmg, viaTaunt, viaCover);
            if (dmg > 0) AudioKit.Hit();
            MitigatedSaved += Mathf.Max(0, baseDmg - dmg); // 경감 = 원 피해와 최종 피해의 차이

            // 수호 낙인 — 낙인 찍힌 아군의 피해 절반을 탱커가 분담
            int tankShare = 0;
            if (!receiver.IsTank && MarkTarget == receiver && Tank.Alive && dmg > 1)
            {
                tankShare = dmg / 2;
                dmg -= tankShare;
                Tank.Hp = Mathf.Max(0, Tank.Hp - tankShare);
                if (Bracing) bracedTaken += tankShare;
                RedirectedSaved += tankShare;
                Popup?.Invoke(Tank, Loc.F("pop.taken", tankShare), new Color(1f, 0.84f, 0.37f));
            }

            Strike?.Invoke(attacker, receiver);
            receiver.Hp = Mathf.Max(0, receiver.Hp - dmg);
            if (receiver.IsTank && Bracing) bracedTaken += dmg;

            // 흡혈 — 준 피해(분담 포함)만큼 회복
            if (attacker.Lifesteal && dmg + tankShare > 0)
            {
                attacker.Hp = Mathf.Min(attacker.MaxHp, attacker.Hp + dmg + tankShare);
                Popup?.Invoke(attacker, "+" + (dmg + tankShare), new Color(0.85f, 0.4f, 0.55f));
            }
            bool redirected = receiver.IsTank && planned != null && !planned.IsTank;
            if (redirected)
            {
                RedirectedSaved += dmg;
                Popup?.Invoke(Tank, Loc.F("pop.taken", dmg), new Color(1f, 0.84f, 0.37f));
                Log = Loc.F("log.redirect", planned.Name, dmg);
            }
            else
            {
                Popup?.Invoke(receiver, "-" + dmg, receiver.IsTank ? new Color(1f, 0.84f, 0.37f) : new Color(1f, 0.45f, 0.45f));
                Log = Loc.F("log.hit", attacker.Name, receiver.Name, dmg);
            }
            if (!receiver.IsTank && dmg > 0)
            { receiver.Shaken = true; Popup?.Invoke(receiver, Loc.T("pop.shaken"), new Color(1f, 0.6f, 0.9f)); }
            return dmg;
        }

        IEnumerator Resolve()
        {
            Phase = Phase.Resolving;
            var wait = new WaitForSeconds(0.5f);
            var quick = new WaitForSeconds(0.35f);

            // 아군 공격수 페이즈 — 킬각 우선, 없으면 HP 최저 집중 (PredictKills와 동일 로직)
            foreach (var a in Allies)
            {
                if (!a.Alive || a.Role != Role.Attacker) continue;
                int dmg = AllyDamage(a);
                var target = PickAttackTarget(dmg, u => u.Alive ? u.Hp : 0);
                if (target == null) break;
                a.Shaken = false;
                Strike?.Invoke(a, target);
                AudioKit.Hit();
                target.Hp = Mathf.Max(0, target.Hp - dmg);
                Popup?.Invoke(target, "-" + dmg, Color.white);
                Log = Loc.F("log.allyHit", a.Name, target.Name, dmg);

                // 가시 반사 — 살아있는 골렘을 때리면 공격자가 아프다 (반사는 쓰러뜨리진 못함)
                if (target.Alive && target.Thorns > 0)
                {
                    a.Hp = Mathf.Max(1, a.Hp - target.Thorns);
                    Popup?.Invoke(a, Loc.F("pop.thorns", target.Thorns), new Color(0.95f, 0.6f, 0.35f));
                }
                // 성기사 — 공격하며 탱커를 회복
                if (a.Trait == Trait.TankHealOnHit && Tank.Hp < Tank.MaxHp)
                {
                    int amt = Balance.I.paladinTankHeal;
                    Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + amt);
                    Popup?.Invoke(Tank, "+" + amt, new Color(0.55f, 1f, 0.55f));
                }
                // 음유시인 — 공격 후 위축된 아군 1명 해제
                if (a.Trait == Trait.Cleanse)
                {
                    foreach (var ally in Allies)
                        if (ally.Alive && ally.Shaken)
                        {
                            ally.Shaken = false;
                            Popup?.Invoke(ally, Loc.T("pop.rally"), new Color(1f, 0.95f, 0.6f));
                            break;
                        }
                }

                yield return quick;
                if (!AnyEnemyAlive()) { Win(); yield break; }
            }

            // 적 페이즈 — 아군 사망 즉시 패배·중단
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                if (e.Stunned) { Log = Loc.F("log.stunned", e.Name); Popup?.Invoke(e, Loc.T("pop.stunnedMark"), new Color(1f, 0.8f, 0.5f)); yield return wait; continue; }
                if (e.Charging) { Log = Loc.F("log.charging", e.Name); yield return wait; continue; }

                if (e.Ai == AiKind.EnemyHealer)
                {
                    if (e.TauntTurns > 0) { Log = Loc.F("log.curseWasted", e.Name); yield return wait; continue; }
                    var ht = e.HealIntent;
                    if (ht != null && ht.Alive)
                    {
                        Strike?.Invoke(e, ht);
                        int amt = Balance.I.necroHeal;
                        ht.Hp = Mathf.Min(ht.MaxHp, ht.Hp + amt);
                        AudioKit.Heal();
                        Popup?.Invoke(ht, "+" + amt, new Color(0.7f, 0.55f, 1f));
                        Log = Loc.F("log.necroHeal", e.Name, ht.Name);
                    }
                    else Log = Loc.F("log.necroIdle", e.Name);
                    yield return wait;
                    continue;
                }

                if (e.Ai == AiKind.ShamanCurse)
                {
                    if (e.TauntTurns > 0) { Log = Loc.F("log.curseWasted", e.Name); yield return wait; continue; }
                    var ct = e.CurseIntent;
                    if (ct != null && ct.Alive)
                    {
                        Strike?.Invoke(e, ct);
                        ct.Shaken = true;
                        Popup?.Invoke(ct, Loc.T("pop.cursed"), new Color(0.8f, 0.5f, 1f));
                        Log = Loc.F("log.curse", e.Name, ct.Name);
                    }
                    yield return wait;
                    continue;
                }

                if (e.AoeIntent && e.TauntTurns == 0)
                {
                    foreach (var b in Allies)
                    {
                        if (b.IsTank || !b.Alive) continue;
                        bool covered = CoverTarget == b;
                        var receiver = covered ? Tank : b;
                        ApplyHit(e, b, receiver, e.AoePower, false, covered);
                        yield return quick;
                        if (AnyAllyDead()) { Lose(); yield break; }
                    }
                    continue;
                }

                // 단일/연타 (도발된 광역 = 탱커 강타)
                var planned = e.AoeIntent ? Tank : e.Intent;
                if (planned == null || !planned.Alive) planned = Tank;
                bool taunted = e.TauntTurns > 0;
                int hits = e.Ai == AiKind.SpiderDouble ? 2 : 1;
                for (int hi = 0; hi < hits; hi++)
                {
                    Unit receiver;
                    bool viaCover = false;
                    if (taunted) receiver = Tank;
                    else if (CoverTarget != null && planned == CoverTarget && hi == 0) { receiver = Tank; viaCover = true; } // 엄호는 첫 타만
                    else receiver = planned;
                    if (!receiver.Alive) receiver = Tank;
                    ApplyHit(e, planned, receiver, e.Power, taunted && receiver.IsTank, viaCover);
                    yield return hits == 2 ? quick : wait;
                    if (AnyAllyDead()) { Lose(); yield break; }
                }
            }

            // 버티기 후불 회복
            if (Bracing && bracedTaken > 0)
            {
                int recover = Mathf.Min(BraceHeal, bracedTaken);
                AudioKit.Heal();
                Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + recover);
                Popup?.Invoke(Tank, Loc.F("pop.brace", recover), new Color(1f, 0.84f, 0.37f));
                Log = Loc.F("log.braceHeal", recover);
                yield return wait;
            }

            // 힐러 v2 — 백라이너 전담. 적의 최대 한 방에 죽을 수 있는(위험권) 아군 중 가장 가치 큰(공격력 높은)
            // 대상을 우선하고, 위험권이 없으면 HP 최저를 채운다.
            var healer = HealerUnit();
            if (healer != null)
            {
                int maxHit = 0;
                foreach (var e in Enemies)
                    if (e.Alive) maxHit = Mathf.Max(maxHit, e.Ai == AiKind.SpiderDouble ? e.Power * 2 : e.Power);
                Unit target = null;
                bool targetDanger = false;
                foreach (var a in Allies)
                {
                    if (!a.Alive || a.IsTank || a.Hp >= a.MaxHp) continue;
                    bool danger = a.Hp <= maxHit;
                    if (target == null || (danger && !targetDanger)
                        || (danger == targetDanger && (danger ? a.Power > target.Power : a.Hp < target.Hp)))
                    { target = a; targetDanger = danger; }
                }
                if (target != null)
                {
                    int amount = healer.Shaken ? healer.Power / 2 : healer.Power;
                    healer.Shaken = false;
                    Strike?.Invoke(healer, target);
                    AudioKit.Heal();
                    target.Hp = Mathf.Min(target.MaxHp, target.Hp + amount);
                    Popup?.Invoke(target, "+" + amount, new Color(0.55f, 1f, 0.55f));
                    Log = Loc.F("log.heal", target.Name, amount);
                    yield return wait;
                }
            }

            if (!AnyEnemyAlive()) { Win(); yield break; }

            // 턴 정리
            Turn++;
            Bracing = false; CoverTarget = null; PhalanxActive = false;
            IronWillActive = false; MarkTarget = null;
            if (OathTurns > 0) OathTurns--;
            foreach (var e in Enemies) { e.Stunned = false; if (e.TauntTurns > 0) e.TauntTurns--; }
            foreach (var a in Allies) a.Shielded = false;
            discardPile.AddRange(Hand);
            Hand.Clear();
            DrawHand();
            var notice = RollIntents();
            Phase = Phase.Player;
            Log = notice ?? Loc.F("log.turn", Turn);
        }

        void Win()
        {
            Phase = Phase.Won;
            AudioKit.Win();
            Log = Loc.F("log.win", RedirectedSaved, MitigatedSaved);
        }

        void Lose()
        {
            foreach (var a in Allies)
                if (!a.Alive) { Log = Loc.F("log.lose", a.Name); break; }
            Phase = Phase.Lost;
            AudioKit.Lose();
        }
    }
}
