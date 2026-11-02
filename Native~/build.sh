#!/bin/bash
# Builds the universal dylib into the package. Needs Xcode command line tools.
set -e
cd "$(dirname "$0")"
OUT=../Editor/Plugins/macOS/libMagicTrackpad.dylib
clang -dynamiclib -fobjc-arc -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 \
  -framework Cocoa -install_name @rpath/libMagicTrackpad.dylib -o "$OUT" MagicTrackpad.m
codesign -f -s - "$OUT"
lipo -info "$OUT"
