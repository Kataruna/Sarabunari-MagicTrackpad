#if UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Sarabunari.MagicTrackpad.Editor.Tests")]

namespace Sarabunari.MagicTrackpad.Editor
{
    /// Two-finger scroll orbits the Scene view (Shift = pan, Command = look), pinch zooms it.
    [InitializeOnLoad]
    static class SceneTrackpad
    {
        const string Lib = "libMagicTrackpad";
        const int NativeVersion = 7; // must match MagicTrackpad_Version in Native~/MagicTrackpad.m
        delegate int ScrollCb(float dx, float dy, int momentum, int mods);
        delegate int PinchCb(float magnification);
        [DllImport(Lib)] static extern void MagicTrackpad_Init(ScrollCb onScroll, PinchCb onPinch);
        [DllImport(Lib)] static extern void MagicTrackpad_Shutdown();
        [DllImport(Lib)] static extern int MagicTrackpad_Version();
        [DllImport(Lib)] static extern void MagicTrackpad_SetDebug(int on);

        // Fields keep the delegates alive while native holds the pointers.
        static readonly ScrollCb s_scroll = OnScroll;
        static readonly PinchCb s_pinch = OnPinch;

        // A throw here must not leave the monitor running: nothing could ever remove
        // it, and its callbacks would hit a type that failed to initialize.
        static SceneTrackpad()
        {
            try
            {
                if (MagicTrackpad_Version() != NativeVersion) throw new EntryPointNotFoundException("version mismatch");
            }
            catch (EntryPointNotFoundException)
            {
                // An older dylib is still in memory from before a package update.
                Stop();
                Debug.LogWarning("Magic Trackpad: native plugin was updated. Restart the Editor to enable gestures.");
                return;
            }
            catch (DllNotFoundException e)
            {
                Debug.LogWarning($"Magic Trackpad: native plugin failed to load, gestures are off. {e.Message}");
                return;
            }

            // Native must never call into a dead domain.
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            SetDebug(TrackpadPrefs.Debug);
            MagicTrackpad_Init(s_scroll, s_pinch);
        }

        internal static void SetDebug(bool on)
        {
            try { MagicTrackpad_SetDebug(on ? 1 : 0); }
            catch (EntryPointNotFoundException) { } // old dylib: warned at startup
        }

        static void Stop()
        {
            try { MagicTrackpad_Shutdown(); }
            catch (EntryPointNotFoundException) { }
        }

        static SceneView Hovered => EditorWindow.mouseOverWindow as SceneView;

        // The callbacks run under an AppKit frame: an exception escaping them kills the Editor.
        [AOT.MonoPInvokeCallback(typeof(ScrollCb))]
        static int OnScroll(float dx, float dy, int momentum, int mods)
        {
            try
            {
                var sv = Hovered;
                if (sv == null) return 0;
                var action = TrackpadPrefs.Resolve(mods);
                if (action == ScrollAction.UnityDefault || sv.in2DMode) return 0;
                if (momentum != 0) return 1; // swallow inertia, it feels like drift

                if (action == ScrollAction.Look) Look(sv, dx, dy);
                else if (action == ScrollAction.Pan) Pan(sv, dx, dy);
                else Orbit(sv, dx, dy);
                sv.Repaint();
                return 1;
            }
            catch (Exception e) { Debug.LogException(e); return 0; }
        }

        [AOT.MonoPInvokeCallback(typeof(PinchCb))]
        static int OnPinch(float magnification)
        {
            try
            {
                var sv = Hovered;
                if (TrackpadPrefs.Debug) Debug.Log($"[MagicTrackpad] pinch over {EditorWindow.mouseOverWindow}");
                if (sv == null) return 0;
                // Spread (positive) zooms in, so the view gets closer.
                sv.size = Mathf.Max(0.001f, sv.size * Mathf.Clamp(1f - magnification * TrackpadPrefs.Zoom * TrackpadPrefs.ZoomDir, 0.1f, 10f));
                sv.Repaint();
                return 1;
            }
            catch (Exception e) { Debug.LogException(e); return 0; }
        }

        static float Sign(bool invert) => invert ? -1f : 1f;

        static void Orbit(SceneView sv, float dx, float dy)
        {
            if (sv.isRotationLocked) return;
            var s = TrackpadPrefs.Orbit;
            sv.LookAt(sv.pivot, OrbitRotation(sv.rotation, dx * s * Sign(TrackpadPrefs.InvertOrbitX), dy * s * Sign(TrackpadPrefs.InvertOrbitY)),
                sv.size, sv.orthographic, true);
        }

        // Right-drag look: the camera turns in place, so the pivot moves instead.
        static void Look(SceneView sv, float dx, float dy)
        {
            if (sv.isRotationLocked) return;
            var s = TrackpadPrefs.Orbit;
            var from = sv.rotation;
            var to = OrbitRotation(from, dx * s * Sign(TrackpadPrefs.InvertLookX), dy * s * Sign(TrackpadPrefs.InvertLookY));
            sv.LookAt(LookPivot(sv.pivot, from, to, sv.cameraDistance), to, sv.size, sv.orthographic, true);
        }

        internal static Vector3 LookPivot(Vector3 pivot, Quaternion from, Quaternion to, float distance) =>
            pivot + (to * Vector3.forward - from * Vector3.forward) * distance;

        internal static Quaternion OrbitRotation(Quaternion r, float yaw, float pitch)
        {
            if ((r * Vector3.up).y < 0f) yaw = -yaw; // past the pole: keep left meaning left
            return Quaternion.Normalize(Quaternion.AngleAxis(yaw, Vector3.up) * r * Quaternion.AngleAxis(pitch, Vector3.right));
        }

        static void Pan(SceneView sv, float dx, float dy)
        {
            var s = TrackpadPrefs.Pan * sv.size;
            sv.pivot += sv.rotation * new Vector3(-dx * s * Sign(TrackpadPrefs.InvertPanX), dy * s * Sign(TrackpadPrefs.InvertPanY), 0f);
        }
    }

    enum ScrollAction { Orbit, Pan, Look, UnityDefault }

    /// Cached: the gesture callbacks read these at trackpad event rate.
    static class TrackpadPrefs
    {
        const string K = "Sarabunari.MagicTrackpad.";
        static bool OldInvert(string name) => EditorPrefs.GetBool(K + name, false); // pre-0.2 single toggles

        public static float Orbit = EditorPrefs.GetFloat(K + "orbit", 0.2f);
        public static float Pan = EditorPrefs.GetFloat(K + "pan", 0.002f);
        public static float Zoom = EditorPrefs.GetFloat(K + "zoom", 2f);
        public static bool InvertOrbitX = EditorPrefs.GetBool(K + "invertOrbitX", OldInvert("invertOrbit"));
        public static bool InvertOrbitY = EditorPrefs.GetBool(K + "invertOrbitY", OldInvert("invertOrbit"));
        public static bool InvertLookX = EditorPrefs.GetBool(K + "invertLookX", OldInvert("invertOrbit"));
        public static bool InvertLookY = EditorPrefs.GetBool(K + "invertLookY", OldInvert("invertOrbit"));
        public static bool InvertPanX = EditorPrefs.GetBool(K + "invertPanX", OldInvert("invertPan"));
        public static bool InvertPanY = EditorPrefs.GetBool(K + "invertPanY", OldInvert("invertPan"));
        public static bool InvertZoom = EditorPrefs.GetBool(K + "invertZoom", false);
        public static bool Debug = EditorPrefs.GetBool(K + "debug", false);
        public static float ZoomDir => InvertZoom ? -1f : 1f;

        // Native modifier bit -> label, checked in this order when several keys are held.
        static readonly (int bit, string label, ScrollAction fallback)[] s_keys =
        {
            (2, "Option + scroll", ScrollAction.UnityDefault),
            (4, "Command + scroll", ScrollAction.Look),
            (8, "Control + scroll", ScrollAction.UnityDefault),
            (1, "Shift + scroll", ScrollAction.Pan),
        };
        static readonly ScrollAction[] s_bindings = LoadBindings();
        static ScrollAction s_plain = (ScrollAction)EditorPrefs.GetInt(K + "bindPlain", (int)ScrollAction.Orbit);

        static ScrollAction[] LoadBindings()
        {
            var b = new ScrollAction[s_keys.Length];
            for (var i = 0; i < b.Length; i++) b[i] = (ScrollAction)EditorPrefs.GetInt(K + "bind" + s_keys[i].bit, (int)s_keys[i].fallback);
            return b;
        }

        internal static ScrollAction Resolve(int mods) => Resolve(mods, s_bindings, s_plain);

        internal static ScrollAction Resolve(int mods, ScrollAction[] bindings, ScrollAction plain)
        {
            for (var i = 0; i < s_keys.Length; i++)
                if ((mods & s_keys[i].bit) != 0) return bindings[i];
            return plain;
        }

        internal static ScrollAction[] DefaultBindings()
        {
            var b = new ScrollAction[s_keys.Length];
            for (var i = 0; i < b.Length; i++) b[i] = s_keys[i].fallback;
            return b;
        }

        static void Save()
        {
            EditorPrefs.SetFloat(K + "orbit", Orbit);
            EditorPrefs.SetFloat(K + "pan", Pan);
            EditorPrefs.SetFloat(K + "zoom", Zoom);
            EditorPrefs.SetBool(K + "invertOrbitX", InvertOrbitX);
            EditorPrefs.SetBool(K + "invertOrbitY", InvertOrbitY);
            EditorPrefs.SetBool(K + "invertLookX", InvertLookX);
            EditorPrefs.SetBool(K + "invertLookY", InvertLookY);
            EditorPrefs.SetBool(K + "invertPanX", InvertPanX);
            EditorPrefs.SetBool(K + "invertPanY", InvertPanY);
            EditorPrefs.SetBool(K + "invertZoom", InvertZoom);
            EditorPrefs.SetBool(K + "debug", Debug);
            EditorPrefs.SetInt(K + "bindPlain", (int)s_plain);
            for (var i = 0; i < s_keys.Length; i++) EditorPrefs.SetInt(K + "bind" + s_keys[i].bit, (int)s_bindings[i]);
        }

        [SettingsProvider]
        static SettingsProvider Provider() => new("Preferences/Sarabunari/Magic Trackpad", SettingsScope.User)
        {
            guiHandler = _ =>
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.LabelField("Two-finger scroll bindings", EditorStyles.boldLabel);
                s_plain = (ScrollAction)EditorGUILayout.EnumPopup("Scroll", s_plain);
                for (var i = 0; i < s_keys.Length; i++)
                    s_bindings[i] = (ScrollAction)EditorGUILayout.EnumPopup(s_keys[i].label, s_bindings[i]);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Invert", EditorStyles.boldLabel);
                InvertOrbitX = EditorGUILayout.Toggle("Orbit horizontal", InvertOrbitX);
                InvertOrbitY = EditorGUILayout.Toggle("Orbit vertical", InvertOrbitY);
                InvertLookX = EditorGUILayout.Toggle("Look horizontal", InvertLookX);
                InvertLookY = EditorGUILayout.Toggle("Look vertical", InvertLookY);
                InvertPanX = EditorGUILayout.Toggle("Pan horizontal", InvertPanX);
                InvertPanY = EditorGUILayout.Toggle("Pan vertical", InvertPanY);
                InvertZoom = EditorGUILayout.Toggle("Pinch zoom", InvertZoom);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel);
                Orbit = EditorGUILayout.Slider("Orbit / look speed", Orbit, 0.02f, 1f);
                Pan = EditorGUILayout.Slider("Pan speed", Pan, 0.0002f, 0.01f);
                Zoom = EditorGUILayout.Slider("Pinch zoom speed", Zoom, 0.2f, 6f);

                EditorGUILayout.Space();
                Debug = EditorGUILayout.Toggle("Log gesture events (Editor.log)", Debug);

                if (!EditorGUI.EndChangeCheck()) return;
                Save();
                SceneTrackpad.SetDebug(Debug);
            },
            keywords = new[] { "trackpad", "orbit", "pinch", "scene", "invert", "keybinding", "modifier" }
        };
    }
}
#endif
