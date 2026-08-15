# CLAUDE.md — 탱커 프로젝트 작업 규약

공격 스킬이 하나도 없는 탱커로 AI 팀원을 지키며 던전을 클리어하는 모바일 턴제 게임.
현재 상태·다음 할 일은 `README.md`, 게임 규칙·밸런스는 `docs/design.md`.

## 문서 규약

- 주제별 소유 문서는 README의 문서 맵을 따른다. **같은 내용을 두 문서에 쓰지 않는다.**
- 결정·발견이 생기면 그 자리에서 소유 문서를 갱신한다. README에는 상태 요약과 다음 할 일만.
- 문서·코드 주석·커밋 메시지는 한국어.

## Unity 개발 루프 (필독)

- 에디터가 켜져 있어야 MCP가 동작한다. `unity-mcp`는 user 스코프로 등록돼 있어 새 세션에서 자동 로드됨.
- MCP 도구가 세션에 없으면 `tools/mcp_unity.py`로 stdio 직결 조작 (사용법: `docs/dev.md`).
- **금칙 3개** (어기면 게임 상태 소실/동결 — 원인은 docs/dev.md 트러블슈팅):
  1. **재생 중 컴파일 금지** — 리프레시/컴파일은 반드시 재생 종료 후
  2. **재생 진입 전 게임 뷰 Focus** — 게임 뷰 탭이 숨으면 프레임이 동결됨 (`tools/unity-snippets/focus-gameview.cs`)
  3. 재생 진입·컴파일 후 **도메인 리로드가 끝날 때까지(~15초) 대기** 후 다음 RunCommand
- RunCommand 스니펫에 `using System.Reflection` 금지(도구가 차단). 리플렉션이 필요하면
  `unity/Assets/Editor/TankerEditorTools.cs`에 메서드를 추가하고 스니펫에서 호출한다.
- 스니펫 클래스명은 반드시 `internal class CommandScript : IRunCommand`. 도구가 `...Extension.Editor`
  네임스페이스로 감싸므로 `Editor` 타입은 항상 완전수식(`UnityEditor.EditorWindow` 등)으로 쓴다.

## 코드 규약

- **씬 의존 금지**: 모든 오브젝트는 `BattleBootstrap`이 런타임 생성. 씬 파일을 만들거나 커밋하지 않는다.
- UI는 uGUI 코드 생성(`BattleUI`). TMP 대신 legacy `Text` + `LegacyRuntime.ttf` (한글 폴백 때문).
- 애니메이션은 코드 트윈만. 스프라이트 프레임 애니메이션은 만들지 않는다 (`docs/art-guide.md`의 전략).
- 네임스페이스 `Tanker`. 게임 규칙은 `BattleManager`에만 — UI는 상태를 읽고 입력을 전달만 한다.
- 밸런스 숫자를 바꾸면 `docs/design.md`의 표를 같이 갱신한다.

## 에셋 규약

- 이미지 생성은 `docs/art-guide.md`의 공통 프롬프트를 그대로 앞에 붙인다. 임의 변형 금지 — 바꾸려면 가이드부터 수정.
- 생성 이미지는 반드시 `tools/pixelize.py`를 통과시킨 뒤 사용. Unity 임포트: Point filter, 압축 없음, PPU 128.

## git

- 솔로 프로젝트 — main 직커밋. 생성물(Library, UserSettings, captures 등)은 .gitignore 준수, 예외 추가 금지.
