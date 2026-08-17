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

            RollIntents();
            StateVersion++;
            Log = Loc.T("log.t1");
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

        /// 적 AI v3 — 처치 가능한 백라이너가 있으면 그중 최고 위협을 노리고,
        /// 없으면 직전 턴 자기가 노리던 대상을 제외한 최저 HP (한 명만 계속 두들기는 단조로움 방지 — 유저 피드백).
        Unit SmartBackliner(Unit e)
        {
            int dmg = e.Ai == AiKind.SpiderDouble ? e.Power * 2 : e.Power;
            dmg += AuraBonus(e);
            var prev = e.Intent; // RollIntents 재할당 전이라 직전 턴 대상이 남아 있다

            // 진형 (v0.9): 근접 적은 전열 아군이 있으면 전열만 노린다 — 도약형(거미·박쥐·늑대)은 무시
            bool frontOnly = false;
            if (!e.Leap)
                foreach (var a in Allies)
                    if (a.Alive && !a.IsTank && a.Row == 0) { frontOnly = true; break; }

            Unit kill = null, low = null, lowAlt = null;
            foreach (var a in Allies)
            {
                if (!a.Alive || a.IsTank) continue;
                if (frontOnly && a.Row != 0) continue;
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

        /// 아군 킬각 우선순위 — 예약 밀치기는 위협 0, 폭발 임박·지휘 오라는 최우선 (예측·해소 공용)
        int EnemyThreat(Unit e) => (IsStunnedNow(e) || e.Charging ? 0 : 100) + e.Power
            + (e.Aura ? Balance.I.threatAuraBonus : 0)
            + (e.Ai == AiKind.Bomber && e.BombTimer <= 1 ? Balance.I.threatBombBonus : 0);

        /// 아군 공격수의 이번 타 피해 — 클래스 특성 전부 반영 (예측·해소 공용)
        int AllyDamage(Unit a) => AllyDamage(a, a.Shaken, a.Momentum);

        int AllyDamage(Unit a, bool shaken, int momentum)
        {
            int d = shaken ? a.Power / 2 : a.Power;
            // 진형 (v0.9): 근접 클래스는 전열 +, 후열 - (원거리는 무관) — 배율 특성 전에 적용
            if (!a.IsTank && !a.RangedClass)
            {
                if (a.Row == 0) d += Balance.I.rowFrontBonus;
                else d = Mathf.Max(1, d - Balance.I.rowBackPenalty);
            }
            if (Turn == 1 && run != null && run.Has(RelicId.WarBanner)) d += Balance.I.relicWarBanner; // 군기
            if (a.Trait == Trait.Frenzy && a.Hp * 2 <= a.MaxHp) d *= 2;       // 광전사: 반피 이하 2배
            if (a.Trait == Trait.FullHpDouble && a.Hp >= a.MaxHp) d *= 2;     // 문지기: 풀피 2배
            if (a.Trait == Trait.FirstStrike && Turn == 1) d *= 2;            // 암살자: 1턴 선제 2배
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
        }

        // ---- 인텐트 ----

        string RollIntents()
        {
            var b = Balance.I;
            string notice = null;
            Unit packTarget = null; // 늑대 무리 — 같은 사냥감을 함께 문다 (v0.9)
            foreach (var e in Enemies)
            {
                e.Charging = false; e.AoeIntent = false; e.CurseIntent = null; e.HealIntent = null;
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
                        e.Intent = StrongestAttacker() ?? LowestNonTank();
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
            // 백라이너 교대: 최강 공격수 ↔ 힐러(없으면 최저 HP)
            var strong = StrongestAttacker();
            var healer = HealerUnit() ?? LowestNonTank();
            var t = e.SmashToDps ? (strong ?? healer) : (healer ?? strong);
            e.SmashToDps = !e.SmashToDps;
            return t ?? Tank;
        }

        // ---- 예측 (UI와 해소가 같은 계산) ----

        CardType? PlannedType => PlannedCard >= 0 && PlannedCard < Hand.Count ? Hand[PlannedCard].Type : (CardType?)null;
        bool PlannedPlus => PlannedCard >= 0 && PlannedCard < Hand.Count && Hand[PlannedCard].Plus;

        /// 보스는 밀치기 면역 — 워로드·리치 공통 판정 (개별 AiKind 비교 금지)
        public static bool BossImmune(Unit u) => u.Ai == AiKind.BossWarlord || u.Ai == AiKind.LichBoss;

        public bool IsTauntedNow(Unit e) => e.TauntTurns > 0
            || (PlannedType == CardType.Taunt && PlannedTarget == e)
            || PlannedType == CardType.WarCry; // 도발 함성 예약 = 전원 도발 — 프리뷰도 같은 세계를 본다
        public bool IsStunnedNow(Unit e) => e.Stunned || (PlannedType == CardType.Shove && PlannedTarget == e && !BossImmune(e));

        /// 적 공격력 — Shove+ 위축이면 다음 행동 절반 (예측·해소 공용)
        int EnemyPow(Unit e) => e.Shaken ? e.Power / 2 : e.Power;
        int EnemyAoePow(Unit e) => e.Shaken ? e.AoePower / 2 : e.AoePower;

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
            if (cover != null && enemy.Intent == cover) return Tank;
            return enemy.Intent;
        }

        /// 통합 피해 계산 — 예측과 해소가 이 함수 하나를 쓴다.
        /// hitIndex: 연타에서 몇 번째 타인가 (엄호는 0타만 리다이렉트).
        /// 고블린 대장 오라(+대장 외 전원) + 늑대 무리(살아있는 다른 늑대 수만큼 +)
        int AuraBonus(Unit attacker, HashSet<Unit> killedPreview = null)
        {
            if (attacker.Team != Team.Enemy) return 0;
            bool AliveNow(Unit e) => e.Alive && (killedPreview == null || !killedPreview.Contains(e));
            int bonus = 0;
            if (!attacker.Aura)
                foreach (var e in Enemies) if (AliveNow(e) && e.Aura) { bonus += Balance.I.chiefAura; break; }
            if (attacker.Pack)
                foreach (var e in Enemies) if (AliveNow(e) && e.Pack && e != attacker) bonus += Balance.I.packBonus;
            return bonus;
        }

        /// 저주 갑옷 — 받는 한 방 피해 상한 (아군 공격·노바·가시 전부 적용, 예측 공용)
        static int CapTo(Unit target, int dmg) =>
            target.DamageCap > 0 ? Mathf.Min(dmg, target.DamageCap) : dmg;

        int ComputeDamage(Unit enemy, Unit receiver, int baseDmg, bool viaTaunt, bool viaCover,
                          HashSet<Unit> killedPreview = null)
        {
            int dmg = baseDmg + AuraBonus(enemy, killedPreview);
            if (viaTaunt) dmg = Mathf.Max(0, dmg - TauntGuardAmt);
            if (viaCover) dmg = Mathf.Max(0, dmg - CoverReduceAmt - CoverBonusNow);
            if (PhalanxNow && receiver.Team == Team.Ally) dmg = Mathf.Max(0, dmg - PhalanxNowAmt);
            if (OathNow && receiver.IsTank) dmg = Mathf.Max(0, dmg - Balance.I.oathReduce);
            if (receiver.IsTank && BracingNow) dmg /= 2;
            if (receiver.IsTank && IronWillNow) dmg = Mathf.Min(dmg, IronWillNowCap); // 철의 의지: 한 방 상한
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
            int hits = enemy.Ai == AiKind.SpiderDouble ? 2 : 1;
            // 거미 2타: 도발이면 둘 다 탱커, 엄호면 첫 타만 — 라벨엔 수신자 기준 합계
            if (hits == 2 && viaCover) return perHit; // 엄호 수신(탱커) 몫은 첫 타만
            return perHit * hits;
        }

        public int EffectiveAoeDamage(Unit enemy, Unit backliner)
        {
            bool covered = CoverPreview == backliner;
            var receiver = covered ? Tank : backliner;
            return ComputeDamage(enemy, receiver, EnemyAoePow(enemy), false, covered, KilledPreview);
        }

        /// 이번 턴 이 유닛이 받을 예상 총 피해 (예약·선행 처치·기절·방패·수호 낙인 분담 반영)
        public int IncomingPreview(Unit u)
        {
            var killed = PredictKills();
            var hpAfter = HpAfterAllyPhase();
            int thorns = ThornsNow;
            int shieldLeft = u.ShieldCharges
                + (ShieldPreview == u ? (PlannedPlus ? Balance.I.shieldPlusCharges : 1) : 0);
            var mark = MarkPreview;
            int sum = 0;

            // 타격 1건을 u 관점 합계에 반영 — 낙인 아군이면 분담분 제외, 탱커면 낙인 분담분 가산
            void Add(Unit recv, int d)
            {
                if (d <= 0) return;
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
                        if (b.IsTank && e.Ai != AiKind.LichBoss) continue; // 리치 폭풍만 탱커 포함
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
                int hits = e.Ai == AiKind.SpiderDouble ? 2 : 1;
                bool taunted = IsTauntedNow(e);
                bool viaCover = !taunted && recvS.IsTank && CoverPreview != null && e.Intent == CoverPreview;
                for (int hi = 0; hi < hits; hi++)
                {
                    // 거미 2타 + 엄호: 둘째 타는 원 대상에게 — 탱커 몫은 첫 타만
                    if (hits == 2 && viaCover && hi == 1)
                    {
                        Add(e.Intent, ComputeDamage(e, e.Intent, EnemyPow(e), false, false, killed));
                        break;
                    }
                    int dS = ComputeDamage(e, recvS, EnemyPow(e), taunted && recvS.IsTank, viaCover, killed);
                    Add(recvS, dS);
                    if (ThornKill(recvS, dS)) break;
                }
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
            int HpOf(Unit e) => hp.TryGetValue(e, out var v) && !killed.Contains(e) ? v : 0;

            foreach (var a in Allies)
            {
                if (!a.Alive || a.Role != Role.Attacker) continue;

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
            if (Hand[idx].Type == CardType.Devotion)
                return Tank.Hp > (Hand[idx].Plus ? Balance.I.devotionAmountPlus : Balance.I.devotionAmount);
            return true;
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
                        BraceCap = plus ? b.braceHealPlus + BraceHeal - b.braceHeal : BraceHeal;
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
            int rawDmg = baseDmg + AuraBonus(attacker); // 보호 점수는 오라·무리 포함 실위협 기준
            if (receiver.ShieldCharges > 0)
            {
                receiver.ShieldCharges--;
                Strike?.Invoke(attacker, receiver);
                AudioKit.Guard();
                Popup?.Invoke(receiver, Loc.T("pop.blocked"), new Color(0.7f, 0.85f, 1f));
                Log = Loc.F("log.blocked", receiver.Name);
                MitigatedSaved += rawDmg;
                return 0;
            }
            int dmg = ComputeDamage(attacker, receiver, baseDmg, viaTaunt, viaCover);
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

            // 가시 태세·가시 방패 유물 — 탱커를 때린 적에게 반사 (v0.7의 유일한 능동 반격)
            int tankThorns = ThornStanceAmt + (run != null && run.Has(RelicId.ThornShield) ? Balance.I.relicThornShield : 0);
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
            return dmg;
        }

        IEnumerator Resolve()
        {
            Phase = Phase.Resolving;
            var wait = new WaitForSeconds(0.5f);
            var quick = new WaitForSeconds(0.35f);

            // 아군 공격수 페이즈 — 클래스 특성 전면 반영 (PredictKills와 동일 로직)
            foreach (var a in Allies)
            {
                if (!a.Alive || a.Role != Role.Attacker) continue;

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
                        ? HighestPowerEnemy(u => u.Alive ? u.Hp : 0)   // 궁수: 최고 위협 저격
                        : PickAttackTarget(dmg, u => u.Alive ? u.Hp : 0, u => u.ShieldCharges);
                    if (target == null) break;
                    if (a.Trait == Trait.Momentum)
                    {
                        if (target == a.LastTarget) a.Momentum++; else a.Momentum = 0;
                        a.LastTarget = target;
                        dmg = AllyDamage(a);
                    }
                    a.Shaken = false;
                    Strike?.Invoke(a, target);
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
                            ApplyHit(e, al, al, Balance.I.bomberBlast, false, false);
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

                if (e.AoeIntent && e.TauntTurns == 0)
                {
                    foreach (var b in Allies)
                    {
                        if (!b.Alive) continue;
                        if (b.IsTank && e.Ai != AiKind.LichBoss) continue; // 리치 폭풍만 탱커 포함
                        bool covered = !b.IsTank && CoverTarget == b;
                        var receiver = covered ? Tank : b;
                        ApplyHit(e, b, receiver, EnemyAoePow(e), false, covered);
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
                int hits = e.Ai == AiKind.SpiderDouble ? 2 : 1;
                for (int hi = 0; hi < hits; hi++)
                {
                    Unit receiver;
                    bool viaCover = false;
                    if (taunted) receiver = Tank;
                    else if (CoverTarget != null && planned == CoverTarget && hi == 0) { receiver = Tank; viaCover = true; } // 엄호는 첫 타만
                    else receiver = planned;
                    if (!receiver.Alive) receiver = Tank;
                    ApplyHit(e, planned, receiver, EnemyPow(e), taunted && receiver.IsTank, viaCover);
                    yield return hits == 2 ? quick : wait;
                    if (AnyAllyDead()) { Lose(); yield break; }
                    if (!e.Alive) break; // 가시 반사로 사망 — 남은 연타 중단
                }
                e.Shaken = false; // 위축은 다음 행동 1회만
            }

            // 버티기 후불 회복
            if (Bracing && bracedTaken > 0)
            {
                int recover = Mathf.Min(BraceCap > 0 ? BraceCap : BraceHeal, bracedTaken);
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
            Bracing = false; CoverTarget = null; CoverBonus = 0; PhalanxActive = false;
            IronWillActive = false; IronWillPlus = false; MarkTarget = null; MarkPlus = false;
            ThornStanceAmt = 0; BraceCap = 0;
            if (OathTurns > 0) OathTurns--;
            foreach (var e in Enemies) { e.Stunned = false; if (e.TauntTurns > 0) e.TauntTurns--; }
            foreach (var a in Allies) a.ShieldCharges = 0;
            if (!RetainHand) { discardPile.AddRange(Hand); Hand.Clear(); } // 재정비: 손패 유지
            RetainHand = false;
            DrawHand(Balance.I.handSize + BonusDraw);
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
