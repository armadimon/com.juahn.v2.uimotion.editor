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

그래프 에셋 인스펙터.

- `MotionGraphInspector` — 노드 · 간선 · 트리거 · 슬롯 수 요약, 트리거 목록(재발사 정책과
  진입 노드, 진입점 없는 트리거는 오류로 표시), 슬롯 목록(요구 타입과 함께,
  **노드에서 계산되는 파생값이라 직접 고칠 수 없다**는 것을 명시), `UseUnscaledTime` 토글,
  그리고 검사 결과. `[SerializeReference]` 노드 배열을 펼치는 기본 인스펙터는 그리지 않는다.
  검사는 캐시하고 최소 0.25초 간격으로만 다시 돌린다 — 인스펙터는 초당 수십 번 다시 그려지고
  검사는 그래프 전체를 훑기 때문이다
- `MotionIssueDrawer` — 검사 결과를 그리는 공통 코드. 심각한 것부터 그리고 개수를 제한한다.
  인스펙터 · Node Doctor · 그래프 창이 같은 것을 쓴다

Node Doctor.

- `MotionNodeDoctor` — 노드의 문서화 계약을 검사한다. `[MotionNode]` 유무 · `Summary` ·
  `Sample`이 가리키는 예시 그래프가 실제로 존재하는지 · 같은 이름의 예시가 둘 이상은 아닌지 ·
  `[Serializable]` 유무. 예시는 정해진 경로가 아니라 **이름으로 프로젝트 전체를 검색한다** —
  프로젝트가 자기 노드를 추가하면 그 예시는 이 패키지 밖에 있기 때문이다
- `MotionNodeDoctor.RunBatch` — 배치 모드 진입점. 문제가 하나라도 있으면
  `EditorApplication.Exit(1)`로 죽는다. Unity 라이선스가 필요해 이 저장소의 CI가 아니라
  로컬 게이트다 — 컴파일 게이트와 같은 이유다

      Unity -batchmode -quit -projectPath <path> \
        -executeMethod Juahn.UiMotion.Editor.MotionNodeDoctor.RunBatch

- `MotionNodeDoctorWindow` — 같은 검사를 표로 보여 준다. 통과하지 못한 것이 위로 올라오고,
  행을 누르면 무엇이 빠졌는지와 예시 에셋을 보여 준다. 검사는 열 때와 "다시 검사"를 눌렀을
  때만 돈다 — 노드마다 `AssetDatabase.FindAssets`를 부르기 때문이다

인에디터 프리뷰.

- `MotionPreviewDriver` — 플레이 모드에 들어가지 않고 그래프를 실제 프리팹 위에서 재생한다.
  `MotionPump`는 플레이 중이 아니면 아무것도 하지 않으므로 `EditorApplication.update`가
  대신 `MotionPlayer.TickFromPump`에 시간을 넣는다. 창이 가려져 몇 초씩 건너뛴 delta는
  0.1초로 자른다 — 그대로 넣으면 연출이 통째로 끝나 버린다
- **프리뷰는 프리팹으로 새어 나가지 않는다.** 멈출 때 `MotionPlayer.StopAll()`이 스코프를
  취소하고 취소가 등록된 원상 복구를 역순으로 전부 돌린다. 도메인 리로드
  (`AssemblyReloadEvents.beforeAssemblyReload`)와 플레이 모드 전환 직전
  (`ExitingEditMode` · `ExitingPlayMode`)에도 같은 정리가 돈다
- `MotionPlayerEditor`의 트리거 시험 재생이 에디트 모드에서도 동작한다. 프리뷰 중에는
  "프리뷰 멈추기"가 함께 뜨고, 대상이 실제로 움직인다는 것을 안내한다

예시 그래프 생성기.

- `MotionSampleGenerator` — 카탈로그의 모든 노드에 대해 `[MotionNode(Sample = "...")]`가
  가리키는 최소 그래프를 만든다. 노드 하나에 트리거 하나짜리 그래프이며, 끝나지 않는
  노드(`Float`·`Bounce`)는 `Start`가 아니라 `Loop`에 문다 — 그것이 그 노드를 실제로 쓰는
  방식이고 검사기도 그때만 통과한다
- **덮어쓰지 않는다.** 사람이 예시를 손봤을 수 있으므로 이미 있는 것은 건너뛴다.
  `Window > UI Motion > Generate Missing Samples`
- 예시 폴더는 이 스크립트 자신의 에셋 경로에서 역산한다. 패키지가 `Packages/`에
  임베드돼 있든 캐시에서 왔든 같은 자리에 만든다

그래프 창 — 뼈대와 왕복.

- `MotionGraphWindow` — `Window > UI Motion > Graph`, 그리고 그래프 에셋 더블클릭
  (`[OnOpenAsset]`)으로 열린다. 툴바에 에셋 이름 · 저장 · 다시 검사 · 검사 요약
- **도메인 리로드를 GUID로 넘긴다.** `MotionGraph` 참조를 창 필드로 들면 리로드 때 사라지므로
  에셋의 GUID를 `[SerializeField] string`으로 저장하고 `CreateGUI`에서 복원한다
- **되돌리기가 뷰에 반영된다.** `Undo.undoRedoPerformed`를 구독해 그래프를 통째로 다시 읽는다.
  구독하지 않으면 화면과 에셋이 어긋난다
- `MotionGraphViewImpl` — `UnityEditor.Experimental.GraphView` 위의 뷰. **뷰는 상태를 갖지
  않는다.** 진실은 언제나 에셋에 있고 편집은 저작 API를 거쳐 에셋을 바꾼 뒤 다시 읽는다.
  이동 · 삭제 · 배선이 전부 `Undo`를 거친다
- `Load` 중에는 `graphViewChanged`를 무시한다(`_loading` 가드). 요소를 지우고 다시 만드는
  동안에도 콜백이 불리므로, 가드가 없으면 방금 읽은 것을 도로 지운다
- `MotionNodeView` — 포트 타입은 흐름 하나뿐이다. **끝나지 않는 노드(`BlocksChildren`)도
  출력 포트를 만들되 새 연결만 막는다** — 포트를 없애면 이미 그렇게 배선된 그래프에서
  간선이 화면에 그려지지 않아 지울 수도 없는데 에셋에는 남아 계속 경고가 뜬다.
  미검증 노드와 검사 결과를 제목 옆 배지로 표시한다
- `[OnOpenAsset]`의 instanceID 해석은 Unity 버전으로 갈라 둔다. 6000.3이 `instanceID`
  오버로드를 오류로 폐기하고 `EntityId`로 옮겼는데 `EntityId`는 6000.0에 없다

그래프 창 — 노드 검색 · 팔레트 · 간선 순서.

- `MotionNodeSearchProvider` — 빈 곳에서 스페이스나 우클릭을 누르면 뜨는 노드 검색 창.
  카탈로그를 카테고리별 트리로 묶고 **미검증 노드는 "미검증" 그룹으로 격리한다**
  (쓰는 것 자체는 막지 않는다). 항목의 툴팁은 `[MotionNode].Summary`다.
  화면 좌표를 창 위치와 `contentViewContainer`를 거쳐 그래프 좌표로 바꾼다 —
  화면 좌표를 그대로 쓰면 스크롤하거나 줌한 상태에서 노드가 엉뚱한 곳에 생긴다
- `MotionNodePalette` — 그래프 창 왼쪽 패널. 카테고리별 접이식 목록(미검증은 접힌 채
  맨 아래), 이름 거르기, 고른 노드의 요약과 **예시 존재 여부**. 두 번 누르면 화면
  한가운데에 노드를 넣는다
- **예시는 재생하지 않고 연다.** 재생하려면 실제 씬 오브젝트와 슬롯 바인딩이 필요한데
  팔레트에는 재생 대상이 없다. 씬에 임시 오브젝트를 만들어 재생하는 것은 프리팹으로
  새어 나갈 위험이 커서 하지 않는다. "예시 그래프 열기"가 그것을 대신한다
- **간선 순서가 화면에 보인다.** `MotionNodeView`가 자식 목록을 `1. · 2. · 3.`으로 그리고
  출력 포트에 자식 수를 붙인다. 같은 부모에서 나가는 간선의 순서가 곧 `Sequence`의 실행
  순서인데 GraphView는 간선에 순서 개념이 없다. 특히 **간선을 지웠다 다시 이으면 새 간선이
  배열 끝에 붙어 순서가 조용히 바뀌는데**, 이 표시가 그 변화를 눈에 보이게 만든다
- `MotionGraphViewImpl.MoveChild(parent, from, to)` — 화면에서 보이는 자식 순서를 받아
  전역 간선 배열의 인덱스로 바꾼 뒤 `MotionGraph.MoveLink`를 부른다. 되돌리기를 거친다
- `MotionGraphInspector`에 "그래프 창에서 열기" 버튼

그래프 창 — 노드 인스펙터와 트리거 편집.

- `MotionNodeInspector` — 고른 노드의 파라미터를 그린다. **리플렉션이 아니라
  `SerializedProperty`로 그린다** — 그래야 되돌리기와 프리팹 오버라이드가 따라온다.
  `_nodes` 배열에서 원소를 찾을 때 배열 인덱스가 아니라 `Id.Value`를 비교한다.
  id는 재사용되지 않고 배열은 지운 자리를 메우므로 둘이 어긋난다
- `[MotionParam]`의 `Label`·`Tooltip`을 쓰고, `Min`/`Max`가 선언된 숫자는 슬라이더로 그린다.
  `Id` 필드는 감춘다 — 고치면 간선과 트리거가 통째로 끊긴다
- **`SlotRef`는 텍스트 입력이 아니라 드롭다운이다.** 이 그래프가 이미 쓰는 슬롯 이름 +
  `Self` + "새 슬롯...". 오타 하나가 아무 경고 없이 동작하지 않는 연출을 만드는 것을 막는다
- **자식 실행 순서를 위/아래로 옮긴다.** 화면에 보이는 순서를 전역 간선 배열의 인덱스로
  바꿔 `MotionGraph.MoveLink`를 부른다
- `MotionTriggerPanel` — 트리거 **선언**의 목록·추가·삭제, 재발사 정책, 진입 노드 지정.
  진입 노드가 없는 트리거는 배경을 칠하고 오류로 말한다 — 쏘아도 아무 일이 없기 때문이다.
  `Start`·`Loop`·`End`는 런타임이 그 철자로 쏘는 예약 이름이라 한 번 눌러 만드는 버튼을 따로 둔다
- **`TriggerDeclaration`과 `TriggerNode`를 구분해 말한다.** 패널이 다루는 것은 선언이고,
  팔레트의 `Trigger` 노드는 그래프 안에서 시작 지점을 눈에 보이게 하는 표식일 뿐이다.
  표식만 놓고 선언을 만들지 않으면 그래프는 재생되지 않는다
- `MotionGraphViewImpl`이 `ISelection` 구현을 가로채 `SelectionChanged`를 낸다.
  GraphView는 선택 변경 이벤트를 주지 않는다

프리셋 브라우저.

- `MotionPresetBrowser` — `Window > UI Motion > Presets`. 프로젝트의 모든 `MotionGraph`를
  이름 · 경로 · 노드 수 · 트리거 목록 · 검사 요약으로 줄 세운다. 이름과 트리거 이름으로
  거른다. 한 번 누르면 에셋을 고르고, 두 번 누르면 그래프 창에서 연다
- **"선택한 오브젝트에 적용"** — 계층에서 고른 오브젝트마다 `MotionPlayer`를 붙이고(없으면)
  그래프를 꽂은 뒤 `SyncBindings()`와 슬롯 자동 바인딩을 돌린다. 그래프를 만들어 두어도
  붙이는 일이 여러 단계면 결국 재사용되지 않으므로, 이 버튼 하나로 끝나야 한다.
  `Undo.AddComponent`와 `Undo.RecordObject`를 거치고 오브젝트 여러 개를 **하나의 되돌리기
  단계로 묶는다** — 버튼 한 번에 되돌리기 열 번이 필요하면 안 된다
- 목록은 캐시하고 "새로 고침" 버튼으로만 다시 훑는다. `AssetDatabase.FindAssets`는
  프로젝트가 크면 느리다

문서와 마무리.

- README에 설치 순서(런타임 패키지가 먼저), 창 셋과 각각이 하는 일, 그래프를 만들어
  프리팹에 붙이는 최단 경로, 노드를 새로 만들 때 Node Doctor를 통과시키는 법,
  검증 명령 둘을 넣었다
- CI에 Node Doctor 배치가 **왜 CI에서 돌지 않는지**와 로컬 실행 명령을 남겼다.
  Unity 라이선스가 필요하기 때문이고, 컴파일 게이트와 같은 이유다. 대신 배치 진입점이
  사라지지 않았는지는 CI가 지킨다
- 런타임 패키지 README에 저작 툴 패키지 안내를 넣고, 스펙의 열린 질문 3번을 닫았다
  (그 저장소의 커밋)

### 고침

적대적 코드 리뷰에서 나온 데이터 손실 경로.

- **중복 간선이 연결을 통째로 지우던 것.** 두 포트가 모두 `Port.Capacity.Multi`라 A에서 B로
  두 번 끌 수 있는데, `MotionGraph.Link`는 중복이라 `false`를 돌려주는 반면 `ApplyNewEdges`는
  그 반환값을 버렸다. 에셋에는 링크 1개, 화면에는 간선 2개가 되고 둘 중 하나를 지우면
  유일한 링크가 사라져 저장하고 다시 열었을 때 그 자식이 통째로 없어졌다. 이제
  `Link`가 실패한 간선을 `graphViewChanged`가 돌려주는 `change.edgesToCreate`에서 빼
  GraphView가 그 Edge를 아예 만들지 않게 한다
- **창을 도킹하면 노드 검색이 영구히 죽던 것.** `DetachFromPanelEvent`는 창을 닫을 때만이
  아니라 도킹 · 언도킹 · Shift+Space 최대화 · 레이아웃 변경 때도 온다. 그때 검색 제공자를
  파괴해 버려 이후 스페이스와 우클릭이 도메인 리로드까지 조용히 아무 일도 하지 않았다.
  이제 제공자는 `OpenSearchWindow`가 필요할 때 만들고, 파괴는 창의 `OnDisable`에서 한다
- **예시 생성기가 아무것도 못 만들고 성공을 보고하던 것.** 패키지가 `Library/PackageCache`에
  있으면 폴더 생성과 에셋 생성이 조용히 실패하는데 개수는 그대로 올랐다. 이제
  `AssetDatabase.IsValidFolder`로 폴더를, `LoadAssetAtPath`로 에셋 하나하나를 확인하고,
  실패하면 읽기 전용 위치라는 것과 패키지를 임베드해야 한다는 것을 오류로 알린다
- **결손 노드를 고칠 방법이 없던 것.** 타입이 사라진 노드는 배열에 `null`로 남는데 `NodeIds`가
  그것을 건너뛰므로 그래프 창에 뷰가 생기지 않는다. 검사기는 그 때문에 생긴 끊어진 간선을
  오류로 보고하지만 화면에는 지울 것이 없었다. `MotionGraphInspector`에 **"결손 노드 정리"**
  버튼을 넣어 `MotionGraph.RemoveMissingNodes()`를 부른다.
  `MotionNodeInspector`의 도달 불가 분기에는 왜 닿지 않는지와 실제 정리 자리를 남겼다
- **검사 결과가 조용히 낡던 것.** 캐시가 그래프 **참조**로만 걸려 있어 그래프 창에서 노드를
  고쳐도 인스펙터의 "오류 N · 경고 M"이 옛 값 그대로였다. 캐시 키에
  `EditorUtility.GetDirtyCount(graph)`를 더하고 `Undo.undoRedoPerformed`에서도 무효화한다

에디터가 예외를 던지거나 계속 바쁘던 것.

- **IMGUI 컨트롤 개수 불일치.** Node Doctor와 프리셋 브라우저에서 같은 이벤트 안에 선택을
  바꾸면 Layout 패스와 Repaint 패스의 컨트롤 개수가 달라져
  `ArgumentException: Getting control N's position in a group with only M controls`가 났다.
  Doctor는 `_selected`가 바뀌면 상세 구역이 더 그려지고, 브라우저는 `Selection.activeObject`
  대입이 계층 선택을 비워 안내 `HelpBox`를 하나 더 켰다. 이제 클릭은 `_pendingSelection`에만
  적어 두고 `OnGUI` 끝에서 반영한다 — `MotionPlayerEditor`가 이미 쓰던 방식이다.
  같은 이유로 두 창의 "다시 검사"·"새로 고침"도 줄 수가 바뀌기 전에 다음 패스의 맨 앞으로 미룬다
- **프리뷰가 끝나도 인스펙터가 매 프레임 다시 그리던 것.** `IsPreviewingPlayer`는 목록에 있기만
  하면 `true`라 연출이 끝난 뒤에도 `RequiresConstantRepaint`가 계속 `true`였다.
  `MotionPreviewDriver.IsPlayingPreview(player)`를 더해 **실제로 도는 트리거가 있는지**를 보게
  했다. 인스펙터의 트리거 "정지"도 드라이버를 거치게 해 프리뷰 목록에서 빠지게 한다 —
  플레이 모드에서만 `player.Stop`을 직접 부른다
- **프리팹 오버라이드 적용이 프리뷰 중간값을 굽던 것.** `sceneSaving`과 `prefabSaving`만
  걸려 있어 `Apply All Overrides` 경로가 무방비였다. `PrefabUtility.prefabInstanceApplying`에도
  `StopAll`을 건다

쓸데없이 도는 일과 남는 자국.

- **슬라이더를 끄는 동안 그래프 전체를 다시 검사하던 것.** `ApplyModifiedProperties`는 드래그
  중 매 이벤트 `true`를 돌려주는데 그때마다 `Invalidate()`와 `Validate()`가 그래프 전체를
  훑었다(순환·도달성 포함). 파생 인덱스를 버리는 것은 **`SlotRef`가 바뀌었을 때만**으로 좁혔다 —
  인덱스가 캐시하는 것 중 노드 필드로 바뀌는 것은 그것뿐이다. 검사는 파라미터에도 영향을
  받으므로(`RepeatNode.Count`가 음수면 "무한 반복", `SubGraph`의 참조는 순환 검사에 들어간다)
  건너뛰지 않고 **손을 뗄 때까지 미룬다**
- 팔레트가 항목을 고를 때마다 `MotionNodeDoctor.FindSample`을 두 번 부르던 것.
  설명과 "예시 열기" 버튼이 각각 불러 클릭 한 번에 프로젝트를 두 번 훑었다. 한 번만 부른다
- `MotionGraphViewImpl.MoveChild`가 `MoveLink` 실패에도 되돌리기 항목을 남기던 것.
  실패하면 `Undo.RevertAllDownToGroup`으로 방금 연 그룹을 되돌린다 —
  아무것도 바뀌지 않았는데 Ctrl+Z가 한 번 헛도는 것을 막는다
- 트리거를 추가해도 `TextField`가 비워지지 않던 것. 포커스를 놓지 않으면 컨트롤이 자기가
  들고 있던 문자열을 다시 그린다. `GUI.FocusControl(null)`을 부른다
- `MotionNodeDoctorWindow.Open`이 검사를 두 번 돌던 것. 창이 새로 만들어지면 `OnEnable`이
  이미 돌리므로, 이미 떠 있던 창을 메뉴로 다시 부른 경우에만 다시 검사한다
