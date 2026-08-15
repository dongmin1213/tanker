# 개발 환경 & MCP 루프

> 소유 주제: Unity/MCP 세팅, Claude 개발 루프 절차, 스니펫 카탈로그, 트러블슈팅.

## 환경

| 항목 | 값 |
|---|---|
| Unity | 6000.3.22f1 (6.3 LTS), `/Applications/Unity/Hub/Editor/6000.3.22f1` |
| 프로젝트 | `unity/` (세로 고정, productName=Tanker) |
| 핵심 패키지 | `com.unity.ai.assistant` 2.17.0-pre.1 (공식 MCP), `com.unity.ugui` 2.0.0, `com.unity.2d.sprite` |
| MCP relay | `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64 --mcp` |
| Claude 등록 | user 스코프 `unity-mcp` (`claude mcp list`로 확인) |

### 새 머신/새 클론 세팅

1. Unity Hub로 `unity/` 열기 (첫 열기에서 Library 재생성, 수 분)
2. MCP 스위치 생성 — **기본 꺼짐이라 이 파일이 없으면 도구가 0개 잡힘**:
   `unity/UserSettings/mcp.json` → `{ "enabled": true, "path": "", "mcpServers": {} }`
3. 에디터 재시작 → relay 등록: `claude mcp add -s user unity-mcp -- ~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64 --mcp`

## Claude 개발 루프

세션에 `unity-mcp` 도구(mcp__unity-mcp__Unity_RunCommand 등)가 있으면 그걸 쓰고,
없으면 `tools/mcp_unity.py`로 같은 도구를 stdio로 호출한다:

```bash
python3 tools/mcp_unity.py list                  # 도구 나열 (에디터 꺼져 있으면 0개)
python3 tools/mcp_unity.py schema <도구명>
python3 tools/mcp_unity.py run <스니펫.cs>        # Unity_RunCommand 실행
python3 tools/mcp_unity.py call Unity_GetConsoleLogs '{}'
```

### 표준 시퀀스

**코드 수정 → 확인** (재생 중이면 먼저 stop):
`run stop.cs` → `run refresh.cs` → 15~20초 대기 → `call Unity_GetConsoleLogs` 에러 0 확인
→ `run focus-gameview.cs` → `run play.cs` → 12~15초 대기 → `run editor-state.cs`로 frameCount 증가 확인
→ `run battle-state.cs` / `run screenshot.cs`(unity/captures/에 저장)로 검증

**패키지 추가**: manifest.json 수정 → `run resolve-packages.cs` (refresh로는 manifest 재해석 안 됨) → 대기 → 에러 확인

스니펫은 `tools/unity-snippets/`에 있음. 새 스니펫 작성 규칙은 CLAUDE.md(클래스명·네임스페이스·리플렉션 금지) 참조.

## 트러블슈팅

| 증상 | 원인 | 해법 |
|---|---|---|
| tools/list가 0개 | 에디터 꺼짐 or mcp.json 없음/비활성 | 에디터 실행, mcp.json `enabled: true` 후 재시작 |
| 재생 중 RunCommand가 NRE, 전투 필드 null | 재생 중 컴파일이 겹쳐 비직렬화 필드 소실 | 재생 종료 → 재진입. 예방: 컴파일은 재생 밖에서 |
| frameCount가 멈춤, 코루틴/전투 동결 | 게임 뷰 탭이 숨어 플레이어 루프 정지 | `run focus-gameview.cs` 후 재생 |
| 스니펫 컴파일 에러 CS0118 'Editor' | 래핑 네임스페이스가 `...Extension.Editor`로 끝남 | `UnityEditor.` 완전수식 사용 |
| "unauthorized namespaces: System.Reflection" | RunCommand의 스니펫 검열 | `Assets/Editor/TankerEditorTools.cs`에 넣고 호출 |
| 게임 뷰가 가로라 레이아웃 잘림 | 기본 Free Aspect | `run set-portrait.cs` (EditorTools가 1080x1920 강제) |

## 에디터 유틸

`unity/Assets/Editor/TankerEditorTools.cs` — 리플렉션 등 스니펫에서 금지된 코드의 수용처.
현재: `SetPortraitGameView()` (게임 뷰 1080x1920 전환).
