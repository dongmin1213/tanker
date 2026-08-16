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

## 비캐릭터 에셋

- **배경(던전)** `bg-dungeon`: 시트 아님, 단일 이미지 1024×1536(세로).
  `16-bit pixel art dungeon interior, dark stone walls and floor, torch light, muted low-contrast tones, empty middle ground for characters, mobile portrait composition` — 캐릭터보다 명도/채도를 확 낮춰서 유닛이 뜨게.
- **UI 프레임** (시트 아님, 단일 1024×1024, 마젠타 배경, 후처리 128×128 → 코드에서 9-slice):
  - `ui-panel`: `16-bit pixel art square UI panel frame for a dark fantasy game, dark blue-purple stone border with subtle gold rivets, solid very dark center fill, flat design, thick even border, flat solid magenta background (#FF00FF) outside the panel, no text`
  - `ui-button`: `16-bit pixel art square UI button frame for a dark fantasy game, dark stone with thin gold trim border, solid dark center fill, flat design, thick even border, flat solid magenta background (#FF00FF) outside the button, no text`
- **UI 아이콘/HP바/데미지 숫자**: 에셋 아님. 코드로 렌더.

## 후처리 파이프라인 (Claude 담당, 스크립트로 자동화)

AI가 뽑는 "픽셀 아트"는 픽셀 그리드가 어긋나 있으므로 원본을 그대로 쓰지 않는다:

1. 캐릭터 시트: `python3 tools/pixelize.py <원본> -s 256 --align bottom -o unity/Assets/Resources/Art/`
   (256×256 = 프레임당 128×128. `--align bottom`이 셀마다 실루엣을 **하단 중앙 기준으로 정렬**해
   프레임 간 좌표 지터를 없앤다 — AI 생성 시트는 셀마다 캐릭터 위치가 어긋나 있음)
2. FX 시트: `--align center` (버스트는 중앙 기준 정렬)
3. UI 프레임: `-s 128` 단일 이미지 (정렬 없음)
4. 배경: PIL nearest 다운스케일 216×384 + 16색 양자화 (마젠타 키잉 없음) → `unity/Assets/Resources/Art/bg-dungeon.png`
5. 원본은 레포 밖(스크래치)에 보관, 커밋 금지.

Unity 임포트: `Assets/Resources/Art/` 이하는 `TankerArtImport.cs`(AssetPostprocessor)가
Point filter · 압축 없음 · PPU 128을 자동 강제한다. 시트 슬라이스는 임포터가 아니라
**런타임에 `BattleUI`가 2x2로 잘라 쓴다** (Sprite.Create) — 씬/메타 의존 없음.

## 애니메이션 (프레임 + 코드 트윈 병용)

- **프레임 애니메이션** (시트): idle 루프 ~5fps, 액션 원샷 ~10fps, FX 원샷 ~12fps.
  상태 매핑 — idle: 기본 / brace: 버티기 중 탱커 / charge: 차징 중 브루트 / attack·cast: Strike 이벤트 시 1회.
- **코드 트윈** (기존 유지): 런지/넉백 임펄스, 피격 글로우, 탱커 피격 화면 셰이크, 팝업 플로팅.
  숨쉬기 sin 트윈은 idle 시트가 있는 유닛에선 끄고 프레임에 맡긴다.
