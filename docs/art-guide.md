# 아트 스타일 가이드 — 픽셀 아트

> 모든 에셋 생성 시 이 문서의 공통 프롬프트를 그대로 앞에 붙인다. 임의 변형 금지.
> 변형이 필요해지면 이 문서를 먼저 고치고, 이후 에셋은 새 버전으로 통일.

## 공통 프롬프트 (영어, 모든 캐릭터 에셋 앞에 붙임)

```
16-bit SNES-era pixel art, single full-body character, side view facing right,
chunky readable pixels, clean 1px dark outline, limited palette (max 12 colors),
muted dungeon tones with one signature accent color per character,
flat solid magenta background (#FF00FF), no drop shadow, no text, no border,
character centered, occupying about 80% of canvas height, square canvas
```

- **방향**: 전원 오른쪽 바라보기. 적군은 Unity에서 좌우 반전(flipX)으로 사용.
- **배경**: 반드시 마젠타 단색. "투명 배경"은 요청해도 가짜(체커보드)로 나오므로 금지.
- **등신/실루엣**: 2.5~3등신. 역할이 실루엣만으로 구분되어야 함 (탱커=방패가 몸보다 큼).

## 후처리 파이프라인 (Claude 담당, 스크립트로 자동화)

AI가 뽑는 "픽셀 아트"는 픽셀 그리드가 어긋나 있으므로 원본을 그대로 쓰지 않는다:

1. 1024×1024로 생성 →
2. nearest-neighbor로 **128×128 다운스케일** (그리드 강제 정렬) →
3. 팔레트 양자화 (색 수 제한) →
4. 마젠타 키잉으로 배경 투명화 →
5. PNG 저장

Unity 임포트 설정: Filter Mode = **Point (no filter)**, Compression = **None**, PPU = 128 통일.

## 캐릭터별 시그니처 컬러 & 추가 프롬프트

| 에셋 | 시그니처 컬러 | 추가 프롬프트 |
|---|---|---|
| 탱커(나) idle | 강철 파랑 | `armored knight with a tower shield larger than his torso, no weapon, heavy stance` |
| 탱커 방어 포즈 | 강철 파랑 | 위 + `crouched bracing behind the raised tower shield` |
| 딜러 | 진홍 | `nimble rogue with twin daggers, light leather armor, eager forward lean` |
| 힐러 | 연두 | `small robed cleric holding a glowing staff, timid posture` |
| 적: 고블린 | 탁한 녹색 | `small goblin with a crude club, hunched, mischievous` |
| 적: 브루트 | 탁한 자주 | `large brutish ogre, slow heavy silhouette, twice the height of a goblin` |
| 적: 궁수 | 탁한 갈색 | `skeletal archer with a shortbow, brittle thin silhouette` |

## 비캐릭터 에셋

- **배경(던전)**: 캐릭터와 별도 프롬프트. `16-bit pixel art dungeon interior, dark stone walls and floor, torch light, muted low-contrast tones, empty middle ground for characters, mobile portrait composition` — 캐릭터보다 명도/채도를 확 낮춰서 유닛이 뜨게.
- **UI 아이콘(스킬 3~4종, 인텐트 화살표)**: 32×32 급 단순 아이콘. 색은 UI 팔레트(별도 정의) 따름. AI 생성보다 직접 그리기/코드 드로잉이 나을 수 있음 — 프로토타입에선 코드로 그린 임시 아이콘 사용.
- **HP바, 데미지 숫자**: 에셋 아님. 코드로 렌더.

## 애니메이션 (에셋 아님 — 코드 트윈)

idle 숨쉬기(sin 상하 2px), 피격(흰 플래시 + 4px 튕김), 시전(앞쏠림 6px), 방어 성공(방패 번쩍 + 화면 미세 셰이크). 전부 Unity에서 트윈으로 구현. 프레임 애니메이션 스프라이트는 만들지 않는다.
