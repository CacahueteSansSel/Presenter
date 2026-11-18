#!/usr/bin/env bash

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
output="$root/build"

if [[ "$(uname -s)" != Darwin ]]; then
    echo "Ce script doit être exécuté sur macOS." >&2
    exit 1
fi

case "${1:-$(uname -m)}" in
    arm64) runtime=osx-arm64 ;;
    x86_64|x64) runtime=osx-x64 ;;
    *) echo "Architecture attendue : arm64 ou x64." >&2; exit 2 ;;
esac

mkdir -p "$output"
work="$(mktemp -d "$output/.presenter.XXXXXX")"
trap 'rm -rf "$work"' EXIT

app="$work/Presenter.app"
contents="$app/Contents"
packaging="$root/src/Presenter/Packaging"

mkdir -p "$contents/MacOS" "$contents/Resources"
AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$root/src/Presenter/Presenter.csproj" -c Release -r "$runtime" \
    --self-contained true -o "$contents/MacOS"

cp "$packaging/Info.plist" "$contents/Info.plist"
cp "$packaging/Presenter.icns" "$contents/Resources/Presenter.icns"
plutil -lint "$contents/Info.plist"
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

if [[ -e "$output/Presenter.app" ]]; then
    mv "$output/Presenter.app" "$work/previous.app"
fi

if ! mv "$app" "$output/Presenter.app"; then
    if [[ -e "$work/previous.app" ]]; then
        mv "$work/previous.app" "$output/Presenter.app"
    fi
    exit 1
fi

printf '%s\n' "$output/Presenter.app"
