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
        public const float SongDuration = 120f;
        public const int SongQuestionCount = 10;

        public static List<MathQuestion> CreateVisual() => CreateSongQuestions(144f);
        public static List<MathQuestion> Create(bool tutorial) => tutorial
            ? new List<MathQuestion>
            {
                new MathQuestion("2 + 3 = ?", 5, 1, 12.5f),
                new MathQuestion("7 − 2 = ?", 5, 3, 17.5f)
            }
            : CreateSongQuestions(128f);

        static List<MathQuestion> CreateSongQuestions(float bpm)
        {
            var questions = new List<MathQuestion>();
            float secondsPerBeat = 60f / bpm;
            int[] answerLanes = { 1, 3, 0, 2, 3, 1, 2, 0, 3, 1 };
            for (int i = 0; i < SongQuestionCount; i++)
            {
                int left = 4 + i % 7;
                int right = 2 + i % 3;
                string op = i % 3 == 0 ? "+" : i % 3 == 1 ? "−" : "×";
                int answer = op == "+" ? left + right : op == "−" ? left - right : left * right;
                float time = (float)System.Math.Round((8f + i * 12f) / secondsPerBeat) * secondsPerBeat;
                questions.Add(new MathQuestion($"{left} {op} {right} = ?", answer, answerLanes[i], time));
            }
            return questions;
        }
    }
}
