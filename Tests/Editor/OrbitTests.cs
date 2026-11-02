#if UNITY_EDITOR_OSX
using NUnit.Framework;
using UnityEngine;
using Sarabunari.MagicTrackpad.Editor;

public class OrbitTests
{
    [Test] public void Yaw_KeepsHorizonLevel()
    {
        var r = SceneTrackpad.OrbitRotation(Quaternion.Euler(30, 0, 0), 40, 0);
        Assert.AreEqual(0f, (r * Vector3.right).y, 1e-4f);
    }

    [Test] public void Yaw_FlipsPastThePole()
    {
        var up = Quaternion.Euler(30, 0, 0);
        var over = Quaternion.Euler(150, 0, 0); // upside down, same view axis family
        Assert.Greater((up * Vector3.up).y, 0f);
        Assert.Less((over * Vector3.up).y, 0f);
        // Either way up, positive yaw must turn the view toward the camera's right.
        Assert.Greater(Vector3.Dot(SceneTrackpad.OrbitRotation(up, 10, 0) * Vector3.forward, up * Vector3.right), 0f);
        Assert.Greater(Vector3.Dot(SceneTrackpad.OrbitRotation(over, 10, 0) * Vector3.forward, over * Vector3.right), 0f);
    }

    [Test] public void DragZoom_UsesDominantAxis_RightOrUpZoomsIn()
    {
        Assert.AreEqual(5f, SceneTrackpad.DragZoom(5, 1));
        Assert.AreEqual(5f, SceneTrackpad.DragZoom(1, -5));
        Assert.AreEqual(-5f, SceneTrackpad.DragZoom(1, 5));
    }

    [Test] public void ManySteps_StayNormalized()
    {
        var r = Quaternion.identity;
        for (var i = 0; i < 100000; i++) r = SceneTrackpad.OrbitRotation(r, 1.3f, 0.7f);
        Assert.AreEqual(1f, Quaternion.Dot(r, r), 1e-5f);
    }
}
#endif
