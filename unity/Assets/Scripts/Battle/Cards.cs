using System.Collections.Generic;

namespace Tanker
{
    /// 탱커의 행동 카드 — 전부 방어/지원 계열 (공격 카드 없음이 게임 정체성).
    public enum CardType
    {
        Taunt, Cover, Brace, Shield, Shove, Devotion, Rally, Phalanx, Oath, IronWill, GuardianMark, Respite,
        ThornStance, FirstAid, Regroup, WarCry, // v0.7
        // v1.0 대확장 — 축별로 겹치지 않게 (진형 / 지속 / 자원 / 상태 / 아군지원 / 반응 / 조건부)
        Reposition, Vanguard, Rearguard,          // 진형: 이동 / 전열 화력 / 후열 보호
        Barricade, Vow, Aegis,                    // 지속: 전원 감산 2턴 / 탱커 지속 회복 / 상한 지속
        Awaken, Scout, Discipline,                // 자원: 카드 1장 더 / 2장 드로우 / 버린 더미 즉시 리셔플
        Feint, Purge, Antidote, Silence,          // 상태: 허공 공격 / 전원 정화 / 해독 / 능력 무효
        Warsong, Bless, Inspire, Sacrifice,       // 아군: 공격 2배 / 완전 방어 / 전원 +1 / 완전 회복(대가)
        Counter, Fortress, Undying, Grudge,       // 반응: 반격 / 요새화 / 치명 보험 / 받은 만큼 다음 턴 화력
        Barter, Study, LastStand,                 // 유틸: 골드 / 대상 고정+약화 / 저체력 무적
    }

    public enum CardTarget { None, Enemy, Ally }

    /// 덱의 카드 한 장 — 강화(+) 여부 포함 (v0.7 업그레이드 시스템)
    [System.Serializable]
    public struct Card
    {
        public CardType Type;
        public bool Plus;
        public Card(CardType type, bool plus = false) { Type = type; Plus = plus; }
    }

    public static class Cards
    {
        public static readonly CardType[] Pool =
        {
            CardType.Taunt, CardType.Cover, CardType.Brace, CardType.Shield, CardType.Shove,
            CardType.Devotion, CardType.Rally, CardType.Phalanx, CardType.Oath,
            CardType.IronWill, CardType.GuardianMark, CardType.Respite,
            CardType.ThornStance, CardType.FirstAid, CardType.Regroup, CardType.WarCry,
            CardType.Reposition, CardType.Vanguard, CardType.Rearguard,
            CardType.Barricade, CardType.Vow, CardType.Aegis,
            CardType.Awaken, CardType.Scout, CardType.Discipline,
            CardType.Feint, CardType.Purge, CardType.Antidote, CardType.Silence,
            CardType.Warsong, CardType.Bless, CardType.Inspire, CardType.Sacrifice,
            CardType.Counter, CardType.Fortress, CardType.Undying, CardType.Grudge,
            CardType.Barter, CardType.Study, CardType.LastStand,
        };

        /// 즉시(Swift) 카드 — 턴당 1장 제한을 소모하지 않고 바로 발동한다.
        /// 자원·정보만 바꾸는 카드라 예약-커밋 없이 확정해도 수읽기를 해치지 않는다.
        /// (각성이 무의미했던 원인: 각성 자체가 그 턴의 1장을 먹어 순증이 0이었다)
        public static bool IsSwift(CardType c) =>
            c == CardType.Scout || c == CardType.Discipline || c == CardType.Barter || c == CardType.Awaken;

        public static CardTarget TargetOf(CardType c)
        {
            switch (c)
            {
                case CardType.Taunt:
                case CardType.Shove:
                case CardType.Feint:
                case CardType.Silence:
                case CardType.Study: return CardTarget.Enemy;
                case CardType.Cover:
                case CardType.Shield:
                case CardType.Devotion:
                case CardType.GuardianMark:
                case CardType.FirstAid:
                case CardType.Rally:
                case CardType.Reposition:
                case CardType.Warsong:
                case CardType.Bless:
                case CardType.Antidote:
                case CardType.Sacrifice: return CardTarget.Ally;
                default: return CardTarget.None; // 자신·전원 대상
            }
        }

        public static string NameOf(Card c) => Loc.T("card." + c.Type) + (c.Plus ? "<color=#8fd4a8>+</color>" : "");
        public static string NameOf(CardType t) => Loc.T("card." + t);

        public static string DescOf(Card c)
        {
            var b = Balance.I;
            switch (c.Type)
            {
                case CardType.Taunt: return Loc.F("card.Taunt.desc", c.Plus ? b.tauntDurationPlus : b.tauntDuration);
                case CardType.Cover: return c.Plus ? Loc.F("card.Cover.descPlus", b.coverPlusReduce) : Loc.T("card.Cover.desc");
                case CardType.Brace: return Loc.F("card.Brace.desc", c.Plus ? b.braceHealPlus : b.braceHeal);
                case CardType.Shield: return c.Plus ? Loc.F("card.Shield.descPlus", b.shieldPlusCharges) : Loc.T("card.Shield.desc");
                case CardType.Shove: return c.Plus ? Loc.T("card.Shove.descPlus") : Loc.T("card.Shove.desc");
                case CardType.Devotion: return Loc.F("card.Devotion.desc", c.Plus ? b.devotionAmountPlus : b.devotionAmount);
                case CardType.Rally: return c.Plus ? Loc.F("card.Rally.descPlus", b.rallyPlusHeal) : Loc.T("card.Rally.desc");
                case CardType.Phalanx: return Loc.F("card.Phalanx.desc", c.Plus ? b.phalanxReducePlus : b.phalanxReduce);
                case CardType.Oath: return Loc.F("card.Oath.desc", b.oathReduce, c.Plus ? b.oathTurnsPlus : b.oathTurns);
                case CardType.IronWill: return Loc.F("card.IronWill.desc", c.Plus ? b.ironWillCapPlus : b.ironWillCap);
                case CardType.GuardianMark: return c.Plus ? Loc.F("card.GuardianMark.descPlus", b.markPlusReduce) : Loc.T("card.GuardianMark.desc");
                case CardType.Respite: return Loc.F("card.Respite.desc", c.Plus ? b.respiteHealPlus : b.respiteHeal);
                case CardType.ThornStance: return Loc.F("card.ThornStance.desc", c.Plus ? b.thornStanceDmgPlus : b.thornStanceDmg);
                case CardType.FirstAid: return Loc.F("card.FirstAid.desc", c.Plus ? b.firstAidHealPlus : b.firstAidHeal);
                case CardType.Regroup: return c.Plus ? Loc.T("card.Regroup.descPlus") : Loc.T("card.Regroup.desc");
                case CardType.WarCry: return c.Plus ? Loc.F("card.WarCry.descPlus", b.warCryPlusHeal) : Loc.T("card.WarCry.desc");
                case CardType.Reposition: return Loc.F("card.Reposition.desc", c.Plus ? b.repositionGuardPlus : b.repositionGuard);
                case CardType.Vanguard: return Loc.F("card.Vanguard.desc", c.Plus ? b.vanguardAtkPlus : b.vanguardAtk);
                case CardType.Rearguard: return Loc.F("card.Rearguard.desc", c.Plus ? b.rearguardReducePlus : b.rearguardReduce);
                case CardType.Barricade: return Loc.F("card.Barricade.desc", c.Plus ? b.barricadeAmtPlus : b.barricadeAmt, c.Plus ? b.barricadeTurnsPlus : b.barricadeTurns);
                case CardType.Vow: return Loc.F("card.Vow.desc", b.vowHeal, c.Plus ? b.vowTurnsPlus : b.vowTurns);
                case CardType.Aegis: return Loc.F("card.Aegis.desc", b.aegisCap, c.Plus ? b.aegisTurnsPlus : b.aegisTurns);
                case CardType.Awaken: return Loc.F("card.Awaken.desc", c.Plus ? b.awakenCostPlus : b.awakenCost);
                case CardType.Scout: return Loc.F("card.Scout.desc", c.Plus ? b.scoutDrawPlus : b.scoutDraw);
                case CardType.Discipline: return c.Plus ? Loc.T("card.Discipline.descPlus") : Loc.T("card.Discipline.desc");
                case CardType.Feint: return c.Plus ? Loc.T("card.Feint.descPlus") : Loc.F("card.Feint.desc", b.feintBackfire);
                case CardType.Purge: return c.Plus ? Loc.F("card.Purge.descPlus", b.purgePlusHeal) : Loc.T("card.Purge.desc");
                case CardType.Antidote: return c.Plus ? Loc.T("card.Antidote.descPlus") : Loc.T("card.Antidote.desc");
                case CardType.Silence: return c.Plus ? Loc.T("card.Silence.descPlus") : Loc.T("card.Silence.desc");
                case CardType.Warsong: return Loc.F("card.Warsong.desc", c.Plus ? b.warsongMultPlus : b.warsongMult);
                case CardType.Bless: return c.Plus ? Loc.T("card.Bless.descPlus") : Loc.T("card.Bless.desc");
                case CardType.Inspire: return Loc.F("card.Inspire.desc", c.Plus ? b.inspireAtkPlus : b.inspireAtk);
                case CardType.Sacrifice: return c.Plus ? Loc.T("card.Sacrifice.descPlus") : Loc.T("card.Sacrifice.desc");
                case CardType.Counter: return Loc.F("card.Counter.desc", c.Plus ? b.counterMultPlus : b.counterMult);
                case CardType.Fortress: return Loc.F("card.Fortress.desc", c.Plus ? b.fortressAllyPlus : b.fortressAlly);
                case CardType.Undying: return Loc.F("card.Undying.desc", c.Plus ? b.undyingHpPlus : b.undyingHp);
                case CardType.Grudge: return c.Plus ? Loc.T("card.Grudge.descPlus") : Loc.T("card.Grudge.desc");
                case CardType.Barter: return Loc.F("card.Barter.desc", c.Plus ? b.barterGoldPlus : b.barterGold);
                case CardType.Study: return Loc.F("card.Study.desc", c.Plus ? b.studyReducePlus : b.studyReduce);
                case CardType.LastStand: return c.Plus ? Loc.T("card.LastStand.descPlus") : Loc.T("card.LastStand.desc");
                default: return Loc.T("card." + c.Type + ".desc");
            }
        }

        public static string ShortDesc(Card c) => Loc.T("card." + c.Type + ".s") + (c.Plus ? " ↑" : "");

        /// 시작 덱 — 도발1 · 엄호1 · 결사 방어1(광역 대응) · 버티기2 · 철벽1
        public static List<Card> StarterDeck() => new List<Card>
        {
            new Card(CardType.Taunt), new Card(CardType.Cover), new Card(CardType.Phalanx),
            new Card(CardType.Brace), new Card(CardType.Brace), new Card(CardType.Shield),
        };
    }
}
