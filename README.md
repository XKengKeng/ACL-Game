# ACL Game — PULSE / Math Rhythm

## Open the project
1. Clone or download this repository. If downloading a ZIP, extract it first.
2. In Unity Hub, select **Add > Add project from disk** and choose the repository folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Open with **Unity 6000.6.3f1** and let Unity import the packages and assets.
4. Open `Assets/Scenes/RhythmPrototype.unity` and press **Play**.

Unity creates `Library`, `Temp`, `Logs`, and `UserSettings` locally. These generated folders are excluded from Git.

## Team contributions
`Math_Question_Generator/` contains the team's C++ generator, question-bank reader, and generated `questions.csv`. The current Unity game uses the questions in `Assets/Scripts/MathLesson.cs`; importing the external question bank is future integration work.

Pull the latest changes before editing. Commit project assets together with their `.meta` files, including `Packages` and `ProjectSettings` changes when needed.

## Levels
- **1 / Tutorial**: 20 seconds at 96 BPM. Learn taps, holds, drag notes, and math with two practice questions.
- **2 / Basic**: Two minutes at 128 BPM with tap and hold notes.
- **3 / Orbit**: Two minutes at 144 BPM with tap, hold, and drag notes. Six visual phases are spread across the song.

Basic and Orbit each have ten math questions about twelve seconds apart. The equation appears at the top shortly before the four answer notes. Choose the correct numbered note when it reaches the line. Between questions, play the regular rhythm notes.

## Controls
- **D / F / J / K** or **click a lane**: hit a note as it reaches the line.
- **Hold ribbons**: keep the key or mouse button pressed until the tail reaches the line.
- **Diamond notes**: hold the lane before the note reaches the line.
- **A** toggles autoplay, **R** retries, and **Esc** opens Levels.
- **M** mutes sound, **V** reduces motion, and **C** opens audio sync.

Correct hits earn up to 1,000 points. A wrong answer costs 500 points and breaks your combo. Misses and early hold releases cost 250 points. Extra off-beat taps cost 100 points. The score floors at zero.

## Source
- `Assets/Scripts/RhythmGamePrototype.cs`: menu, visual tracks, mouse/keyboard input, scoring, feedback.
- `Assets/Scripts/VisualMotion.cs`: musical-time motion poses, smooth transitions, reduced motion, gesture types.
- `Assets/Scripts/MathLesson.cs`: charts, question answers and choices.
- `Assets/Scripts/HoldTracker.cs`: hold lifecycle.
- `Assets/Scripts/PulseAudio.cs`: generated percussion, melody, and interaction sounds on the DSP clock.
- `Assets/Editor/RhythmValidation.cs`: automated gameplay, math, audio, gesture, and motion checks.
- `Assets/Editor/VisualPreview.cs`: Tools > Pulse > Preview Orbit captures an autoplay run and returns to the menu.

## Verification
Run the Unity editor in batch mode with `-executeMethod RhythmValidation.Run` to check the charts, scoring, note gestures, motion, and audio timing.

## Audio synchronization update
The backing is now rendered into one 44.1 kHz PCM clip before playback. Each percussion hit, melody note, and judgment cue is placed at its exact sample index. The whole clip is scheduled once against the same DSP origin used by note positions and judgments, rather than scheduling individual sounds from Update frames.

Press **C** or click **AUDIO SYNC** on level selection. Listen to the 100 BPM count-in, then tap SPACE with the clicks eight times. The median timing offset rejects outliers and estimates device/input latency. LEFT/RIGHT adjusts the visual pulse by 10 ms; positive offset delays the pulse. SAVE/BACK or ESC persists the setting. The offset changes both note visuals and judgment timing together. Calibration returns to level selection.

The device-specific offset starts at zero. Automated tests check sample positions, shared DSP origin, cue placement for every note/tail, bounded audio samples, median calibration, and the offset direction. A human calibration on the actual audio device is still needed to measure that device's output delay.
