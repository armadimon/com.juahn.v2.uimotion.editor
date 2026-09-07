# UI Motion Tools (com.juahn.v2.uimotion.editor)

`com.juahn.v2.uimotion`의 저작 툴. 그래프 편집 창 · 노드 팔레트 · 슬롯 자동 바인딩 ·
Node Doctor · 인에디터 프리뷰 · 프리셋 브라우저가 여기에 들어간다.

런타임과 저장소를 나눈 이유는 배포 단위가 다르기 때문이다. 게임 빌드에 들어가는 것은
런타임 패키지뿐이고, 이 패키지는 에디터에서만 산다 (`includePlatforms: ["Editor"]`).

## 의존

| | |
|---|---|
| `com.juahn.v2.uimotion` | 0.1.0 이상 |
| Unity | 6000.0 이상 |

## 어셈블리

| 어셈블리 | 참조 | 내용 |
|---|---|---|
| `juahn.v2.UiMotion.Editor` | `juahn.v2.UiMotion` · `juahn.v2.UiMotion.Core` | 에디터 툴 전부 |

## 컴파일 게이트

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

## CI

CI(`.github/workflows/ci.yml`)는 Unity 라이선스 없이 되는 것만 본다 — 매니페스트 형식,
에디터 전용 asmdef, `Runtime` 폴더가 생기지 않았는지, `.meta` 누락과 GUID 중복,
컴파일 게이트가 제자리에 있는지.

## `.meta` 파일

git으로 배포되는 UPM 패키지는 `.meta`를 함께 커밋해야 한다. 빠지면 클론할 때마다
GUID가 새로 생겨 이 패키지의 에셋을 참조하던 것들이 끊긴다. `.github`와 `Tools~`
아래는 Unity가 임포트하지 않으므로 제외한다.

## 라이선스

MIT. `LICENSE.md` 참조.
