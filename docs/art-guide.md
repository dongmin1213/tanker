# 아트 스타일 가이드 — 픽셀 아트 (v2: 스프라이트 시트 애니메이션)

> 모든 에셋 생성 시 이 문서의 공통 프롬프트를 그대로 앞에 붙인다. 임의 변형 금지.
> 변형이 필요해지면 이 문서를 먼저 고치고, 이후 에셋은 새 버전으로 통일.
> v2 (2026-08-15): 단일 프레임 → **2x2 시트 4프레임 애니메이션**으로 전략 변경 (유저 결정).

## 공통 프롬프트 (영어, **캐릭터 시트에만** 앞에 붙임 — FX 시트는 표의 자체 프롬프트만 사용)

```
16-bit SNES-era pixel art sprite sheet, a 2x2 grid of exactly 4 animation frames,
the very same single full-body character repeated in all 4 cells with small pose changes,
side view facing right, chunky readable pixels, clean 1px dark outline,
limited palette (max 12 colors), muted dungeon tones with one signature accent color,
flat solid magenta background (#FF00FF), no drop shadow, no text, no border, no grid lines,
character centered in each cell, occupying about 80% of cell height, square canvas
```

- **프레임 순서**: 좌상 → 우상 → 좌하 → 우하. 루프 애니메이션은 1↔4 연결이 자연스럽게.
- **방향**: 전원 오른쪽 바라보기. 적군은 코드에서 좌우 반전(scale.x = -1)으로 사용.
- **배경**: 반드시 마젠타 단색. "투명 배경"은 요청해도 가짜(체커보드)로 나오므로 금지.
- **등신/실루엣**: 2.5~3등신. 역할이 실루엣만으로 구분되어야 함 (탱커=방패가 몸보다 큼).
- **셀 경계**: 캐릭터가 셀 경계에 닿지 않게 (마젠타 키잉이 가장자리 flood fill이라 경계가 뚫려 있어야 함).

## 시트 목록 (파일명 = Unity 로드 키, 전부 1024×1024 → 후처리 256×256)

| 파일명 | 애니메이션 | 추가 프롬프트 |
|---|---|---|
| tank-idle | idle 숨쉬기 루프 | `armored knight with a tower shield larger than his torso, no weapon, heavy stance, subtle breathing idle animation across the 4 frames` |
| tank-brace | 방어 자세 루프 | `armored knight crouched bracing behind his raised tower shield larger than his torso, no weapon, shield shimmering slightly across the 4 frames` |
| dps-idle | idle 루프 | `nimble rogue with twin daggers, light leather armor, eager forward lean, subtle breathing idle animation across the 4 frames` |
| dps-attack | 공격 원샷 | `nimble rogue with twin daggers performing a fast double dagger slash, anticipation to follow-through across the 4 frames` |
| healer-idle | idle 루프 | `small robed cleric holding a glowing staff, timid posture, subtle breathing idle animation across the 4 frames` |
| healer-cast | 시전 원샷 | `small robed cleric raising a glowing staff casting a healing spell, staff light growing brighter across the 4 frames` |
| goblin-idle | idle 루프 | `small goblin with a crude club, hunched, mischievous, fidgety idle animation across the 4 frames` |
| goblin-attack | 공격 원샷 | `small goblin swinging a crude club overhead, wind-up to smash across the 4 frames` |
| brute-idle | idle 루프 | `large brutish ogre, slow heavy silhouette, twice the height of a goblin, heavy breathing idle across the 4 frames` |
| brute-charge | 힘 모으기 루프 | `large brutish ogre crouching and gathering dark red energy, muscles tensing, energy glow growing across the 4 frames` |
| brute-attack | 강타 원샷 | `large brutish ogre performing a massive two-handed overhead smash, wind-up to impact across the 4 frames` |
| fx-hit | 타격 임팩트 원샷 | (캐릭터 아님) `16-bit pixel art impact slash effect sprite sheet, a 2x2 grid of 4 frames, white and yellow diagonal slash burst appearing then fading, flat solid magenta background (#FF00FF), no text, no grid lines` |
| fx-heal | 회복 반짝임 원샷 | (캐릭터 아님) `16-bit pixel art healing sparkle effect sprite sheet, a 2x2 grid of 4 frames, soft green and white rising sparkles appearing then fading, flat solid magenta background (#FF00FF), no text, no grid lines` |
| warrior-idle | 전사 idle 루프 | `armored human warrior with a longsword and round shield, sturdy stance, subtle breathing idle across the 4 frames` (시그니처: 주황) |
| warrior-attack | 전사 공격 원샷 | `armored human warrior slashing with a longsword, wind-up to follow-through across the 4 frames` |
| mage-idle | 마법사 idle 루프 | `robed human mage with a glowing arcane tome, scholarly posture, subtle floating rune idle across the 4 frames` (시그니처: 보라) |
| mage-cast | 마법사 시전 원샷 | `robed human mage casting an arcane bolt from a glowing tome, gathering to release across the 4 frames` |
| ranger-idle | 궁수(인간) idle 루프 | `hooded human ranger with a longbow and quiver, calm poised stance, subtle idle sway across the 4 frames` (시그니처: 초록 — 스켈레톤 궁수와 확연히 다르게 인간) |
| ranger-attack | 궁수 공격 원샷 | `hooded human ranger drawing and loosing a longbow arrow, draw to release across the 4 frames` |
| assassin-idle | 암살자 idle 루프 | `masked assassin with twin curved daggers, low crouched stance, coiled idle across the 4 frames` (시그니처: 검은 자주) |
| assassin-attack | 암살자 공격 원샷 | `masked assassin lunging with twin curved daggers, blur-fast strike across the 4 frames` |
| beastkin-idle | 수인 idle 루프 | `wolf beastkin fighter with clawed gauntlets, feral hunched stance, bristling idle across the 4 frames` (시그니처: 회갈색) |
| beastkin-attack | 수인 공격 원샷 | `wolf beastkin fighter raking with clawed gauntlets, pounce to slash across the 4 frames` |
| slime-idle | 슬라임 idle 루프 | `round teal gel slime monster, wobbling jiggle idle across the 4 frames` (시그니처: 청록) |
| slime-attack | 슬라임 공격 원샷 | `round teal gel slime monster lunging forward in a squashing tackle across the 4 frames` |
| orc-idle | 오크 전사 idle 루프 | `orc warrior with a jagged axe and hide armor, heavy stance, breathing idle across the 4 frames` (시그니처: 탁한 녹갈색) |
| orc-attack | 오크 공격 원샷 | `orc warrior chopping with a jagged axe, overhead wind-up to impact across the 4 frames` |
| shaman-idle | 다크 샤먼 idle 루프 | `goblin dark shaman with a bone staff and small totem, hunched muttering idle, faint purple flame across the 4 frames` (시그니처: 보랏빛 불꽃) |
| shaman-cast | 샤먼 저주 원샷 | `goblin dark shaman casting a purple curse from a bone staff, totem glowing, gather to release across the 4 frames` |
| spider-idle | 독거미 idle 루프 | `giant venomous cave spider, low wide silhouette, legs shifting idle across the 4 frames` (시그니처: 암적색) |
| spider-attack | 독거미 공격 원샷 | `giant venomous cave spider striking twice with fangs, quick double bite across the 4 frames` |
| archer-idle | 스켈레톤 궁수 idle 루프 | `skeletal archer with a shortbow, brittle thin silhouette, tattered quiver, subtle idle sway across the 4 frames` (시그니처: 탁한 갈색) |
| archer-attack | 활 쏘기 원샷 | `skeletal archer drawing and releasing a shortbow arrow, draw to release across the 4 frames` |
| boss-idle | 보스 오거 워로드 idle 루프 | `massive armored ogre warlord with a huge spiked club and skull pauldrons, towering heavy silhouette, heavy breathing idle across the 4 frames` (시그니처: 검붉은색) |
| boss-attack | 보스 강타 원샷 | `massive armored ogre warlord swinging a huge spiked club in a devastating overhead smash, wind-up to impact across the 4 frames` |
| boss-charge | 보스 힘 모으기 루프 | `massive armored ogre warlord crouching and gathering crackling dark crimson energy, glow intensifying across the 4 frames` |
| paladin-idle | 성기사 idle 루프 (v0.5) | `holy paladin in gilded plate armor with a warhammer and sacred sigils, stalwart idle across the 4 frames` (시그니처: 금빛) |
| paladin-attack | 성기사 공격 원샷 | `holy paladin swinging a warhammer with a soft golden blessing glow, wind-up to strike across the 4 frames` |
| berserker-idle | 광전사 idle 루프 (v0.5) | `shirtless berserker with a two-handed axe and red war tattoos, heaving breath idle across the 4 frames` (시그니처: 붉은 문신) |
| berserker-attack | 광전사 공격 원샷 | `shirtless berserker with red war tattoos swinging a two-handed axe in a wild arc across the 4 frames` |
| bard-idle | 음유시인 idle 루프 (v0.5) | `wandering bard with a feathered cap strumming a lute, relaxed idle across the 4 frames` (시그니처: 깃털 모자) |
| bard-attack | 음유시인 연주 원샷 | `wandering bard with a feathered cap playing a rousing lute chord with music note sparkles across the 4 frames` |
| golem-idle | 가시 골렘 idle 루프 (v0.5) | `hulking grey stone golem covered in jagged thorn spikes, slow heavy idle across the 4 frames` (시그니처: 가시 실루엣) |
| golem-attack | 가시 골렘 공격 원샷 | `hulking thorn-spiked stone golem slamming both fists down, wind-up to impact across the 4 frames` |
| bat-idle | 흡혈 박쥐 idle 루프 (v0.5) | `purple vampire bat hovering with beating wings, small airborne silhouette across the 4 frames` (시그니처: 보라색) |
| bat-attack | 흡혈 박쥐 공격 원샷 | `purple vampire bat lunging with bared fangs, swoop and bite across the 4 frames` |
| necro-idle | 네크로맨서 idle 루프 (v0.5) | `necromancer in a ragged dark robe holding a skull-topped staff, hunched idle with faint green wisps across the 4 frames` (시그니처: 녹색 사령술) |
| necro-cast | 네크로맨서 주문 원샷 | `necromancer raising a skull-topped staff casting green necrotic mending magic, gather to release across the 4 frames` |
| warden-idle | 문지기 idle 루프 (v0.7) | `heavily armored gate warden with a massive tower shield and flanged mace, immovable guarding stance, subtle breathing idle across the 4 frames` (시그니처: 회청색 갑옷) |
| warden-attack | 문지기 공격 원샷 | `heavily armored gate warden bashing forward with tower shield and mace, brace to slam across the 4 frames` |
| shadow-idle | 그림자 idle 루프 (v0.7) | `smoky black silhouette assassin, half-dissolved wispy edges, low coiled stance, purple afterimage trails across the 4 frames` (시그니처: 보라 잔상) |
| shadow-attack | 그림자 공격 원샷 | `smoky black silhouette assassin blink-striking with a shadow blade, teleport slash with purple afterimages across the 4 frames` |
| chief-idle | 고블린 대장 idle 루프 (v0.7) | `goblin chieftain larger than common goblins, feathered helmet and a command banner, barking orders idle across the 4 frames` (시그니처: 깃털 투구+깃발) |
| chief-attack | 고블린 대장 공격 원샷 | `goblin chieftain roaring and swinging his command banner overhead, rally to strike across the 4 frames` |
| bomber-idle | 고블린 폭탄꾼 idle 루프 (v0.7) | `goblin bomber carrying a huge round bomb keg on his back, lit fuse sparking, nervous fidgety idle across the 4 frames` (시그니처: 도화선 불꽃) |
| bomber-attack | 폭탄꾼 공격 원샷 | `goblin bomber hoisting the sparking bomb overhead about to throw, lift to hurl across the 4 frames` |
| thief-idle | 도굴꾼 idle 루프 (v0.7) | `hooded goblin grave robber with a loot sack leaking gold coins, shifty crouched idle across the 4 frames` (시그니처: 금화 자루) |
| thief-attack | 도굴꾼 공격 원샷 | `hooded goblin grave robber snatching with a quick grabbing lunge, coins scattering across the 4 frames` |
| skeleton-idle | 해골 방패병 idle 루프 (v0.8) | `skeletal soldier holding a large square shield raised in front, defensive stance, rattling idle across the 4 frames` (시그니처: 큰 사각 방패) |
| skeleton-attack | 해골 방패병 공격 원샷 | `skeletal soldier bashing forward with his large square shield, brace to shove across the 4 frames` |
| wolf-idle | 던전 늑대 idle 루프 (v0.8) | `ash-grey feral dungeon wolf, low prowling four-legged silhouette, hackles raised idle across the 4 frames` (시그니처: 잿빛) |
| wolf-attack | 던전 늑대 공격 원샷 | `ash-grey feral dungeon wolf leaping in a lunging bite, pounce to snap across the 4 frames` |
| mimic-idle | 미믹 idle 루프 (v0.8) | `treasure chest mimic monster disguised as a wooden chest with gold trim, lid slightly ajar showing teeth, subtle breathing idle across the 4 frames` (icon-chest와 같은 상자 디자인 기반) |
| mimic-attack | 미믹 공격 원샷 | `treasure chest mimic bursting open with fangs and a long tongue, lunging chomp across the 4 frames` |
| armor-idle | 저주받은 갑옷 idle 루프 (v0.8) | `hollow haunted suit of plate armor floating slightly above ground, glowing purple eyes in an empty helm, hovering idle across the 4 frames` (시그니처: 보라 안광) |
| armor-attack | 저주받은 갑옷 공격 원샷 | `hollow haunted suit of plate armor swinging a greatsword in a heavy arc, wind-up to slash across the 4 frames` |
| lich-idle | 최종 보스 리치 왕 idle 루프 (v0.8) | `crowned skeletal lich king archmage in purple and green robes, floating, staff with green soulfire, towering presence filling 90% of each cell, hovering idle across the 4 frames` (시그니처: 왕관+녹색 사령술 — boss-idle보다 크고 위압적) |
| lich-attack | 리치 왕 강타 원샷 | `crowned skeletal lich king striking with his soulfire staff, gather to smite across the 4 frames` |
| lich-cast | 리치 왕 사령 폭풍 원샷 | `crowned skeletal lich king unleashing a swirling green necrotic storm from raised arms, gather to eruption across the 4 frames` |

## 비캐릭터 에셋

- **배경(던전)** `bg-dungeon`: 시트 아님, 단일 이미지 1024×1536(세로).
  `16-bit pixel art dungeon interior, dark stone walls and floor, torch light, muted low-contrast tones, empty middle ground for characters, mobile portrait composition` — 캐릭터보다 명도/채도를 확 낮춰서 유닛이 뜨게.
- **UI 프레임** (시트 아님, 단일 1024×1024, 마젠타 배경, 후처리 128 + **PIL bbox 크롭** → 코드에서 9-slice).
  **v0.8 재톤 확정 — 보라 금지, 던전 배경(bg-dungeon)의 회갈색 돌과 어울리는 돌+금 팔레트가 최우선**:
  - `ui-panel`: 어두운 돌(회갈색) + 청동/금 모서리 장식 정사각 프레임, 내부 아주 어두운 갈회색 단색, 균일한 테두리 두께
  - `ui-button`: **가로형(약 3:1, 예: 900×300)** — 4변 모두 균일 두께의 어두운 돌+청동 리벳 테두리(ui-panel과 같은 계열),
    모서리 금 장식, 내부는 ui-panel과 동일한 아주 어두운 갈회색 단색. **밝은 갈색 내부·비대칭 테두리 금지**
    (v0.8 실기 검증: 위만 돌이고 내부가 밝은 갈색이면 큰 버튼에서 "갈색 네모"로 보인다). 후처리 256 + bbox 크롭
  - `ui-card`: 낡은 양피지 테두리 + 금 모서리 장식 세로 카드 프레임, 내부 짙은 갈회색
  - `card-back`: ui-card와 같은 비율 카드 뒷면 — 갈색 가죽 바탕 + 금 방패 엠블럼
  - **주의**: 생성물의 투명 여백은 9-slice를 깨뜨린다 — 후처리에서 반드시 알파 bbox로 크롭할 것 (후처리 절 참조).
- **노드 아이콘** (v0.6~, 단일 1024×1024 마젠타 배경, 12색 이하): `icon-battle` 교차된 두 검 /
  `icon-elite` 뿔 달린 해골 왕관(붉은 눈) / `icon-trap` 바닥 가시 / `icon-rest` 모닥불(주황 불꽃) /
  `icon-shop` 저울·금화 / `icon-chest` 보물상자. 후처리: 128로 pixelize 후 **2×2 타일 복제해 256×256으로 저장**
  (`UiKit.LoadSheet`가 2×2 시트를 기대하기 때문 — 4프레임 동일 이미지면 정지 아이콘이 된다).
- **UI 아이콘(기타)/HP바/데미지 숫자**: 에셋 아님. 코드로 렌더.

## 후처리 파이프라인 (Claude 담당, 스크립트로 자동화)

AI가 뽑는 "픽셀 아트"는 픽셀 그리드가 어긋나 있으므로 원본을 그대로 쓰지 않는다:

1. 캐릭터 시트: `python3 tools/pixelize.py <원본> -s 256 --align bottom -o unity/Assets/Resources/Art/`
   (256×256 = 프레임당 128×128. `--align bottom`이 셀마다 실루엣을 **하단 중앙 기준으로 정렬**해
   프레임 간 좌표 지터를 없앤다 — AI 생성 시트는 셀마다 캐릭터 위치가 어긋나 있음)
2. FX 시트: `--align center` (버스트는 중앙 기준 정렬)
3. UI 프레임: `-s 128` 단일 이미지 (정렬 없음) → **PIL `getbbox()`로 투명 여백 크롭** 후 저장
   (여백이 남으면 9-slice 테두리가 안쪽으로 밀려 프레임이 사라져 보인다 — v0.8 재톤에서 실증)
4. 노드 아이콘: `-s 128` → 2×2 타일 복제 256×256 (비캐릭터 절 참조)
5. 배경: PIL nearest 다운스케일 216×384 + 16색 양자화 (마젠타 키잉 없음) → `unity/Assets/Resources/Art/bg-dungeon.png`
6. 원본은 레포 밖(스크래치)에 보관, 커밋 금지.

Unity 임포트: `Assets/Resources/Art/` 이하는 `TankerArtImport.cs`(AssetPostprocessor)가
Point filter · 압축 없음 · PPU 128을 자동 강제한다. 시트 슬라이스는 임포터가 아니라
**런타임에 `BattleUI`가 2x2로 잘라 쓴다** (Sprite.Create) — 씬/메타 의존 없음.

## 애니메이션 (프레임 + 코드 트윈 병용)

- **프레임 애니메이션** (시트): idle 루프 ~5fps, 액션 원샷 ~10fps, FX 원샷 ~12fps.
  상태 매핑 — idle: 기본 / brace: 버티기 중 탱커 / charge: 차징 중 브루트 / attack·cast: Strike 이벤트 시 1회.
- **코드 트윈** (기존 유지): 런지/넉백 임펄스, 피격 글로우, 탱커 피격 화면 셰이크, 팝업 플로팅.
  숨쉬기 sin 트윈은 idle 시트가 있는 유닛에선 끄고 프레임에 맡긴다.
