using System.Collections.Generic;

namespace Tanker
{
    /// 탱커의 행동 카드 — 전부 방어/지원 계열 (공격 카드 없음이 게임 정체성).
    public enum CardType
    {
        Taunt, Cover, Brace, Shield, Shove, Devotion, Rally, Phalanx, Oath, IronWill, GuardianMark, Respite,
        ThornStance, FirstAid, Regroup, WarCry, // v0.7
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
        };

        public static CardTarget TargetOf(CardType c)
        {
            switch (c)
            {
                case CardType.Taunt:
                case CardType.Shove: return CardTarget.Enemy;
                case CardType.Cover:
                case CardType.Shield:
                case CardType.Devotion:
                case CardType.GuardianMark:
                case CardType.FirstAid:
                case CardType.Rally: return CardTarget.Ally;
                default: return CardTarget.None; // Brace, Phalanx, Oath, IronWill, Respite, ThornStance, Regroup, WarCry
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
                default: return Loc.T("card." + c.Type + ".desc");
            }
        }

        public static string ShortDesc(Card c) => Loc.T("card." + c.Type + ".s") + (c.Plus ? " ↑" : "");

        /// 시작 덱 — 도발1 · 엄호2 · 버티기2 · 철벽1
        public static List<Card> StarterDeck() => new List<Card>
        {
            new Card(CardType.Taunt), new Card(CardType.Cover), new Card(CardType.Cover),
            new Card(CardType.Brace), new Card(CardType.Brace), new Card(CardType.Shield),
        };
    }
}
