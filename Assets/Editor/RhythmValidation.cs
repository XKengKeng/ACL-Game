#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RhythmMilestone;

public static class RhythmValidation
{
    static int checks;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool condition,string message) { if(!condition) throw new Exception(message); checks++; }
    static object Get(object obj,string field)=>obj.GetType().GetField(field,Flags).GetValue(obj);
    static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Flags).Invoke(obj,args);
    static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Flags).SetValue(obj,value);
    static List<MathQuestion> Questions(object game)=>(List<MathQuestion>)Get(game,"questions");

    public static void Run()
    {
        try
        {
            var hold=new HoldTracker(2); hold.Begin();
            Check(hold.Tick(1,true)==HoldTracker.Result.Holding,"Sustain pressed");
            Check(hold.Tick(2,true)==HoldTracker.Result.Complete,"Complete at tail");
            Check(hold.Tick(3,false)==HoldTracker.Result.Complete,"No duplicate tail reward");
            hold=new HoldTracker(2); hold.Begin(); Check(hold.Tick(1,false)==HoldTracker.Result.Broken,"Early release breaks");
            Check(hold.Tick(2,true)==HoldTracker.Result.Broken,"Cannot revive broken hold");
            hold=new HoldTracker(2); hold.Begin(); Check(hold.Tick(1.94f,false)==HoldTracker.Result.Complete,"Forgiving final release");
            int[] answers={5,5,7,6,8,6,9,5};
            for(int level=0;level<2;level++)
            {
                var questions=MathLesson.Create(level==0);
                Check(questions.Count==(level==0?8:16),"Level question count");
                for(int i=0;i<questions.Count;i++)
                {
                    var q=questions[i];
                    Check(q.Choices.Distinct().Count()==4,"Four distinct choices");
                    Check(q.Choices[q.CorrectLane]==q.Answer,"Exactly one correct lane");
                    string[] parts=q.Prompt.Split(' '); int a=int.Parse(parts[0]),b=int.Parse(parts[2]);
                    int expected=parts[1]=="+"?a+b:parts[1]=="−"?a-b:a*b;
                    Check(q.Answer==expected,"Equation matches answer");
                    if(level==0) Check(q.Answer==answers[i],"Known tutorial arithmetic");
                    if(i+1<questions.Count) Check(q.Time+q.Duration+.38f<questions[i+1].Time,"Enough room between questions and holds");
                }
            }
            var root=new GameObject("Validation"); var game=root.AddComponent<RhythmGamePrototype>(); Call(game,"Awake");
            var audio=(PulseAudio)Get(game,"sound"); Call(audio,"Awake");
            Check(!(bool)Get(game,"playing") && ((GameObject)Get(game,"menu")).activeSelf,"Boots to level select");
            foreach(string name in new[]{"click","correct","wrong","hold","kick","snare","hat"})
            {
                AudioClip clip=(AudioClip)Get(audio,name);
                Check(clip!=null && clip.frequency==44100 && clip.channels==1,"Generated sound format");
                var samples=new float[clip.samples]; clip.GetData(samples,0);
                Check(samples.Any(v=>Math.Abs(v)>.01f),"Sound contains audible signal");
                Check(samples.All(v=>!float.IsNaN(v)&&Math.Abs(v)<=1),"Finite, unclipped samples");
            }
            audio.ToggleMute(); Check(audio.Muted,"Mute enabled"); audio.ToggleMute(); Check(!audio.Muted,"Mute disabled");
            for(int level=0;level<2;level++)
            {
                game.StartLevel(level); Set(game,"autoplay",true);
                Check((bool)Get(game,"playing") && !((GameObject)Get(game,"menu")).activeSelf,"Starting level switches screens");
                int expected=Questions(game).Sum(q=>q.Duration>0?2:1);
                for(float t=-2;t<50;t+=.016f) { Call(game,"Spawn",t); Call(game,"TickNotes",t); }
                Check((int)Get(game,"score")==expected*1000,"Autoplay only scores correct answers and tails");
                Check((int)Get(game,"combo")==expected && (int)Get(game,"judgments")==expected,"Distractors do not break combo");
                Check((int)Get(game,"mistakes")==0 && ((IList)Get(game,"notes")).Count==0,"Every question resolves once");
                Call(game,"Complete"); Check(!(bool)Get(audio,"running"),"Beat stops at results");
                Check(((GameObject)Get(game,"resultButtons")).activeSelf,"Results navigation visible");
            }
            game.StartLevel(0); var lesson=Questions(game);
            Call(game,"Spawn",lesson[0].Time); Call(game,"TryHit",lesson[0].CorrectLane,lesson[0].Time);
            Check((int)Get(game,"score")==1000 && (int)Get(game,"combo")==1,"Manual correct math choice");
            Call(game,"TickNotes",lesson[0].Time+.4f);
            Check((int)Get(game,"score")==1000 && (int)Get(game,"mistakes")==0,"Ignoring wrong options is safe");
            Call(game,"Spawn",lesson[1].Time); Call(game,"TryHit",(lesson[1].CorrectLane+1)%4,lesson[1].Time);
            Check((int)Get(game,"score")==500 && (int)Get(game,"combo")==0,"Wrong answer loses 500 and breaks combo");
            Call(game,"TickNotes",lesson[1].Time+.5f);
            Check((int)Get(game,"judgments")==2 && (int)Get(game,"mistakes")==1,"Wrong round cannot also trigger a miss");
            Call(game,"Restart");
            Check((int)Get(game,"score")==0 && (int)Get(game,"next")==0 && Questions(game).All(q=>!q.Resolved),"Retry resets round state");
            Call(game,"Spawn",lesson[0].Time); Call(game,"TryHit",lesson[0].CorrectLane,lesson[0].Time+.2f);
            Check((int)Get(game,"score")==650,"Tutorial has forgiving GOOD timing");
            Call(game,"Restart");
            foreach(var q in lesson) if(q!=lesson[4]) q.Resolved=true;
            Call(game,"Spawn",lesson[4].Time); Call(game,"TryHit",lesson[4].CorrectLane,lesson[4].Time);
            Check((int)Get(game,"score")==1000,"Correct hold starts");
            Call(game,"TickNotes",lesson[4].Time+.25f);
            Check((int)Get(game,"score")==750 && (int)Get(game,"combo")==0 && (int)Get(game,"brokenHolds")==1,"Early release loses points and combo");
            Call(game,"Restart");
            for(float t=-2;t<50;t+=.016f) { Call(game,"Spawn",t); Call(game,"TickNotes",t); }
            Check((int)Get(game,"judgments")==10 && (int)Get(game,"mistakes")==8 && (int)Get(game,"score")==0,"Only missed correct answers count, score floors at zero");
            Call(game,"ShowMenu");
            Check(!(bool)Get(game,"playing") && !(bool)Get(audio,"running") && ((IList)Get(game,"notes")).Count==0,"Menu stops audio and clears playfield");
            ValidateVisual(game);
            ValidateSync(game,audio);
            ExportBeat(audio);
            Debug.Log("VISUAL RHYTHM VALIDATION PASSED: "+checks+" checks; Tutorial 10 / Basic 20 / Orbit 21 judgments; all six motion phases and gestures.");
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void ValidateVisual(RhythmGamePrototype game)
    {
        game.StartLevel(2);
        var math=Questions(game);
        Check(math.Count==12,"Orbit has twelve spaced math questions");
        foreach(var q in math)
        {
            Check(q.Choices.Distinct().Count()==4 && q.Choices[q.CorrectLane]==q.Answer,"Orbit choices are unique and solvable");
            var parts=q.Prompt.Split(' '); int a=int.Parse(parts[0]),b=int.Parse(parts[2]);
            Check(q.Answer==(parts[1]=="+"?a+b:parts[1]=="−"?a-b:a*b),"Orbit arithmetic");
        }
        for(int i=1;i<math.Count;i++)
            Check(math[i].Time-math[i-1].Time>3.3f,"Orbit gives over three seconds between questions");
        var chart=(IList)Get(game,"chart"); int expected=0;
        var bonuses=new List<object>();
        foreach(var n in chart)
        {
            if((bool)Get(n,"bonus")) bonuses.Add(n);
            if((int)Get(n,"lane")==((MathQuestion)Get(n,"question")).CorrectLane)
                expected+=(float)Get(n,"duration")>0?2:1;
        }
        Check(expected==21 && bonuses.Count==6,"Orbit has fifteen math judgments plus six rhythm bonuses");
        for(int i=0;i<chart.Count;i++) for(int j=i+1;j<chart.Count;j++)
        {
            var a=chart[i]; var b=chart[j];
            if((int)Get(a,"lane")!=(int)Get(b,"lane")) continue;
            Check((float)Get(b,"time")>=(float)Get(a,"time")+(float)Get(a,"duration")+.18f,"No overlapping input windows on a lane");
        }
        Set(game,"autoplay",true);
        for(float t=-2;t<48;t+=.016f) { Call(game,"UpdateVisuals",t); Call(game,"Spawn",t); Call(game,"TickNotes",t); }
        Check((int)Get(game,"score")==21000 && (int)Get(game,"combo")==21 && (int)Get(game,"mistakes")==0,"Full Orbit autoplay including holds, drag, and flick");
        Check(((IList)Get(game,"notes")).Count==0,"Orbit resolves every visual note");
        Call(game,"Restart");
        var tracks=(Transform[])Get(game,"tracks");
        for(int phase=0;phase<6;phase++)
        {
            float beat=phase*16+8,t=(beat+4)*60/144;
            var pose=VisualMotion.Sample(beat);
            Check(pose.Phase==phase,"All six visual phases are reachable");
            Call(game,"UpdateVisuals",t);
            for(int lane=0;lane<4;lane++)
            {
                Vector2 position=VisualMotion.LanePosition(pose,lane);
                Check(Vector2.Distance(position,tracks[lane].position)<.001f,"Runtime lane matches motion model");
                Check((int)Call(game,"PickLane",tracks[lane].position,t)==lane,"Mouse picking follows rotating and split lanes");
                Check(Mathf.Abs(position.x)<4 && Mathf.Abs(position.y)<4,"Receptors stay inside playable area");
            }
            var calm=VisualMotion.Sample(beat,true);
            Check(calm.PrimaryAngle==0 && calm.SecondaryAngle==0,"Reduced motion keeps all lanes horizontal");
        }
        for(int boundary=16;boundary<=80;boundary+=16)
        {
            var before=VisualMotion.Sample(boundary-.001f); var after=VisualMotion.Sample(boundary+.001f);
            for(int lane=0;lane<4;lane++)
                Check(Vector2.Distance(VisualMotion.LanePosition(before,lane),VisualMotion.LanePosition(after,lane))<.01f,"Phase transitions have no position jump");
        }
        foreach(NoteGesture gesture in new[]{NoteGesture.Drag,NoteGesture.Flick})
        {
            Call(game,"Restart"); Set(game,"autoplay",false);
            object target=bonuses.First(n=>((MathQuestion)Get(n,"question")).Gesture==gesture);
            var q=(MathQuestion)Get(target,"question");
            foreach(var n in chart) ((MathQuestion)Get(n,"question")).Resolved=true;
            q.Resolved=false;
            Call(game,"Spawn",q.Time);
            Call(game,"TryHit",q.CorrectLane,q.Time);
            Check((int)Get(game,"score")==0 && (int)Get(game,"mistakes")==0,"Preparing a gesture is not punished");
            if(gesture==NoteGesture.Drag)
            {
                Call(game,"TryGesture",q.CorrectLane,q.Time-.15f,gesture);
                Check((int)Get(game,"score")==0 && !q.Resolved,"An early held drag waits for the judgment line");
            }
            Call(game,"TryGesture",q.CorrectLane,q.Time,gesture);
            Check((int)Get(game,"score")==1000 && q.Resolved,"Manual gesture completes its own note type");
            Call(game,"TryGesture",q.CorrectLane,q.Time,gesture);
            Check((int)Get(game,"score")==1000,"Gesture scan cannot award duplicate points");
        }
        Call(game,"Restart");
        Check(((IList)Get(game,"notes")).Count==0 && bonuses.All(n=>!((MathQuestion)Get(n,"question")).Resolved),"Retry restores bonus state too");
        Call(game,"ShowMenu");
        Check(((GameObject)Get(game,"menu")).GetComponentsInChildren<UnityEngine.UI.Button>().Count(button=>button.name=="Level Card")==3,"Three selectable level cards");
    }

    static void ValidateSync(RhythmGamePrototype game,PulseAudio audio)
    {
        Check(BeatTrack.SampleIndex(1)==44100 && BeatTrack.SampleIndex(.5)==22050,"Audio sample positions are exact");
        var impulse=new float[1000]; BeatTrack.Mix(impulse,new[]{1f,.5f},.01,.25f);
        Check(impulse[441]==.25f && impulse[442]==.125f && impulse[440]==0,"Sound starts at its scheduled sample, without frame rounding");
        Check(BeatTrack.CalibrationOffsetMs(new double[]{.079,.081,.082,.083,.080,.079,.081,.220})==80,"Tap median rejects an outlier");
        Check(BeatTrack.CalibrationOffsetMs(new double[]{-.041,-.039,-.042,-.04})==-40,"Calibration supports earlier visuals");
        Check(BeatTrack.CalibrationOffsetMs(new double[]{.9})==250,"Calibration stays within safe adjustment range");
        for(int level=0;level<3;level++)
        {
            game.StartLevel(level);
            var chart=(IList)Get(game,"chart"); var cues=(float[])Get(audio,"cueTimes"); int expected=0;
            foreach(var note in chart)
            {
                var q=(MathQuestion)Get(note,"question"); if((int)Get(note,"lane")!=q.CorrectLane) continue;
                Check(cues.Contains((float)Get(note,"time")),"Every answer or bonus has a sample-aligned audio cue"); expected++;
                if((float)Get(note,"duration")>0)
                { Check(cues.Contains(q.Time+q.Duration),"Hold release has an audio cue"); expected++; }
            }
            Check(cues.Length==expected,"No duplicate cue for four answer choices");
            var backing=(AudioClip)Get(audio,"backing");
            Check(backing!=null && backing.length>(float)Get(game,"chartEnd"),"One backing clip covers the full chart");
            var samples=BeatTrack.Read(backing);
            Check(samples.Any(v=>Math.Abs(v)>.01f) && samples.All(v=>!float.IsNaN(v)&&Math.Abs(v)<=.94f+.00001f),"Rendered track has signal and cannot clip");
            Check(Math.Abs((double)Get(audio,"origin")-(double)Get(game,"songStartDSP"))<.000001,"Music and notes share exactly one DSP origin");
        }
        Set(game,"songStartDSP",AudioSettings.dspTime-5); Set(game,"audioOffsetMs",125);
        float time=(float)game.GetType().GetProperty("SongTime",Flags).GetValue(game);
        Check(Math.Abs(time-4.875f)<.02,"Positive device offset delays visuals and input judgment together");
        Set(game,"audioOffsetMs",0); Call(game,"ShowMenu");
    }

    static void ExportBeat(PulseAudio audio)
    {
        const int rate=44100; float beat=60f/96;
        var output=new float[(int)(beat*16*rate)];
        for(int tick=0;tick<32;tick++)
        {
            Mix((AudioClip)Get(audio,"hat"),output,(int)(tick*beat*.5f*rate),.18f);
            if(tick%4==0) Mix((AudioClip)Get(audio,"kick"),output,(int)(tick*beat*.5f*rate),.6f);
            if(tick%4==2) Mix((AudioClip)Get(audio,"snare"),output,(int)(tick*beat*.5f*rate),.32f);
        }
        Check(output.Max(v=>Math.Abs(v))<1,"Mixed beat does not clip");
        Directory.CreateDirectory("Screenshots");
        using(var writer=new BinaryWriter(File.Create("Screenshots/tutorial-beat.wav")))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+output.Length*2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate*2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(output.Length*2);
            foreach(float v in output) writer.Write((short)(v*32767));
        }
    }
    static void Mix(AudioClip clip,float[] output,int offset,float gain)
    { var samples=new float[clip.samples]; clip.GetData(samples,0); for(int i=0;i<samples.Length && i+offset<output.Length;i++) output[i+offset]+=samples[i]*gain; }
}
#endif
