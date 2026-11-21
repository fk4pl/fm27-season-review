#!/usr/bin/env bash
# Builds the release zip: dist/SeasonReview-<version>.zip
#
# Contents: only this project's plugin DLL, its language files, the README files, licence,
# changelog and the Linux installer. No BepInEx, Unity or Sports Interactive files.
# Needs FM27 with BepInEx run once on this machine (for the local build references).
#   scripts/package.sh [-p:GameDir="..."]

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# The version lives only in the csproj; Plugin.Version is generated from it at build time.
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/SeasonReview/SeasonReview.csproj | head -1)"

dotnet test tests/SeasonReview.Tests -c Release --nologo -v quiet
dotnet build src/SeasonReview -c Release --nologo -v quiet -p:DeployToGame=false "$@"

# The built DLL must carry the version being packaged (UTF-16 strings in the assembly metadata).
# (Read into a variable first: with pipefail, "strings | grep -q" fails when grep exits early.)
dll=src/SeasonReview/bin/Release/net6.0/SeasonReview.dll
dll_strings="$(strings -el "$dll")"
if ! grep -qxF "$version" <<<"$dll_strings"; then
  echo "The built DLL does not report version $version:" >&2
  grep -E '^[0-9]+\.[0-9]+\.[0-9]+' <<<"$dll_strings" >&2 || true
  exit 1
fi

stage="$(mktemp -d)"
trap 'rm -rf -- "$stage"' EXIT
plugin="$stage/BepInEx/plugins/SeasonReview"
mkdir -p "$plugin/lang"
cp src/SeasonReview/bin/Release/net6.0/SeasonReview.dll "$plugin/"
cp lang/*.txt "$plugin/lang/"
cp README.md README.tr.md LICENSE CHANGELOG.md "$stage/"
cp scripts/install-linux.sh "$stage/"
chmod +x "$stage/install-linux.sh"

# Allow-list check: nothing but our own files may be in the zip.
unexpected="$(cd "$stage" && find . -type f \
  ! -path './BepInEx/plugins/SeasonReview/SeasonReview.dll' \
  ! -path './BepInEx/plugins/SeasonReview/lang/*.txt' \
  ! -name README.md ! -name README.tr.md ! -name LICENSE ! -name CHANGELOG.md ! -name install-linux.sh)"
if [[ -n "$unexpected" ]]; then
  echo "Unexpected files in the package:" >&2
  echo "$unexpected" >&2
  exit 1
fi

mkdir -p dist
zip_path="dist/SeasonReview-$version.zip"
rm -f -- "$zip_path"
(cd "$stage" && zip -q -r -X "$OLDPWD/$zip_path" .)
# Checksum file lists the bare file name, so `sha256sum -c` works wherever both files are downloaded.
(cd dist && sha256sum "SeasonReview-$version.zip" > "SeasonReview-$version.zip.sha256")
echo "Built $zip_path"
unzip -l "$zip_path"
cat "$zip_path.sha256"
