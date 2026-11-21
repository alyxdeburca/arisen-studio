#!/usr/bin/env bash
# Renders the shared SVG icons to PNG for embedding in the Mono build.
# Requires rsvg-convert (librsvg2-bin). Run from anywhere: paths come from this script's location.
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
SVG="$HERE/../../Arisen Studio/Resources/NewWindow.svg"
OUT="$HERE/../Resources/Icons/new-window.png"
mkdir -p "$(dirname "$OUT")"
rsvg-convert -h 48 "$SVG" -o "$OUT"
