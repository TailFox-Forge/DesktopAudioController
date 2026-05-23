#!/usr/bin/env bash
set -euo pipefail

# 이 스크립트는 Windows x64 self-contained single-file exe 배포 산출물을 표준 방식으로 생성합니다.
# 인자가 없으면 현재 커밋 해시를 포함한 로컬 검증용 버전 문자열을 사용합니다.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_PATH="${REPO_ROOT}/src/DesktopAudioController/DesktopAudioController.csproj"
UPDATER_PROJECT_PATH="${REPO_ROOT}/src/DesktopAudioController.Updater/DesktopAudioController.Updater.csproj"

# 사용자가 명시하지 않으면 현재 커밋 기준 로컬 검증용 버전명을 사용합니다.
VERSION="${1:-v0.13.18-local-$(git -C "${REPO_ROOT}" rev-parse --short HEAD)}"
GIT_COMMIT="$(git -C "${REPO_ROOT}" rev-parse --short=12 HEAD 2>/dev/null || true)"
if [[ -z "${GIT_COMMIT}" ]]; then
    GIT_COMMIT="unknown"
fi

# 우선순위:
# 1. DOTNET_BIN 환경 변수
# 2. 현재 PATH 의 dotnet
# 3. 홈 디렉터리의 로컬 설치 경로
DOTNET_BIN="${DOTNET_BIN:-}"
if [[ -z "${DOTNET_BIN}" ]]; then
    if command -v dotnet >/dev/null 2>&1; then
        DOTNET_BIN="$(command -v dotnet)"
    elif [[ -x "${HOME}/.dotnet/dotnet" ]]; then
        DOTNET_BIN="${HOME}/.dotnet/dotnet"
    else
        echo "dotnet 실행 파일을 찾지 못했습니다. DOTNET_BIN을 지정하거나 .NET SDK를 설치하십시오." >&2
        exit 1
    fi
fi

RUNTIME_ID="win-x64"
PUBLISH_ROOT="${REPO_ROOT}/artifacts/release/${RUNTIME_ID}/${VERSION}"
PUBLISH_DIR="${PUBLISH_ROOT}/publish"
UPDATER_PUBLISH_DIR="${PUBLISH_ROOT}/updater"
PACKAGE_STAGING_DIR="${PUBLISH_ROOT}/package"
PACKAGE_PAYLOAD_DIR="${PACKAGE_STAGING_DIR}/DesktopAudioController"
PACKAGE_DIR="${REPO_ROOT}/artifacts/release/packages"
PACKAGE_BASENAME="DesktopAudioController-${VERSION}-${RUNTIME_ID}"
PACKAGE_PATH="${PACKAGE_DIR}/${PACKAGE_BASENAME}.zip"
CHECKSUM_PATH="${PACKAGE_PATH}.sha256"

# 이전 실행 산출물이 남아 있으면 혼동되므로 같은 버전의 publish 폴더와 패키지를 정리합니다.
rm -rf "${PUBLISH_ROOT}"
rm -f "${PACKAGE_PATH}" "${CHECKSUM_PATH}"
mkdir -p "${PUBLISH_DIR}" "${UPDATER_PUBLISH_DIR}" "${PACKAGE_PAYLOAD_DIR}" "${PACKAGE_DIR}"

create_zip() {
    local source_dir="$1"
    local target_zip="$2"

    python3 - "${source_dir}" "${target_zip}" <<'PY'
import pathlib
import sys
import zipfile

source_dir = pathlib.Path(sys.argv[1])
target_zip = pathlib.Path(sys.argv[2])

with zipfile.ZipFile(target_zip, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    for file_path in sorted(source_dir.rglob("*")):
        if file_path.is_file():
            archive.write(file_path, file_path.relative_to(source_dir))
PY
}

echo "[1/4] publish 시작: ${VERSION}"
echo "  - sourceRevision: ${GIT_COMMIT}"
echo "  - RID restore 수행: ${RUNTIME_ID}"
"${DOTNET_BIN}" restore "${PROJECT_PATH}" \
    -r "${RUNTIME_ID}" \
    -p:EnableWindowsTargeting=true
"${DOTNET_BIN}" restore "${UPDATER_PROJECT_PATH}" \
    -r "${RUNTIME_ID}"

"${DOTNET_BIN}" publish "${PROJECT_PATH}" \
    -c Release \
    -r "${RUNTIME_ID}" \
    --self-contained true \
    --no-restore \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:EnableWindowsTargeting=true \
    -p:SourceRevisionId="${GIT_COMMIT}" \
    -o "${PUBLISH_DIR}"

"${DOTNET_BIN}" publish "${UPDATER_PROJECT_PATH}" \
    -c Release \
    -r "${RUNTIME_ID}" \
    --self-contained true \
    --no-restore \
    -p:PublishSingleFile=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "${UPDATER_PUBLISH_DIR}"

echo "[2/4] updater 포함"
cp "${UPDATER_PUBLISH_DIR}/DesktopAudioController.Updater.exe" "${PUBLISH_DIR}/DesktopAudioController.Updater.exe"
cp -a "${PUBLISH_DIR}/." "${PACKAGE_PAYLOAD_DIR}/"

echo "[3/4] zip 생성: ${PACKAGE_PATH}"
create_zip "${PACKAGE_STAGING_DIR}" "${PACKAGE_PATH}"

echo "[4/4] sha256 생성: ${CHECKSUM_PATH}"
(
    cd "${PACKAGE_DIR}"
    sha256sum "${PACKAGE_BASENAME}.zip" > "${CHECKSUM_PATH}"
)

echo
echo "배포 산출물 생성 완료"
echo "  publish:  ${PUBLISH_DIR}"
echo "  payload:  ${PACKAGE_PAYLOAD_DIR}"
echo "  package:  ${PACKAGE_PATH}"
echo "  checksum: ${CHECKSUM_PATH}"
