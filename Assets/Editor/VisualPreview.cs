#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RhythmMilestone;

[InitializeOnLoad]
public static class VisualPreview
{
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static double began;
    static int stage,shot,frame;
    static float nextFrame;
    static readonly float[] Targets={7f,30f,54f,78f,90f,114f};
    static VisualPreview() { EditorApplication.update+=Tick; }
    public static void OpenLevelSelect()
    {
        Start();
        SessionState.SetBool("VisualPreview",false);
    }
    [MenuItem("Tools/Pulse/Preview Orbit")]
    public static void Start()
    {
        SessionState.SetBool("VisualPreview",true);
        EditorApplication.delayCall+=()=> {
            EditorSceneManager.OpenScene("Assets/Scenes/RhythmPrototype.unity");
            var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            view.Show(); view.Focus(); view.maximized=true;
            EditorApplication.isPlaying=true;
        };
    }
    static void Capture(string name)
    { Directory.CreateDirectory("Screenshots/VisualFrames"); ScreenCapture.CaptureScreenshot(Path.GetFullPath("Screenshots/"+name+".png")); }
    static bool IsScoringNote(object note)
    {
        var type=note.GetType();
        var question=(MathQuestion)type.GetField("question").GetValue(note);
        return (int)type.GetField("lane").GetValue(note)==question.CorrectLane;
    }
    static void Tick()
    {
        if(!SessionState.GetBool("VisualPreview",false)||!EditorApplication.isPlaying) return;
        var game=Object.FindAnyObjectByType<RhythmGamePrototype>(); if(game==null) return;
        double now=EditorApplication.timeSinceStartup;
        if(stage==0) { began=now; stage=1; }
        if(stage==1 && now-began>.7) { Capture("visual-level-select"); stage=2; }
        if(stage==2 && now-began>1.5)
        {
            var menu=(GameObject)game.GetType().GetField("menu",Flags).GetValue(game);
            menu.GetComponentsInChildren<Button>()[2].onClick.Invoke();
            game.GetType().GetField("autoplay",Flags).SetValue(game,true);
            began=now; stage=3; nextFrame=0;
            Debug.Log("ORBIT PREVIEW: level-card click passed.");
        }
        if(stage==3)
        {
            float t=(float)(AudioSettings.dspTime-(double)game.GetType().GetField("songStartDSP",Flags).GetValue(game));
            bool capturedShot=false;
            if(shot<Targets.Length && t>=Targets[shot])
            { Capture("orbit-"+VisualMotion.Names[shot].ToLower()); shot++; capturedShot=true; }
            for(int i=0;i<Targets.Length;i++)
            {
                if(!capturedShot && t>=Targets[i]-.7f && t<=Targets[i]+.8f && t>=nextFrame)
                { Capture("VisualFrames/"+(frame++).ToString("0000")); nextFrame=t+.125f; break; }
            }
            if((bool)game.GetType().GetField("finished",Flags).GetValue(game))
            {
                Capture("orbit-results"); stage=4; began=now;
                int score=(int)game.GetType().GetField("score",Flags).GetValue(game);
                int combo=(int)game.GetType().GetField("combo",Flags).GetValue(game);
                var chart=(IList)game.GetType().GetField("chart",Flags).GetValue(game);
                int expected=chart.Cast<object>().Count(note=>IsScoringNote(note))
                    + chart.Cast<object>().Count(note=>IsScoringNote(note) && (float)note.GetType().GetField("duration").GetValue(note)>0);
                if(score!=expected*1000||combo!=expected) Debug.LogError("ORBIT PREVIEW FAILED: score="+score+", combo="+combo);
                else Debug.Log("ORBIT PREVIEW PASSED: score="+score+", combo="+combo+", "+frame+" recorded frames.");
            }
        }
        if(stage==4 && now-began>1)
        {
            game.GetType().GetMethod("ShowMenu",Flags).Invoke(game,null);
            SessionState.SetBool("VisualPreview",false); stage=0;
            Debug.Log("ORBIT PREVIEW: returned to level select.");
        }
    }
}
#endif
