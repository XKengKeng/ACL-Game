using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmMilestone
{
    // One sample-aligned backing clip replaces per-frame scheduling of individual drums.
    public static class BeatTrack
    {
        public const int SampleRate=44100;
        public static int CalibrationOffsetMs(double[] errors)
        {
            if(errors.Length==0) return 0;
            var sorted=(double[])errors.Clone(); Array.Sort(sorted);
            double median=sorted.Length%2==0?(sorted[sorted.Length/2-1]+sorted[sorted.Length/2])*.5:sorted[sorted.Length/2];
            return Math.Max(-250,Math.Min(250,(int)Math.Round(median*200)*5));
        }
        public static int SampleIndex(double seconds)=>Math.Max(0,(int)Math.Round(seconds*SampleRate));
        public static void Mix(float[] output,float[] clip,double seconds,float volume)
        {
            int offset=SampleIndex(seconds);
            for(int i=0;i<clip.Length && offset+i<output.Length;i++) output[offset+i]+=clip[i]*volume;
        }
        public static float[] Read(AudioClip clip)
        { var samples=new float[clip.samples]; clip.GetData(samples,0); return samples; }
        public static float[] Render(float bpm,float duration,bool advanced,float[] kick,float[] snare,float[] hat,float[][] plucks,float[] cue,IList<float> cueTimes)
        {
            var output=new float[SampleIndex(duration+1)];
            double beat=60.0/bpm;
            for(int tick=0;tick*beat*.5<duration+.5;tick++)
            {
                double time=tick*beat*.5;
                Mix(output,hat,time,.18f);
                if(tick%4==0) Mix(output,kick,time,.6f);
                if(tick%4==2) Mix(output,snare,time,.32f);
                if(advanced && tick%2==0)
                {
                    int phase=Math.Min(5,(int)(VisualMotion.SongBeat((float)time)/16));
                    Mix(output,plucks[(tick/2+phase)%4],time,.45f);
                    if(phase>=4 && tick%8==6) Mix(output,snare,time+beat*.5,.16f);
                }
            }
            // A distinct, quiet cue also marks hold tails and off-beat bonus judgments.
            if(cueTimes!=null) foreach(float time in cueTimes) Mix(output,cue,time,.42f);
            float peak=0; foreach(float sample in output) peak=Math.Max(peak,Math.Abs(sample));
            if(peak>.94f) for(int i=0;i<output.Length;i++) output[i]*=.94f/peak;
            return output;
        }
    }
}
