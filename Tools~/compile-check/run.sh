#!/usr/bin/env bash
#
# 에디터 계층 컴파일 게이트.
#
# Unity 에디터를 열지 않고 런타임 패키지 + 이 패키지의 Editor 전체를 컴파일한다.
# 에디터 파일을 건드렸으면 커밋 전에 이것을 돌린다.
#
#   ./Tools~/compile-check/run.sh
#   UNITY_ROOT=... RUNTIME_PACKAGE=... ./Tools~/compile-check/run.sh
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(cd "${HERE}/../.." && pwd)"

# --- 런타임 패키지를 찾는다 --------------------------------------------
if [ -z "${RUNTIME_PACKAGE:-}" ]; then
  RUNTIME_PACKAGE="$(cd "${PACKAGE_ROOT}/../com.juahn.v2.uimotion" 2>/dev/null && pwd || true)"
fi

if [ -z "${RUNTIME_PACKAGE}" ] || [ ! -d "${RUNTIME_PACKAGE}/Runtime/Core" ]; then
  echo "런타임 패키지를 찾지 못했습니다. RUNTIME_PACKAGE를 지정하세요." >&2
  echo "  예: RUNTIME_PACKAGE=/path/to/com.juahn.v2.uimotion $0" >&2
  exit 2
fi

# --- Unity 설치를 찾는다 ------------------------------------------------
if [ -z "${UNITY_ROOT:-}" ]; then
  UNITY_ROOT="$(ls -d /Applications/Unity/Hub/Editor/*/ 2>/dev/null | sort -V | tail -1 || true)"
fi

if [ -z "${UNITY_ROOT}" ] || [ ! -d "${UNITY_ROOT}" ]; then
  echo "Unity 설치를 찾지 못했습니다. UNITY_ROOT를 지정하세요." >&2
  exit 2
fi

UNITY_MANAGED="${UNITY_ROOT}/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine"
if [ ! -d "${UNITY_MANAGED}" ]; then
  echo "매니지드 어셈블리 폴더가 없습니다: ${UNITY_MANAGED}" >&2
  echo "이 스크립트는 macOS 레이아웃을 가정합니다." >&2
  exit 2
fi

if [ -z "${UNITY_UGUI:-}" ]; then
  UNITY_UGUI="$(ls "${UNITY_ROOT}"/Unity.app/Contents/Resources/PackageManager/ProjectTemplates/libcache/*/ScriptAssemblies/UnityEngine.UI.dll 2>/dev/null | head -1 || true)"
fi

if [ -z "${UNITY_UGUI}" ] || [ ! -f "${UNITY_UGUI}" ]; then
  echo "UnityEngine.UI.dll을 찾지 못했습니다. UNITY_UGUI로 지정하세요." >&2
  exit 2
fi

echo "Unity:   ${UNITY_ROOT}"
echo "런타임:  ${RUNTIME_PACKAGE}"
echo

dotnet build "${HERE}/UiMotion.Editor.Compile.csproj" \
  -p:UnityManaged="${UNITY_MANAGED}" \
  -p:UnityUgui="${UNITY_UGUI}" \
  -p:RuntimePackage="${RUNTIME_PACKAGE}" \
  -v quiet --nologo

echo
echo "에디터 계층 컴파일 통과."
