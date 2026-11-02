# Magic Trackpad

macOS trackpad gestures for the Unity Scene view.

| Gesture | Action |
|---|---|
| Two-finger scroll | Orbit |
| Shift + two-finger scroll | Pan |
| Command + two-finger scroll | Dolly zoom, like Alt + right-drag (add Shift for 3x) |
| Option + two-finger scroll | Unity's default behavior |
| Pinch | Zoom (also stops the panel-maximize gesture) |

Wheel mice and non-Scene panels are untouched. In 2D mode plain scroll stays Unity's; pinch and Command still zoom. Tune speed/direction in
**Preferences > Sarabunari > Magic Trackpad**.

## Install

Package Manager > **+** > *Add package from disk…* > pick `package.json`, or add to `manifest.json`:

```json
"com.sarabunari.magictrackpad": "file:../../MagicTrackpad"
```

Or from git: `"com.sarabunari.magictrackpad": "https://github.com/<you>/MagicTrackpad.git"`.

## Rebuilding the native plugin

`Native~/build.sh` (needs Xcode CLI tools) rebuilds the universal `libMagicTrackpad.dylib`.

Unity keeps the dylib loaded across domain reloads, so a new one needs an Editor restart.
If you change an export or callback signature, bump `MagicTrackpad_Version` and
`NativeVersion` together. On a mismatch the package logs a warning and stays off rather
than calling through the wrong signature.
