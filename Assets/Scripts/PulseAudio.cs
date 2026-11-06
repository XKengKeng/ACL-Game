using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmMilestone
{
    // Original PCM percussion, played on the DSP clock instead of accumulated frame deltas.
    public sealed class PulseAudio : MonoBehaviour
    {
        const int Rate=44100;
        AudioSource effects, music;
        AudioClip backing,calibrationClip;
        float preparedBpm,preparedDuration; bool preparedAdvanced;
        float[] cueTimes=new float[0];
        AudioSource[] drums;
        AudioClip click,correct,wrong,hold,kick,snare,hat;
        AudioClip[] plucks;
        int visualPhase=-1;
        double origin;
        float beat;
        bool running,muted;
        public bool Muted => muted;

        void Awake()
        {
            effects=gameObject.AddComponent<AudioSource>(); effects.playOnAwake=false;
            music=gameObject.AddComponent<AudioSource>(); music.playOnAwake=false; music.spatialBlend=0;
            drums=new AudioSource[16];
            for(int i=0;i<drums.Length;i++) { drums[i]=gameObject.AddComponent<AudioSource>(); drums[i].playOnAwake=false; }
            click=Tone("Click",.045f,1300,600,.25f);
            correct=Tone("Correct",.10f,880,1320,.22f);
            wrong=Tone("Wrong",.16f,180,65,.23f);
            hold=Tone("Hold complete",.18f,660,1320,.23f);
            kick=Drum("Kick",.24f,0); snare=Drum("Snare",.13f,1); hat=Drum("Hi hat",.045f,2);
            float[] notes={130.81f,155.56f,196f,233.08f}; plucks=new AudioClip[4];
            for(int i=0;i<4;i++) plucks[i]=Tone("Orbit pluck "+i,.24f,notes[i],notes[i],.22f);
        }
        public void Prepare(float bpm,float duration,bool advanced,float[] targets)
        {
            bool same=backing!=null && preparedBpm==bpm && preparedDuration==duration && preparedAdvanced==advanced && cueTimes.Length==targets.Length;
            if(same) for(int i=0;i<targets.Length;i++) if(cueTimes[i]!=targets[i]) { same=false; break; }
            if(same) return;
            Stop();
            if(backing!=null) { if(Application.isPlaying) Destroy(backing); else DestroyImmediate(backing); }
            cueTimes=(float[])targets.Clone(); preparedBpm=bpm; preparedDuration=duration; preparedAdvanced=advanced;
            var tones=new float[plucks.Length][];
            for(int i=0;i<tones.Length;i++) tones[i]=BeatTrack.Read(plucks[i]);
            backing=Clip("Sample aligned backing",BeatTrack.Render(bpm,duration,advanced,BeatTrack.Read(kick),BeatTrack.Read(snare),BeatTrack.Read(hat),tones,BeatTrack.Read(click),cueTimes));
        }
        public void Begin(double dspOrigin,float bpm)
        {
            Stop(); origin=dspOrigin; beat=60/bpm; running=true;
            if(backing==null) Prepare(bpm,60,false,new float[0]);
            running=true; music.clip=backing; music.loop=false; music.mute=muted;
            music.PlayScheduled(dspOrigin);
        }
        public double BeginCalibration()
        {
            Stop();
            if(calibrationClip==null)
            {
                var samples=new float[BeatTrack.SampleIndex(2.4)]; var tick=BeatTrack.Read(click);
                for(int i=0;i<4;i++) BeatTrack.Mix(samples,tick,i*.6,i==0?1:.65f);
                calibrationClip=Clip("Sync calibration / 100 BPM",samples);
            }
            music.clip=calibrationClip; music.loop=true; music.mute=false;
            double start=AudioSettings.dspTime+.6; music.PlayScheduled(start); return start;
        }
        public void Stop()
        { running=false; if(music!=null) music.Stop(); if(drums!=null) foreach(var source in drums) source.Stop(); if(effects!=null) effects.Stop(); }
        public void ToggleMute()
        {
            muted=!muted; effects.mute=muted; music.mute=muted;
            foreach(var source in drums) source.mute=muted;
        }
        public void Click() { Play(click); }
        public void Correct(bool tail=false) { Play(tail?hold:correct); }
        public void Wrong() { Play(wrong); }
        public void SetVisualPhase(int phase) { visualPhase=phase; }
        void Play(AudioClip clip) { if(Application.isPlaying && !muted) effects.PlayOneShot(clip); }
        static AudioClip Tone(string name,float seconds,float from,float to,float volume)
        {
            var samples=new float[(int)(seconds*Rate)]; double phase=0;
            for(int i=0;i<samples.Length;i++)
            {
                float p=(float)i/samples.Length; phase+=2*Math.PI*Mathf.Lerp(from,to,p)/Rate;
                float envelope=Mathf.Min(1,i/(Rate*.003f))*Mathf.Pow(1-p,2);
                samples[i]=(float)Math.Sin(phase)*envelope*volume;
            }
            return Clip(name,samples);
        }
        static AudioClip Drum(string name,float seconds,int type)
        {
            var samples=new float[(int)(seconds*Rate)]; var random=new System.Random(96+type); double phase=0;
            float previous=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/Rate,p=(float)i/samples.Length,noise=(float)random.NextDouble()*2-1;
                phase+=2*Math.PI*(type==0?45+105*Math.Exp(-t*40):185)/Rate;
                float value=type==0?(float)Math.Sin(phase):type==1?noise*.75f+(float)Math.Sin(phase)*.25f:(noise-previous)*.5f;
                previous=noise;
                samples[i]=value*Mathf.Exp(-p*(type==0?6:8))*Mathf.Min(1,i/50f)*.75f;
            }
            return Clip(name,samples);
        }
        static AudioClip Clip(string name,float[] samples)
        { var clip=AudioClip.Create(name,samples.Length,1,Rate,false); clip.SetData(samples,0); return clip; }
    }
}
