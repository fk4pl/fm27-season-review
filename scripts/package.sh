#!/usr/bin/env bash
# Builds the release zip: dist/SeasonReview-<version>.zip
#
# Contents: only this project's plugin DLL, its language files, the README files, licence,
# changelog and the Linux installer. No BepInEx, Unity or Sports Interactive files.
# Needs FM27 with BepInEx run once on this machine (for the local build references).
#   scripts/package.sh [-p:GameDir="..."]

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/SeasonReview/SeasonReview.csproj | head -1)"
plugin_version="$(sed -n 's/.*public const string Version = "\(.*\)";.*/\1/p' src/SeasonReview/Plugin.cs)"
if [[ "$version" != "$plugin_version" ]]; then
  echo "Version mismatch: csproj $version, Plugin.cs $plugin_version" >&2
  exit 1
fi

dotnet test tests/SeasonReview.Tests -c Release --nologo -v quiet
dotnet build src/SeasonReview -c Release --nologo -v quiet -p:DeployToGame=false "$@"

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
sha256sum "$zip_path" > "$zip_path.sha256"
echo "Built $zip_path"
unzip -l "$zip_path"
cat "$zip_path.sha256"
