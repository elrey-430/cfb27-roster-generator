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

( cd "${ROOT}/dist" && zip -qr "${NAME}.zip" "${NAME}" )

echo
echo "Release built:"
echo "  ${OUT}"
echo "  ${OUT}.zip"
echo
ls -la "${OUT}"
