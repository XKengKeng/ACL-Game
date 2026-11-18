using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RhythmMilestone
{
    public sealed class RhythmGamePrototype : MonoBehaviour
    {
        const float LeadIn = 2f, Delay = 1.5f, Perfect = .06f, Good = .12f, Bad = .18f, Miss = .20f;
        const float Speed = 3.8f;
        static readonly KeyCode[] Keys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K };
        readonly Color[] colors = {
            new Color(.22f,.88f,1), new Color(.62f,.47f,1),
            new Color(1,.36f,.67f), new Color(1,.78f,.3f) };
        readonly List<ChartNote> chart = new List<ChartNote>();
        readonly List<Note> notes = new List<Note>();
        readonly List<Effect> effects = new List<Effect>();
        readonly SpriteRenderer[] receptors = new SpriteRenderer[4];
        readonly SpriteRenderer[] beams = new SpriteRenderer[4];
        readonly float[] flashes = new float[4];
        Transform line;
        readonly Transform[] tracks=new Transform[4], lineDecor=new Transform[2];
        readonly TextMesh[] keyLabels=new TextMesh[4];
        readonly SpriteRenderer[] lineCores=new SpriteRenderer[2],lineGlows=new SpriteRenderer[2];
        readonly List<LineRenderer> orbitRings=new List<LineRenderer>();
        GameObject syncPanel;
        Text syncStatus;
        Image syncFlash;
        bool calibrating;
        double calibrationStart;
        readonly List<double> syncTaps=new List<double>();
        int audioOffsetMs,lastTapBeat=int.MinValue;
        bool advanced, reducedMotion;
        GameObject questionCard;
        float Bpm => advanced?144:tutorial?96:128;
        float TravelSpeed => advanced?2.7f:Speed;
        Sprite white, capsule, glow;
        Text scoreText, comboText, feedbackText, modeText, resultText;
        Text questionText, lessonText;
        GameObject hud, menu, resultButtons;
        PulseAudio sound;
        List<MathQuestion> questions=new List<MathQuestion>();
        bool playing, tutorial;
        int mouseLane=-1, mistakes, earnedPoints;
        double songStartDSP;
        float HitWindow => tutorial?.35f:Bad;
        float MissLimit => tutorial?.38f:Miss;
        float PerfectLimit => tutorial?.16f:Perfect;
        float GoodLimit => tutorial?.25f:Good;
        Image progress;
        float feedbackAt, comboAt;
        int next, score, combo, maxCombo, judgments, perfectCount, brokenHolds;
        bool autoplay, finished;
        float SongTime => (float)(AudioSettings.dspTime-songStartDSP)-audioOffsetMs*.001f;
        float chartEnd;
        float EndTime => Mathf.Max(1,chartEnd);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreatePrototype()
        {
            if (FindAnyObjectByType<RhythmGamePrototype>() == null)
                new GameObject("Rhythm Game Prototype").AddComponent<RhythmGamePrototype>();
        }

        void Awake()
        {
            Application.targetFrameRate = 120;
            Application.runInBackground=true;
            white = MakeTexture(0);
            capsule = MakeTexture(1);
            glow = MakeTexture(2);
            BuildCamera(); BuildBackground(); BuildLine(); BuildUI();
            sound=gameObject.AddComponent<PulseAudio>();
            if(Camera.main.GetComponent<AudioListener>()==null) Camera.main.gameObject.AddComponent<AudioListener>();
            audioOffsetMs=Application.isPlaying?PlayerPrefs.GetInt("PulseAudioOffsetMs",0):0;
            BuildMenu(); BuildCalibration(); ShowMenu();
        }

        void Update()
        {
            if(calibrating) { UpdateCalibration(); return; }
            if(Input.GetKeyDown(KeyCode.C)) { OpenCalibration(); return; }
            if(Input.GetKeyDown(KeyCode.M)) sound.ToggleMute();
            if(Input.GetKeyDown(KeyCode.V)) reducedMotion=!reducedMotion;
            if(Input.GetKeyDown(KeyCode.Escape)) { ShowMenu(); return; }
            if(!playing)
            {
                if(Input.GetKeyDown(KeyCode.Alpha1)) StartLevel(0);
                else if(Input.GetKeyDown(KeyCode.Alpha2)) StartLevel(1);
                else if(Input.GetKeyDown(KeyCode.Alpha3)) StartLevel(2);
                return;
            }
            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            if (Input.GetKeyDown(KeyCode.A)) autoplay = !autoplay;
            float t = SongTime;
            UpdateVisuals(t);
            Spawn(t);
            if (!autoplay && !finished)
            {
                for (int lane=0;lane<4;lane++)
                {
                    if(Input.GetKeyDown(Keys[lane])) { flashes[lane]=1; sound.Click(); TryHit(lane,t); }
                    if(Input.GetKey(Keys[lane])) TryGesture(lane,t,NoteGesture.Drag);
                }
                Vector2 pointer=Input.mousePosition;
                Vector3 world=Camera.main.ScreenToWorldPoint(pointer);
                world.z=0;
                bool overUI=EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject();
                int hovered=PickLane(world,t);
                if(Input.GetMouseButtonDown(0) && !overUI)
                {
                    mouseLane=hovered;
                    if(mouseLane>=0) { flashes[mouseLane]=1; sound.Click(); TryHit(mouseLane,t); }
                }
                if (Input.GetMouseButton(0) && !overUI && hovered >= 0)
                    TryGesture(hovered, t, NoteGesture.Drag);
            }
            TickNotes(t); TickEffects(t); UpdateQuestion(t);
            if(Input.GetMouseButtonUp(0)) mouseLane=-1;
            progress.fillAmount = Mathf.Clamp01(t / EndTime);
            modeText.text = autoplay && !finished ? "Auto" : "";
            if (!finished && t >= EndTime && next == chart.Count && notes.Count == 0)
                Complete();
        }

        void BuildCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) { var obj = new GameObject("Main Camera"); obj.tag="MainCamera"; cam=obj.AddComponent<Camera>(); }
            cam.transform.SetPositionAndRotation(new Vector3(0,0,-10),Quaternion.identity);
            cam.orthographic=true; cam.orthographicSize=5.4f;
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.018f,.025f,.065f);
        }

        void BuildBackground()
        {
            for(int i=0;i<4;i++)
            {
                var haze=Sprite("Nebula",transform,glow,Alpha(colors[i],.07f),-30);
                haze.transform.localPosition=new Vector3((i-1.5f)*4,1.5f+Mathf.Sin(i)*2,0);
                haze.transform.localScale=new Vector3(9,9,1);
            }
            var random = new System.Random(128);
            for(int i=0;i<70;i++)
            {
                var star=Sprite("Starlight",transform,glow,new Color(.6f,.8f,1,.10f+(float)random.NextDouble()*.3f),-20);
                star.transform.localPosition=new Vector3((float)random.NextDouble()*20-10,(float)random.NextDouble()*11-5.5f,0);
                float size=.025f+(float)random.NextDouble()*.06f;
                star.transform.localScale=new Vector3(size,size,1);
            }
            for(int i=0;i<9;i++)
            {
                var stripe=Sprite("Horizon",transform,white,new Color(.25f,.5f,.8f,.045f),-15);
                stripe.transform.localPosition=new Vector3(0,-4.5f+i*1.15f,0);
                stripe.transform.localScale=new Vector3(25,.012f,1);
            }
        }

        void BuildLine()
        {
            line=new GameObject("Dynamic Judgment Lines").transform; line.SetParent(transform,false);
            for(int lane=0;lane<4;lane++)
            {
                var track=new GameObject("Track "+Keys[lane]).transform; track.SetParent(line,false); tracks[lane]=track;
                var rail=Sprite("Lane Guide",track,white,Alpha(colors[lane],.055f),-8);
                rail.transform.localPosition=new Vector3(0,3,0); rail.transform.localScale=new Vector3(.018f,6,1);
                beams[lane]=Sprite("Input Light Column",track,glow,Alpha(colors[lane],0),-2);
                beams[lane].transform.localPosition=new Vector3(0,1.5f,0); beams[lane].transform.localScale=new Vector3(1.4f,4,1);
                var pad=Sprite("Receptor Frame",track,capsule,Alpha(colors[lane],.35f),3);
                pad.transform.localScale=new Vector3(1.12f,.23f,1);
                receptors[lane]=Sprite("Receptor",track,capsule,colors[lane],5);
                receptors[lane].transform.localScale=new Vector3(.98f,.065f,1);
                var label=new GameObject("Key "+Keys[lane]).AddComponent<TextMesh>();
                label.transform.SetParent(track,false); label.transform.localPosition=new Vector3(0,-.45f,0);
                label.text=Keys[lane].ToString(); label.anchor=TextAnchor.MiddleCenter;
                label.alignment=TextAlignment.Center; label.fontSize=48; label.characterSize=.065f;
                label.color=Alpha(colors[lane],.85f); label.GetComponent<MeshRenderer>().sortingOrder=8;
                keyLabels[lane]=label;
            }
            for(int i=0;i<2;i++)
            {
                var root=new GameObject("Judgment Line "+(i+1)).transform; root.SetParent(line,false); lineDecor[i]=root;
                lineGlows[i]=Sprite("Line Halo",root,glow,new Color(.3f,.8f,1,.25f),1);
                lineCores[i]=Sprite("Line Core",root,white,new Color(.7f,.95f,1,.8f),2);
            }
            for(int i=0;i<3;i++)
            {
                var ring=new GameObject("Orbital Background "+i).AddComponent<LineRenderer>(); ring.transform.SetParent(transform,false);
                ring.useWorldSpace=false; ring.loop=true; ring.positionCount=96;
                ring.material=new Material(Shader.Find("Sprites/Default")); ring.widthMultiplier=.012f;
                ring.sortingOrder=-12;
                for(int j=0;j<96;j++)
                {
                    float angle=j*Mathf.PI*2/96;
                    ring.SetPosition(j,new Vector3(Mathf.Cos(angle)*(3.4f+i*.4f),Mathf.Sin(angle)*(3.4f+i*.4f),0));
                }
                orbitRings.Add(ring);
            }
            UpdateVisuals(0);
        }

        void UpdateVisuals(float t)
        {
            VisualPose pose;
            if(advanced) pose=VisualMotion.Sample(VisualMotion.SongBeat(t),reducedMotion);
            else
            {
                float motion=reducedMotion?0:tutorial?.15f:1;
                var center=new Vector2(Mathf.Sin(Mathf.Max(0,t)*.31f)*.38f*motion,-2.55f+Mathf.Sin(Mathf.Max(0,t)*.72f)*.52f*motion);
                float angle=Mathf.Sin(Mathf.Max(0,t)*.48f)*8.5f*motion;
                pose=new VisualPose { Primary=center,Secondary=center,PrimaryAngle=angle,SecondaryAngle=angle };
            }
            for(int lane=0;lane<4;lane++)
            {
                tracks[lane].position=VisualMotion.LanePosition(pose,lane);
                tracks[lane].rotation=Quaternion.Euler(0,0,VisualMotion.LaneAngle(pose,lane));
                keyLabels[lane].transform.rotation=Quaternion.identity;
            }
            float pulse=Mathf.Exp(-Mathf.Repeat(Mathf.Max(0,t)*Bpm/60,1)*5);
            for(int i=0;i<2;i++)
            {
                lineDecor[i].position=i==0?(Vector3)pose.Primary:(Vector3)pose.Secondary;
                lineDecor[i].rotation=Quaternion.Euler(0,0,i==0?pose.PrimaryAngle:pose.SecondaryAngle);
                float width=i==0?Mathf.Lerp(10,3.65f,pose.Split):3.65f;
                float alpha=i==0?1:pose.Split;
                lineCores[i].transform.localScale=new Vector3(width,.025f+pulse*.018f,1);
                lineGlows[i].transform.localScale=new Vector3(width+1,.3f+pulse*.15f,1);
                Color tint=advanced?Color.Lerp(colors[pose.Phase%4],Color.white,.55f):new Color(.7f,.95f,1);
                lineCores[i].color=Alpha(tint,.8f*alpha); lineGlows[i].color=Alpha(tint,(.18f+pulse*.08f)*alpha);
            }
            for(int i=0;i<orbitRings.Count;i++)
            {
                var ring=orbitRings[i]; ring.enabled=advanced;
                ring.transform.rotation=Quaternion.Euler(0,0,reducedMotion?i*25:t*(i%2==0?3:-4));
                ring.transform.localScale=new Vector3(1, i==1?.65f:1,1)*(1+pulse*(reducedMotion?0:.015f));
                ring.startColor=ring.endColor=Alpha(colors[(pose.Phase+i)%4],.065f+pulse*.025f);
            }
            if(sound!=null) sound.SetVisualPhase(advanced?pose.Phase:-1);
        }

        int PickLane(Vector3 world,float t)
        {
            int selected=-1; float nearest=float.MaxValue;
            for(int lane=0;lane<4;lane++)
            {
                Vector3 local=tracks[lane].InverseTransformPoint(world);
                if(Mathf.Abs(local.x)>.72f || local.y<-.8f || local.y>7) continue;
                float distance=Mathf.Abs(local.x)+Mathf.Abs(local.y)*.5f;
                foreach(Note note in notes)
                    if(note.data.lane==lane && !note.data.question.Resolved)
                        distance=Mathf.Min(distance,Mathf.Abs(local.x)+Mathf.Abs(local.y-(note.data.time-t)*TravelSpeed)*.5f);
                if(distance<nearest) { nearest=distance; selected=lane; }
            }
            return selected;
        }

        void BuildUI()
        {
            var obj=new GameObject("HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); obj.transform.SetParent(transform,false); hud=obj;
            obj.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            MakeButton(obj.transform,"Levels",.035f,.885f,.125f,.955f,new Color(.12f,.24f,.35f),ShowMenu);
            scoreText=MakeText(obj.transform,"",36,TextAnchor.MiddleRight,.78f,.89f,.965f,.96f,Color.white);
            comboText=MakeText(obj.transform,"",23,TextAnchor.MiddleRight,.78f,.83f,.965f,.89f,new Color(.65f,.8f,.95f));
            feedbackText=MakeText(obj.transform,"",22,TextAnchor.MiddleRight,.72f,.76f,.965f,.82f,Color.white);
            modeText=MakeText(obj.transform,"",18,TextAnchor.MiddleLeft,.038f,.83f,.16f,.88f,new Color(.5f,.7f,.85f));
            resultText=MakeText(obj.transform,"",32,TextAnchor.MiddleCenter,.2f,.35f,.8f,.58f,Color.white);
            questionCard=new GameObject("Question Card",typeof(RectTransform),typeof(Image));
            questionCard.transform.SetParent(obj.transform,false);
            SetRect(questionCard.GetComponent<RectTransform>(),.37f,.865f,.63f,.953f);
            var cardImage=questionCard.GetComponent<Image>();
            cardImage.color=new Color(.045f,.075f,.12f,.96f);
            cardImage.raycastTarget=false;
            questionText=MakeText(questionCard.transform,"",44,TextAnchor.MiddleCenter,0,0,1,1,Color.white);
            questionCard.SetActive(false);
            lessonText=MakeText(obj.transform,"",24,TextAnchor.MiddleCenter,.18f,.035f,.82f,.17f,new Color(.75f,.86f,.96f));
            lessonText.gameObject.name="Tutorial instruction";
            resultButtons=new GameObject("Result Actions",typeof(RectTransform)); resultButtons.transform.SetParent(obj.transform,false);
            SetRect(resultButtons.GetComponent<RectTransform>(),0,0,1,1);
            MakeButton(resultButtons.transform,"RETRY",.30f,.23f,.48f,.31f,new Color(.10f,.3f,.4f),Restart);
            MakeButton(resultButtons.transform,"LEVEL SELECT",.52f,.23f,.70f,.31f,new Color(.2f,.16f,.35f),ShowMenu);
            resultButtons.SetActive(false);
            var track=new GameObject("Song Progress Track",typeof(RectTransform),typeof(Image)); track.transform.SetParent(obj.transform,false);
            SetRect(track.GetComponent<RectTransform>(),.035f,.978f,.965f,.982f);
            track.GetComponent<Image>().color=new Color(.2f,.3f,.45f,.4f);
            var fill=new GameObject("Song Progress",typeof(RectTransform),typeof(Image)); fill.transform.SetParent(track.transform,false);
            SetRect(fill.GetComponent<RectTransform>(),0,0,1,1); progress=fill.GetComponent<Image>();
            progress.sprite=white; progress.type=Image.Type.Filled; progress.fillMethod=Image.FillMethod.Horizontal;
            progress.fillOrigin=0; progress.color=new Color(.3f,.88f,1);
        }

        void BuildMenu()
        {
            if(FindAnyObjectByType<EventSystem>()==null)
                new GameObject("Input Events",typeof(EventSystem),typeof(StandaloneInputModule));
            menu=new GameObject("Level Select",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            menu.transform.SetParent(transform,false);
            menu.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=menu.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            MakeText(menu.transform,"Pulse",68,TextAnchor.MiddleCenter,.14f,.74f,.86f,.88f,Color.white);
            MakeText(menu.transform,"Choose a level",24,TextAnchor.MiddleCenter,.14f,.65f,.86f,.73f,new Color(.65f,.77f,.9f));
            var one=MakeButton(menu.transform,"",.13f,.38f,.36f,.61f,new Color(.06f,.14f,.22f),()=>StartLevel(0));
            var two=MakeButton(menu.transform,"",.385f,.38f,.615f,.61f,new Color(.14f,.08f,.23f),()=>StartLevel(1));
            var three=MakeButton(menu.transform,"",.64f,.38f,.87f,.61f,new Color(.19f,.09f,.15f),()=>StartLevel(2));
            LevelCard(one.transform,"Tutorial","Learn to play");
            LevelCard(two.transform,"Basic","2:00");
            LevelCard(three.transform,"Orbit","2:00");
            MakeButton(menu.transform,"Audio sync",.42f,.22f,.58f,.30f,new Color(.12f,.25f,.31f),OpenCalibration);
        }

        void BuildCalibration()
        {
            syncPanel=new GameObject("Audio Sync",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            syncPanel.transform.SetParent(transform,false); syncPanel.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            syncPanel.GetComponent<Canvas>().sortingOrder=20;
            var scaler=syncPanel.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            MakeText(syncPanel.transform,"AUDIO / VISUAL SYNC",42,TextAnchor.MiddleCenter,.15f,.80f,.85f,.92f,Color.white);
            MakeText(syncPanel.transform,"Follow the clicks and tap SPACE in rhythm 8 times.\nListen to one full bar first; keep tapping with the beat.",27,TextAnchor.MiddleCenter,.1f,.65f,.9f,.80f,new Color(.65f,.8f,.95f));
            var flash=new GameObject("Sync pulse",typeof(RectTransform),typeof(Image)); flash.transform.SetParent(syncPanel.transform,false);
            SetRect(flash.GetComponent<RectTransform>(),.40f,.41f,.60f,.57f); syncFlash=flash.GetComponent<Image>(); syncFlash.raycastTarget=false;
            syncStatus=MakeText(syncPanel.transform,"",27,TextAnchor.MiddleCenter,.12f,.24f,.88f,.40f,Color.white);
            MakeButton(syncPanel.transform,"FLASH EARLIER −10ms",.12f,.14f,.36f,.22f,new Color(.13f,.24f,.36f),()=>AdjustSync(-10));
            MakeButton(syncPanel.transform,"FLASH LATER +10ms",.39f,.14f,.63f,.22f,new Color(.13f,.24f,.36f),()=>AdjustSync(10));
            MakeButton(syncPanel.transform,"RESET",.66f,.14f,.83f,.22f,new Color(.2f,.16f,.28f),()=>{audioOffsetMs=0;syncTaps.Clear();});
            MakeButton(syncPanel.transform,"SAVE / BACK",.38f,.035f,.62f,.11f,new Color(.12f,.33f,.32f),CloseCalibration);
            MakeText(syncPanel.transform,"Or use LEFT / RIGHT until the flash and click line up. ESC saves and returns.",20,TextAnchor.MiddleCenter,.1f,.105f,.9f,.14f,new Color(.5f,.7f,.85f));
            syncPanel.SetActive(false);
        }

        void OpenCalibration()
        {
            ShowMenu(); menu.SetActive(false); syncPanel.SetActive(true); calibrating=true;
            syncTaps.Clear(); lastTapBeat=int.MinValue; calibrationStart=sound.BeginCalibration();
        }
        void AdjustSync(int amount) { audioOffsetMs=Mathf.Clamp(audioOffsetMs+amount,-250,250); }
        void CloseCalibration()
        {
            PlayerPrefs.SetInt("PulseAudioOffsetMs",audioOffsetMs); PlayerPrefs.Save();
            calibrating=false; syncPanel.SetActive(false); sound.Stop(); menu.SetActive(true);
        }
        void UpdateCalibration()
        {
            if(Input.GetKeyDown(KeyCode.Escape)) { CloseCalibration(); return; }
            if(Input.GetKeyDown(KeyCode.LeftArrow)) AdjustSync(-10);
            if(Input.GetKeyDown(KeyCode.RightArrow)) AdjustSync(10);
            double elapsed=AudioSettings.dspTime-calibrationStart;
            int nearest=(int)Math.Round(elapsed/.6);
            if(Input.GetKeyDown(KeyCode.Space) && nearest>=4 && nearest!=lastTapBeat)
            {
                double error=elapsed-nearest*.6;
                if(Math.Abs(error)<.25)
                {
                    lastTapBeat=nearest; syncTaps.Add(error);
                    if(syncTaps.Count>=8) { audioOffsetMs=BeatTrack.CalibrationOffsetMs(syncTaps.ToArray()); syncTaps.Clear(); }
                }
            }
            float phase=Mathf.Repeat((float)(elapsed-audioOffsetMs*.001),.6f);
            float flash=elapsed<0?0:Mathf.Exp(-phase*18);
            syncFlash.color=Color.Lerp(new Color(.08f,.14f,.20f),new Color(.4f,1,.85f),flash);
            syncStatus.text=$"OFFSET {audioOffsetMs:+0;-0;0} ms\n"+(nearest<4?"LISTEN TO THE COUNT-IN":$"TAPS {syncTaps.Count} / 8  •  AUTO ESTIMATE AFTER 8")+"\nPositive offset delays the visual pulse.";
        }

        void LevelCard(Transform parent,string title,string description)
        {
            MakeText(parent,title,38,TextAnchor.MiddleCenter,.04f,.43f,.96f,.84f,Color.white);
            MakeText(parent,description,22,TextAnchor.MiddleCenter,.04f,.18f,.96f,.45f,new Color(.69f,.79f,.91f));
        }

        Button MakeButton(Transform parent,string label,float x1,float y1,float x2,float y2,Color color,Action action)
        {
            var obj=new GameObject(label.Length>0?label:"Level Card",typeof(RectTransform),typeof(Image),typeof(Button));
            obj.transform.SetParent(parent,false); SetRect(obj.GetComponent<RectTransform>(),x1,y1,x2,y2);
            var image=obj.GetComponent<Image>(); image.color=color;
            var button=obj.GetComponent<Button>(); button.targetGraphic=image;
            var palette=button.colors; palette.highlightedColor=new Color(1.3f,1.3f,1.3f); palette.pressedColor=new Color(.6f,.9f,1);
            button.colors=palette;
            if(label.Length>0) MakeText(obj.transform,label,22,TextAnchor.MiddleCenter,.02f,0,.98f,1,Color.white);
            button.onClick.AddListener(()=> { sound.Click(); action(); });
            return button;
        }

        public void StartLevel(int level)
        {
            tutorial=level<=0; advanced=level>=2; playing=true; autoplay=false;
            menu.SetActive(false); hud.SetActive(true); line.gameObject.SetActive(true);
            lessonText.gameObject.SetActive(tutorial);
            BuildChart(); Restart();
        }

        void ShowMenu()
        {
            playing=false; finished=false; ClearNotes(); sound.Stop();
            menu.SetActive(true); hud.SetActive(false); line.gameObject.SetActive(false);
        }

        void BuildChart()
        {
            chart.Clear();
            questions = advanced ? MathLesson.CreateVisual() : MathLesson.Create(tutorial);
            foreach (MathQuestion question in questions)
                for (int lane = 0; lane < 4; lane++)
                    chart.Add(new ChartNote(question.Time, lane, question.Duration, question));

            float beat = 60f / Bpm;
            if (tutorial)
            {
                AddRhythmNote(4 * beat, 0);
                AddRhythmNote(8 * beat, 2, 2 * beat);
                AddRhythmNote(14 * beat, 3, gesture: NoteGesture.Drag);
                chartEnd = 20f;
            }
            else
            {
                int[] lanes = { 0, 1, 3, 2, 1, 0, 2, 3 };
                for (int beatIndex = 4; beatIndex * beat < MathLesson.SongDuration - 1.5f; beatIndex += 2)
                {
                    int step = beatIndex / 2;
                    float time = beatIndex * beat;
                    float duration = step % 8 == 4 ? beat : 0;
                    if (questions.Exists(q => time + duration >= q.Time - 3f && time <= q.Time + .5f))
                        continue;
                    var gesture = advanced && step % 8 == 6 ? NoteGesture.Drag : NoteGesture.Tap;
                    AddRhythmNote(time, lanes[step % lanes.Length], duration, gesture);
                }
                chartEnd = MathLesson.SongDuration;
            }
            chart.Sort((a,b)=>a.time.CompareTo(b.time));
        }

        void AddRhythmNote(float time, int lane, float duration = 0, NoteGesture gesture = NoteGesture.Tap)
        {
            var note = new MathQuestion("", 0, lane, time, duration, gesture);
            chart.Add(new ChartNote(time, lane, duration, note, true));
        }

        void UpdateQuestion(float t)
        {
            MathQuestion current = questions.Find(q => !q.Resolved && t >= q.Time - 3f && t <= q.Time + MissLimit);
            questionCard.SetActive(current != null && !finished);
            questionText.text = current?.Prompt ?? "";
            if (!tutorial || finished) return;

            if (t < 4f)
                lessonText.text = "Press D, F, J, or K, or click a lane when its note reaches the line.";
            else if (t < 7f)
                lessonText.text = "Hold the key or mouse button until the tail reaches the line.";
            else if (t < 10f)
                lessonText.text = "For a diamond note, hold the lane before it reaches the line.";
            else if (t < 15f)
                lessonText.text = "Solve the equation and hit the note with the correct answer.";
            else
                lessonText.text = "A wrong answer costs 500 points and breaks your combo.\nEsc opens Levels. R retries. C opens audio sync.";
        }

        void Spawn(float t)
        {
            while(next<chart.Count && chart[next].time-t<=LeadIn)
            {
                ChartNote data=chart[next++];
                var root=new GameObject(data.bonus?"Rhythm "+data.question.Gesture:data.duration>0?"Hold Note":"Tap Note").transform; root.SetParent(tracks[data.lane],false);
                var n=new Note(data,root.gameObject);
                if(data.duration>0)
                {
                    n.tail=Sprite("Sustain Ribbon",root,capsule,Alpha(colors[data.lane],.35f),7);
                    n.tailCore=Sprite("Sustain Thread",root,white,Alpha(colors[data.lane],.8f),8);
                    n.cap=Sprite("Release Cap",root,capsule,colors[data.lane],10);
                }
                var aura=Sprite("Soft Glow",root,glow,Alpha(colors[data.lane],.4f),9);
                aura.transform.localScale=new Vector3(1.45f,.6f,1);
                n.head=Sprite("Note Frame",root,capsule,colors[data.lane],11);
                n.head.transform.localScale=new Vector3(1.10f,.42f,1);
                var inset=Sprite("Note Face",root,capsule,new Color(.065f,.09f,.18f),12);
                inset.transform.localScale=new Vector3(.96f,.30f,1);
                var shine=Sprite("Note Highlight",root,capsule,Color.Lerp(colors[data.lane],Color.white,.8f),13);
                shine.transform.localPosition=new Vector3(0,.15f,0); shine.transform.localScale=new Vector3(.82f,.035f,1);
                if(data.duration>0)
                {
                    var diamond=Sprite("Hold Diamond",root,white,Color.white,14);
                    diamond.transform.localPosition=new Vector3(.43f,0,0); diamond.transform.localRotation=Quaternion.Euler(0,0,45); diamond.transform.localScale=new Vector3(.07f,.07f,1);
                }
                if (!data.bonus || data.question.Gesture == NoteGesture.Drag)
                {
                    var answer = new GameObject("Note label").AddComponent<TextMesh>();
                    answer.transform.SetParent(root,false);
                    answer.transform.localPosition=new Vector3(0,0,-.01f);
                    answer.text=data.bonus?"◆":data.question.Choices[data.lane].ToString();
                    answer.anchor=TextAnchor.MiddleCenter;
                    answer.alignment=TextAlignment.Center;
                    answer.fontSize=64; answer.characterSize=.065f;
                    answer.color=Color.white; answer.GetComponent<MeshRenderer>().sortingOrder=16;
                    n.answer=answer;
                }
                notes.Add(n);
            }
        }

        void TickNotes(float t)
        {
            for(int i=notes.Count-1;i>=0;i--)
            {
                Note n=notes[i];
                bool correct=n.data.lane==n.data.question.CorrectLane;
                if(n.data.question.Resolved && !n.started) { Remove(i); continue; }
                if(!n.started && autoplay && correct && t>=n.data.time) Begin(n,"PERFECT",1000);
                if(n.started && n.hold!=null)
                {
                    var state=n.hold.Tick(t,autoplay||Input.GetKey(Keys[n.data.lane])||(Input.GetMouseButton(0)&&mouseLane==n.data.lane));
                    if(state==HoldTracker.Result.Complete) { Judge(n,"Held",1000); Remove(i); continue; }
                    if(state==HoldTracker.Result.Broken) { brokenHolds++; Fail(n,"Hold break",1); Remove(i); continue; }
                    flashes[n.data.lane]=.75f;
                    if(Time.unscaledTime>=n.nextSpark)
                    { Burst(n.data.lane,3,.5f); n.nextSpark=Time.unscaledTime+.09f; }
                }
                if(!n.started && t>n.data.time+MissLimit)
                {
                    if(correct) { n.data.question.Resolved=true; Fail(n,"Miss",n.data.duration>0?2:1); }
                    Remove(i); continue;
                }
                if(n.started && n.hold==null) { Remove(i); continue; }
                float y=n.started?0:(n.data.time-t)*TravelSpeed;
                n.obj.transform.localPosition=new Vector3(0,y,0);
                if(n.answer!=null) n.answer.transform.rotation=Quaternion.identity;
                if(n.tail!=null)
                {
                    float length=Mathf.Max(.02f,(n.started?n.data.End-t:n.data.duration)*TravelSpeed);
                    n.tail.transform.localPosition=new Vector3(0,length*.5f,0);
                    n.tail.transform.localScale=new Vector3(n.started?.46f:.34f,length,1);
                    n.tail.color=Alpha(colors[n.data.lane],n.started?.65f:.32f);
                    n.tailCore.transform.localPosition=new Vector3(0,length*.5f,0);
                    n.tailCore.transform.localScale=new Vector3(.04f,length,1);
                    n.cap.transform.localPosition=new Vector3(0,length,0); n.cap.transform.localScale=new Vector3(.7f,.10f,1);
                    n.head.color=n.started?Color.Lerp(colors[n.data.lane],Color.white,.45f):colors[n.data.lane];
                }
            }
        }

        void TryHit(int lane,float t) { TryGesture(lane,t,NoteGesture.Tap); }

        void TryGesture(int lane,float t,NoteGesture gesture)
        {
            Note best=null; float error=float.MaxValue;
            foreach(Note n in notes)
            {
                if(n.started||n.data.question.Resolved||n.data.lane!=lane) continue;
                float e=Mathf.Abs(n.data.time-t);
                if(e<error) { error=e; best=n; }
            }
            if(best!=null && best.data.question.Gesture==NoteGesture.Drag && gesture==NoteGesture.Tap && error<=LeadIn)
                return;
            bool matching=best!=null && (best.data.question.Gesture==gesture || (gesture==NoteGesture.Tap && best.data.question.Gesture==NoteGesture.Hold));
            // Sustained input only collects drag notes.
            if(gesture!=NoteGesture.Tap && (!matching||error>HitWindow)) return;
            if(gesture==NoteGesture.Drag && t<best.data.time) return;
            if(best==null || error>HitWindow || !matching)
            {
                bool sustaining=notes.Exists(n=>n.started&&n.hold!=null&&n.data.lane==lane);
                if(!sustaining && chart.Count>0 && t>=chart[0].time-HitWindow && t<EndTime)
                    Penalty("Off beat",100,0);
                return;
            }
            if(lane!=best.data.question.CorrectLane)
            {
                best.data.question.Resolved=true;
                Penalty("Wrong answer",500,best.data.duration>0?2:1);
                return;
            }
            string label=error<=PerfectLimit?"PERFECT":error<=GoodLimit?"GOOD":"BAD";
            Begin(best,label,error<=PerfectLimit?1000:error<=GoodLimit?650:250);
        }

        void Begin(Note n,string label,int points)
        {
            n.started=true; n.data.question.Resolved=true;
            if(n.hold!=null) n.hold.Begin();
            Judge(n,label,points);
        }

        void Judge(Note n,string label,int points)
        {
            score+=points; earnedPoints+=points; judgments++; if(points==1000) perfectCount++;
            combo++; maxCombo=Mathf.Max(combo,maxCombo); comboAt=Time.unscaledTime;
            flashes[n.data.lane]=1;
            Burst(n.data.lane,label=="Held"?18:10,1);
            Feedback(label,points==1000?colors[n.data.lane]:points==650?new Color(.5f,1,.65f):new Color(1,.65f,.3f));
            sound.Correct(label=="Held"); UpdateHud();
        }

        void Fail(Note n,string label,int count)
        {
            flashes[n.data.lane]=.3f; Penalty(label,250,count);
        }

        void Penalty(string label,int points,int count)
        {
            score=Mathf.Max(0,score-points); combo=0; judgments+=count; mistakes++;
            sound.Wrong(); Feedback(label,new Color(1,.32f,.4f)); UpdateHud();
        }

        void Remove(int i) { Dispose(notes[i].obj); notes.RemoveAt(i); }

        void Burst(int lane,int count,float strength)
        {
            Color color=colors[lane];
            if(count>3)
            {
                var ring=Sprite("Hit Shockwave",tracks[lane],capsule,Alpha(color,.5f),18);
                ring.transform.localPosition=new Vector3(0,0,0);
                effects.Add(new Effect(ring,Vector3.zero,new Vector3(1.1f,.18f,1),.34f,true));
                var bloom=Sprite("Hit Bloom",tracks[lane],glow,Alpha(color,.65f),17);
                bloom.transform.localPosition=new Vector3(0,0,0);
                effects.Add(new Effect(bloom,Vector3.zero,new Vector3(1.8f,1.8f,1),.23f,true));
            }
            for(int i=0;i<count;i++)
            {
                var spark=Sprite("Hit Spark",tracks[lane],white,Color.Lerp(color,Color.white,.35f),19);
                spark.transform.localPosition=new Vector3(0,.06f,0);
                spark.transform.localRotation=Quaternion.Euler(0,0,UnityEngine.Random.Range(0,180));
                Vector3 velocity=new Vector3(UnityEngine.Random.Range(-2.8f,2.8f),UnityEngine.Random.Range(.8f,4f),0)*strength;
                effects.Add(new Effect(spark,velocity,new Vector3(.035f,.12f,1),UnityEngine.Random.Range(.2f,.5f),false));
            }
        }

        void TickEffects(float t)
        {
            float dt=Time.unscaledDeltaTime;
            for(int i=effects.Count-1;i>=0;i--)
            {
                Effect e=effects[i]; e.age+=dt;
                if(e.age>=e.life) { Dispose(e.sprite.gameObject); effects.RemoveAt(i); continue; }
                float p=e.age/e.life;
                e.sprite.transform.localPosition+=e.velocity*dt;
                e.sprite.transform.localScale=e.scale*(e.expand?1+p*1.7f:1-p*.6f);
                e.sprite.color=Alpha(e.color,e.color.a*(1-p)*(1-p));
            }
            for(int i=0;i<4;i++)
            {
                flashes[i]=Mathf.MoveTowards(flashes[i],0,dt*4);
                beams[i].color=Alpha(colors[i],flashes[i]*.4f);
                receptors[i].color=Color.Lerp(colors[i],Color.white,flashes[i]*.8f);
                receptors[i].transform.localScale=new Vector3(.98f+flashes[i]*.16f,.065f+flashes[i]*.075f,1);
            }
            float pulse=Mathf.Exp(-(Time.unscaledTime-comboAt)*12)*.12f;
            comboText.transform.localScale=Vector3.one*(1+pulse);
            if(!finished)
            {
                float age=Time.unscaledTime-feedbackAt;
                feedbackText.color=Alpha(feedbackText.color,Mathf.Clamp01(1-(age-.4f)*2));
                feedbackText.rectTransform.anchoredPosition=new Vector2(0,Mathf.Clamp(age,0,.5f)*14);
            }
        }

        void Feedback(string label,Color color)
        { feedbackText.text=label; feedbackText.color=color; feedbackAt=Time.unscaledTime; }

        void UpdateHud()
        {
            scoreText.text=score.ToString("N0");
            comboText.text=combo>0?$"{combo} combo":"";
        }

        void Complete()
        {
            finished=true; sound.Stop(); resultButtons.SetActive(true);
            questionCard.SetActive(false); questionText.text=""; lessonText.text="";
            feedbackText.text="";
            float accuracy=judgments==0?0:100f*earnedPoints/(judgments*1000);
            string rank=accuracy>=98?"S":accuracy>=90?"A":accuracy>=80?"B":"C";
            resultText.text=$"Rank {rank}\n<size=24>{accuracy:0.0}% accuracy   ·   {maxCombo} best combo</size>";
        }

        void ClearNotes()
        {
            foreach(Note n in notes) Dispose(n.obj);
            foreach(Effect e in effects) Dispose(e.sprite.gameObject);
            notes.Clear(); effects.Clear(); Array.Clear(flashes,0,4); mouseLane=-1;
        }

        void Restart()
        {
            ClearNotes();
            foreach(ChartNote note in chart) note.question.Resolved=false;
            next=score=combo=maxCombo=judgments=perfectCount=brokenHolds=mistakes=earnedPoints=0;
            finished=false; resultText.text="";
            resultButtons.SetActive(false);
            var targets=new List<float>();
            foreach(ChartNote note in chart)
                if(note.lane==note.question.CorrectLane)
                {
                    if(!targets.Contains(note.time)) targets.Add(note.time);
                    if(note.duration>0 && !targets.Contains(note.End)) targets.Add(note.End);
                }
            targets.Sort();
            sound.Prepare(Bpm,chartEnd,advanced,targets.ToArray());
            // Generate the backing first; both playback and note positions use this same origin.
            songStartDSP=AudioSettings.dspTime+Delay;
            sound.Begin(songStartDSP,Bpm); UpdateVisuals(0);
            feedbackText.text=""; UpdateHud(); UpdateQuestion(-Delay);
        }

        SpriteRenderer Sprite(string name,Transform parent,Sprite sprite,Color color,int order)
        {
            var obj=new GameObject(name); obj.transform.SetParent(parent,false);
            var renderer=obj.AddComponent<SpriteRenderer>(); renderer.sprite=sprite; renderer.color=color; renderer.sortingOrder=order;
            return renderer;
        }

        static Sprite MakeTexture(int kind)
        {
            int size=kind==0?1:64;
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false); tex.name="Original procedural sprite "+kind;
            tex.wrapMode=TextureWrapMode.Clamp; tex.filterMode=FilterMode.Bilinear;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float px=(x+.5f)/size*2-1, py=(y+.5f)/size*2-1;
                float a=1;
                if(kind==1)
                {
                    float dx=Mathf.Max(Mathf.Abs(px)-.72f,0), dy=Mathf.Max(Mathf.Abs(py)-.72f,0);
                    a=1-Mathf.SmoothStep(.24f,.29f,Mathf.Sqrt(dx*dx+dy*dy));
                }
                if(kind==2) a=Mathf.Pow(Mathf.Clamp01(1-Mathf.Sqrt(px*px+py*py)),2);
                tex.SetPixel(x,y,new Color(1,1,1,a));
            }
            tex.Apply(); return UnityEngine.Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
        }

        static Text MakeText(Transform parent,string value,int size,TextAnchor alignment,float x1,float y1,float x2,float y2,Color color)
        {
            var obj=new GameObject(value.Length>0?value:"HUD Label",typeof(RectTransform),typeof(Text)); obj.transform.SetParent(parent,false);
            Text text=obj.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text=value; text.fontSize=size; text.fontStyle=FontStyle.Bold; text.alignment=alignment; text.color=color;
            text.horizontalOverflow=HorizontalWrapMode.Overflow; text.verticalOverflow=VerticalWrapMode.Overflow;
            text.raycastTarget=false; SetRect(text.rectTransform,x1,y1,x2,y2); return text;
        }
        static void SetRect(RectTransform rect,float x1,float y1,float x2,float y2)
        { rect.anchorMin=new Vector2(x1,y1); rect.anchorMax=new Vector2(x2,y2); rect.offsetMin=rect.offsetMax=Vector2.zero; }
        static Color Alpha(Color color,float a) { color.a=a; return color; }
        static void Dispose(GameObject obj)
        { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        static float LaneX(int lane) => (lane-1.5f)*1.55f;

        sealed class ChartNote
        {
            public readonly float time,duration; public readonly int lane; public readonly MathQuestion question; public readonly bool bonus;
            public float End => time+duration;
            public ChartNote(float time,int lane,float duration,MathQuestion question,bool bonus=false) { this.time=time; this.lane=lane; this.duration=duration; this.question=question; this.bonus=bonus; }
        }
        sealed class Note
        {
            public readonly ChartNote data; public readonly GameObject obj; public readonly HoldTracker hold;
            public TextMesh answer; public SpriteRenderer head,tail,tailCore,cap; public bool started; public float nextSpark;
            public Note(ChartNote data,GameObject obj)
            { this.data=data; this.obj=obj; if(data.duration>0) hold=new HoldTracker(data.End); }
        }
        sealed class Effect
        {
            public readonly SpriteRenderer sprite; public readonly Vector3 velocity,scale;
            public readonly Color color; public readonly float life; public readonly bool expand; public float age;
            public Effect(SpriteRenderer sprite,Vector3 velocity,Vector3 scale,float life,bool expand)
            { this.sprite=sprite; this.velocity=velocity; this.scale=scale; this.life=life; this.expand=expand;
                color=sprite.color; sprite.transform.localScale=scale; }
        }
    }
}
