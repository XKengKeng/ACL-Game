#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RhythmMilestone;

[InitializeOnLoad]
public static class SyncPreview
{
    static int stage; static double began;
    static SyncPreview() { EditorApplication.update+=Tick; }
    public static void Start()
    { SessionState.SetBool("SyncPreview",true); VisualPreview.OpenLevelSelect(); }
    static void Tick()
    {
        if(!SessionState.GetBool("SyncPreview",false)||!EditorApplication.isPlaying) return;
        var game=Object.FindAnyObjectByType<RhythmGamePrototype>(); if(game==null) return;
        double now=EditorApplication.timeSinceStartup;
        if(stage==0) { began=now; stage=1; }
        if(stage==1 && now-began>1)
        { game.GetType().GetMethod("OpenCalibration",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null); stage=2; began=now; }
        if(stage==2 && now-began>1)
        { Directory.CreateDirectory("Screenshots"); ScreenCapture.CaptureScreenshot(Path.GetFullPath("Screenshots/audio-sync.png")); stage=3; began=now; }
        if(stage==3 && now-began>1)
        { game.GetType().GetMethod("CloseCalibration",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null); SessionState.SetBool("SyncPreview",false); Debug.Log("SYNC PREVIEW PASSED: calibration opened and returned to menu."); }
    }
}
#endif
