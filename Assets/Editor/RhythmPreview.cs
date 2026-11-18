#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RhythmMilestone;

[InitializeOnLoad]
public static class RhythmPreview
{
    static double began,levelBegan;
    static int stage;
    static RhythmPreview() { EditorApplication.update+=Tick; }
    [MenuItem("Tools/Pulse/Preview Demo")]
    public static void Start()
    {
        SessionState.SetBool("PulsePreview",true);
        EditorApplication.delayCall+=()=> {
            EditorSceneManager.OpenScene("Assets/Scenes/RhythmPrototype.unity");
            var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            view.Show(); view.Focus(); view.maximized=true;
            EditorApplication.isPlaying=true;
        };
    }
    static void Capture(string name)
    {
        string directory=Path.GetFullPath("Screenshots"); Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"),1);
    }
    static void Tick()
    {
        if(!SessionState.GetBool("PulsePreview",false)||!EditorApplication.isPlaying) return;
        var game=Object.FindAnyObjectByType<RhythmGamePrototype>(); if(game==null) return;
        double now=EditorApplication.timeSinceStartup;
        if(stage==0) { began=now; stage=1; }
        if(stage==1 && now-began>.7) { Capture("level-select"); stage=2; }
        if(stage==2 && now-began>1.5)
        {
            var menu=(GameObject)game.GetType().GetField("menu",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
            menu.GetComponentsInChildren<Button>()[1].onClick.Invoke();
            if((bool)game.GetType().GetField("tutorial",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game))
                throw new System.Exception("Basic selection failed");
            game.GetType().GetMethod("ShowMenu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);
            menu.GetComponentsInChildren<Button>()[0].onClick.Invoke();
            Debug.Log("PULSE MATH PREVIEW: both level-card click handlers passed.");
            game.GetType().GetField("autoplay",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(game,true);
            levelBegan=now; stage=3;
        }
        if(stage==3 && now-levelBegan>6.8) { Capture("tutorial-hold"); stage=4; }
        if(stage==4 && now-levelBegan>13.5) { Capture("math-tutorial"); stage=5; }
        if(stage==5 && (bool)game.GetType().GetField("finished",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game))
        {
            Capture("tutorial-results"); stage=6; began=now;
            Debug.Log("PULSE MATH PREVIEW: Tutorial runtime score="+game.GetType().GetField("score",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game));
        }
        if(stage==6 && now-began>1)
        {
            game.GetType().GetMethod("ShowMenu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);
            SessionState.SetBool("PulsePreview",false); stage=0;
            Debug.Log("PULSE MATH PREVIEW: captures complete; level selection ready.");
        }
    }
}
#endif
