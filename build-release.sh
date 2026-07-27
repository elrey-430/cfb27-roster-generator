#!/usr/bin/env bash
# Builds the Windows release: two self-contained executables that run on a
# clean Windows 10/11 machine with no .NET runtime installed, each with the
# editable data files and the roster templates beside it.
#
#   ./build-release.sh [version]
#
# Output: dist/CFB27-Roster-Generator-<version>-win-x64/ and a .zip of it.
set -euo pipefail

VERSION="${1:-$(date +%Y.%m.%d)}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAME="CFB27-Roster-Generator-${VERSION}-win-x64"
OUT="${ROOT}/dist/${NAME}"

echo "Building ${NAME}"
rm -rf "${OUT}"
mkdir -p "${OUT}"

# A release ships neither debug symbols nor the API documentation XML; both
# are build outputs, not things a player needs next to the executable.
COMMON=(
  -c Release
  -r win-x64
  --self-contained
  -p:PublishSingleFile=true
  # Roughly halves each executable (67 MB -> 34 MB). It costs a moment of
  # decompression on first launch, which is a good trade for a download an
  # end user has to fetch over a home connection.
  -p:EnableCompressionInSingleFile=true
  -p:DebugType=none
  -p:GenerateDocumentationFile=false
  -p:Version="${VERSION}"
  --nologo
  -v q
)

dotnet publish "${ROOT}/src/RosterGenerator.Gui" "${COMMON[@]}" -o "${OUT}"
dotnet publish "${ROOT}/src/RosterGenerator.Cli" "${COMMON[@]}" -o "${OUT}"

# Both projects copy data/ and templates/, so publishing the second on top of
# the first is deliberate: one folder, one copy of each file.
cp "${ROOT}/QUICKSTART.md" "${OUT}/README.txt"

# The save reader. Its dependencies are vendored into the release rather than
# left to an npm install on the user's machine: somebody downloading a roster
# tool should not have to run a package manager to open their own dynasty.
# Node itself is still required, and the app says so plainly when it is absent.
mkdir -p "${OUT}/tools/native-save"
cp "${ROOT}"/tools/native-save/*.mjs \
   "${ROOT}/tools/native-save/package.json" \
   "${ROOT}/tools/native-save/README.md" "${OUT}/tools/native-save/"
if [ -d "${ROOT}/tools/native-save/node_modules" ]; then
  cp -R "${ROOT}/tools/native-save/node_modules" "${OUT}/tools/native-save/"
else
  echo "WARNING: tools/native-save/node_modules is missing, so this build cannot"
  echo "         read dynasty saves directly. Run 'npm install' in"
  echo "         tools/native-save and build again."
fi

# The JavaScript runtime itself, shipped with the app. 87 MB raw, ~33 MB in the
# zip, and it buys the thing that matters most about this release: a user drops
# their dynasty save in and gets one back having installed nothing at all. A
# private copy also cannot be broken by whatever else on the machine wants a
# different Node version.
#
# Node.js is MIT licensed and its LICENSE ships beside it. The download is
# checksum-verified against nodejs.org's own SHASUMS256.txt, and cached between
# builds because it is 87 MB.
NODE_VERSION="v22.23.1"          # LTS "Jod"; the library needs >= 22.19
NODE_CACHE="${ROOT}/.node-cache/${NODE_VERSION}"
mkdir -p "${NODE_CACHE}"

if [ ! -f "${NODE_CACHE}/node.exe" ]; then
  echo "Fetching Node ${NODE_VERSION} (win-x64) …"
  curl -fsS -o "${NODE_CACHE}/SHASUMS256.txt" "https://nodejs.org/dist/${NODE_VERSION}/SHASUMS256.txt"
  curl -fsS -o "${NODE_CACHE}/node.exe"       "https://nodejs.org/dist/${NODE_VERSION}/win-x64/node.exe"
  curl -fsS -o "${NODE_CACHE}/LICENSE"        "https://raw.githubusercontent.com/nodejs/node/${NODE_VERSION}/LICENSE"
fi

# Verified every build, not just on download: a corrupted cache must not become
# a corrupted release.
EXPECTED="$(grep 'win-x64/node.exe' "${NODE_CACHE}/SHASUMS256.txt" | awk '{print $1}')"
ACTUAL="$(sha256sum "${NODE_CACHE}/node.exe" | awk '{print $1}')"
if [ -z "${EXPECTED}" ] || [ "${EXPECTED}" != "${ACTUAL}" ]; then
  echo "ERROR: node.exe failed checksum verification." >&2
  echo "  expected ${EXPECTED:-<none found>}" >&2
  echo "  actual   ${ACTUAL}" >&2
  echo "  Delete ${NODE_CACHE} and build again." >&2
  exit 1
fi

mkdir -p "${OUT}/tools/native-save/runtime"
cp "${NODE_CACHE}/node.exe" "${OUT}/tools/native-save/runtime/"
cp "${NODE_CACHE}/LICENSE"  "${OUT}/tools/native-save/runtime/LICENSE-nodejs.txt"
echo "Node ${NODE_VERSION} bundled (sha256 ${ACTUAL:0:16}…)"

( cd "${ROOT}/dist" && zip -qr "${NAME}.zip" "${NAME}" )

echo
echo "Release built:"
echo "  ${OUT}"
echo "  ${OUT}.zip"
echo
ls -la "${OUT}"
