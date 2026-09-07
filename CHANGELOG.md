# Changelog

## [0.1.0] - 미출시

### 추가

에디터 패키지 뼈대.

- `juahn.v2.UiMotion.Editor` 어셈블리 (에디터 전용). `juahn.v2.UiMotion`과
  `juahn.v2.UiMotion.Core`를 참조한다
- `MotionEditorPaths` — 메뉴 경로와 창 제목 상수
- 에디터 계층 컴파일 게이트 (`Tools~/compile-check`). 설치된 Unity의 매니지드 DLL을
  참조해 런타임 패키지의 `Runtime` 전체와 이 패키지의 `Editor` 전체를 `dotnet build`로
  컴파일한다. Unity DLL은 재배포할 수 없어 CI가 아니라 로컬이다
- CI 게이트 — 매니페스트 형식 · 에디터 전용 asmdef · `Runtime` 폴더 금지 ·
  `.meta` 누락 · GUID 중복

노드 카탈로그.

- `MotionNodeCatalog` — `TypeCache`로 프로젝트의 모든 `MotionNodeBase` 파생을 모은다.
  도메인 리로드마다 다시 훑는다. 팔레트 · Node Doctor · 그래프 창의 노드 검색이 전부 이것을 쓴다
- `MotionNodeEntry` — 항목 하나. `[MotionNode]` 유무 · `[Serializable]` 유무 ·
  설명과 예시를 갖췄는지를 따로 들고 있어 "미검증" 격리와 Node Doctor가 원인을 구분할 수 있다

`MotionPlayer` 인스펙터와 슬롯 자동 바인딩.

- `MotionPlayerEditor` — 그래프가 선언한 슬롯을 이름 그대로 줄 세우고 요구 타입으로
  거른다. 비어 있는 슬롯은 배경을 칠하고 라벨에 표시한다. 그래프를 바꾸면 슬롯 목록을
  자동으로 맞추고, 지금 그래프에 없는 바인딩은 "이 그래프에 없는 슬롯"으로 남긴다 —
  조용히 지우지 않는다. 그래프 검사 결과(오류·경고 개수)와 트리거 시험 재생 버튼도 함께
- `SlotAutoBinder` — 슬롯 이름과 같은 이름의 자식을 계층에서 너비 우선으로 찾아 채운다.
  "비어 있는 것만"과 "전부 다시" 두 가지. 모든 변경은 `Undo.RecordObject`를 거치고
  프리팹 인스턴스면 오버라이드까지 기록한다
