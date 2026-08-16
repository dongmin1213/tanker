using System.Collections.Generic;

namespace Tanker
{
    /// 탱커의 행동 카드 — 전부 방어/지원 계열 (공격 카드 없음이 게임 정체성).
    public enum CardType { Taunt, Cover, Brace, Shield, Shove, Devotion, Rally, Phalanx, Oath }

    public enum CardTarget { None, Enemy, Ally }

    public static class Cards
    {
        public static readonly CardType[] Pool =
        {
            CardType.Taunt, CardType.Cover, CardType.Brace, CardType.Shield, CardType.Shove,
            CardType.Devotion, CardType.Rally, CardType.Phalanx, CardType.Oath,
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
                case CardType.Rally: return CardTarget.Ally;
                default: return CardTarget.None; // Brace, Phalanx, Oath
            }
        }

        public static string NameOf(CardType c) => Loc.T("card." + c);
        public static string DescOf(CardType c)
        {
            var b = Balance.I;
            switch (c)
            {
                case CardType.Taunt: return Loc.F("card.Taunt.desc", b.tauntDuration);
                case CardType.Brace: return Loc.F("card.Brace.desc", b.braceHeal);
                case CardType.Devotion: return Loc.F("card.Devotion.desc", b.devotionAmount);
                case CardType.Phalanx: return Loc.F("card.Phalanx.desc", b.phalanxReduce);
                case CardType.Oath: return Loc.F("card.Oath.desc", b.oathReduce, b.oathTurns);
                default: return Loc.T("card." + c + ".desc");
            }
        }

        /// 시작 덱 — 도발1 · 엄호2 · 버티기2 · 철벽1
        public static List<CardType> StarterDeck() => new List<CardType>
        {
            CardType.Taunt, CardType.Cover, CardType.Cover,
            CardType.Brace, CardType.Brace, CardType.Shield,
        };
    }
}
