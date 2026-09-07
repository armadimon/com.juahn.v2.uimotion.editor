# UI Motion Tools (com.juahn.v2.uimotion.editor)

`com.juahn.v2.uimotion`의 저작 툴. 그래프 편집 창 · 노드 팔레트 · 슬롯 자동 바인딩 ·
Node Doctor · 인에디터 프리뷰 · 프리셋 브라우저가 여기에 들어간다.

런타임과 저장소를 나눈 이유는 배포 단위가 다르기 때문이다. 게임 빌드에 들어가는 것은
런타임 패키지뿐이고, 이 패키지는 에디터에서만 산다 (`includePlatforms: ["Editor"]`).

## 설치

**런타임 패키지가 먼저다.** 이 패키지는 그것 없이는 아무것도 컴파일되지 않는다.

Unity Package Manager → Add package from git URL, 순서대로 둘:

```
https://github.com/armadimon/com.juahn.v2.uimotion.git
https://github.com/armadimon/com.juahn.v2.uimotion.editor.git
```

| | |
|---|---|
| `com.juahn.v2.uimotion` | 0.1.0 이상 |
| Unity | 6000.0 이상 |

## 어셈블리

| 어셈블리 | 참조 | 내용 |
|---|---|---|
| `juahn.v2.UiMotion.Editor` | `juahn.v2.UiMotion` · `juahn.v2.UiMotion.Core` | 에디터 툴 전부 |

## 창 셋

전부 `Window > UI Motion` 아래에 있다.

| 창 | 하는 일 |
|---|---|
| **Graph** | 그래프를 노드로 조립한다. 왼쪽이 팔레트, 가운데가 그래프, 오른쪽이 노드 파라미터와 트리거 선언이다. 그래프 에셋을 더블클릭해도 열린다 |
| **Node Doctor** | 프로젝트의 모든 노드가 문서화 계약을 지키는지 표로 보여 준다. 배포 전에 여기가 비어야 한다 |
| **Presets** | 프로젝트의 모든 그래프를 훑고, 고른 것을 계층의 오브젝트에 한 번에 적용한다 |

메뉴에는 창이 아닌 항목이 둘 더 있다.

| 항목 | 하는 일 |
|---|---|
| **Stop All Previews** | 재생 중인 인에디터 프리뷰를 전부 멈춘다. 무한 루프 연출을 켜 둔 채 선택을 옮겼을 때의 탈출구다 |
| **Generate Missing Samples** | `[MotionNode(Sample = "...")]`가 가리키는데 아직 없는 예시 그래프를 만든다. 이미 있는 것은 덮어쓰지 않는다 |

### 그래프 창에서

- 빈 곳에서 **스페이스**나 우클릭 → 노드 검색 창. 미검증 노드는 "미검증" 그룹으로 격리돼 있다
- 포트를 끌어 간선을 잇는다. 노드 안에 **자식 실행 순서가 번호로** 보인다 —
  같은 부모에서 나가는 간선의 순서가 곧 `Sequence`의 실행 순서이기 때문이다
- 순서를 바꾸는 것은 오른쪽 노드 인스펙터의 **위로 / 아래로**다.
  간선을 지웠다 다시 이으면 그 자식이 맨 뒤로 가므로, 순서가 중요한 그래프라면 여기서 확인한다
- 슬롯은 텍스트가 아니라 **드롭다운**이다. 목록에 없는 이름은 "새 슬롯..."으로만 만든다 —
  오타 하나가 아무 경고 없이 동작하지 않는 연출을 만들기 때문이다

## 최단 경로 — 그래프를 만들어 프리팹에 붙이기

1. `Create > Juahn > UI Motion > Motion Graph`로 에셋을 만들고 더블클릭한다
2. 팔레트나 스페이스 검색으로 노드를 넣고 잇는다
3. 오른쪽 **트리거 선언**에서 `Start` 만들기를 누른다. 시작 노드를 고른 채 누르면
   진입점까지 함께 붙는다. **트리거 선언이 없으면 그래프는 아무것도 재생하지 않는다**
4. `Window > UI Motion > Presets`를 열고 만든 그래프를 고른다
5. 계층에서 연출을 붙일 오브젝트를 고른다 (고른 그래프는 그대로 남는다)
6. **선택한 오브젝트에 적용**을 누른다 — `MotionPlayer`가 붙고, 그래프가 꽂히고,
   슬롯 이름과 같은 이름의 자식이 자동으로 채워진다. 되돌리기 한 번으로 전부 돌아간다
7. 채워지지 않은 슬롯은 `MotionPlayer` 인스펙터에서 손으로 꽂는다.
   같은 인스펙터의 트리거 시험 재생으로 **플레이 모드에 들어가지 않고** 확인한다

## 노드를 새로 만들 때 — Node Doctor를 통과시키는 법

노드는 만들자마자 팔레트의 **"미검증"** 그룹으로 격리된다. 쓸 수는 있지만 배포 검사에서 걸린다.
격리를 벗어나려면 네 가지가 필요하다.

```csharp
[MotionNode(
    Name     = "Scale Punch",           // 팔레트에 뜨는 이름
    Category = "Transform",             // 팔레트의 그룹
    Summary  = "눌렀다 튀어 오르는 느낌을 준다. 버튼 피드백용.",
    Sample   = "ScalePunch")]           // 예시 그래프 에셋의 이름
[Serializable]                          // 없으면 저장되지 않아 노드가 조용히 사라진다
public sealed class ScalePunchNode : MotionEffectNode
{
    protected override IMotionHandle OnPlay(IMotionContext ctx) { ... }
}
```

`Sample`은 **경로가 아니라 이름**이다. 프로젝트 전체에서 그 이름의 `MotionGraph`를 찾는다 —
프로젝트가 자기 노드를 추가하면 그 예시는 이 패키지 밖에 있기 때문이다. 같은 이름이 둘 이상이면
어느 것이 쓰일지 알 수 없으므로 그것도 검사에서 걸린다.

예시 그래프는 손으로 만들어도 되고, `Window > UI Motion > Generate Missing Samples`로
빠진 것만 한꺼번에 만들어도 된다.

`[Serializable]`이 빠지면 `[SerializeReference]`가 저장하지 못해 **그래프를 다시 열었을 때
노드가 사라진다.** 조용히 일어나므로 Node Doctor가 따로 잡는다.

## 검증

### 컴파일 게이트 (로컬)

에디터 코드는 `UnityEditor` 어셈블리가 필요하고 그것은 재배포할 수 없다. 그래서
컴파일 검증은 CI가 아니라 로컬 게이트다. 설치된 Unity의 매니지드 DLL을 참조해
런타임 패키지의 `Runtime` 전체와 이 패키지의 `Editor` 전체를 `dotnet build`로 컴파일한다.

```bash
./Tools~/compile-check/run.sh
```

런타임 패키지는 기본적으로 `../com.juahn.v2.uimotion`에서 찾는다. 다른 곳에 있으면
경로를 넘긴다.

```bash
RUNTIME_PACKAGE=/path/to/com.juahn.v2.uimotion ./Tools~/compile-check/run.sh
UNITY_ROOT=/Applications/Unity/Hub/Editor/6000.5.3f1 ./Tools~/compile-check/run.sh
```

**에디터 파일을 건드린 모든 커밋은 이 게이트 통과가 조건이다.** Unity를 열기 전까지는
오타조차 발견되지 않기 때문이다.

### Node Doctor 배치 (로컬)

노드의 문서화 계약을 검사한다. 문제가 하나라도 있으면 종료 코드 1로 죽는다.

```bash
Unity -batchmode -quit -projectPath <프로젝트 경로> \
  -executeMethod Juahn.UiMotion.Editor.MotionNodeDoctor.RunBatch
```

Unity 라이선스가 필요해 CI에서는 돌릴 수 없다. 노드를 추가하거나 고친 뒤,
그리고 배포 전에 로컬에서 돌린다.

## CI

CI(`.github/workflows/ci.yml`)는 Unity 라이선스 없이 되는 것만 본다 — 매니페스트 형식,
에디터 전용 asmdef, `Runtime` 폴더가 생기지 않았는지, `.meta` 누락과 GUID 중복,
컴파일 게이트가 제자리에 있는지. 컴파일 게이트와 Node Doctor 배치는 로컬 게이트다.

## `.meta` 파일

git으로 배포되는 UPM 패키지는 `.meta`를 함께 커밋해야 한다. 빠지면 클론할 때마다
GUID가 새로 생겨 이 패키지의 에셋을 참조하던 것들이 끊긴다. `.github`와 `Tools~`
아래는 Unity가 임포트하지 않으므로 제외한다.

## 라이선스

MIT. `LICENSE.md` 참조.
