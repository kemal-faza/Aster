#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# Pinned to microsoft/fluentui-system-icons release tag 1.1.346.
url="https://github.com/microsoft/fluentui-system-icons/raw/1.1.346/fonts/FluentSystemIcons-Regular.ttf"
meta_url="https://github.com/microsoft/fluentui-system-icons/raw/1.1.346/fonts/FluentSystemIcons-Regular.json"

curl -fsSL "$url" -o "$tmp/FluentSystemIcons-Regular.ttf"
curl -fsSL "$meta_url" -o "$tmp/FluentSystemIcons-Regular.json"

mkdir -p "$root/src/Aster.Avalonia/Aster.UI/Assets/Fonts"
mkdir -p "$root/src/Aster.Avalonia/Aster.UI/Controls"
cp "$tmp/FluentSystemIcons-Regular.ttf" "$root/src/Aster.Avalonia/Aster.UI/Assets/Fonts/"
python3 "$root/scripts/generate-glyph-map.py" "$tmp/FluentSystemIcons-Regular.json" \
  "$root/src/Aster.Avalonia/Aster.UI/Controls/GlyphMap.g.cs" \
  "$root/src/Aster.Avalonia/Aster.UI/Assets/Fonts/FluentSystemIcons-Regular.ttf"
(cd "$root" && sha256sum "src/Aster.Avalonia/Aster.UI/Assets/Fonts/FluentSystemIcons-Regular.ttf") \
  | tee "$root/src/Aster.Avalonia/Aster.UI/Assets/Fonts/FluentSystemIcons-Regular.sha256"
