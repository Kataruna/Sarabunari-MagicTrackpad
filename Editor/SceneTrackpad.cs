#if UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Sarabunari.MagicTrackpad.Editor
{
    /// Two-finger scroll orbits the Scene view (Shift = pan), pinch zooms it.
    [InitializeOnLoad]
    static class SceneTrackpad
    {
        const string Lib = "libMagicTrackpad";
        delegate int ScrollCb(float dx, float dy, int momentum, int shift);
        delegate int PinchCb(float magnification);
        [DllImport(Lib)] static extern void MagicTrackpad_Init(ScrollCb onScroll, PinchCb onPinch);
        [DllImport(Lib)] static extern void MagicTrackpad_Shutdown();

        // Fields keep the delegates alive while native holds the pointers.
        static readonly ScrollCb s_scroll = OnScroll;
        static readonly PinchCb s_pinch = OnPinch;

        static SceneTrackpad()
        {
            MagicTrackpad_Init(s_scroll, s_pinch);
            // Native must never call into a dead domain.
            AssemblyReloadEvents.beforeAssemblyReload += MagicTrackpad_Shutdown;
            EditorApplication.quitting += MagicTrackpad_Shutdown;
        }

        static SceneView Hovered => EditorWindow.mouseOverWindow as SceneView;

        [AOT.MonoPInvokeCallback(typeof(ScrollCb))]
        static int OnScroll(float dx, float dy, int momentum, int shift)
        {
            var sv = Hovered;
            if (sv == null) return 0;
            if (momentum != 0) return 1; // swallow inertia, it feels like drift
            if (sv.in2DMode) return 0;   // 2D has no orbit; keep Unity's behavior

            if (shift != 0) Pan(sv, dx, dy); else Orbit(sv, dx, dy);
            sv.Repaint();
            return 1;
        }

        [AOT.MonoPInvokeCallback(typeof(PinchCb))]
        static int OnPinch(float magnification)
        {
            var sv = Hovered;
            if (sv == null) return 0;
            // Spread (positive) zooms in, so the view gets closer.
            sv.size = Mathf.Max(0.001f, sv.size * Mathf.Clamp(1f - magnification * TrackpadPrefs.Zoom, 0.1f, 10f));
            sv.Repaint();
            return 1; // also swallows smart-magnify (magnification 0)
        }

        static void Orbit(SceneView sv, float dx, float dy)
        {
            var s = TrackpadPrefs.Orbit * TrackpadPrefs.Direction;
            var r = Quaternion.AngleAxis(-dx * s, Vector3.up) * sv.rotation
                    * Quaternion.AngleAxis(-dy * s, Vector3.right);
            sv.LookAt(sv.pivot, r, sv.size, false, true);
        }

        static void Pan(SceneView sv, float dx, float dy)
        {
            var s = TrackpadPrefs.Pan * TrackpadPrefs.Direction * sv.size;
            sv.pivot += sv.rotation * new Vector3(-dx * s, dy * s, 0f);
        }
    }

    static class TrackpadPrefs
    {
        const string K = "Sarabunari.MagicTrackpad.";
        public static float Orbit { get => EditorPrefs.GetFloat(K + "orbit", 0.2f); set => EditorPrefs.SetFloat(K + "orbit", value); }
        public static float Pan { get => EditorPrefs.GetFloat(K + "pan", 0.002f); set => EditorPrefs.SetFloat(K + "pan", value); }
        public static float Zoom { get => EditorPrefs.GetFloat(K + "zoom", 2f); set => EditorPrefs.SetFloat(K + "zoom", value); }
        public static bool Invert { get => EditorPrefs.GetBool(K + "invert", false); set => EditorPrefs.SetBool(K + "invert", value); }
        public static float Direction => Invert ? -1f : 1f;

        [SettingsProvider]
        static SettingsProvider Provider() => new("Preferences/Sarabunari/Magic Trackpad", SettingsScope.User)
        {
            guiHandler = _ =>
            {
                Orbit = EditorGUILayout.Slider("Orbit speed", Orbit, 0.02f, 1f);
                Pan = EditorGUILayout.Slider("Pan speed (Shift)", Pan, 0.0002f, 0.01f);
                Zoom = EditorGUILayout.Slider("Pinch zoom speed", Zoom, 0.2f, 6f);
                Invert = EditorGUILayout.Toggle("Invert orbit / pan", Invert);
            },
            keywords = new[] { "trackpad", "orbit", "pinch", "scene" }
        };
    }
}
#endif
