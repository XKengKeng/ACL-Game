using UnityEngine;

namespace RhythmMilestone
{
    public struct VisualPose
    {
        public Vector2 Primary, Secondary;
        public float PrimaryAngle, SecondaryAngle, Split;
        public int Phase;
    }

    // All positions are deterministic samples of musical time, including transitions.
    public static class VisualMotion
    {
        public static readonly string[] Names={"DRIFT","TILT","SPLIT","REVERSE","ORBIT","FINALE"};
        public static VisualPose Sample(float beat,bool reduced=false)
        {
            float b=Mathf.Max(0,beat);
            int section=Mathf.Min(5,(int)(b/16));
            var target=Pose(section,b);
            if(section>0)
            {
                float blend=Mathf.SmoothStep(0,1,(b-section*16)/2);
                var from=Pose(section-1,b);
                target.Primary=Vector2.Lerp(from.Primary,target.Primary,blend);
                target.Secondary=Vector2.Lerp(from.Secondary,target.Secondary,blend);
                target.PrimaryAngle=Mathf.LerpAngle(from.PrimaryAngle,target.PrimaryAngle,blend);
                target.SecondaryAngle=Mathf.LerpAngle(from.SecondaryAngle,target.SecondaryAngle,blend);
                target.Split=Mathf.Lerp(from.Split,target.Split,blend);
            }
            target.Phase=section;
            if(reduced)
            {
                target.Primary=Vector2.Lerp(new Vector2(0,-2.4f),new Vector2(-1.65f,-2),target.Split);
                target.Secondary=Vector2.Lerp(new Vector2(0,-2.4f),new Vector2(1.65f,-2),target.Split);
                target.PrimaryAngle=target.SecondaryAngle=0;
            }
            return target;
        }
        static VisualPose Pose(int phase,float b)
        {
            float wave=Mathf.Sin(b*.20f);
            var p=new VisualPose { Primary=new Vector2(wave*.4f,-2.3f), Secondary=new Vector2(wave*.4f,-2.3f) };
            if(phase==0) p.Primary.y=p.Secondary.y=-2.3f+Mathf.Sin(b*.35f)*.35f;
            if(phase==1) { p.Primary=new Vector2(0,-1.4f); p.PrimaryAngle=wave*28; p.Secondary=p.Primary; p.SecondaryAngle=p.PrimaryAngle; }
            if(phase==2) { p.Split=1; p.Primary=new Vector2(-1.65f,-1.6f); p.Secondary=new Vector2(1.65f,-.5f); p.PrimaryAngle=-20; p.SecondaryAngle=20; }
            if(phase==3) { p.Primary=p.Secondary=new Vector2(wave*.45f,1.5f); p.PrimaryAngle=p.SecondaryAngle=180; }
            if(phase==4)
            {
                p.Split=1; float a=(b-64)*.22f;
                p.Primary=new Vector2(-1.55f+Mathf.Sin(a)*.3f,Mathf.Cos(a)*.7f);
                p.Secondary=new Vector2(1.55f-Mathf.Sin(a)*.3f,-Mathf.Cos(a)*.7f);
                p.PrimaryAngle=25+Mathf.Sin(a)*50; p.SecondaryAngle=205+Mathf.Sin(a)*50;
            }
            if(phase==5)
            {
                p.Split=1; p.Primary=new Vector2(-1.6f,-.5f+wave*.7f); p.Secondary=new Vector2(1.6f,.5f-wave*.7f);
                p.PrimaryAngle=-35+Mathf.Sin(b*.30f)*18; p.SecondaryAngle=145+Mathf.Sin(b*.30f)*18;
            }
            return p;
        }
        public static Vector2 LanePosition(VisualPose pose,int lane)
        {
            bool second=lane>=2;
            float fullX=(lane-1.5f)*1.55f;
            float splitX=((lane%2)-.5f)*1.5f;
            Vector2 center=second?pose.Secondary:pose.Primary;
            float angle=second?pose.SecondaryAngle:pose.PrimaryAngle;
            return center+(Vector2)(Quaternion.Euler(0,0,angle)*new Vector3(Mathf.Lerp(fullX,splitX,pose.Split),0,0));
        }
        public static float LaneAngle(VisualPose pose,int lane)=>lane<2?pose.PrimaryAngle:pose.SecondaryAngle;
    }

    public enum NoteGesture { Tap, Hold, Drag, Flick }
}
