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
        public readonly List<Card> Hand = new();
        readonly List<Card> drawPile = new();
        readonly List<Card> discardPile = new();
        RunState run;                                  // 유물·골드 등 런 상태 접근 (v0.7)
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
        public bool IronWillPlus;
        public Unit MarkTarget;                        // 수호 낙인: 그 아군 피해 절반을 탱커가 분담
        public bool MarkPlus;
        public int ThornStanceAmt;                     // 가시 태세: 이번 턴 탱커 직격 반사량 (v0.7)
        public int CoverBonus;                         // 엄호+: 대신 맞을 때 추가 감산
        public int BraceCap;                           // 버티기 후불 상한 (강화 시 상향)
        public int PhalanxAmt;                         // 결사 방어 감산량 (강화 시 상향)
        public bool RetainHand;                        // 재정비: 이번 턴 손패를 버리지 않음
        public int BonusDraw;                          // 재정비+: 다음 드로우 추가
        public int BonusGold;                          // 도적 처치 골드·도굴꾼 회수 — 승리 보상에 합산
        // ---- v1.0 카드 상태 ----
        public int BarricadeTurns, BarricadeAmt;       // 방벽: 지속 턴 동안 아군 전원 감산
        public int VowTurns;                           // 서약: 지속 턴 동안 매 턴 탱커 회복
        public int AegisTurns, AegisCap;               // 수호 방벽: 지속 턴 동안 탱커 한 방 상한
        public int UndyingHp;                          // 불굴: 치명 피해를 이 HP로 버팀 (0 = 없음)
        public int GrudgeNext;                         // 원한: 다음 턴 아군 전원 공격 증가분
        public int CounterMult;                        // 반격 태세: 이번 턴 배수 (0 = 없음)
        int counterHits; Unit counterLast;             // 반격 집계
        public Unit WarsongTarget; public int WarsongMult;
        public Unit BlessTarget;                       // 완전 방어 (이번 턴 피해 0)
        public int InspireAmt;                         // 격려: 이번 턴 아군 전원 공격 +
        public int VanguardAmt, RearguardAmt;          // 진형 카드
        public int FortressAlly; public bool FortressActive;
        public Unit SilenceTarget;                     // 침묵: 그 적의 특수 능력 이번 턴 무효
        public Unit StudyTarget; public int StudyReduce, StudyTurns;
        public bool InvulnTurn;                        // 최후의 저항: 이번 턴 탱커 무적
        public int SealedCard = -1;                    // 주술 방해자: 이번 턴 사용 불가 카드 인덱스
        public bool HealBlockedTurn;                   // 부패 술사: 이번 턴 회복 무효
        bool extraPlay;                                // 각성: 이번 턴 카드 한 장 더
        bool grudgeArmed, grudgePlus; int grudgeTaken; // 원한: 이번 턴 받은 피해 → 다음 턴 화력
        bool awakenUsed;                               // 각성은 턴당 1회
        int plannedRemoved = -1;                       // 이번 커밋에서 손패에서 빠진 인덱스
        int bracedTaken;

        public int RedirectedSaved, MitigatedSaved;
        public int TotalSaved => RedirectedSaved + MitigatedSaved;
        public int RewardGold;
        public int StateVersion;                       // 플레이어 페이즈 예측 캐시 무효화용 — 입력·턴 시작에만 증가
        public string EncounterTitle = "";
        string titleKey, titleArgKey;                  // 언어 전환 시 전투명 재번역 레시피
        public string Log = "";

        /// 언어 전환 — 유닛 이름·전투명을 현재 언어로 재번역 (진행 중 전투 포함)
        public void RebindNames()
        {
            foreach (var u in Allies) if (u.NameKey != null) u.Name = Loc.T(u.NameKey);
            foreach (var u in Enemies) if (u.NameKey != null) u.Name = Loc.T(u.NameKey);
            if (titleKey != null)
                EncounterTitle = titleArgKey == null ? Loc.T(titleKey) : Loc.F(titleKey, Loc.T(titleArgKey));
        }

        public event Action<Unit, string, Color> Popup;
        public event Action<Unit, Unit> Strike;
        public event Action<bool> Finished;

        public void Init(EncounterDef def, RunState run)
        {
            var b = Balance.I;
            this.run = run;
            Tank = Unit.Make(Loc.T("unit.tank"), Team.Ally, run.TankMaxHp, 0, tank: true, sheet: "tank");
            Tank.NameKey = "unit.tank";
            Tank.Hp = Mathf.Clamp(run.TankHp, 1, run.TankMaxHp);
            if (run.Has(RelicId.VeteranHelm)) Tank.ShieldCharges = 1; // 노병의 투구 — 개전 방패
            Allies.Clear(); Allies.Add(Tank);
            for (int i = 0; i < run.Party.Count; i++)
            {
                var cd = RunData.Class(run.Party[i]);
                var u = Unit.Make(Loc.T(cd.LocKey), Team.Ally, cd.Hp, cd.Power, sheet: cd.Sheet,
                                  role: cd.IsHealer ? Role.Healer : Role.Attacker);
                u.Hp = Mathf.Clamp(run.PartyHp[i], 1, cd.Hp);
                u.NameKey = cd.LocKey;
                u.Trait = cd.Trait;
                u.RangedClass = cd.Ranged;
                u.Row = i < run.Rows.Count ? run.Rows[i] : (cd.Ranged ? 1 : 0); // 진형 (v0.9)
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
                e.NameKey = d.NameKey;
                e.Thorns = d.Thorns;
                e.Lifesteal = d.Lifesteal;
                e.Aura = d.Aura;
                e.SelfShield = d.SelfShield;
                e.Pack = d.Pack;
                e.Leap = d.Leap;
                e.Pierce = d.Pierce;
                e.Reflector = d.Reflector;
                e.HealBlock = d.HealBlock;
                e.Splitter = d.Splitter;
                e.Dormant = d.Dormant;
                e.DeathBuff = d.DeathBuff;
                e.Env = d.Env;
                e.CounterOnce = d.CounterOnce;
                e.PoisonHit = d.PoisonHit;
                if (d.Dormant) e.Hp = 0;                       // 분열 대기체 — 부모가 죽은 다음 턴에 일어난다
                if (d.Ai == AiKind.Sentinel) e.WakeTimer = b.sentinelWake;
                e.BountyGold = d.BountyGold;
                e.DamageCap = d.DamageCap;
                if (d.SelfShield) e.ShieldCharges = 1;
                if (d.Ai == AiKind.Bomber) e.BombTimer = b.bomberFuse;
                Enemies.Add(e);
            }

            TauntDuration = b.tauntDuration + (run.Has(RelicId.OldStandard) ? b.relicOldStandard : 0);
            TauntGuardAmt = run.TauntGuard;
            CoverReduceAmt = run.CoverReduce;
            BraceHeal = b.braceHeal + run.BraceBonus;
            RewardGold = def.Gold + (run.Has(RelicId.GoldMagnet) && def.Gold > 0 ? b.relicGoldMagnet : 0);
            EncounterTitle = def.Title;
            titleKey = def.TitleKey; titleArgKey = def.TitleArgKey;

            Phase = Phase.Player; Turn = 1;
            PendingCard = -1; PlannedCard = -1; PlannedTarget = null;
            CoverTarget = null; Bracing = false; PhalanxActive = false; OathTurns = 0;
            IronWillActive = false; IronWillPlus = false; MarkTarget = null; MarkPlus = false;
            ThornStanceAmt = 0; RetainHand = false; BonusDraw = 0; BonusGold = 0;
            RedirectedSaved = 0; MitigatedSaved = 0;

            // 덱 — 시드 결정 셔플
            cardRng = new System.Random(run.Seed * 131 + run.Act * 3557 + run.Cur * 17); // 막마다 다른 스트림
            drawPile.Clear(); discardPile.Clear(); Hand.Clear();
            drawPile.AddRange(run.Deck);
            ShufflePile(drawPile);
            DrawHand();

            // v1.0 개전 유물
            if (run.Has(RelicId.StoneHeart)) { BarricadeTurns = b.relicStoneHeart; BarricadeAmt = b.barricadeAmt; }
            if (run.Has(RelicId.HolyWater))
                foreach (var a in Allies) { a.Shaken = false; a.Poison = 0; }
            if (run.Has(RelicId.SilverBell)) DrawOne();
            if (run.Has(RelicId.BoneWhistle))
                foreach (var e in Enemies) if (e.Alive && !BossImmune(e)) { e.Stunned = true; break; }

            RollIntents();
            StateVersion++;
            Log = Loc.T("log.t1");
        }

        /// 즉시 드로우 — 정찰·군율 등 (손패 상한은 두지 않는다: 카드 자원 축)
        void BonusDrawNow(int n)
        {
            for (int i = 0; i < n; i++) DrawOne();
        }

        void ShufflePile(List<Card> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = cardRng.Next(i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }

        public int DrawCount => drawPile.Count;
        public int DiscardCount => discardPile.Count;

        /// 카드 1장 드로우 — 뽑을 더미가 비면 버린 더미를 섞는다 (손패 상한 5)
        bool DrawOne()
        {
            if (Hand.Count >= 5) return false;
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0) return false;
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                ShufflePile(drawPile);
            }
            Hand.Add(drawPile[drawPile.Count - 1]);
            drawPile.RemoveAt(drawPile.Count - 1);
            return true;
        }

        void DrawHand(int target = -1)
        {
            if (target < 0) target = Balance.I.handSize;
            target = Mathf.Min(target, 5); // UI 카드 슬롯 상한
            while (Hand.Count < target)
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

        /// 살아있는 아군 중 해당 특성 보유자가 있는가 (예측·해소 공용)
        bool HasLivingTrait(Trait t)
        {
            foreach (var a in Allies) if (a.Alive && a.Trait == t) return true;
            return false;
        }

        /// 창병이 지키는 전열 아군 — 결정론: 전열 비탱커 중 HP 최저 (창병 자신 제외)
        Unit LancerGuardTarget()
        {
            Unit lancer = null;
            foreach (var a in Allies) if (a.Alive && a.Trait == Trait.LongReach) { lancer = a; break; }
            if (lancer == null) return null;
            Unit best = null;
            foreach (var a in Allies)
                if (a.Alive && !a.IsTank && a != lancer && RowOf(a) == 0 && (best == null || a.Hp < best.Hp)) best = a;
            return best;
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

        // 진형 후보 집합을 지키는 변형 — 근접 일반 적(FixedDps·브루트)용 (v0.9 전수검사 H-01)
        Unit StrongestAttackerFor(Unit e)
        {
            Unit best = null;
            foreach (var a in Allies)
                if (a.Alive && a.Role == Role.Attacker && !a.IsTank && !RowBlocked(e, a)
                    && (best == null || a.Power > best.Power)) best = a;
            return best;
        }

        Unit LowestNonTankFor(Unit e)
        {
            Unit best = null;
            foreach (var a in Allies)
                if (a.Alive && !a.IsTank && !RowBlocked(e, a) && (best == null || a.Hp < best.Hp)) best = a;
            return best ?? Tank;
        }

        /// 적 AI v3 — 처치 가능한 백라이너가 있으면 그중 최고 위협을 노리고,
        /// 없으면 직전 턴 자기가 노리던 대상을 제외한 최저 HP (한 명만 계속 두들기는 단조로움 방지 — 유저 피드백).
        /// 진형 (v0.9): 근접 적은 전열 아군이 살아있으면 전열만 노린다 — 도약·매복 태그는 무시.
        /// 모든 단일 공격 근접 AI가 이 후보 판정을 공유한다 (AI별 개별 구현 금지 — v0.9 전수검사 H-01)
        bool RowBlocked(Unit e, Unit target)
        {
            if (e.Leap || target.IsTank || target.Row == 0) return false;
            foreach (var a in Allies)
                if (a.Alive && !a.IsTank && a.Row == 0) return true; // 전열이 버티는 한 후열은 못 노린다
            return false;
        }

        Unit SmartBackliner(Unit e)
        {
            int per = e.Power + AuraBonus(e);                       // 킬각 추정도 타격당 오라 포함 (M-02)
            int dmg = e.Ai == AiKind.SpiderDouble ? per * 2 : per;
            var prev = e.Intent; // RollIntents 재할당 전이라 직전 턴 대상이 남아 있다

            Unit kill = null, low = null, lowAlt = null;
            foreach (var a in Allies)
            {
                if (!a.Alive || a.IsTank || RowBlocked(e, a)) continue;
                if (a.Hp <= dmg && (kill == null || a.Power > kill.Power)) kill = a;
                if (low == null || a.Hp < low.Hp) low = a;
                if (a != prev && (lowAlt == null || a.Hp < lowAlt.Hp)) lowAlt = a;
            }
            return kill ?? lowAlt ?? low ?? Tank;
        }

        /// 아군 공격수 AI v2 — 이번 타로 처치 가능한 적 우선(활동 중 > 무력화 중, 그중 위협 큰 쪽),
        /// 킬각이 없으면 HP 최저 집중(오버킬 최소화). 예측(PredictKills)과 해소가 같은 함수를 쓴다.
        Unit PickAttackTarget(int dmg, System.Func<Unit, int> hpOf, System.Func<Unit, int> shieldOf)
        {
            Unit kill = null, focus = null;
            foreach (var e in Enemies)
            {
                int h = hpOf(e);
                if (h <= 0) continue;
                // 킬각 판정은 실제 들어갈 피해 기준 — 방패는 이 한 방을 막고, 피해 상한은 피해를 깎는다
                bool killable = shieldOf(e) <= 0 && h <= CapTo(e, dmg);
                if (killable && (kill == null || EnemyThreat(e) > EnemyThreat(kill)
                                  || (EnemyThreat(e) == EnemyThreat(kill) && h < hpOf(kill)))) kill = e;
                if (focus == null || h < hpOf(focus)) focus = e;
            }
            return kill ?? focus;
        }

        /// 아군 킬각 우선순위 — 무력화(기절 예약 포함·차징)면 특수 가중치까지 전부 0 (예측·해소 공용)
        int EnemyThreat(Unit e)
        {
            if (IsStunnedNow(e) || e.Charging) return 0;
            return 100 + e.Power
                + (e.Aura ? Balance.I.threatAuraBonus : 0)
                + (e.Ai == AiKind.Bomber && e.BombTimer <= 1 ? Balance.I.threatBombBonus : 0);
        }

        /// 아군 공격수의 이번 타 피해 — 클래스 특성 전부 반영 (예측·해소 공용)
        int AllyDamage(Unit a) => AllyDamage(a, a.Shaken, a.Momentum);

        /// 확정 연산 순서 (v0.9): 기본공격 + 진형 + 군기 → 위축 절반(진짜 절반) → 배율 특성 → 기세 가산
        int AllyDamage(Unit a, bool shaken, int momentum)
        {
            int d = a.Power + a.Retaliation + a.PowerBonus;                   // 거인 피격 누적·일시 증가
            // 진형: 근접 클래스는 전열 +, 후열 - (원거리·장창은 무관)
            if (!a.IsTank && !a.RangedClass && a.Trait != Trait.LongReach)
                d += RowOf(a) == 0 ? Balance.I.rowFrontBonus : -Balance.I.rowBackPenalty;
            if (!a.IsTank && run != null && run.Formation == 2) d += Balance.I.formAtkBonus; // 공격 진형
            if (Turn == 1 && run != null && run.Has(RelicId.WarBanner)) d += Balance.I.relicWarBanner; // 군기
            if (run != null && run.Has(RelicId.BattleDrum)) d += Balance.I.relicBattleDrum;            // 전투 북
            if (run != null && run.Has(RelicId.BloodPact)) d += Balance.I.relicBloodPactAtk;           // 피의 계약
            if (!a.IsTank && RowOf(a) == 0) d += VanguardNow;                 // 선봉: 전열 화력
            d += InspireNow + GrudgeNext;                                     // 격려·원한
            if (shaken) d /= 2;                                               // 위축 = 이번 행동 전체 절반
            d = Mathf.Max(1, d);
            // 배율은 곱으로 누적하지 않는다 — 가장 큰 배율 하나만 (v1.0 전수검사: 180 피해 지배 조합 차단)
            int mult = 1;
            if (a.Trait == Trait.Focus && PlannedCard < 0) mult = Mathf.Max(mult, 2);              // 수도승
            if (WarsongPreview == a) mult = Mathf.Max(mult, WarsongMultNow);                       // 속공
            if (a.Trait == Trait.Frenzy && a.Hp * 2 <= a.MaxHp) mult = Mathf.Max(mult, 2);         // 광전사
            if (a.Trait == Trait.FullHpDouble && a.Hp >= a.MaxHp) mult = Mathf.Max(mult, 2);       // 문지기
            if (a.Trait == Trait.FirstStrike && Turn == 1) mult = Mathf.Max(mult, 2);              // 암살자
            if (Turn == 1 && run != null && run.Has(RelicId.SharpStone)) mult = Mathf.Max(mult, 2);// 숫돌
            d *= mult;
            if (a.Trait == Trait.Momentum) d += momentum * Balance.I.momentumStep; // 전사: 연속 공격 누적
            return d;
        }

        Unit HighestPowerEnemy(System.Func<Unit, int> hpOf)
        {
            Unit best = null;
            foreach (var e in Enemies)
                if (hpOf(e) > 0 && (best == null || e.Power > best.Power)) best = e;
            return best;
        }

        /// 처치 트리거 — 도적 골드·수인 포식·도굴꾼 회수 (해소 전용)
        void OnEnemyKilled(Unit killer, Unit dead)
        {
            var b = Balance.I;
            if (killer.Trait == Trait.GoldOnKill)
            { BonusGold += b.killGold; Popup?.Invoke(killer, "+" + b.killGold + "G", new Color(1f, 0.84f, 0.37f)); }
            if (killer.Trait == Trait.Devour && killer.Hp < killer.MaxHp)
            { killer.Hp = Mathf.Min(killer.MaxHp, killer.Hp + b.devourHeal); Popup?.Invoke(killer, "+" + b.devourHeal, new Color(0.55f, 1f, 0.55f)); }
            if (dead.Ai == AiKind.Thief && dead.StolenGold > 0)
            { BonusGold += dead.StolenGold + b.thiefSteal; Popup?.Invoke(dead, "+" + (dead.StolenGold + b.thiefSteal) + "G", new Color(1f, 0.84f, 0.37f)); }
            if (dead.BountyGold > 0)
            { BonusGold += dead.BountyGold; Popup?.Invoke(dead, "+" + dead.BountyGold + "G", new Color(1f, 0.84f, 0.37f)); } // 미믹 전리품
            if (dead.Splitter) dead.SplitPending = true;     // 다음 턴 분열체 각성
            if (dead.DeathBuff)
            {
                foreach (var o in Enemies)
                    if (o.Alive && o != dead) o.PowerBonus += b.zealotDeathBuff;
                Popup?.Invoke(dead, Loc.T("pop.deathbuff"), new Color(1f, 0.5f, 0.4f));
            }
            if (run != null && run.Has(RelicId.GraveMoss) && Tank.Alive)
            {
                int gm = HealAmount(b.relicGraveMoss);
                if (gm > 0) { Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + gm); Popup?.Invoke(Tank, "+" + gm, new Color(0.55f, 1f, 0.55f)); }
            }
        }

        // ---- 인텐트 ----

        string RollIntents()
        {
            var b = Balance.I;
            string notice = null;
            Unit packTarget = null; // 늑대 무리 — 같은 사냥감을 함께 문다 (v0.9)

            // 분열체 각성 — 지난 턴에 죽은 분열 슬라임 자리에서 절반 개체 2마리가 일어난다 (예고 후 행동)
            foreach (var e in Enemies)
            {
                if (!e.SplitPending) continue;
                e.SplitPending = false;
                int woke = 0;
                foreach (var c in Enemies)
                {
                    if (woke >= 2) break;
                    if (!c.Dormant || c.Alive) continue;
                    c.Dormant = false;
                    c.Hp = c.MaxHp;
                    woke++;
                }
                if (woke > 0) notice = Loc.F("log.split", e.Name, woke);
            }
            // 상태 초기화 — 이번 턴 카드 봉인·석화·회복 무효는 매 턴 새로 정해진다
            SealedCard = -1;
            HealBlockedTurn = false;
            foreach (var a in Allies) a.Petrified = false;
            foreach (var e in Enemies)
            {
                e.Charging = false; e.AoeIntent = false; e.CurseIntent = null; e.HealIntent = null;
                e.Hidden = false;   // 잠복·비행은 그 턴 한정 (초기화 누락 시 전투 소프트락)
                if (!e.Alive) { e.Intent = null; continue; }
                if (e.SelfShield) e.ShieldCharges = 1; // 해골 방패병 — 매턴 방패 리필
                e.Step++;
                switch (e.Ai)
                {
                    case AiKind.LichBoss:
                        var lichNotice = CheckEnrage(e, b.lichEnrageHp, 0, Loc.F("log.enrageLich", e.Name));
                        if (lichNotice != null) { notice = lichNotice; e.Step = 1; }
                        if (!e.Enraged)
                        {
                            int lp = (e.Step - 1) % 3;
                            if (lp == 0) { e.Intent = null; e.Charging = true; }
                            else if (lp == 1) e.AoeIntent = true;      // 사령 폭풍 — 탱커 포함 전원
                            else e.Intent = StrongestAttacker() ?? LowestNonTank();
                        }
                        else e.AoeIntent = true; // 격노: 폭풍이 쉬지 않는다 (문서·도감 명세와 동일)
                        break;
                    case AiKind.Bomber:
                        e.Intent = null;
                        e.BombTimer--; // 표시: 남은 턴, 0이면 이번 해소에 폭발
                        break;
                    // ---- v1.0 신규 적 행동 ----
                    case AiKind.Sealer:
                        e.Intent = null;
                        // 손패 1장 봉인 — 결정론: 손패 크기 기준 회전 (턴마다 다른 카드)
                        if (Hand.Count > 0 && e.TauntTurns == 0) SealedCard = (Turn - 1) % Hand.Count;
                        break;
                    case AiKind.Gazer:
                        e.Intent = null;
                        if (e.TauntTurns == 0)
                        {
                            // 석화 — 결정론: 살아있는 공격수 중 공격력 최고 (없으면 최저 HP 비탱커)
                            var pz = StrongestAttacker() ?? LowestNonTank();
                            if (pz != null && !pz.IsTank) { pz.Petrified = true; e.CurseIntent = pz; }
                        }
                        break;
                    case AiKind.Dispeller:
                        e.Intent = null;
                        break;
                    case AiKind.Rotmancer:
                        e.Intent = null;
                        if (e.TauntTurns == 0) HealBlockedTurn = true; // 이번 턴 회복 무효 (예고)
                        break;
                    case AiKind.Burrower:
                        // 홀수 턴 잠복(무행동·피해 무효) / 짝수 턴 강타
                        e.Hidden = (e.Step % 2 == 1);
                        e.Intent = e.Hidden ? null : (StrongestAttackerFor(e) ?? LowestNonTankFor(e));
                        break;
                    case AiKind.Sentinel:
                        if (e.WakeTimer > 0) { e.WakeTimer--; e.Intent = null; e.Charging = true; }
                        else e.Intent = StrongestAttackerFor(e) ?? LowestNonTankFor(e);
                        break;
                    case AiKind.TwinBlade:
                        // 짝이 죽으면 광분 — 배수는 EnemyPow에서 계산 (예측·해소 공용)
                        e.Intent = StrongestAttackerFor(e) ?? LowestNonTankFor(e);
                        break;
                    case AiKind.BroodBoss:
                        var bn = CheckEnrage(e, b.broodEnrageHp, 0, Loc.F("log.enrageBrood", e.Name));
                        if (bn != null) { notice = bn; e.Step = 1; }
                        int bp = (e.Step - 1) % 3;
                        if (!e.Enraged && bp == 0) { e.Intent = null; e.Charging = true; }   // 산란(각성 전 준비)
                        else if (bp == 1 || (e.Enraged && e.Step % 2 == 1)) e.AoeIntent = true;
                        else e.Intent = SmartBackliner(e);
                        break;
                    case AiKind.ColossusBoss:
                        // 격노 시 강타 2회 = 연타 취급 (SpiderDouble와 같은 다단 경로 재사용)
                        var cn = CheckEnrage(e, b.colossusEnrageHp, 0, Loc.F("log.enrageColossus", e.Name));
                        if (cn != null) { notice = cn; e.Step = 1; }
                        int cp = (e.Step - 1) % 3;
                        if (!e.Enraged && cp == 0) { e.Intent = null; e.Charging = true; }
                        else if (cp == 1) e.AoeIntent = true;
                        else e.Intent = StrongestAttacker() ?? LowestNonTank();
                        break;
                    case AiKind.WyrmBoss:
                        var wn = CheckEnrage(e, b.wyrmEnrageHp, 0, Loc.F("log.enrageWyrm", e.Name));
                        if (wn != null) { notice = wn; e.Step = 1; }
                        int wp = (e.Step - 1) % 3;
                        if (wp == 0) e.AoeIntent = true;                       // 화염 브레스 — 탱커 포함 전원
                        else if (wp == 1) e.Intent = StrongestAttacker() ?? LowestNonTank(); // 꼬리 강타
                        else if (!e.Enraged) { e.Intent = null; e.Hidden = true; }           // 비행 — 이번 턴 회피
                        else e.AoeIntent = true;                                             // 격노: 비행 대신 브레스
                        break;
                    case AiKind.Thief:
                        e.Intent = null; // 골드 노림 — 인텐트 라벨 별도 처리
                        break;
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
                        e.Intent = StrongestAttackerFor(e) ?? LowestNonTankFor(e); // 근접 — 진형 후보 집합 준수
                        break;
                    case AiKind.LowestBackliner:
                        if (e.Pack)
                        {
                            if (packTarget == null || !packTarget.Alive) packTarget = SmartBackliner(e);
                            e.Intent = packTarget;
                        }
                        else e.Intent = SmartBackliner(e);
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
            // 백라이너 교대: 최강 공격수 ↔ 힐러(없으면 최저 HP) — 진형 후보 집합 안에서 (브루트도 근접)
            var strong = StrongestAttackerFor(e);
            var healerU = HealerUnit();
            var healer = healerU != null && !RowBlocked(e, healerU) ? healerU : LowestNonTankFor(e);
            var t = e.SmashToDps ? (strong ?? healer) : (healer ?? strong);
            e.SmashToDps = !e.SmashToDps;
            return t ?? Tank;
        }

        // ---- 예측 (UI와 해소가 같은 계산) ----

        CardType? PlannedType => PlannedCard >= 0 && PlannedCard < Hand.Count ? Hand[PlannedCard].Type : (CardType?)null;
        bool PlannedPlus => PlannedCard >= 0 && PlannedCard < Hand.Count && Hand[PlannedCard].Plus;

        /// 보스는 밀치기 면역 — 워로드·리치 공통 판정 (개별 AiKind 비교 금지)
        public static bool BossImmune(Unit u) => u.Ai == AiKind.BossWarlord || u.Ai == AiKind.LichBoss
            || u.Ai == AiKind.BroodBoss || u.Ai == AiKind.ColossusBoss || u.Ai == AiKind.WyrmBoss;

        public bool IsTauntedNow(Unit e) => e.TauntTurns > 0
            || (PlannedType == CardType.Taunt && PlannedTarget == e)
            || PlannedType == CardType.WarCry; // 도발 함성 예약 = 전원 도발 — 프리뷰도 같은 세계를 본다
        public bool IsStunnedNow(Unit e) => e.Stunned || e.Feinted || e.Hidden
            || (PlannedType == CardType.Shove && PlannedTarget == e && !BossImmune(e))
            || (PlannedType == CardType.Feint && PlannedTarget == e); // 교란은 보스에게도 통한다

        /// 적 공격력 — 위축 절반, 쌍둥이 광분 2배, 유언 버프·교란 반동 가산 (예측·해소 공용)
        int EnemyPowRaw(Unit e)
        {
            int p = e.Power + e.PowerBonus + e.FeintBonus;
            if (e.Ai == AiKind.TwinBlade)
            {
                bool alone = true;
                foreach (var o in Enemies) if (o != e && o.Alive && o.Ai == AiKind.TwinBlade) alone = false;
                if (alone) p *= 2;
            }
            return p;
        }
        int EnemyPow(Unit e) => e.Shaken ? EnemyPowRaw(e) / 2 : EnemyPowRaw(e);
        public int EnemyPowPublic(Unit e) => EnemyPow(e);   // UI 표시용 (환경 피해 등)
        int EnemyAoePow(Unit e) => e.Shaken ? (e.AoePower + e.PowerBonus) / 2 : e.AoePower + e.PowerBonus;

        // ---- v1.0 예약 반영 헬퍼 (프리뷰·해소 공용) ----
        public Unit BlessPreview => BlessTarget ?? (PlannedType == CardType.Bless ? PlannedTarget : null);
        Unit RepositionPreview => PlannedType == CardType.Reposition ? PlannedTarget : null;
        Unit WarsongPreview => WarsongTarget ?? (PlannedType == CardType.Warsong ? PlannedTarget : null);
        int WarsongMultNow => WarsongTarget != null ? WarsongMult
            : PlannedType == CardType.Warsong ? (PlannedPlus ? Balance.I.warsongMultPlus : Balance.I.warsongMult) : 1;
        int InspireNow => InspireAmt > 0 ? InspireAmt
            : PlannedType == CardType.Inspire ? (PlannedPlus ? Balance.I.inspireAtkPlus : Balance.I.inspireAtk) : 0;
        int VanguardNow => VanguardAmt > 0 ? VanguardAmt
            : PlannedType == CardType.Vanguard ? (PlannedPlus ? Balance.I.vanguardAtkPlus : Balance.I.vanguardAtk) : 0;
        int RearguardNow => RearguardAmt > 0 ? RearguardAmt
            : PlannedType == CardType.Rearguard ? (PlannedPlus ? Balance.I.rearguardReducePlus : Balance.I.rearguardReduce) : 0;
        int FortressNow => FortressActive ? FortressAlly
            : PlannedType == CardType.Fortress ? (PlannedPlus ? Balance.I.fortressAllyPlus : Balance.I.fortressAlly) : 0;
        bool FortressOn => FortressActive || PlannedType == CardType.Fortress;
        int RepositionGuardNow => PlannedType == CardType.Reposition ? (PlannedPlus ? Balance.I.repositionGuardPlus : Balance.I.repositionGuard) : 0;
        bool InvulnNow => InvulnTurn || (PlannedType == CardType.LastStand && Tank.Hp * 2 <= Tank.MaxHp);
        Unit SilencePreview => SilenceTarget ?? (PlannedType == CardType.Silence ? PlannedTarget : null);
        int StudyReduceNow(Unit e) => (StudyTarget == e ? StudyReduce : 0)
            + (PlannedType == CardType.Study && PlannedTarget == e ? (PlannedPlus ? Balance.I.studyReducePlus : Balance.I.studyReduce) : 0);
        int AegisCapNow => AegisTurns > 0 ? AegisCap : 0;
        int BarricadeNow => BarricadeTurns > 0 ? BarricadeAmt
            : PlannedType == CardType.Barricade ? (PlannedPlus ? Balance.I.barricadeAmtPlus : Balance.I.barricadeAmt) : 0;

        /// 진형 행 — 예약 교대 반영 (프리뷰·해소 공용)
        int RowOf(Unit a) => RepositionPreview == a ? (a.Row == 0 ? 1 : 0) : a.Row;

        /// 이번 턴 탱커 직격 반사량 — 예약 가시 태세 포함 (프리뷰 공용)
        int ThornsNow => (ThornStanceAmt > 0 ? ThornStanceAmt
                : PlannedType == CardType.ThornStance
                    ? (PlannedPlus ? Balance.I.thornStanceDmgPlus : Balance.I.thornStanceDmg) : 0)
            + (run != null && run.Has(RelicId.ThornShield) ? Balance.I.relicThornShield : 0);
        public bool AoeActive(Unit e) => e.AoeIntent && !IsTauntedNow(e) && !IsStunnedNow(e);
        public Unit CoverPreview => CoverTarget ?? (PlannedType == CardType.Cover ? PlannedTarget : null);
        public Unit ShieldPreview => PlannedType == CardType.Shield ? PlannedTarget : null;
        bool BracingNow => Bracing || PlannedType == CardType.Brace;
        bool PhalanxNow => PhalanxActive || PlannedType == CardType.Phalanx;
        bool OathNow => OathTurns > 0 || PlannedType == CardType.Oath;
        bool IronWillNow => IronWillActive || PlannedType == CardType.IronWill;
        public Unit MarkPreview => MarkTarget ?? (PlannedType == CardType.GuardianMark ? PlannedTarget : null);
        int CoverBonusNow => CoverBonus > 0 ? CoverBonus
            : PlannedType == CardType.Cover && PlannedPlus ? Balance.I.coverPlusReduce : 0;
        int PhalanxNowAmt => PhalanxActive ? PhalanxAmt
            : PlannedType == CardType.Phalanx ? (PlannedPlus ? Balance.I.phalanxReducePlus : Balance.I.phalanxReduce) : 0;
        int IronWillNowCap => IronWillActive ? (IronWillPlus ? Balance.I.ironWillCapPlus : Balance.I.ironWillCap)
            : PlannedPlus && PlannedType == CardType.IronWill ? Balance.I.ironWillCapPlus : Balance.I.ironWillCap;
        int MarkReduce => (MarkPlus || (MarkTarget == null && PlannedType == CardType.GuardianMark && PlannedPlus)
                           ? Balance.I.markPlusReduce : 0)
                        + (run != null && run.Has(RelicId.GuardCharm) ? Balance.I.relicGuardCharm : 0);

        public Unit EffectiveTarget(Unit enemy)
        {
            if (enemy.Charging || IsStunnedNow(enemy)) return null;
            if (enemy.Ai == AiKind.ShamanCurse || enemy.Ai == AiKind.EnemyHealer
                || enemy.Ai == AiKind.Bomber || enemy.Ai == AiKind.Thief) return null;
            if (IsTauntedNow(enemy)) return Tank;
            if (enemy.AoeIntent || enemy.Intent == null) return null;
            var cover = CoverPreview;
            if (cover != null && enemy.Intent == cover && !enemy.Pierce) return Tank; // 관통 적은 엄호 무시
            return enemy.Intent;
        }

        /// 통합 피해 계산 — 예측과 해소가 이 함수 하나를 쓴다.
        /// hitIndex: 연타에서 몇 번째 타인가 (엄호는 0타만 리다이렉트).
        /// 고블린 대장 오라(+대장 외 전원) + 늑대 무리(살아있는 다른 늑대 수만큼 +)
        int AuraBonus(Unit attacker, HashSet<Unit> killedPreview = null)
        {
            if (attacker.Team != Team.Enemy) return 0;
            if (run != null && run.Has(RelicId.Lodestone)) return 0;   // 자철석 — 적 오라·무리 무효
            bool AliveNow(Unit e) => e.Alive && (killedPreview == null || !killedPreview.Contains(e));
            var sil = SilencePreview;
            int bonus = 0;
            if (!attacker.Aura)
                foreach (var e in Enemies) if (AliveNow(e) && e.Aura && e != sil) { bonus += Balance.I.chiefAura; break; }
            if (attacker.Pack && attacker != sil)
                foreach (var e in Enemies) if (AliveNow(e) && e.Pack && e != attacker) bonus += Balance.I.packBonus;
            return bonus;
        }

        /// 저주 갑옷 — 받는 한 방 피해 상한 (아군 공격·노바·가시 전부 적용, 예측 공용)
        static int CapTo(Unit target, int dmg) =>
            target.DamageCap > 0 ? Mathf.Min(dmg, target.DamageCap) : dmg;

        int ComputeDamage(Unit enemy, Unit receiver, int baseDmg, bool viaTaunt, bool viaCover,
                          HashSet<Unit> killedPreview = null, bool aoe = false)
        {
            int dmg = baseDmg + AuraBonus(enemy, killedPreview);
            if (aoe && run != null && run.Has(RelicId.WardStone))
                dmg = Mathf.Max(0, dmg - Balance.I.relicWardStone);   // 방호석 — 광역 감산 (예측·해소 공용)
            if (viaTaunt) dmg = Mathf.Max(0, dmg - TauntGuardAmt);
            if (viaCover) dmg = Mathf.Max(0, dmg - CoverReduceAmt - CoverBonusNow);
            if (PhalanxNow && receiver.Team == Team.Ally) dmg = Mathf.Max(0, dmg - PhalanxNowAmt);
            if (run != null && run.Formation == 3 && receiver.Team == Team.Ally && !receiver.IsTank)
                dmg = Mathf.Max(0, dmg - Balance.I.formGuardReduce); // 보호 진형 — 동료 피해 감산
            // v1.0: 지속 방벽 / 요새화 / 후위 정렬 / 종사 / 창병 / 교대 보호 / 관찰 / 완전 방어
            if (receiver.Team == Team.Ally && BarricadeNow > 0) dmg = Mathf.Max(0, dmg - BarricadeNow);
            if (receiver.Team == Team.Ally && FortressOn && !receiver.IsTank) dmg = Mathf.Max(0, dmg - FortressNow);
            if (receiver.Team == Team.Ally && !receiver.IsTank && RowOf(receiver) == 1 && RearguardNow > 0)
                dmg = Mathf.Max(0, dmg - RearguardNow);
            if (receiver.IsTank && HasLivingTrait(Trait.Bulwark)) dmg = Mathf.Max(0, dmg - 1);          // 종사
            if (run != null && receiver.Team == Team.Ally && !receiver.IsTank && run.Has(RelicId.ThickHide))
                dmg = Mathf.Max(0, dmg - Balance.I.relicThickHide);                                      // 두꺼운 가죽
            if (run != null && viaTaunt && run.Has(RelicId.RunedChain))
                dmg = Mathf.Max(0, dmg - Balance.I.relicRunedChain);                                     // 룬 사슬
            if (receiver.Team == Team.Ally && !receiver.IsTank && RowOf(receiver) == 0 && LancerGuardTarget() == receiver)
                dmg = Mathf.Max(0, dmg - Balance.I.lancerGuard);                                        // 창병 보호
            if (RepositionPreview == receiver) dmg = Mathf.Max(0, dmg - RepositionGuardNow);
            dmg = Mathf.Max(0, dmg - StudyReduceNow(enemy));
            if (BlessPreview == receiver) return 0;                                                     // 완전 방어
            if (receiver.IsTank && InvulnNow) return 0;                                                 // 최후의 저항
            if (OathNow && receiver.IsTank) dmg = Mathf.Max(0, dmg - Balance.I.oathReduce);
            if (receiver.IsTank && (BracingNow || FortressOn)) dmg /= 2;                                // 버티기·요새화
            if (receiver.IsTank && IronWillNow) dmg = Mathf.Min(dmg, IronWillNowCap); // 철의 의지: 한 방 상한
            if (receiver.IsTank && AegisCapNow > 0) dmg = Mathf.Min(dmg, AegisCapNow); // 수호 방벽: 지속 상한
            if (receiver.IsTank && run != null && run.Has(RelicId.TowerShield))
                dmg = Mathf.Min(dmg, Balance.I.relicTowerShield);                       // 타워 실드: 상시 한 방 상한
            return dmg;
        }

        // 프리뷰 계산은 아군 페이즈 선행 처치를 반영한다 (플레이어 페이즈에서만 유효)
        HashSet<Unit> KilledPreview => Phase == Phase.Player ? PredictKills() : null;

        public int EffectiveDamage(Unit enemy)
        {
            var target = EffectiveTarget(enemy);
            if (target == null) return 0;
            bool taunted = IsTauntedNow(enemy);
            bool viaCover = !taunted && target.IsTank && CoverPreview != null && enemy.Intent == CoverPreview;
            int perHit = ComputeDamage(enemy, target, EnemyPow(enemy), taunted && target.IsTank, viaCover, KilledPreview);
            int hits = enemy.Ai == AiKind.SpiderDouble || (enemy.Ai == AiKind.ColossusBoss && enemy.Enraged) ? 2 : 1;
            // 거미 2타: 도발이면 둘 다 탱커, 엄호면 첫 타만 — 라벨엔 수신자 기준 합계
            if (hits == 2 && viaCover) return perHit; // 엄호 수신(탱커) 몫은 첫 타만
            return perHit * hits;
        }

        public int EffectiveAoeDamage(Unit enemy, Unit backliner)
        {
            bool covered = CoverPreview == backliner;
            var receiver = covered ? Tank : backliner;
            return ComputeDamage(enemy, receiver, EnemyAoePow(enemy), false, covered, KilledPreview, aoe: true);
        }

        /// 이번 턴 이 유닛이 받을 예상 총 피해 (예약·선행 처치·기절·방패·수호 낙인 분담 반영)
        public int IncomingPreview(Unit u)
        {
            // 이번 턴 부여될 독(역병 쥐)을 먼저 집계 — 예고된 정보이므로 프리뷰에 포함
            foreach (var a in Allies) a.PoisonIncoming = 0;
            foreach (var pe in Enemies)
            {
                if (!pe.Alive || !pe.PoisonHit || IsStunnedNow(pe) || pe.Charging || SilencePreview == pe) continue;
                var pt = EffectiveTarget(pe);
                if (pt != null) pt.PoisonIncoming += Balance.I.ratPoison;
            }
            var killed = PredictKills();
            var hpAfter = HpAfterAllyPhase();
            int thorns = ThornsNow;
            int shieldLeft = u.ShieldCharges
                + (ShieldPreview == u ? (PlannedPlus ? Balance.I.shieldPlusCharges : 1) : 0);
            var mark = MarkPreview;
            int sum = 0;

            // 타격 1건을 u 관점 합계에 반영 — 낙인 아군이면 분담분 제외, 탱커면 낙인 분담분 가산
            void Add(Unit recv, int d, bool pierce = false)
            {
                if (d <= 0) return;
                if (pierce) { if (recv == u) sum += d; return; } // 관통: 방패·분담 무시 직격
                int share = mark != null && recv == mark && !recv.IsTank && d > 1
                    ? Mathf.Max(0, d / 2 - MarkReduce) : 0;
                if (recv == u)
                {
                    if (shieldLeft > 0) { shieldLeft--; return; } // 방패는 그 한 방 전체 무효 (분담 포함)
                    sum += d - share;
                }
                else if (u.IsTank && share > 0) sum += share;
            }

            foreach (var e in Enemies)
            {
                if (!e.Alive || e.Charging || killed.Contains(e) || IsStunnedNow(e)) continue;
                if (e.Ai == AiKind.ShamanCurse || e.Ai == AiKind.EnemyHealer || e.Ai == AiKind.Thief) continue;
                if (e.Ai == AiKind.Sealer || e.Ai == AiKind.Gazer || e.Ai == AiKind.Dispeller
                    || e.Ai == AiKind.Rotmancer) continue;              // 공격하지 않는 특수형
                if (e.Env)
                {
                    // 가시 덩굴 — 도발·엄호 무시 환경 피해 (전원 고정)
                    if (u.Alive) sum += EnemyPow(e);
                    continue;
                }

                // 가시 반사 사망 미러 — 탱커를 때리다 죽으면 남은 타격이 중단된다 (해소와 같은 순서)
                int eHp = hpAfter.TryGetValue(e, out var hv) ? hv : e.Hp;
                bool ThornKill(Unit recv, int d)
                {
                    if (thorns <= 0 || d <= 0 || !recv.IsTank) return false;
                    eHp -= CapTo(e, thorns);
                    return eHp <= 0;
                }

                if (e.Ai == AiKind.Bomber)
                {
                    // 폭발 예정 턴이면 전원 광역이 프리뷰에 잡혀야 한다 (자폭은 반사로도 안 끊김 — 해소 동일)
                    if (e.BombTimer <= 0)
                        foreach (var al in Allies)
                            if (al.Alive) Add(al, ComputeDamage(e, al, Balance.I.bomberBlast, false, false, killed));
                    continue;
                }
                if (AoeActive(e))
                {
                    foreach (var b in Allies)
                    {
                        if (!b.Alive) continue;
                        if (b.IsTank && e.Ai != AiKind.LichBoss && e.Ai != AiKind.WyrmBoss) continue; // 리치 폭풍·용 브레스는 탱커 포함
                        bool covered = !b.IsTank && CoverPreview == b;
                        var recvA = covered ? Tank : b;
                        int dA = EffectiveAoeDamage(e, b);
                        Add(recvA, dA);
                        if (ThornKill(recvA, dA)) break;
                    }
                    continue;
                }
                var recvS = EffectiveTarget(e);
                if (recvS == null) continue;
                int hits = e.Ai == AiKind.SpiderDouble || (e.Ai == AiKind.ColossusBoss && e.Enraged) ? 2 : 1;
                bool taunted = IsTauntedNow(e);
                bool viaCover = !taunted && recvS.IsTank && CoverPreview != null && e.Intent == CoverPreview && !e.Pierce;
                for (int hi = 0; hi < hits; hi++)
                {
                    // 거미 2타 + 엄호: 둘째 타는 원 대상에게 — 탱커 몫은 첫 타만
                    if (hits == 2 && viaCover && hi == 1)
                    {
                        Add(e.Intent, ComputeDamage(e, e.Intent, EnemyPow(e), false, false, killed));
                        break;
                    }
                    int dS = ComputeDamage(e, recvS, EnemyPow(e), taunted && recvS.IsTank, viaCover, killed);
                    Add(recvS, dS, e.Pierce);
                    if (ThornKill(recvS, dS)) break;
                }
            }
            // 독 진행 — 이번 적 페이즈 종료에 들어올 피해 (예약 정화·해독으로 지워지면 0)
            bool cleansed = PlannedType == CardType.Purge
                         || (PlannedType == CardType.Antidote && PlannedTarget == u);
            if (!cleansed) sum += (u.Poison + u.PoisonIncoming) * Balance.I.poisonTick;

            // 저주 인형 피해 전가 — 아군 페이즈에 최고 HP 아군이 대신 받는다 (결정론 근사)
            if (!u.IsTank && u.Alive)
            {
                Unit victim = null;
                foreach (var al in Allies) if (al.Alive && !al.IsTank && (victim == null || al.Hp > victim.Hp)) victim = al;
                if (victim == u)
                    foreach (var e in Enemies)
                        if (e.Alive && e.Reflector && SilencePreview != e) sum += Mathf.Max(1, e.Power);
            }
            return sum;
        }

        // 킬 예측은 StateVersion당 1회만 시뮬 — 아군 수만큼 반복 호출돼도 재계산 없음
        HashSet<Unit> killsCache;
        Dictionary<Unit, int> hpAfterCache;
        int killsCacheVersion = -1;

        HashSet<Unit> PredictKills()
        {
            if (killsCacheVersion != StateVersion || killsCache == null)
            {
                killsCache = SimulateAllyPhase(out hpAfterCache);
                killsCacheVersion = StateVersion;
            }
            return killsCache;
        }

        /// 아군 페이즈 후 적 잔여 HP (가시 반사 사망 프리뷰용) — PredictKills와 같은 시뮬 공유
        Dictionary<Unit, int> HpAfterAllyPhase() { PredictKills(); return hpAfterCache; }

        HashSet<Unit> SimulateAllyPhase(out Dictionary<Unit, int> hpOut)
        {
            // 아군 공격 페이즈 시뮬 — 처치될 적의 공격은 프리뷰에서 제외
            var killed = new HashSet<Unit>();
            var hp = new Dictionary<Unit, int>();
            foreach (var e in Enemies) if (e.Alive) hp[e] = e.Hp;
            var shaken = new Dictionary<Unit, bool>();
            var momentum = new Dictionary<Unit, int>();
            var lastT = new Dictionary<Unit, Unit>();
            var shield = new Dictionary<Unit, int>();
            foreach (var a in Allies) { shaken[a] = a.Shaken; momentum[a] = a.Momentum; lastT[a] = a.LastTarget; }
            foreach (var e in Enemies) if (e.Alive && e.ShieldCharges > 0) shield[e] = e.ShieldCharges;
            var counter = new Dictionary<Unit, bool>();
            foreach (var e in Enemies) if (e.Alive && e.CounterOnce) counter[e] = true; // 거울 정령 반사 1회
            int HpOf(Unit e) => hp.TryGetValue(e, out var v) && !killed.Contains(e) && !e.Hidden ? v : 0;

            foreach (var a in Allies)
            {
                if (!a.Alive || a.Role != Role.Attacker) continue;
                bool purged = PlannedType == CardType.Purge
                           || (PlannedType == CardType.Antidote && PlannedTarget == a)
                           || (PlannedType == CardType.Bless && PlannedTarget == a && PlannedPlus)
                           || (PlannedType == CardType.Warsong && PlannedTarget == a && PlannedPlus);
                if (a.Petrified && PlannedType != CardType.Purge) continue;    // 석화 = 행동 취소 (정화로 해제)
                if (purged) shaken[a] = false;                                  // 위축 해제 반영

                // 노병 사수 — 두 적을 각각 절반 피해로 동시 사격
                if (a.Trait == Trait.Volley)
                {
                    int half = Mathf.Max(1, AllyDamage(a, shaken[a], momentum[a]) / 2);
                    shaken[a] = false;
                    Unit prevT = null;
                    for (int v = 0; v < 2; v++)
                    {
                        Unit vt = null;
                        foreach (var e in Enemies)
                        {
                            if (HpOf(e) <= 0 || e == prevT || e.Hidden) continue;
                            if (vt == null || HpOf(e) < HpOf(vt)) vt = e;
                        }
                        if (vt == null) break;
                        prevT = vt;
                        if (counter.TryGetValue(vt, out var cv) && cv) { counter[vt] = false; continue; }
                        if (shield.TryGetValue(vt, out var sv) && sv > 0) { shield[vt] = sv - 1; continue; }
                        hp[vt] -= CapTo(vt, half);
                        if (hp[vt] <= 0) killed.Add(vt);
                    }
                    continue;
                }

                if (a.Trait == Trait.ArcaneNova && Turn % Balance.I.novaEvery == 0)
                {
                    int nova = Mathf.Max(1, AllyDamage(a, shaken[a], 0) / 2);
                    shaken[a] = false;
                    foreach (var e in Enemies)
                    {
                        if (HpOf(e) <= 0) continue;
                        if (shield.TryGetValue(e, out var sc2) && sc2 > 0) { shield[e] = sc2 - 1; continue; }
                        hp[e] -= CapTo(e, nova);
                        if (hp[e] <= 0) killed.Add(e);
                    }
                    continue;
                }

                for (int strike = 0; strike < 2; strike++)
                {
                    int dmg = AllyDamage(a, shaken[a], momentum[a]);
                    var tgt = a.Trait == Trait.Sniper ? HighestPowerEnemy(HpOf)
                        : PickAttackTarget(dmg, HpOf, e2 => shield.TryGetValue(e2, out var s2) ? s2 : 0);
                    if (tgt == null) break;
                    if (a.Trait == Trait.Momentum)
                    {
                        momentum[a] = tgt == lastT[a] ? momentum[a] + 1 : 0;
                        lastT[a] = tgt;
                        dmg = AllyDamage(a, shaken[a], momentum[a]);
                    }
                    shaken[a] = false;
                    if (counter.TryGetValue(tgt, out var cc) && cc) { counter[tgt] = false; break; }     // 거울 정령 미러
                    if (shield.TryGetValue(tgt, out var sc) && sc > 0) { shield[tgt] = sc - 1; break; } // 방패병 미러
                    hp[tgt] -= CapTo(tgt, dmg);
                    bool died = hp[tgt] <= 0;
                    if (died) killed.Add(tgt);
                    // 음유시인 정화를 해소와 같은 순서로 반영 — 뒤 순번 공격수의 피해가 달라진다
                    if (a.Trait == Trait.Cleanse)
                        foreach (var ally in Allies)
                            if (ally.Alive && shaken[ally]) { shaken[ally] = false; break; }
                    if (!(a.Trait == Trait.KillChain && died && strike == 0)) break;
                }
            }
            hpOut = hp;
            return killed;
        }

        // ---- 플레이어 입력 ----

        public bool CanUseSkill => Phase == Phase.Player;

        public bool CardPlayable(int idx)
        {
            if (idx < 0 || idx >= Hand.Count) return false;
            if (idx == SealedCard) return false;                        // 주술 방해자 봉인
            var c = Hand[idx];
            var b = Balance.I;
            switch (c.Type)
            {
                case CardType.Devotion: return Tank.Hp > (c.Plus ? b.devotionAmountPlus : b.devotionAmount);
                case CardType.Awaken: return Tank.Hp > (c.Plus ? b.awakenCostPlus : b.awakenCost);
                case CardType.LastStand: return Tank.Hp * 2 <= Tank.MaxHp; // 저체력 전용
                case CardType.Sacrifice:
                {
                    foreach (var a in Allies) if (a.Alive && !a.IsTank && a.Hp < a.MaxHp) return Tank.Hp > 1;
                    return false;
                }
                default: return true;
            }
        }

        public void PressCard(int idx)
        {
            if (!CanUseSkill || !CardPlayable(idx)) return;
            StateVersion++;
            if (PendingCard == idx) { PendingCard = -1; Log = Loc.T("log.selCancel"); return; }
            if (PlannedCard == idx)
            {
                PlannedCard = -1; PlannedTarget = null; PendingCard = -1;
                Log = Loc.T("log.planCancel");
                return;
            }
            PlannedCard = -1; PlannedTarget = null;
            var t = Cards.TargetOf(Hand[idx].Type);
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
            StateVersion++;
            var card = Hand[PendingCard];
            var need = Cards.TargetOf(card.Type);
            if (need == CardTarget.Enemy && u.Team == Team.Enemy)
            {
                if (u.Hidden) { Log = Loc.T("log.hiddenTarget"); return; }   // 잠복 중엔 조준 불가
                if (card.Type == CardType.Shove && BossImmune(u)) { Log = Loc.T("log.shoveBossImmune"); return; }
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
            if (extraPlay)
            {
                if (SealedCard > plannedRemoved) SealedCard--;         // 손패 축소분 보정
                else if (SealedCard == plannedRemoved) SealedCard = -1;
                // 각성 — 이번 턴 카드를 한 장 더 사용한다 (턴당 1장 규칙을 깨는 유일 경로)
                extraPlay = false;
                PlannedCard = -1; PlannedTarget = null;
                StateVersion++;
                Log = Loc.T("log.extraPlay");
                return;
            }
            StartCoroutine(Resolve());
        }

        void CommitPlanned()
        {
            if (PlannedCard >= 0 && PlannedCard < Hand.Count)
            {
                var card = Hand[PlannedCard];
                bool plus = card.Plus;
                var b = Balance.I;
                switch (card.Type)
                {
                    case CardType.Taunt:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            PlannedTarget.TauntTurns = plus ? b.tauntDurationPlus + TauntDuration - b.tauntDuration : TauntDuration;
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.taunt"), new Color(1f, 0.55f, 0.35f));
                        }
                        break;
                    case CardType.Cover:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            CoverTarget = PlannedTarget;
                            CoverBonus = plus ? b.coverPlusReduce : 0;
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.cover"), new Color(0.55f, 0.75f, 1f));
                        }
                        break;
                    case CardType.Brace:
                        Bracing = true; bracedTaken = 0;
                        // 단단한 각오(BraceBonus)는 기본·강화 양쪽에 적용 — BraceHeal이 이미 보정 포함
                        BraceCap = plus ? Plus(b.braceHealPlus) + BraceHeal - b.braceHeal : BraceHeal;
                        break;
                    case CardType.Shield:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            PlannedTarget.ShieldCharges = plus ? b.shieldPlusCharges : 1;
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.shield"), new Color(0.7f, 0.85f, 1f));
                        }
                        break;
                    case CardType.Shove:
                        if (PlannedTarget != null && PlannedTarget.Alive && !BossImmune(PlannedTarget))
                        {
                            PlannedTarget.Stunned = true;
                            if (plus) PlannedTarget.Shaken = true; // 강화: 취소 + 위축
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.shove"), new Color(1f, 0.8f, 0.5f));
                        }
                        break;
                    case CardType.Devotion:
                        int dev = plus ? b.devotionAmountPlus : b.devotionAmount;
                        if (PlannedTarget != null && PlannedTarget.Alive && Tank.Hp > dev)
                        {
                            Tank.Hp -= dev;
                            PlannedTarget.Hp = Mathf.Min(PlannedTarget.MaxHp, PlannedTarget.Hp + dev);
                            Popup?.Invoke(PlannedTarget, "+" + dev, new Color(0.55f, 1f, 0.55f));
                        }
                        break;
                    case CardType.Rally:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            PlannedTarget.Shaken = false;
                            if (plus) PlannedTarget.Hp = Mathf.Min(PlannedTarget.MaxHp, PlannedTarget.Hp + b.rallyPlusHeal);
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.rally"), new Color(1f, 0.95f, 0.6f));
                        }
                        break;
                    case CardType.Phalanx:
                        PhalanxActive = true;
                        PhalanxAmt = plus ? b.phalanxReducePlus : b.phalanxReduce;
                        Popup?.Invoke(Tank, Loc.T("pop.phalanx"), new Color(0.8f, 0.9f, 1f));
                        break;
                    case CardType.Oath:
                        OathTurns = plus ? b.oathTurnsPlus : b.oathTurns;
                        Popup?.Invoke(Tank, Loc.T("pop.oath"), new Color(1f, 0.84f, 0.37f));
                        break;
                    case CardType.IronWill:
                        IronWillActive = true; IronWillPlus = plus;
                        Popup?.Invoke(Tank, Loc.T("pop.ironwill"), new Color(0.75f, 0.8f, 0.95f));
                        break;
                    case CardType.GuardianMark:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            MarkTarget = PlannedTarget; MarkPlus = plus;
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.mark"), new Color(1f, 0.84f, 0.37f));
                        }
                        break;
                    case CardType.Respite:
                        int rsp = plus ? b.respiteHealPlus : b.respiteHeal;
                        Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + rsp);
                        AudioKit.Heal();
                        Popup?.Invoke(Tank, "+" + rsp, new Color(0.55f, 1f, 0.55f));
                        break;
                    case CardType.ThornStance:
                        ThornStanceAmt = plus ? b.thornStanceDmgPlus : b.thornStanceDmg;
                        Popup?.Invoke(Tank, Loc.T("pop.thornstance"), new Color(0.95f, 0.6f, 0.35f));
                        break;
                    case CardType.FirstAid:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            int aid = plus ? b.firstAidHealPlus : b.firstAidHeal;
                            PlannedTarget.Hp = Mathf.Min(PlannedTarget.MaxHp, PlannedTarget.Hp + aid);
                            AudioKit.Heal();
                            Popup?.Invoke(PlannedTarget, "+" + aid, new Color(0.55f, 1f, 0.55f));
                        }
                        break;
                    case CardType.Regroup:
                        RetainHand = true;
                        if (plus) BonusDraw = 1;
                        Popup?.Invoke(Tank, Loc.T("pop.regroup"), new Color(0.8f, 0.9f, 1f));
                        break;
                    // ---- v1.0 신규 카드 ----
                    case CardType.Reposition:
                        if (PlannedTarget != null && PlannedTarget.Alive && !PlannedTarget.IsTank)
                        {
                            PlannedTarget.Row = PlannedTarget.Row == 0 ? 1 : 0;
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.reposition"), new Color(0.6f, 0.85f, 1f));
                        }
                        break;
                    case CardType.Vanguard:
                        VanguardAmt = plus ? Plus(b.vanguardAtkPlus) : b.vanguardAtk;
                        break;
                    case CardType.Rearguard:
                        RearguardAmt = plus ? Plus(b.rearguardReducePlus) : b.rearguardReduce;
                        break;
                    case CardType.Barricade:
                        BarricadeAmt = plus ? Plus(b.barricadeAmtPlus) : b.barricadeAmt;
                        BarricadeTurns = plus ? b.barricadeTurnsPlus : b.barricadeTurns;
                        Popup?.Invoke(Tank, Loc.T("pop.barricade"), new Color(0.8f, 0.8f, 0.9f));
                        break;
                    case CardType.Vow:
                        VowTurns = plus ? b.vowTurnsPlus : b.vowTurns;
                        Popup?.Invoke(Tank, Loc.T("pop.vow"), new Color(1f, 0.9f, 0.6f));
                        break;
                    case CardType.Aegis:
                        AegisCap = b.aegisCap;
                        AegisTurns = plus ? b.aegisTurnsPlus : b.aegisTurns;
                        break;
                    case CardType.Awaken:
                        int cost = plus ? b.awakenCostPlus : b.awakenCost;
                        if (Tank.Hp > cost && !awakenUsed)
                        {
                            Tank.Hp -= cost;
                            awakenUsed = true;  // 턴당 1회 — 체인 금지
                            extraPlay = true;   // 이번 턴 카드 한 장 더
                            Popup?.Invoke(Tank, Loc.T("pop.awaken"), new Color(1f, 0.7f, 0.9f));
                        }
                        break;
                    case CardType.Scout:
                        BonusDrawNow(plus ? b.scoutDrawPlus : b.scoutDraw);
                        break;
                    case CardType.Discipline:
                        // 버린 더미를 즉시 뽑을 더미로 섞어 넣는다 (리셔플 타이밍 조작)
                        if (discardPile.Count > 0)
                        {
                            drawPile.AddRange(discardPile);
                            discardPile.Clear();
                            ShufflePile(drawPile);
                        }
                        if (plus) BonusDrawNow(1);
                        break;
                    case CardType.Feint:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            PlannedTarget.Feinted = true;
                            if (!plus) PlannedTarget.FeintBonus = b.feintBackfire; // 강화 시 반동 없음
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.feint"), new Color(0.9f, 0.9f, 1f));
                        }
                        break;
                    case CardType.Purge:
                        foreach (var al in Allies)
                        {
                            if (!al.Alive) continue;
                            al.Shaken = false; al.Poison = 0; al.Petrified = false;
                            if (plus) al.Hp = Mathf.Min(al.MaxHp, al.Hp + HealAmount(b.purgePlusHeal));
                        }
                        Popup?.Invoke(Tank, Loc.T("pop.purge"), new Color(1f, 1f, 0.7f));
                        break;
                    case CardType.Antidote:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            int cured = PlannedTarget.Poison;
                            PlannedTarget.Poison = 0;
                            PlannedTarget.Shaken = false;
                            int amt = HealAmount(plus ? cured * 2 : cured);
                            if (amt > 0) PlannedTarget.Hp = Mathf.Min(PlannedTarget.MaxHp, PlannedTarget.Hp + amt);
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.antidote"), new Color(0.6f, 1f, 0.7f));
                        }
                        break;
                    case CardType.Silence:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            SilenceTarget = PlannedTarget;
                            if (plus) PlannedTarget.Stunned = !BossImmune(PlannedTarget); // 강화: 침묵 + 무력화
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.silence"), new Color(0.7f, 0.7f, 0.9f));
                        }
                        break;
                    case CardType.Warsong:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            WarsongTarget = PlannedTarget;
                            WarsongMult = plus ? b.warsongMultPlus : b.warsongMult;
                            if (plus) PlannedTarget.Shaken = false;
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.warsong"), new Color(1f, 0.8f, 0.4f));
                        }
                        break;
                    case CardType.Bless:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            BlessTarget = PlannedTarget;
                            if (plus) PlannedTarget.Shaken = false;
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.bless"), new Color(1f, 1f, 0.8f));
                        }
                        break;
                    case CardType.Inspire:
                        InspireAmt = plus ? Plus(b.inspireAtkPlus) : b.inspireAtk;
                        break;
                    case CardType.Sacrifice:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            int need = PlannedTarget.MaxHp - PlannedTarget.Hp;
                            int pay = plus ? need / 2 : need;
                            if (need > 0 && Tank.Hp > pay)
                            {
                                Tank.Hp -= pay;
                                PlannedTarget.Hp = PlannedTarget.MaxHp;
                                Popup?.Invoke(PlannedTarget, "+" + need, new Color(0.55f, 1f, 0.55f));
                                Popup?.Invoke(Tank, "-" + pay, new Color(1f, 0.84f, 0.37f));
                            }
                        }
                        break;
                    case CardType.Counter:
                        CounterMult = plus ? b.counterMultPlus : b.counterMult;
                        counterHits = 0; counterLast = null;
                        break;
                    case CardType.Fortress:
                        FortressActive = true;
                        FortressAlly = plus ? b.fortressAllyPlus : b.fortressAlly;
                        break;
                    case CardType.Undying:
                        UndyingHp = plus ? b.undyingHpPlus : b.undyingHp;
                        Popup?.Invoke(Tank, Loc.T("pop.undying"), new Color(1f, 0.9f, 0.5f));
                        break;
                    case CardType.Grudge:
                        grudgeArmed = true; grudgePlus = plus; grudgeTaken = 0;
                        break;
                    case CardType.Barter:
                        int gold = plus ? b.barterGoldPlus : b.barterGold;
                        BonusGold += gold;
                        Popup?.Invoke(Tank, "+" + gold + "G", new Color(1f, 0.84f, 0.37f));
                        break;
                    case CardType.Study:
                        if (PlannedTarget != null && PlannedTarget.Alive)
                        {
                            StudyTarget = PlannedTarget;
                            StudyReduce = plus ? b.studyReducePlus : b.studyReduce;
                            StudyTurns = 2; // 2턴간 그 적을 관찰 — 피해 감소 + 대상 고정
                            Popup?.Invoke(PlannedTarget, Loc.T("pop.study"), new Color(0.8f, 0.9f, 1f));
                        }
                        break;
                    case CardType.LastStand:
                        if (Tank.Hp * 2 <= Tank.MaxHp)
                        {
                            InvulnTurn = true;
                            Popup?.Invoke(Tank, Loc.T("pop.laststand"), new Color(1f, 0.95f, 0.6f));
                        }
                        break;
                    case CardType.WarCry:
                        // 낡은 깃발(도발 +1턴)은 도발 함성에도 적용 — TauntDuration이 이미 유물 보정 포함
                        int cryTurns = b.warCryTurns + (TauntDuration - b.tauntDuration);
                        foreach (var e in Enemies)
                            if (e.Alive && e.TauntTurns < cryTurns) e.TauntTurns = cryTurns;
                        if (plus) Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + b.warCryPlusHeal);
                        Popup?.Invoke(Tank, Loc.T("pop.warcry"), new Color(1f, 0.55f, 0.35f));
                        break;
                }
                AudioKit.Card();
                discardPile.Add(card);
                plannedRemoved = PlannedCard;
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
            // 분열 예정이 남아 있으면 전투는 계속된다 (대기체가 다음 턴에 일어난다)
            foreach (var e in Enemies) if (e.SplitPending && HasDormant()) return true;
            return false;
        }

        bool HasDormant()
        {
            foreach (var c in Enemies) if (c.Dormant && !c.Alive) return true;
            return false;
        }

        /// 티탄석 — 강화(+) 카드 수치 보정 (모든 강화 수치가 이 함수를 거친다)
        int Plus(int value) => value + (run != null && run.Has(RelicId.Titanite) ? Balance.I.relicTitanite : 0);

        /// 회복량 보정 — 흡혈 정령(생존 시 절반)·부패 술사(이번 턴 무효). 모든 회복 경로가 이 함수를 쓴다.
        public int HealAmount(int raw)
        {
            if (HealBlockedTurn) return 0;
            foreach (var e in Enemies) if (e.Alive && e.HealBlock && SilenceTarget != e) return raw / 2;
            return raw;
        }

        /// 단일 타격 적용 — 방패/감산/버티기까지. 실제 받은 피해 반환.
        int ApplyHit(Unit attacker, Unit planned, Unit receiver, int baseDmg, bool viaTaunt, bool viaCover, bool aoe = false)
        {
            int rawDmg = baseDmg + AuraBonus(attacker); // 보호 점수는 오라·무리 포함 실위협 기준
            if (receiver.ShieldCharges > 0 && attacker.Pierce && SilenceTarget != attacker)
            {
                Popup?.Invoke(receiver, Loc.T("pop.pierce"), new Color(1f, 0.55f, 0.55f)); // 관통 — 방패 무시
            }
            else if (receiver.ShieldCharges > 0)
            {
                receiver.ShieldCharges--;
                Strike?.Invoke(attacker, receiver);
                AudioKit.Guard();
                Popup?.Invoke(receiver, Loc.T("pop.blocked"), new Color(0.7f, 0.85f, 1f));
                Log = Loc.F("log.blocked", receiver.Name);
                MitigatedSaved += rawDmg;
                return 0;
            }
            int dmg = ComputeDamage(attacker, receiver, baseDmg, viaTaunt, viaCover, null, aoe);
            if (dmg > 0) AudioKit.Hit();
            MitigatedSaved += Mathf.Max(0, rawDmg - dmg); // 경감 = 원 피해와 최종 피해의 차이

            // 수호 낙인 — 낙인 찍힌 아군의 피해 절반을 탱커가 분담
            int tankShare = 0;
            if (!receiver.IsTank && MarkTarget == receiver && Tank.Alive && dmg > 1)
            {
                tankShare = Mathf.Max(0, dmg / 2 - MarkReduce);
                dmg -= tankShare;
                Tank.Hp = Mathf.Max(0, Tank.Hp - tankShare);
                if (Bracing) bracedTaken += tankShare;
                RedirectedSaved += tankShare;
                Popup?.Invoke(Tank, Loc.F("pop.taken", tankShare), new Color(1f, 0.84f, 0.37f));
            }

            Strike?.Invoke(attacker, receiver);
            receiver.Hp = Mathf.Max(0, receiver.Hp - dmg);
            if (receiver.IsTank && Bracing) bracedTaken += dmg;

            // 가시 태세·가시 방패·거울 판금 — 탱커를 때린 적에게 반사
            int tankThorns = ThornStanceAmt + (run != null && run.Has(RelicId.ThornShield) ? Balance.I.relicThornShield : 0)
                           + (run != null && run.Has(RelicId.MirrorPlate) && receiver.IsTank ? Mathf.Max(1, dmg * Balance.I.relicMirrorPlate / 100) : 0);
            if (receiver.IsTank && dmg > 0 && attacker.Team == Team.Enemy && tankThorns > 0 && attacker.Alive)
            {
                attacker.Hp = Mathf.Max(0, attacker.Hp - CapTo(attacker, tankThorns));
                Popup?.Invoke(attacker, "-" + CapTo(attacker, tankThorns), new Color(0.95f, 0.6f, 0.35f));
            }

            // 흡혈 — 준 피해(분담 포함)만큼 회복
            if (attacker.Lifesteal && dmg + tankShare > 0 && attacker.Alive)
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
            // v1.0: 반격 집계 · 거인 피격 누적 · 역병 독 · 불굴 보험
            if (receiver.IsTank && dmg > 0) { counterHits++; counterLast = attacker; grudgeTaken += dmg; }
            if (dmg > 0 && receiver.Trait == Trait.Retaliate) receiver.Retaliation += Balance.I.titanRetaliate;
            if (dmg > 0 && attacker.PoisonHit && SilenceTarget != attacker)
            {
                receiver.Poison += Balance.I.ratPoison;
                Popup?.Invoke(receiver, Loc.F("pop.poison", receiver.Poison), new Color(0.6f, 1f, 0.5f));
            }
            if (receiver.IsTank && receiver.Hp <= 0 && UndyingHp > 0)
            {
                receiver.Hp = UndyingHp;
                UndyingHp = 0;
                AudioKit.Guard();
                Popup?.Invoke(receiver, Loc.T("pop.undying"), new Color(1f, 0.9f, 0.5f));
                Log = Loc.T("log.undying");
            }
            return dmg;
        }

        /// 아군 1타를 적에게 적용 — 방패·거울 반사·상한·독·전가·가시·처치까지 (해소 전용, 연출 없음)
        void AllyStrike(Unit a, Unit target, int dmg)
        {
            Strike?.Invoke(a, target);
            if (target.CounterOnce && SilenceTarget != target)
            {
                target.CounterOnce = false;
                a.Hp = Mathf.Max(1, a.Hp - Mathf.Max(1, dmg / 2)); // 거울 정령: 무효화 + 절반 반사 (죽이진 않음)
                AudioKit.Guard();
                Popup?.Invoke(target, Loc.T("pop.mirror"), new Color(0.7f, 0.9f, 1f));
                Log = Loc.F("log.mirror", target.Name, a.Name);
                return;
            }
            if (target.ShieldCharges > 0)
            {
                target.ShieldCharges--;
                AudioKit.Guard();
                Popup?.Invoke(target, Loc.T("pop.blocked"), new Color(0.7f, 0.85f, 1f));
                Log = Loc.F("log.blocked", target.Name);
                return;
            }
            AudioKit.Hit();
            dmg = CapTo(target, dmg);
            target.Hp = Mathf.Max(0, target.Hp - dmg);
            Popup?.Invoke(target, "-" + dmg, Color.white);
            AfterAllyHit(a, target, dmg);
        }

        /// 아군 타격 직후 공용 처리 — 독 부여·피해 전가·가시 반사·처치 (예측과 같은 규칙)
        void AfterAllyHit(Unit a, Unit target, int dmg)
        {
            if (a.Trait == Trait.Poison && dmg > 0)
            {
                target.Poison += Balance.I.venomPoison;
                Popup?.Invoke(target, Loc.F("pop.poison", target.Poison), new Color(0.6f, 1f, 0.5f));
            }
            else if (dmg > 0 && run != null && run.Has(RelicId.SerpentRing)) target.Poison += Balance.I.relicSerpentRing;
            // 저주 인형 — 받은 피해 절반을 최고 HP 아군에게 전가 (결정론, 아군을 죽이지는 않는다)
            if (target.Reflector && dmg > 1 && SilenceTarget != target)
            {
                Unit victim = null;
                foreach (var al in Allies) if (al.Alive && !al.IsTank && (victim == null || al.Hp > victim.Hp)) victim = al;
                victim ??= Tank;
                int share = Mathf.Max(1, dmg / 2);
                victim.Hp = Mathf.Max(1, victim.Hp - share);
                Popup?.Invoke(victim, Loc.F("pop.transfer", share), new Color(1f, 0.5f, 0.8f));
            }
            if (target.Alive && target.Thorns > 0)
            {
                a.Hp = Mathf.Max(1, a.Hp - target.Thorns);
                Popup?.Invoke(a, Loc.F("pop.thorns", target.Thorns), new Color(0.95f, 0.6f, 0.35f));
            }
            if (!target.Alive) OnEnemyKilled(a, target);
        }

        IEnumerator Resolve()
        {
            Phase = Phase.Resolving;
            // 전투 속도 옵션 — 해소 연출 대기가 체감 길이의 대부분 (설정에서 전환)
            float sp = PlayerPrefs.GetInt("speed.fast", 0) == 1 ? 0.45f : 1f;
            var wait = new WaitForSeconds(0.5f * sp);
            var quick = new WaitForSeconds(0.35f * sp);

            // 아군 공격수 페이즈 — 클래스 특성 전면 반영 (PredictKills와 동일 로직)
            foreach (var a in Allies)
            {
                if (!a.Alive || a.Role != Role.Attacker) continue;
                if (a.Petrified)
                {
                    Log = Loc.F("log.petrified", a.Name);
                    Popup?.Invoke(a, Loc.T("pop.petrified"), new Color(0.75f, 0.75f, 0.8f));
                    yield return quick;
                    continue;
                }

                // 노병 사수 — 두 적 동시 사격 (각각 절반)
                if (a.Trait == Trait.Volley)
                {
                    int half = Mathf.Max(1, AllyDamage(a) / 2);
                    a.Shaken = false;
                    Unit prevT = null;
                    for (int v = 0; v < 2; v++)
                    {
                        Unit vt = null;
                        foreach (var e2 in Enemies)
                        {
                            if (!e2.Alive || e2 == prevT || e2.Hidden) continue;
                            if (vt == null || e2.Hp < vt.Hp) vt = e2;
                        }
                        if (vt == null) break;
                        prevT = vt;
                        AllyStrike(a, vt, half);
                        yield return quick;
                        if (!AnyEnemyAlive()) { Win(); yield break; }
                    }
                    Log = Loc.F("log.volley", a.Name, half);
                    continue;
                }

                // 마법사 — N턴마다 비전 폭발: 모든 적에게 절반 피해 (원거리 마법이라 가시 반사 면제)
                if (a.Trait == Trait.ArcaneNova && Turn % Balance.I.novaEvery == 0)
                {
                    int nova = Mathf.Max(1, AllyDamage(a) / 2);
                    a.Shaken = false;
                    AudioKit.Hit();
                    foreach (var e2 in Enemies)
                    {
                        if (!e2.Alive) continue;
                        if (e2.ShieldCharges > 0)
                        {
                            e2.ShieldCharges--;
                            Popup?.Invoke(e2, Loc.T("pop.blocked"), new Color(0.7f, 0.85f, 1f));
                            continue;
                        }
                        e2.Hp = Mathf.Max(0, e2.Hp - CapTo(e2, nova));
                        Popup?.Invoke(e2, "-" + CapTo(e2, nova), new Color(0.75f, 0.6f, 1f));
                        if (!e2.Alive) OnEnemyKilled(a, e2);
                    }
                    Log = Loc.F("log.nova", a.Name, nova);
                    yield return quick;
                    if (!AnyEnemyAlive()) { Win(); yield break; }
                    continue;
                }

                for (int strike = 0; strike < 2; strike++) // 그림자: 처치 시 1회 추가 공격
                {
                    int dmg = AllyDamage(a);
                    var target = a.Trait == Trait.Sniper
                        ? HighestPowerEnemy(u => u.Alive && !u.Hidden ? u.Hp : 0)   // 궁수: 최고 위협 저격
                        : PickAttackTarget(dmg, u => u.Alive && !u.Hidden ? u.Hp : 0, u => u.ShieldCharges);
                    if (target == null) break;
                    if (a.Trait == Trait.Momentum)
                    {
                        if (target == a.LastTarget) a.Momentum++; else a.Momentum = 0;
                        a.LastTarget = target;
                        dmg = AllyDamage(a);
                    }
                    a.Shaken = false;
                    Strike?.Invoke(a, target);
                    if (target.CounterOnce && SilenceTarget != target)
                    {
                        target.CounterOnce = false;
                        a.Hp = Mathf.Max(1, a.Hp - Mathf.Max(1, dmg / 2));
                        AudioKit.Guard();
                        Popup?.Invoke(target, Loc.T("pop.mirror"), new Color(0.7f, 0.9f, 1f));
                        Log = Loc.F("log.mirror", target.Name, a.Name);
                        yield return quick;
                        break;
                    }
                    if (target.ShieldCharges > 0)
                    {
                        // 해골 방패병 — 방패가 한 방을 막는다
                        target.ShieldCharges--;
                        AudioKit.Guard();
                        Popup?.Invoke(target, Loc.T("pop.blocked"), new Color(0.7f, 0.85f, 1f));
                        Log = Loc.F("log.blocked", target.Name);
                        yield return quick;
                        break;
                    }
                    AudioKit.Hit();
                    dmg = CapTo(target, dmg);
                    target.Hp = Mathf.Max(0, target.Hp - dmg);
                    Popup?.Invoke(target, "-" + dmg, Color.white);
                    Log = Loc.F("log.allyHit", a.Name, target.Name, dmg);

                    // 독 부여·피해 전가·가시 반사 (공용 후처리 — 처치 판정은 아래 chained 계산 뒤 별도)
                    if (a.Trait == Trait.Poison && dmg > 0)
                    {
                        target.Poison += Balance.I.venomPoison;
                        Popup?.Invoke(target, Loc.F("pop.poison", target.Poison), new Color(0.6f, 1f, 0.5f));
                    }
                    if (target.Reflector && dmg > 1 && SilenceTarget != target)
                    {
                        Unit victim = null;
                        foreach (var al in Allies) if (al.Alive && !al.IsTank && (victim == null || al.Hp > victim.Hp)) victim = al;
                        victim ??= Tank;
                        int share = Mathf.Max(1, dmg / 2);
                        victim.Hp = Mathf.Max(1, victim.Hp - share);
                        Popup?.Invoke(victim, Loc.F("pop.transfer", share), new Color(1f, 0.5f, 0.8f));
                    }
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
                    bool chained = a.Trait == Trait.KillChain && !target.Alive && strike == 0;
                    if (!target.Alive) OnEnemyKilled(a, target);

                    yield return quick;
                    if (!AnyEnemyAlive()) { Win(); yield break; }
                    if (!chained) break;
                    Popup?.Invoke(a, Loc.T("pop.chain"), new Color(0.8f, 0.6f, 1f));
                }
            }

            // 적 페이즈 — 아군 사망 즉시 패배·중단
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                if (e.Stunned) { Log = Loc.F("log.stunned", e.Name); Popup?.Invoke(e, Loc.T("pop.stunnedMark"), new Color(1f, 0.8f, 0.5f)); yield return wait; continue; }
                if (e.Ai == AiKind.BroodBoss && e.Charging)
                {
                    // 산란 — 예고된 차징 턴에 대기 새끼를 깨운다 (일반 차징 처리보다 먼저)
                    int wokeB = 0;
                    foreach (var c in Enemies)
                    {
                        if (wokeB >= 2) break;
                        if (!c.Dormant || c.Alive) continue;
                        c.Dormant = false; c.Hp = c.MaxHp; wokeB++;
                    }
                    Log = wokeB > 0 ? Loc.F("log.spawn", e.Name, wokeB) : Loc.F("log.charging", e.Name);
                    yield return wait;
                    continue;
                }
                if (e.Charging) { Log = Loc.F("log.charging", e.Name); yield return wait; continue; }

                if (e.Ai == AiKind.Bomber)
                {
                    if (e.BombTimer <= 0)
                    {
                        // 자폭 — 아군 전원 광역 (감산·방패 정상 적용), 자신은 소멸
                        Log = Loc.F("log.bomb", e.Name);
                        Popup?.Invoke(e, Loc.T("pop.boom"), new Color(1f, 0.5f, 0.2f));
                        foreach (var al in Allies)
                        {
                            if (!al.Alive) continue;
                            ApplyHit(e, al, al, Balance.I.bomberBlast, false, false, aoe: true);
                            if (AnyAllyDead()) { Lose(); yield break; }
                        }
                        e.Hp = 0;
                        yield return wait;
                        if (!AnyEnemyAlive()) { Win(); yield break; }
                        continue;
                    }
                    Log = Loc.F("log.fuse", e.Name, e.BombTimer);
                    yield return wait;
                    continue;
                }

                if (e.Ai == AiKind.Sealer || e.Ai == AiKind.Gazer || e.Ai == AiKind.Rotmancer)
                {
                    // 봉인·석화·부패는 RollIntents에서 이미 적용(예고) — 해소는 연출만. 도발되면 낭비
                    if (e.TauntTurns > 0) Log = Loc.F("log.curseWasted", e.Name);
                    else if (e.Ai == AiKind.Sealer) Log = Loc.F("log.seal", e.Name);
                    else if (e.Ai == AiKind.Gazer) Log = Loc.F("log.petrify", e.Name, e.CurseIntent != null ? e.CurseIntent.Name : "-");
                    else Log = Loc.F("log.rot", e.Name);
                    yield return wait;
                    continue;
                }

                if (e.Ai == AiKind.Dispeller)
                {
                    if (e.TauntTurns > 0) { Log = Loc.F("log.curseWasted", e.Name); yield return wait; continue; }
                    // 방패 파괴 — 탱커 방패·맹세·방벽·수호 방벽을 해제
                    Tank.ShieldCharges = 0; OathTurns = 0; BarricadeTurns = 0; AegisTurns = 0;
                    AudioKit.Guard();
                    Popup?.Invoke(Tank, Loc.T("pop.dispel"), new Color(1f, 0.6f, 0.6f));
                    Log = Loc.F("log.dispel", e.Name);
                    yield return wait;
                    continue;
                }

                if (e.Hidden)
                {
                    Log = Loc.F("log.hidden", e.Name);
                    yield return wait;
                    continue;
                }

                if (e.Ai == AiKind.BroodBoss && e.Charging)
                {
                    // 산란 — 대기 중인 새끼를 깨운다 (예고된 차징 턴에 발동)
                    int woke = 0;
                    foreach (var c in Enemies)
                    {
                        if (woke >= 2) break;
                        if (!c.Dormant || c.Alive) continue;
                        c.Dormant = false; c.Hp = c.MaxHp; woke++;
                    }
                    Log = woke > 0 ? Loc.F("log.spawn", e.Name, woke) : Loc.F("log.charging", e.Name);
                    yield return wait;
                    continue;
                }

                if (e.Ai == AiKind.Thief)
                {
                    if (e.TauntTurns > 0) { Log = Loc.F("log.curseWasted", e.Name); yield return wait; continue; }
                    int steal = Mathf.Min(Balance.I.thiefSteal, run.Gold);
                    if (steal > 0)
                    {
                        run.Gold -= steal;
                        e.StolenGold += steal;
                        Popup?.Invoke(e, "+" + steal + "G", new Color(1f, 0.84f, 0.37f));
                        Log = Loc.F("log.steal", e.Name, steal);
                    }
                    else Log = Loc.F("log.stealFail", e.Name);
                    yield return wait;
                    continue;
                }

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

                if (e.Env && SilenceTarget != e)
                {
                    Log = Loc.F("log.env", e.Name);
                    foreach (var al in Allies)
                    {
                        if (!al.Alive) continue;
                        int envDmg = EnemyPow(e);
                        if (BlessTarget == al || (al.IsTank && InvulnTurn)) continue;
                        al.Hp = Mathf.Max(al.IsTank ? 0 : 1, al.Hp - envDmg); // 동료는 환경 피해로 죽지 않는다
                        Popup?.Invoke(al, "-" + envDmg, new Color(0.8f, 1f, 0.6f));
                    }
                    yield return wait;
                    if (AnyAllyDead()) { Lose(); yield break; }
                    continue;
                }

                if (e.AoeIntent && e.TauntTurns == 0)
                {
                    foreach (var b in Allies)
                    {
                        if (!b.Alive) continue;
                        if (b.IsTank && e.Ai != AiKind.LichBoss && e.Ai != AiKind.WyrmBoss) continue; // 리치 폭풍·용 브레스는 탱커 포함
                        bool covered = !b.IsTank && CoverTarget == b;
                        var receiver = covered ? Tank : b;
                        ApplyHit(e, b, receiver, EnemyAoePow(e), false, covered, aoe: true);
                        yield return quick;
                        if (AnyAllyDead()) { Lose(); yield break; }
                        if (!e.Alive) break; // 가시 반사로 사망 — 남은 광역 중단
                    }
                    e.Shaken = false; // 위축은 다음 행동 1회만
                    continue;
                }

                // 단일/연타 (도발된 광역 = 탱커 강타)
                var planned = e.AoeIntent ? Tank : e.Intent;
                if (planned == null || !planned.Alive) planned = Tank;
                bool taunted = e.TauntTurns > 0;
                int hits = e.Ai == AiKind.SpiderDouble || (e.Ai == AiKind.ColossusBoss && e.Enraged) ? 2 : 1;
                for (int hi = 0; hi < hits; hi++)
                {
                    Unit receiver;
                    bool viaCover = false;
                    if (taunted) receiver = Tank;
                    else if (CoverTarget != null && planned == CoverTarget && hi == 0 && !e.Pierce) { receiver = Tank; viaCover = true; } // 엄호는 첫 타만, 관통은 무시
                    else receiver = planned;
                    if (!receiver.Alive) receiver = Tank;
                    ApplyHit(e, planned, receiver, EnemyPow(e), taunted && receiver.IsTank, viaCover);
                    yield return hits == 2 ? quick : wait;
                    if (AnyAllyDead()) { Lose(); yield break; }
                    if (!e.Alive) break; // 가시 반사로 사망 — 남은 연타 중단
                }
                e.Shaken = false; // 위축은 다음 행동 1회만
            }

            // 독 진행 — 적·아군 공용, 중첩당 고정 피해 (적 페이즈 종료 시점)
            bool anyPoison = false;
            foreach (var u in Enemies)
            {
                if (!u.Alive || u.Poison <= 0) continue;
                anyPoison = true;
                int pd = u.Poison * Balance.I.poisonTick;
                u.Hp = Mathf.Max(0, u.Hp - pd);
                Popup?.Invoke(u, "-" + pd, new Color(0.6f, 1f, 0.5f));
                u.Poison--;
                if (!u.Alive) OnEnemyKilled(Tank, u);
            }
            foreach (var u in Allies)
            {
                if (!u.Alive || u.Poison <= 0) continue;
                anyPoison = true;
                int pd = u.Poison * Balance.I.poisonTick;
                u.Hp = Mathf.Max(u.IsTank ? 0 : 1, u.Hp - pd);
                Popup?.Invoke(u, "-" + pd, new Color(0.6f, 1f, 0.5f));
                u.Poison--;
            }
            if (anyPoison)
            {
                Log = Loc.T("log.poisonTick");
                yield return wait;
                if (AnyAllyDead()) { Lose(); yield break; }
                if (!AnyEnemyAlive()) { Win(); yield break; }
            }

            // 반격 태세 — 이번 턴 맞은 횟수 × 배수를 마지막 공격자에게
            if (CounterMult > 0 && counterHits > 0 && counterLast != null && counterLast.Alive)
            {
                int cd = counterHits * CounterMult;
                counterLast.Hp = Mathf.Max(0, counterLast.Hp - CapTo(counterLast, cd));
                AudioKit.Hit();
                Popup?.Invoke(counterLast, "-" + cd, new Color(1f, 0.8f, 0.4f));
                Log = Loc.F("log.counter", counterLast.Name, cd);
                if (!counterLast.Alive) OnEnemyKilled(Tank, counterLast);
                yield return wait;
                if (!AnyEnemyAlive()) { Win(); yield break; }
            }

            // 불굴의 서약 — 지속 턴 동안 매 턴 탱커 회복
            if (VowTurns > 0)
            {
                int vh = HealAmount(Balance.I.vowHeal);
                if (vh > 0)
                {
                    Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + vh);
                    AudioKit.Heal();
                    Popup?.Invoke(Tank, "+" + vh, new Color(0.55f, 1f, 0.55f));
                }
            }

            // 버티기 후불 회복
            if (Bracing && bracedTaken > 0)
            {
                int recover = HealAmount(Mathf.Min(BraceCap > 0 ? BraceCap : BraceHeal, bracedTaken));
                AudioKit.Heal();
                Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + recover);
                Popup?.Invoke(Tank, Loc.F("pop.brace", recover), new Color(1f, 0.84f, 0.37f));
                Log = Loc.F("log.braceHeal", recover);
                yield return wait;
            }

            // 힐러 v2 — 백라이너 전담. 적의 최대 한 방에 죽을 수 있는(위험권) 아군 중 가장 가치 큰(공격력 높은)
            // 대상을 우선하고, 위험권이 없으면 HP 최저를 채운다.
            foreach (var healerAny in Allies)
            {
                if (!healerAny.Alive || healerAny.Role != Role.Healer || healerAny.Petrified) continue;
                if (healerAny.Trait != Trait.GroveHeal) continue;
                // 드루이드 — 동료 전원 소량 회복 (광역 힐)
                int gh = HealAmount(healerAny.Shaken ? healerAny.Power / 2 : healerAny.Power);
                healerAny.Shaken = false;
                if (gh <= 0) continue;
                bool anyHealed = false;
                foreach (var al in Allies)
                {
                    if (!al.Alive || al.IsTank || al.Hp >= al.MaxHp) continue;
                    al.Hp = Mathf.Min(al.MaxHp, al.Hp + gh);
                    Popup?.Invoke(al, "+" + gh, new Color(0.55f, 1f, 0.55f));
                    anyHealed = true;
                }
                if (anyHealed)
                {
                    AudioKit.Heal();
                    Log = Loc.F("log.grove", healerAny.Name, gh);
                    yield return wait;
                }
            }

            var healer = HealerUnit();
            if (healer != null && !healer.Petrified && healer.Trait != Trait.GroveHeal)
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
                    int amount = HealAmount(healer.Shaken ? healer.Power / 2 : healer.Power);
                    healer.Shaken = false;
                    if (amount <= 0) { yield return quick; goto healDone; }
                    Strike?.Invoke(healer, target);
                    AudioKit.Heal();
                    target.Hp = Mathf.Min(target.MaxHp, target.Hp + amount);
                    Popup?.Invoke(target, "+" + amount, new Color(0.55f, 1f, 0.55f));
                    Log = Loc.F("log.heal", target.Name, amount);
                    yield return wait;
                }
            }
            healDone:;

            if (!AnyEnemyAlive()) { Win(); yield break; }

            // 턴 정리
            Turn++;
            Bracing = false; CoverTarget = null; CoverBonus = 0; PhalanxActive = false;
            IronWillActive = false; IronWillPlus = false; MarkTarget = null; MarkPlus = false;
            ThornStanceAmt = 0; BraceCap = 0;
            if (OathTurns > 0) OathTurns--;
            // v1.0 상태 정리 — 이번 턴 카드 효과 소멸, 지속 효과 감소
            VanguardAmt = 0; RearguardAmt = 0; InspireAmt = 0; awakenUsed = false;
            WarsongTarget = null; WarsongMult = 1; BlessTarget = null;
            FortressActive = false; FortressAlly = 0; CounterMult = 0; counterHits = 0; counterLast = null;
            InvulnTurn = false; SilenceTarget = null;
            GrudgeNext = grudgeArmed ? (grudgePlus ? grudgeTaken : grudgeTaken / 2) : 0; // 원한: 받은 만큼 다음 턴 화력
            grudgeArmed = false; grudgeTaken = 0;
            if (BarricadeTurns > 0) BarricadeTurns--;
            if (VowTurns > 0) VowTurns--;
            if (AegisTurns > 0) AegisTurns--;
            if (StudyTurns > 0 && --StudyTurns == 0) { StudyTarget = null; StudyReduce = 0; }
            foreach (var e in Enemies)
            {
                e.Stunned = false; e.Feinted = false; e.FeintBonus = 0;
                if (e.TauntTurns > 0) e.TauntTurns--;
            }
            foreach (var a in Allies) a.ShieldCharges = 0;
            if (!RetainHand) { discardPile.AddRange(Hand); Hand.Clear(); } // 재정비: 손패 유지
            RetainHand = false;
            int foresight = 0;
            foreach (var fa in Allies) if (fa.Alive && fa.Trait == Trait.Foresight) foresight++; // 점술사 예지
            DrawHand(Balance.I.handSize + BonusDraw + foresight);
            BonusDraw = 0;
            var notice = RollIntents();
            StateVersion++;
            Phase = Phase.Player;
            Log = notice ?? Loc.F("log.turn", Turn);
        }

        void Win()
        {
            Phase = Phase.Won;
            RewardGold += BonusGold; // 도적 처치 골드·도굴꾼 회수 합산
            if (run != null && run.Has(RelicId.VictoryMeal))
                Tank.Hp = Mathf.Min(Tank.MaxHp, Tank.Hp + Balance.I.relicVictoryMeal); // 승전 축배
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
