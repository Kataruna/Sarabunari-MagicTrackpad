#if UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Sarabunari.MagicTrackpad.Editor.Tests")]

namespace Sarabunari.MagicTrackpad.Editor
{
    /// Two-finger scroll orbits the Scene view (Shift = pan), pinch zooms it.
    [InitializeOnLoad]
    static class SceneTrackpad
    {
        const string Lib = "libMagicTrackpad";
        const int NativeVersion = 4; // must match MagicTrackpad_Version in Native~/MagicTrackpad.m
        delegate int ScrollCb(float dx, float dy, int momentum, int mods);
        delegate int PinchCb(int type, float magnification);
        const int Magnify = 30; // NSEventTypeMagnify
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
                bool shift = (mods & 1) != 0, command = (mods & 4) != 0;
                if ((mods & 2) != 0) return 0; // Option: Unity's own behavior
                if (sv.in2DMode && !command) return 0; // 2D has no orbit
                if (momentum != 0) return 1; // swallow inertia, it feels like drift

                if (command) Zoom(sv, DragZoom(dx, dy) * (shift ? 3f : 1f) * 0.0015f);
                else if (shift) Pan(sv, dx, dy);
                else Orbit(sv, dx, dy);
                sv.Repaint();
                return 1;
            }
            catch (Exception e) { Debug.LogException(e); return 0; }
        }

        [AOT.MonoPInvokeCallback(typeof(PinchCb))]
        static int OnPinch(int type, float magnification)
        {
            try
            {
                var sv = Hovered;
                if (sv == null) return 0;
                if (type != Magnify) return 1; // swallow the rest so nothing maximizes the panel
                // Spread (positive) zooms in, so the view gets closer.
                Zoom(sv, magnification);
                sv.Repaint();
                return 1;
            }
            catch (Exception e) { Debug.LogException(e); return 0; }
        }

        static void Zoom(SceneView sv, float amount) =>
            sv.size = Mathf.Max(0.001f, sv.size * Mathf.Clamp(1f - amount * TrackpadPrefs.Zoom * TrackpadPrefs.ZoomDir, 0.1f, 10f));

        // Same as Alt + right-drag: dominant axis, right or up zooms in.
        internal static float DragZoom(float dx, float dy) => Mathf.Abs(dx) > Mathf.Abs(dy) ? dx : -dy;

        static void Orbit(SceneView sv, float dx, float dy)
        {
            if (sv.isRotationLocked) return;
            var s = TrackpadPrefs.Orbit * TrackpadPrefs.OrbitDir;
            sv.LookAt(sv.pivot, OrbitRotation(sv.rotation, dx * s, dy * s), sv.size, sv.orthographic, true);
        }

        internal static Quaternion OrbitRotation(Quaternion r, float yaw, float pitch)
        {
            if ((r * Vector3.up).y < 0f) yaw = -yaw; // past the pole: keep left meaning left
            return Quaternion.Normalize(Quaternion.AngleAxis(yaw, Vector3.up) * r * Quaternion.AngleAxis(pitch, Vector3.right));
        }

        static void Pan(SceneView sv, float dx, float dy)
        {
            var s = TrackpadPrefs.Pan * TrackpadPrefs.PanDir * sv.size;
            sv.pivot += sv.rotation * new Vector3(-dx * s, dy * s, 0f);
        }
    }

    /// Cached: the gesture callbacks read these at trackpad event rate.
    static class TrackpadPrefs
    {
        const string K = "Sarabunari.MagicTrackpad.";
        public static float Orbit = EditorPrefs.GetFloat(K + "orbit", 0.2f);
        public static float Pan = EditorPrefs.GetFloat(K + "pan", 0.002f);
        public static float Zoom = EditorPrefs.GetFloat(K + "zoom", 2f);
        public static bool InvertOrbit = EditorPrefs.GetBool(K + "invertOrbit", false);
        public static bool InvertPan = EditorPrefs.GetBool(K + "invertPan", false);
        public static bool InvertZoom = EditorPrefs.GetBool(K + "invertZoom", false);
        public static bool Debug = EditorPrefs.GetBool(K + "debug", false);
        public static float OrbitDir => InvertOrbit ? -1f : 1f;
        public static float PanDir => InvertPan ? -1f : 1f;
        public static float ZoomDir => InvertZoom ? -1f : 1f;

        static void Save()
        {
            EditorPrefs.SetFloat(K + "orbit", Orbit);
            EditorPrefs.SetFloat(K + "pan", Pan);
            EditorPrefs.SetFloat(K + "zoom", Zoom);
            EditorPrefs.SetBool(K + "invertOrbit", InvertOrbit);
            EditorPrefs.SetBool(K + "invertPan", InvertPan);
            EditorPrefs.SetBool(K + "invertZoom", InvertZoom);
            EditorPrefs.SetBool(K + "debug", Debug);
        }

        [SettingsProvider]
        static SettingsProvider Provider() => new("Preferences/Sarabunari/Magic Trackpad", SettingsScope.User)
        {
            guiHandler = _ =>
            {
                EditorGUI.BeginChangeCheck();
                Orbit = EditorGUILayout.Slider("Orbit speed", Orbit, 0.02f, 1f);
                Pan = EditorGUILayout.Slider("Pan speed (Shift)", Pan, 0.0002f, 0.01f);
                Zoom = EditorGUILayout.Slider("Zoom speed (pinch, Command)", Zoom, 0.2f, 6f);
                InvertOrbit = EditorGUILayout.Toggle("Invert orbit", InvertOrbit);
                InvertPan = EditorGUILayout.Toggle("Invert pan", InvertPan);
                InvertZoom = EditorGUILayout.Toggle("Invert zoom", InvertZoom);
                Debug = EditorGUILayout.Toggle("Log gesture events (Editor.log)", Debug);
                if (!EditorGUI.EndChangeCheck()) return;
                Save();
                SceneTrackpad.SetDebug(Debug);
            },
            keywords = new[] { "trackpad", "orbit", "pinch", "scene" }
        };
    }
}
#endif
