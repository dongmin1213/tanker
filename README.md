# 탱커 (가제)

> 공격 스킬이 하나도 없는 탱커가 되어, AI 팀원들을 지키며 던전을 클리어하는 모바일 턴제 게임.

## 현재 상태 (2026-08-15)

- **프로토타입 v0.1 동작 중** — 전투 한 판: 도발/엄호/버티기, 인텐트 라인(도발 시 황금색으로 꺾임),
  브루트 격노, 타격 연출(런지/플래시/화면 흔들림). 자동 플레이 검증: 좋은 판단은 승리, 나쁜 판단은 3턴 패배.
- 다음 관문: **유저 직접 플레이 재미 판정** → 통과 시 에셋 교체(v0.2), 실패 시 룰 재설계 또는 중단.
- 저장소: https://github.com/dongmin1213/tanker (private)

## 빠른 시작

1. Unity Hub로 `unity/` 열기 (Unity 6000.3.22f1)
2. 아무 씬에서나 **Play** — 씬 세팅 불필요, `BattleBootstrap`이 전부 런타임 생성
3. Claude로 개발할 땐 `CLAUDE.md`(규약)와 `docs/dev.md`(MCP 루프)부터

## 문서 맵

각 주제는 소유 문서 한 곳에만 기록한다 (중복 금지 — 규약은 CLAUDE.md).

| 문서 | 소유 주제 |
|---|---|
| `CLAUDE.md` | Claude 세션 작업 규약 (개발 금칙, 코드/에셋/문서 규약) |
| `docs/design.md` | 게임 디자인 — 컨셉, 원칙, 전투 규칙·밸런스 수치, 플레이테스트, 열린 질문 |
| `docs/dev.md` | 개발 환경 — Unity/MCP 세팅, 개발 루프, 스니펫, 트러블슈팅 |
| `docs/art-guide.md` | 에셋 파이프라인 — 스타일 프롬프트, 후처리(pixelize), 임포트 설정 |
| `docs/research.md` | 시장 리서치 — 유사작, 포지셔닝 |

## 구조

```
tanker/
├─ unity/                 # Unity 프로젝트 (Assets/Scripts/Battle/ 4파일이 게임 전부)
├─ tools/
│  ├─ mcp_unity.py        # Unity MCP stdio 클라이언트 (Claude 개발 루프용)
│  ├─ unity-snippets/     # RunCommand 스니펫 (play/stop/refresh/상태/캡처 등)
│  └─ pixelize.py         # AI 생성 이미지 → 게임용 픽셀 스프라이트 후처리
└─ docs/
```

## 다음 할 일 (v0.2)

- [ ] 유저 직접 플레이 → 재미 판정 (계속 갈지 결정하는 관문)
- [ ] Codex로 에셋 생성 → 회색 사각형을 스프라이트로 교체 (`docs/art-guide.md`)
- [ ] 실행취소 (스킬 선택 되돌리기 — 디자인 원칙 2)
- [ ] 사운드 (피격/막기 타격음)
