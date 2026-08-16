using System.Collections.Generic;
using UnityEngine;

namespace Tanker
{
    /// 다국어 문자열 테이블 (ko/en). 유저 노출 문자열은 전부 Loc.T/F를 거친다 (규약).
    /// 배열 인덱스: 0=한국어, 1=영어. 언어는 PlayerPrefs("lang")로 저장.
    public static class Loc
    {
        public const int KO = 0, EN = 1;
        static int lang = -1;

        public static int Lang
        {
            get { if (lang < 0) lang = PlayerPrefs.GetInt("lang", KO); return lang; }
        }

        public static void SetLang(int value)
        {
            lang = value;
            PlayerPrefs.SetInt("lang", value);
            PlayerPrefs.Save();
        }

        public static string T(string key) =>
            table.TryGetValue(key, out var v) ? v[Lang] : key;

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
        {
            // 유닛
            ["unit.tank"] = new[] { "나", "Me" },
            ["unit.dps"] = new[] { "딜러", "DPS" },
            ["unit.healer"] = new[] { "힐러", "Healer" },
            ["unit.goblinA"] = new[] { "고블린A", "Goblin A" },
            ["unit.goblinB"] = new[] { "고블린B", "Goblin B" },
            ["unit.goblin"] = new[] { "고블린", "Goblin" },
            ["unit.archer"] = new[] { "해골 궁수", "Skeleton Archer" },
            ["unit.brute"] = new[] { "브루트", "Brute" },
            ["unit.bruteX"] = new[] { "브루트X", "Brute X" },
            ["unit.bruteY"] = new[] { "브루트Y", "Brute Y" },
            ["unit.warlord"] = new[] { "워로드", "Warlord" },

            // 인카운터·노드
            ["enc.goblin"] = new[] { "고블린 매복", "Goblin Ambush" },
            ["enc.backline"] = new[] { "백라인 사냥", "Backline Hunt" },
            ["enc.twin"] = new[] { "쌍둥이 브루트", "Twin Brutes" },
            ["enc.warlord"] = new[] { "오거 워로드", "Ogre Warlord" },
            // 분기 맵 방 타입 (v0.6)
            ["nodeT.Battle"] = new[] { "전투", "Battle" },
            ["nodeT.Elite"] = new[] { "엘리트", "Elite" },
            ["nodeT.Event"] = new[] { "함정", "Trap" },
            ["nodeT.Rest"] = new[] { "휴식", "Rest" },
            ["nodeT.Shop"] = new[] { "상점", "Shop" },
            ["nodeT.Treasure"] = new[] { "보물", "Loot" },
            ["nodeT.Boss"] = new[] { "보스", "Boss" },
            ["map.pick"] = new[] { "다음 방을 골라라 — 연결된 길로만 갈 수 있다", "Pick your next room — follow the paths" },
            ["treasure.h1"] = new[] { "보물 상자", "Treasure" },
            ["treasure.desc"] = new[] { "{0}G를 손에 넣었다. 카드도 한 장 챙겨 가라.", "You found {0}G. Take a card as well." },

            // 타이틀
            ["title.name"] = new[] { "탱 커", "T A N K E R" },
            ["title.sub"] = new[] { "공격 스킬은 없다. 팀은 내가 지킨다.", "No attack skills. I protect the team." },
            ["title.start"] = new[] { "던전에 들어간다", "Enter the Dungeon" },
            ["title.ver"] = new[] { "v0.5 — 원정대의 완성", "v0.5 — The Full Expedition" },
            ["title.lang"] = new[] { "English", "한국어" },

            // 설정 오버레이
            ["set.open"] = new[] { "설정", "Menu" },
            ["set.title"] = new[] { "설 정", "S E T T I N G S" },
            ["set.bgm"] = new[] { "배경음", "Music" },
            ["set.sfx"] = new[] { "효과음", "Sound FX" },
            ["set.lang"] = new[] { "언어: 한국어 ▸ English", "Language: English ▸ 한국어" },
            ["set.toTitle"] = new[] { "타이틀 화면으로", "Back to Title" },
            ["set.confirmTitle"] = new[] { "정말요? 진행 중인 원정이 사라져요", "Sure? Your expedition will be lost" },
            ["set.close"] = new[] { "닫기", "Close" },

            // 엔딩 추가
            ["end.retry"] = new[] { "새 원정 시작", "New Expedition" },
            ["map.gold"] = new[] { "골드", "Gold" },

            // 맵
            ["map.h1"] = new[] { "던전 진행", "Dungeon Progress" },
            ["map.enter"] = new[] { "진입 ▶", "Enter ▶" },
            ["map.owned"] = new[] { "보유: {0}", "Owned: {0}" },
            ["map.done"] = new[] { "[완료]", "[Done]" },

            // 이벤트
            ["ev.h1"] = new[] { "가시 함정 복도", "Spike Trap Corridor" },
            ["ev.desc"] = new[] { "복도 바닥 가득 녹슨 가시가 솟아 있다.\n누군가는 대가를 치러야 지나간다.", "Rusty spikes cover the corridor floor.\nSomeone must pay the price to pass." },
            ["ev.a"] = new[] { "내가 앞장선다  (나 -{0})", "I go first  (Me -{0})" },
            ["ev.b"] = new[] { "무너진 우회로 통행료  (-{0}G)", "Toll for the collapsed detour  (-{0}G)" },
            ["ev.c"] = new[] { "날렵한 딜러를 보낸다  (딜러 -{0}, 다음 전투 위축 시작)", "Send the nimble DPS  (DPS -{0}, next battle starts Shaken)" },
            ["ev.hint"] = new[] { "결과는 표기된 그대로다 — 숨은 주사위는 없다.", "What you see is what you get — no hidden dice." },

            // 갈림길·휴식·상점
            ["ch.h1"] = new[] { "갈림길", "Fork in the Path" },
            ["ch.desc"] = new[] { "왼쪽에선 모닥불 냄새가, 오른쪽에선 동전 소리가 난다.\n하나만 고를 수 있다.", "Campfire smoke to the left, clinking coins to the right.\nYou may pick only one." },
            ["ch.rest"] = new[] { "모닥불에서 쉰다  (전원 {0}% 회복)", "Rest at the campfire  (all heal {0}%)" },
            ["ch.shop"] = new[] { "상인에게 간다", "Visit the merchant" },
            ["rest.h1"] = new[] { "모닥불", "Campfire" },
            ["rest.desc"] = new[] { "잠시나마 등을 벽에 기댄다.\n\n나 +{0}   딜러 +{1}   힐러 +{2}", "For a moment, we rest our backs against the wall.\n\nMe +{0}   DPS +{1}   Healer +{2}" },
            ["rest.go"] = new[] { "계속 ▶", "Continue ▶" },
            ["shop.h1"] = new[] { "떠돌이 상인", "Wandering Merchant" },
            ["shop.soldout"] = new[] { "{0} — 품절", "{0} — Sold out" },
            ["shop.item"] = new[] { "{0}  ({1}G)\n{2}", "{0}  ({1}G)\n{2}" },
            ["shop.leave"] = new[] { "상점을 나선다 ▶", "Leave the shop ▶" },
            ["item.guard"] = new[] { "도발 함성", "Taunting Roar" },
            ["item.guard.desc"] = new[] { "도발된 적에게 받는 피해 -{0}", "-{0} damage taken from taunted enemies" },
            ["item.cover"] = new[] { "철벽 엄호", "Iron Cover" },
            ["item.cover.desc"] = new[] { "엄호로 대신 맞는 피해 -{0}", "-{0} damage taken when covering" },
            ["item.brace"] = new[] { "단단한 각오", "Hard Resolve" },
            ["item.brace.desc"] = new[] { "버티기 회복 +{0}", "Brace heal +{0}" },
            ["item.potion"] = new[] { "응급 물약", "Emergency Potion" },
            ["item.potion.desc"] = new[] { "즉시 탱커 {0} 회복", "Instantly heal the tank for {0}" },

            // 엔딩
            ["end.win.h1"] = new[] { "던전 정복", "Dungeon Conquered" },
            ["end.lose.h1"] = new[] { "원정 실패", "Expedition Failed" },
            ["end.win.desc"] = new[] { "아무도 죽지 않았다.\n그것이 탱커의 승리다.", "No one died.\nThat is a tanker's victory." },
            ["end.lose.desc"] = new[] { "지키지 못한 원정은 여기서 끝났다.", "The expedition I failed to protect ends here." },
            ["end.stats"] = new[] { "도달: {0}/{1} 층\n전투 승리: {2}\n대신 맞은 피해: {3}\n경감한 피해: {4}\n남은 골드: {5}G", "Reached: floor {0}/{1}\nBattles won: {2}\nDamage intercepted: {3}\nDamage reduced: {4}\nGold left: {5}G" },
            ["end.title"] = new[] { "타이틀로", "To Title" },

            // 전투 UI
            ["bt.header"] = new[] { "{0}턴  |  {1}", "Turn {0}  |  {1}" },
            ["bt.score"] = new[] { "보호 점수  대신 맞음 {0} · 경감 {1}", "Protection  intercepted {0} · reduced {1}" },
            ["skill.taunt"] = new[] { "도발", "Taunt" },
            ["skill.taunt.cd"] = new[] { "도발 (쿨 {0})", "Taunt (CD {0})" },
            ["skill.cover"] = new[] { "엄호", "Cover" },
            ["skill.brace"] = new[] { "버티기", "Brace" },
            ["skill.go"] = new[] { "진행 ▶", "Go ▶" },
            ["hint.taunt"] = new[] { "적 1명을 {0}턴간\n나에게 고정", "Lock one enemy\non me for {0} turns" },
            ["hint.cover"] = new[] { "아군 1명 대신\n내가 맞기", "Take the hits\nfor one ally" },
            ["hint.brace"] = new[] { "받는 피해 절반\n받은 만큼 회복(최대 {0})", "Halve damage taken\nheal what you took (max {0})" },
            ["hint.go"] = new[] { "진행 전엔 언제든\n예약 취소 가능", "Plans can be cancelled\nany time before Go" },
            ["st.dead"] = new[] { "사망", "Dead" },
            ["st.enraged"] = new[] { "격노", "Enraged" },
            ["st.enragedTaunt"] = new[] { "격노·도발됨 {0}", "Enraged·Taunted {0}" },
            ["st.taunted"] = new[] { "도발됨 {0}", "Taunted {0}" },
            ["st.planTaunt"] = new[] { "도발 예약", "Taunt planned" },
            ["st.planCover"] = new[] { "엄호 예약", "Cover planned" },
            ["st.planBrace"] = new[] { "버티기 예약", "Brace planned" },
            ["st.bracing"] = new[] { "버티는 중", "Bracing" },
            ["st.shaken"] = new[] { "위축", "Shaken" },
            ["st.covered"] = new[] { "엄호받는 중", "Covered" },
            ["st.incoming"] = new[] { "예상 -{0}", "Incoming -{0}" },
            ["intent.charge"] = new[] { "힘 모으는 중...", "Gathering power..." },
            ["intent.single"] = new[] { "▶ {0}에게 {1}", "▶ {1} to {0}" },
            ["intent.aoe"] = new[] { "▶ 휩쓸기: {0}", "▶ Sweep: {0}" },
            ["result.win"] = new[] { "{0} 격파!\n대신 맞음 {1} · 경감 {2}", "{0} defeated!\nIntercepted {1} · Reduced {2}" },
            ["result.loot"] = new[] { "전리품 +{0}G", "Loot +{0}G" },
            ["result.continue"] = new[] { "계속 ▶", "Continue ▶" },
            ["result.lose"] = new[] { "팀을 지키지 못했다...", "I couldn't protect them..." },
            ["result.view"] = new[] { "결과 보기", "See the outcome" },

            // 전투 로그·팝업
            ["log.t1"] = new[] { "1턴: 적의 공격 예고를 보고 팀을 지켜라", "Turn 1: read the telegraphs and protect the team" },
            ["log.turn"] = new[] { "{0}턴: 예고를 읽고 결정하라", "Turn {0}: read and decide" },
            ["log.trapShaken"] = new[] { "함정의 여파로 딜러가 위축된 채 전투 시작...", "The trap's toll lingers — DPS starts the battle Shaken..." },
            ["log.enrageBrute"] = new[] { "{0}이(가) 격노했다 — 이제 매 턴 공격한다!", "{0} is enraged — it attacks every turn now!" },
            ["log.enrageBoss"] = new[] { "{0}이(가) 격노했다 — 숨 돌릴 틈이 사라진다!", "{0} is enraged — no more breathing room!" },
            ["log.selCancel"] = new[] { "스킬 선택 취소", "Selection cancelled" },
            ["log.planCancel"] = new[] { "예약 취소 — 다른 스킬을 고르거나 그대로 진행", "Plan cancelled — pick another skill or just Go" },
            ["log.planBrace"] = new[] { "버티기 예약: 받는 피해 절반, 받은 만큼 회복(최대 {0}) (진행 시 확정)", "Brace planned: halve damage, heal what you take (max {0}) (commits on Go)" },
            ["log.pickTaunt"] = new[] { "도발할 적을 선택", "Choose an enemy to taunt" },
            ["log.pickCover"] = new[] { "엄호할 아군을 선택", "Choose an ally to cover" },
            ["log.planTaunt"] = new[] { "{0} 도발 예약 — {1}턴간 나만 공격한다 (진행 시 확정)", "Taunt planned on {0} — attacks only me for {1} turns (commits on Go)" },
            ["log.planCover"] = new[] { "{0} 엄호 예약 — 그를 노리는 공격은 내가 맞는다 (진행 시 확정)", "Cover planned on {0} — I take the hits aimed at them (commits on Go)" },
            ["log.dpsHit"] = new[] { "딜러가 {0}에게 {1} 피해", "DPS hits {0} for {1}" },
            ["log.charging"] = new[] { "{0}이(가) 힘을 모은다...", "{0} is gathering power..." },
            ["log.redirect"] = new[] { "{0}을(를) 노린 공격을 내가 받아냈다 ({1})", "I intercepted the attack aimed at {0} ({1})" },
            ["log.aoeRedirect"] = new[] { "{0} 몫의 휩쓸기를 내가 받아냈다 ({1})", "I intercepted {0}'s share of the sweep ({1})" },
            ["log.aoeHit"] = new[] { "{0}의 휩쓸기가 {1}에게 {2} 피해", "{0}'s sweep hits {1} for {2}" },
            ["log.hit"] = new[] { "{0}이(가) {1}에게 {2} 피해", "{0} hits {1} for {2}" },
            ["log.heal"] = new[] { "힐러가 {0}을(를) {1} 회복", "Healer restores {0} for {1}" },
            ["log.braceHeal"] = new[] { "버티며 흘린 피를 추스른다 (+{0})", "Braced through it and recovered (+{0})" },
            ["log.win"] = new[] { "승리! 대신 맞음 {0} · 경감 {1}", "Victory! Intercepted {0} · Reduced {1}" },
            ["log.lose"] = new[] { "{0} 사망... 탱커의 실패다", "{0} has fallen... the tanker failed" },
            ["pop.enrage"] = new[] { "격노!!", "ENRAGED!!" },
            ["pop.taunt"] = new[] { "도발!", "Taunt!" },
            ["pop.cover"] = new[] { "엄호", "Cover" },
            ["pop.brace"] = new[] { "+{0} 버티기", "+{0} Brace" },
            ["pop.taken"] = new[] { "-{0} 대신 맞음!", "-{0} intercepted!" },
            ["pop.shaken"] = new[] { "위축!", "Shaken!" },

            // ---- v0.4: 클래스 ----
            ["class.warrior"] = new[] { "전사", "Warrior" },
            ["class.rogue"] = new[] { "도적", "Rogue" },
            ["class.mage"] = new[] { "마법사", "Mage" },
            ["class.ranger"] = new[] { "궁수", "Ranger" },
            ["class.assassin"] = new[] { "암살자", "Assassin" },
            ["class.beastkin"] = new[] { "수인", "Beastkin" },
            ["class.cleric"] = new[] { "클레릭", "Cleric" },
            ["class.paladin"] = new[] { "성기사", "Paladin" },
            ["class.berserker"] = new[] { "광전사", "Berserker" },
            ["class.bard"] = new[] { "음유시인", "Bard" },
            ["trait.TankHealOnHit"] = new[] { "특성: 공격할 때마다 탱커 {0} 회복", "Trait: heals the tank {0} on every attack" },
            ["trait.Frenzy"] = new[] { "특성: HP 절반 이하면 공격력 2배", "Trait: double damage below half HP" },
            ["trait.Cleanse"] = new[] { "특성: 공격 후 위축된 아군 1명 해제", "Trait: clears Shaken from an ally after attacking" },

            // ---- v0.4: 신규 적 ----
            ["unit.slime"] = new[] { "슬라임", "Slime" },
            ["unit.orc"] = new[] { "오크 전사", "Orc Warrior" },
            ["unit.shaman"] = new[] { "다크 샤먼", "Dark Shaman" },
            ["unit.spider"] = new[] { "독거미", "Venom Spider" },
            ["unit.golem"] = new[] { "가시 골렘", "Thorn Golem" },
            ["unit.bat"] = new[] { "흡혈 박쥐", "Vampire Bat" },
            ["unit.necro"] = new[] { "네크로맨서", "Necromancer" },
            ["enc.random"] = new[] { "조우전", "Encounter" },
            ["enc.elite"] = new[] { "정예 조우전", "Elite Encounter" },
            ["node.battleRandom"] = new[] { "전투 — 무작위 조우", "Battle — Random Encounter" },

            // ---- v0.4: 카드 ----
            ["card.Taunt"] = new[] { "도발", "Taunt" },
            ["card.Taunt.desc"] = new[] { "적 1명이 {0}턴간 나만 공격", "One enemy attacks only me for {0} turns" },
            ["card.Cover"] = new[] { "엄호", "Cover" },
            ["card.Cover.desc"] = new[] { "아군 1명의 첫 공격을 대신 맞음", "Intercept the first attack on an ally" },
            ["card.Brace"] = new[] { "버티기", "Brace" },
            ["card.Brace.desc"] = new[] { "피해 절반, 받은 만큼 회복(최대 {0})", "Halve damage, heal what you take (max {0})" },
            ["card.Shield"] = new[] { "철벽 방패", "Iron Bulwark" },
            ["card.Shield.desc"] = new[] { "아군 1명의 첫 피해 무효", "Block the first damage on an ally" },
            ["card.Shove"] = new[] { "밀쳐내기", "Shove" },
            ["card.Shove.desc"] = new[] { "적 1명 이번 턴 행동 취소 (보스 면역)", "Cancel one enemy's action (boss immune)" },
            ["card.Devotion"] = new[] { "헌신", "Devotion" },
            ["card.Devotion.desc"] = new[] { "내 HP {0} → 아군 회복 {0}", "Give {0} of my HP to heal an ally" },
            ["card.Rally"] = new[] { "사기 고취", "Rally" },
            ["card.Rally.desc"] = new[] { "아군 1명 위축 해제", "Remove Shaken from an ally" },
            ["card.Phalanx"] = new[] { "결사 방어", "Last Stand" },
            ["card.Phalanx.desc"] = new[] { "이번 턴 아군 전원 피해 -{0}", "All allies take -{0} damage this turn" },
            ["card.Oath"] = new[] { "반석의 맹세", "Oath of Stone" },
            ["card.Oath.desc"] = new[] { "{1}턴간 내가 받는 피해 -{0}", "I take -{0} damage for {1} turns" },
            // 카드 버튼용 짧은 설명 (카드 폭 안에 들어가야 함)
            ["card.IronWill"] = new[] { "철의 의지", "Iron Will" },
            ["card.IronWill.desc"] = new[] { "이번 턴 내가 받는 한 방 피해 상한 {0}", "This turn each hit on me deals at most {0}" },
            ["card.GuardianMark"] = new[] { "수호 낙인", "Guardian Mark" },
            ["card.GuardianMark.desc"] = new[] { "이번 턴 그 아군 피해의 절반을 내가 분담", "I absorb half of that ally's damage this turn" },
            ["card.Respite"] = new[] { "숨 고르기", "Respite" },
            ["card.Respite.desc"] = new[] { "즉시 내 HP {0} 회복", "Instantly restore {0} of my HP" },

            ["card.Taunt.s"] = new[] { "적 고정 2턴", "Lock enemy 2t" },
            ["card.Cover.s"] = new[] { "대신 맞기", "Intercept" },
            ["card.Brace.s"] = new[] { "절반+회복", "Halve+heal" },
            ["card.Shield.s"] = new[] { "첫 피해 무효", "Block first hit" },
            ["card.Shove.s"] = new[] { "행동 취소", "Cancel action" },
            ["card.Devotion.s"] = new[] { "HP 나눔", "Give HP" },
            ["card.Rally.s"] = new[] { "위축 해제", "Clear Shaken" },
            ["card.Phalanx.s"] = new[] { "전원 -2", "All -2" },
            ["card.Oath.s"] = new[] { "2턴 -2", "2t -2" },
            ["card.IronWill.s"] = new[] { "한 방 상한", "Hit cap" },
            ["card.GuardianMark.s"] = new[] { "피해 분담", "Share damage" },
            ["card.Respite.s"] = new[] { "즉시 회복", "Self heal" },

            // v0.5 신규 매커니즘 팝업·로그
            ["pop.ironwill"] = new[] { "철의 의지!", "Iron Will!" },
            ["pop.mark"] = new[] { "수호 낙인!", "Marked!" },
            ["pop.thorns"] = new[] { "반사 -{0}", "Thorns -{0}" },
            ["log.necroHeal"] = new[] { "{0}이(가) {1}을(를) 치유한다", "{0} mends {1}" },
            ["log.necroIdle"] = new[] { "{0}이(가) 주문을 고른다", "{0} ponders a spell" },
            ["intent.heal"] = new[] { "회복 ▶ {0}", "Heal ▶ {0}" },
            ["intent.aoeMore"] = new[] { " 외 {0}", " +{0} more" },

            // v0.6 — 전투 하단 UI 개편
            ["bt.draw"] = new[] { "뽑을 {0}", "Draw {0}" },
            ["bt.discard"] = new[] { "버림 {0}", "Discard {0}" },
            ["bt.drawL"] = new[] { "뽑을 더미", "Draw pile" },
            ["bt.discardL"] = new[] { "버림 더미", "Discard" },
            ["bt.deckView"] = new[] { "내 덱 {0}", "Deck {0}" },
            ["bt.plan"] = new[] { "예약: {0} — 진행하면 발동", "Planned: {0} — resolves on Go" },
            ["bt.planTarget"] = new[] { "예약: {0} ▶ {1} — 진행하면 발동", "Planned: {0} ▶ {1} — resolves on Go" },
            ["bt.pickTarget"] = new[] { "{0} — 빛나는 대상을 탭하라", "{0} — tap a glowing target" },
            ["deck.title"] = new[] { "내 덱 — {0}장", "My Deck — {0} cards" },
            ["deck.sub"] = new[] { "손 {0} · 뽑을 더미 {1} · 버림 더미 {2}", "Hand {0} · Draw {1} · Discard {2}" },
            ["badge.enemy"] = new[] { "◆ 적 대상", "◆ Enemy" },
            ["badge.ally"] = new[] { "◆ 아군 대상", "◆ Ally" },
            ["badge.self"] = new[] { "◆ 즉시/자신", "◆ Instant/Self" },
            ["st.frenzy"] = new[] { "격노 ×2", "Frenzy ×2" },
            ["st.thorns"] = new[] { "가시 반사 {0}", "Thorns {0}" },
            ["st.lifesteal"] = new[] { "흡혈", "Lifesteal" },
            ["st.enemyHealer"] = new[] { "치유사", "Mender" },

            // ---- v0.4: 전투 UI/로그 ----
            ["bt.deck"] = new[] { "핸드 {0} · 덱 {1}", "Hand {0} · Deck {1}" },
            ["hint.cards"] = new[] { "카드 1장 사용(선택) 후 진행 — 진행 전엔 언제든 취소 가능", "Play up to 1 card, then Go — cancel any time before Go" },
            ["log.planCard"] = new[] { "{0} 예약 (진행 시 확정)", "{0} planned (commits on Go)" },
            ["log.planCardTarget"] = new[] { "{0} 예약 → {1} (진행 시 확정)", "{0} planned → {1} (commits on Go)" },
            ["log.pickEnemy"] = new[] { "대상 적을 선택", "Choose an enemy" },
            ["log.pickAlly"] = new[] { "대상 아군을 선택", "Choose an ally" },
            ["log.shoveBossImmune"] = new[] { "보스는 밀쳐낼 수 없다", "The boss cannot be shoved" },
            ["log.allyHit"] = new[] { "{0}이(가) {1}에게 {2} 피해", "{0} hits {1} for {2}" },
            ["log.stunned"] = new[] { "{0}이(가) 밀쳐져 행동하지 못한다", "{0} is shoved and loses its action" },
            ["log.curseWasted"] = new[] { "{0}의 저주가 나에게는 통하지 않는다", "{0}'s curse has no hold on me" },
            ["log.curse"] = new[] { "{0}이(가) {1}에게 위축의 저주를 걸었다", "{0} curses {1} with Shaken" },
            ["log.blocked"] = new[] { "{0}의 방패가 공격을 통째로 막아냈다", "{0}'s bulwark blocks the hit entirely" },
            ["pop.shield"] = new[] { "철벽!", "Bulwark!" },
            ["pop.shove"] = new[] { "밀쳐냄!", "Shoved!" },
            ["pop.stunnedMark"] = new[] { "행동 불가", "No action" },
            ["pop.cursed"] = new[] { "저주!", "Cursed!" },
            ["pop.rally"] = new[] { "사기 회복!", "Rallied!" },
            ["pop.phalanx"] = new[] { "결사 방어!", "Last Stand!" },
            ["pop.oath"] = new[] { "맹세!", "Oath!" },
            ["pop.blocked"] = new[] { "무효!", "Blocked!" },
            ["st.stunned"] = new[] { "밀쳐짐", "Shoved" },
            ["st.shielded"] = new[] { "철벽", "Bulwark" },
            ["st.cardPlanned"] = new[] { "{0} 예약", "{0} planned" },
            ["intent.stunned"] = new[] { "밀쳐짐 — 행동 불가", "Shoved — no action" },
            ["intent.curse"] = new[] { "저주 ▶ {0}", "Curse ▶ {0}" },
            ["intent.curseWasted"] = new[] { "저주 (도발됨 — 불발)", "Curse (taunted — fizzles)" },

            // ---- v0.4: 파티/보상/제거 화면 ----
            ["party.h1"] = new[] { "오늘의 원정대", "Today's Expedition" },
            ["party.desc"] = new[] { "매 런마다 동료가 달라진다 — 이 파티로 끝까지 간다.", "Companions change every run — this party goes all the way." },
            ["party.go"] = new[] { "출발 ▶", "Set out ▶" },
            ["party.attack"] = new[] { "HP {0} · 공격 {1}", "HP {0} · Attack {1}" },
            ["party.heal"] = new[] { "HP {0} · 치유 {1} (백라이너 전담)", "HP {0} · Heal {1} (backline only)" },
            ["party.tank"] = new[] { "HP {0} · 공격 없음 — 팀을 지킨다", "HP {0} · No attacks — I protect" },
            ["map.deck"] = new[] { "덱 {0}장", "Deck: {0} cards" },
            ["reward.h1"] = new[] { "전리품 — 카드 1장", "Spoils — Pick a Card" },
            ["reward.desc"] = new[] { "덱에 넣을 카드를 고르거나, 덱을 가볍게 유지하라.", "Add a card to your deck, or keep it lean." },
            ["reward.skip"] = new[] { "넘어간다", "Skip" },
            ["shop.remove"] = new[] { "카드 제거  ({0}G)", "Remove a card  ({0}G)" },
            ["remove.h1"] = new[] { "카드 제거", "Remove a Card" },
            ["remove.desc"] = new[] { "제거할 카드를 선택 ({0}G) — 덱이 가벼울수록 원하는 카드가 자주 온다.", "Pick a card to remove ({0}G) — a lean deck draws what you need." },
            ["remove.back"] = new[] { "돌아간다", "Back" },
            ["rest.desc2"] = new[] { "잠시나마 등을 벽에 기댄다.\n전원 회복 (나 +{0})", "For a moment, we rest our backs against the wall.\nEveryone recovers (Me +{0})" },
            ["end.seed"] = new[] { "시드: {0}", "Seed: {0}" },
        };
    }
}
