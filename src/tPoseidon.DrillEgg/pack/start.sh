#!/bin/sh
here=$(cd "$(dirname "$0")" && pwd)
cd "$here" || exit 1
app="$here/app/drill-egg"
chmod +x "$app"
if [ "$(uname)" = "Darwin" ]; then
  xattr -dr com.apple.quarantine "$here" 2>/dev/null
  for file in "$app" "$here"/app/*.dylib; do
    codesign --verify "$file" 2>/dev/null || codesign --force --sign - "$file"
  done
fi
exec "$app" "$@"
