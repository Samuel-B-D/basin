#!/usr/bin/env bash
#
# msstyles-dump.sh THEME.msstyles [--color C] [--size S] [--images DIR]
#                  [--class NAME] [--mui FILE] [--diagnostics]
#
# Prints a Windows visual style as XP-style INI, whichever format it is in.
# PACKTHEM 3 (XP) and PACKTHEM 4 (Vista+) both go through
# Basin.Themes.MsStyles, so this shows exactly what the reader understood.
# Classes, parts and states are named where the stylemap knows them and
# numbered where it does not (DWMWindow has no names anywhere).
#
#   --color C       the color scheme, the theme's first by default
#   --size S        the size variant, the theme's first by default
#   --images DIR    write every image, and every atlas crop, as PNG
#   --class NAME    print one class only
#   --mui FILE      read strings from this .mui rather than looking beside
#                   the theme in <locale>/ and en-US/
#   --diagnostics   print what the reader counted and skipped
#   --no-build      use the tool already built
#
# The tool is scripts/msstyles-dump, outside basin.slnx: it is for reading
# themes by hand, and no Microsoft theme is ever a test fixture.

set -euo pipefail

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
build=1
args=()
for arg in "$@"; do
    case "$arg" in
        --no-build) build=0 ;;
        -h|--help) sed -n '3,25p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) args+=("$arg") ;;
    esac
done

if [ "$build" = 1 ]; then
    dotnet build "$root/scripts/msstyles-dump" -c Release --nologo -v quiet >&2
fi

exec "$root/scripts/msstyles-dump/bin/Release/net10.0/msstyles-dump" "${args[@]}"
