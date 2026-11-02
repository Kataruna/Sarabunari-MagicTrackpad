# Magic Trackpad

macOS trackpad gestures for the Unity Scene view.

| Gesture | Action |
|---|---|
| Two-finger scroll | Orbit |
| Shift + two-finger scroll | Pan |
| Command + two-finger scroll | Look around in place, like right-drag |
| Option + two-finger scroll | Unity's default behavior |
| Pinch | Zoom (also stops the panel-maximize gesture) |

Those are the defaults. Wheel mice and non-Scene panels are untouched. In 2D mode scroll stays
Unity's; pinch still zooms.

**Preferences > Sarabunari > Magic Trackpad** sets which action each modifier triggers
(Orbit, Pan, Look or Unity Default), per-axis inversion, and speeds. If several modifiers
are held, the first of Option, Command, Control, Shift wins.

## Install

Package Manager > **+** > *Add package from disk…* > pick `package.json`, or add to `manifest.json`:

```json
"com.sarabunari.magictrackpad": "file:../../MagicTrackpad"
```

Or from git: `"com.sarabunari.magictrackpad": "https://github.com/Kataruna/Sarabunari-MagicTrackpad.git"`.

## Rebuilding the native plugin

`Native~/build.sh` (needs Xcode CLI tools) rebuilds the universal `libMagicTrackpad.dylib`.

Unity keeps the dylib loaded across domain reloads, so a new one needs an Editor restart.
If you change an export or callback signature, bump `MagicTrackpad_Version` and
`NativeVersion` together. On a mismatch the package logs a warning and stays off rather
than calling through the wrong signature.
