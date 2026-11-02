# Magic Trackpad

macOS trackpad gestures for the Unity Scene view.

| Gesture | Action |
|---|---|
| Two-finger scroll | Orbit |
| Shift + two-finger scroll | Pan |
| Pinch | Zoom (also stops the panel-maximize gesture) |

Wheel mice and non-Scene panels are untouched. Tune speed/direction in
**Preferences > Sarabunari > Magic Trackpad**.

## Install

Package Manager > **+** > *Add package from disk…* > pick `package.json`, or add to `manifest.json`:

```json
"com.sarabunari.magictrackpad": "file:../../MagicTrackpad"
```

Or from git: `"com.sarabunari.magictrackpad": "https://github.com/<you>/MagicTrackpad.git"`.

## Rebuilding the native plugin

`Native~/build.sh` (needs Xcode CLI tools) rebuilds the universal `libMagicTrackpad.dylib`.
