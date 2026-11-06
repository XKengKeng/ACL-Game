using System.Collections.Generic;

namespace RhythmMilestone
{
    public sealed class MathQuestion
    {
        public readonly string Prompt;
        public readonly int Answer, CorrectLane;
        public readonly int[] Choices;
        public readonly float Time, Duration;
        public readonly NoteGesture Gesture;
        public bool Resolved;
        public MathQuestion(string prompt,int answer,int lane,float time,float duration=0,NoteGesture gesture=NoteGesture.Tap)
        {
            Prompt=prompt; Answer=answer; CorrectLane=lane; Time=time; Duration=duration;
            Gesture=duration>0?NoteGesture.Hold:gesture;
            Choices=new int[4];
            for(int i=0;i<4;i++) Choices[i]=i==lane?answer:answer+i-lane;
        }
    }

    public static class MathLesson
    {
        public static List<MathQuestion> CreateVisual()
        {
            var result=new List<MathQuestion>();
            float beat=60f/144;
            for(int i=0;i<12;i++)
            {
                int a=3+i%6,b=2+i%3;
                int answer=i%3==1?a-b:i%3==2?a*b:a+b;
                string op=i%3==1?"−":i%3==2?"×":"+";
                // Vary answer placement rather than teaching a repeated lane sequence.
                int lane=new[]{1,3,0,2,3,1,2,0}[i%8];
                result.Add(new MathQuestion($"{a} {op} {b} = ?",answer,lane,(4+i*8)*beat,i%4==2?beat*1.5f:0));
            }
            return result;
        }

        public static List<MathQuestion> Create(bool tutorial)
        {
            var questions=new List<MathQuestion>();
            float beat=60f/(tutorial?96:128);
            int count=tutorial?8:16;
            for(int i=0;i<count;i++)
            {
                int lane=new[]{1,0,3,2}[i%4];
                int a,b,answer; string prompt;
                if(tutorial)
                {
                    int[] left={2,7,3,9,4,10,6,8},right={3,2,4,3,4,4,3,3};
                    a=left[i]; b=right[i]; bool subtract=i%2==1;
                    answer=subtract?a-b:a+b; prompt=$"{a} {(subtract?"−":"+")} {b} = ?";
                }
                else
                {
                    a=4+i%7; b=2+i%4;
                    if(i%3==2) { answer=a*b; prompt=$"{a} × {b} = ?"; }
                    else if(i%3==1) { answer=a-b; prompt=$"{a} − {b} = ?"; }
                    else { answer=a+b; prompt=$"{a} + {b} = ?"; }
                }
                bool hold=tutorial?i==4||i==7:i%4==3;
                questions.Add(new MathQuestion(prompt,answer,lane,4*beat+i*(tutorial?8:6)*beat,hold?beat*1.5f:0));
            }
            return questions;
        }
    }
}
