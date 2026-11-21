#!/usr/bin/env bash
# Renders the old project's SVG icons to 48px-high PNGs for embedding in the Mono build.
# Output: src/Arisen Studio Mono/Resources/Icons/svg/<lowercased-basename>.png
# Sources: Resources/*.svg, Resources/Icons/*.svg and Resources/Icons/Socials/*.svg
# (Socials is beyond the original brief; the SvgImageBox social icons need it.)
# Requires rsvg-convert (librsvg2-bin). Run with: bash tools/render-svgs.sh
# Paths come from this script's location, so it can be run from anywhere.
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
SRC="$HERE/../../Arisen Studio/Resources"
OUT="$HERE/../Resources/Icons/svg"

if ! command -v rsvg-convert >/dev/null 2>&1; then
  echo "render-svgs: rsvg-convert not found (install librsvg2-bin); nothing rendered." >&2
  exit 0
fi

if [ ! -d "$SRC" ]; then
  echo "render-svgs: missing source folder $SRC" >&2
  exit 1
fi

mkdir -p "$OUT"

declare -A SEEN=()
rendered=0
skipped=0
failed=0

for dir in "$SRC" "$SRC/Icons" "$SRC/Icons/Socials"; do
  for svg in "$dir"/*.svg; do
    [ -e "$svg" ] || continue
    name=$(basename "$svg" .svg | tr '[:upper:]' '[:lower:]')
    if [ -n "${SEEN[$name]+x}" ]; then
      echo "render-svgs: skip $svg ($name.png already rendered from ${SEEN[$name]})" >&2
      skipped=$((skipped+1))
      continue
    fi
    SEEN[$name]=$svg
    if rsvg-convert -h 48 "$svg" -o "$OUT/$name.png"; then
      rendered=$((rendered+1))
    else
      echo "render-svgs: FAILED $svg" >&2
      failed=$((failed+1))
    fi
  done
done

echo "render-svgs: $rendered rendered, $skipped skipped, $failed failed -> $OUT"
[ "$failed" -eq 0 ]
