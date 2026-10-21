using System;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;
using BiteOrBetray.DebugTools;
using UnityEditor;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    /// <summary>Run hundreds of bot vs bot games and generate a balancing report.</summary>
    public class SimulatorWindow : EditorWindow
    {
        [SerializeField] RuleConfig rules = new RuleConfig();
        [SerializeField] int games = 500;
        [SerializeField] int seatCount = 3;
        [SerializeField] BotDifficulty[] seats =
        {
            BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard,
            BotDifficulty.Normal, BotDifficulty.Normal, BotDifficulty.Normal
        };
        [SerializeField] int startSeed = 1;
        [SerializeField] TimeModel timeModel = new TimeModel();

        SerializedObject so;
        SimulationReport last;
        string report;
        Vector2 scroll, reportScroll;

        [MenuItem("Bite or Betray/Simulator")]
        public static void Open() => GetWindow<SimulatorWindow>("BoB Simulator").minSize = new Vector2(420, 500);

        void OnEnable()
        {
            if (seats == null || seats.Length < 6)
            {
                var fixedSeats = new BotDifficulty[6];
                for (int i = 0; i < 6; i++) fixedSeats[i] = seats != null && i < seats.Length ? seats[i] : BotDifficulty.Normal;
                seats = fixedSeats;
            }
            if (rules == null) rules = new RuleConfig();
            if (timeModel == null) timeModel = new TimeModel();
            so = new SerializedObject(this);
        }

        void OnGUI()
        {
            if (so == null) so = new SerializedObject(this);
            so.Update();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Bot vs bot game simulation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("All seats are played by bots. Results check game duration, bot difficulty balance, and spawn values.", MessageType.Info);

            games = EditorGUILayout.IntSlider("Game count", games, 10, 5000);
            seatCount = EditorGUILayout.IntSlider("Player count", seatCount, 2, 6);
            EditorGUI.indentLevel++;
            for (int i = 0; i < seatCount; i++)
                seats[i] = (BotDifficulty)EditorGUILayout.EnumPopup($"Seat {i + 1}", seats[i]);
            EditorGUI.indentLevel--;
            startSeed = EditorGUILayout.IntField("Starting seed", startSeed);
            var target = SimulationReport.TargetMinutes(seatCount);
            EditorGUILayout.LabelField("Target duration", $"{target.min:0}-{target.max:0} minutes for {seatCount} players");

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(so.FindProperty("rules"), new GUIContent("Rules"), true);
            EditorGUILayout.PropertyField(so.FindProperty("timeModel"), new GUIContent("Timing model (seconds)", "Estimated duration of each VR action"), true);
            so.ApplyModifiedProperties();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Rules from scene"))
            {
                var dg = UnityEngine.Object.FindAnyObjectByType<BiteOrBetrayDebugGame>();
                if (dg != null) rules = dg.rules.Clone();
                else EditorUtility.DisplayDialog("Simulator", "No BiteOrBetrayDebugGame in the open scene.", "OK");
            }
            if (GUILayout.Button("Reset Rules to defaults")) rules = new RuleConfig();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Calibrate from telemetry", "Read average human thinking seconds from test table telemetry CSVs.")))
                Calibrate();
            if (GUILayout.Button("Reset timing model")) timeModel = new TimeModel();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox($"Turn timer follows Rules: {rules.TurnTimeLimitSeconds} thinking seconds per turn.", MessageType.None);

            EditorGUILayout.Space();
            if (GUILayout.Button("Run simulation", GUILayout.Height(32))) Run();
            if (GUILayout.Button(new GUIContent("Run matrix of all bot combinations (2-6 players)",
                    "80 Easy/Normal/Hard combinations, using Game count per combination. Results saved as 2 CSV files.")))
                RunMatrix();

            if (!string.IsNullOrEmpty(report))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy report")) EditorGUIUtility.systemCopyBuffer = report;
                if (GUILayout.Button("Save report (.md)")) Save("bob_simulation.md", "md", report);
                GUI.enabled = last != null;
                if (GUILayout.Button("Save per-game data (.csv)")) Save("bob_simulation.csv", "csv", last.ToCsv());
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true, richText = false };
                reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.MinHeight(300));
                EditorGUILayout.TextArea(report, style, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndScrollView();
        }

        void Run()
        {
            var settings = new SimulationSettings
            {
                Games = games,
                Seats = seats.Take(seatCount).ToArray(),
                Rules = rules.Clone(),
                StartSeed = startSeed,
                Time = SyncedTime()
            };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                last = Simulator.Run(settings, (done, total) =>
                    EditorUtility.DisplayCancelableProgressBar("Bite or Betray Simulator", $"Game {done}/{total}", (float)done / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            report = last.ToMarkdown();
            Debug.Log($"[Bite or Betray] Simulation of {last.Results.Count} games finished in {watch.Elapsed.TotalSeconds:0.0} seconds.");
        }

        void RunMatrix()
        {
            string folder = EditorUtility.SaveFolderPanel("Save matrix results to folder", Directory.GetParent(Application.dataPath).FullName, "");
            if (string.IsNullOrEmpty(folder)) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var rows = new System.Collections.Generic.List<MatrixRow>();
            var profile = new System.Collections.Generic.List<RoundProfileRow>();
            try
            {
                int done = 0, total = Enumerable.Range(2, 5).Sum(p => SimulationMatrix.Combos(p).Count);
                for (int p = 2; p <= 6; p++)
                {
                    int offset = done;
                    var reps = SimulationMatrix.Run(new[] { p }, games, rules, SyncedTime(), startSeed, (i, n) =>
                        EditorUtility.DisplayCancelableProgressBar("Simulation matrix", $"{p} players, combination {i + 1}/{n}", (float)(offset + i) / total));
                    done += reps.Count;
                    rows.AddRange(reps.Select(SimulationMatrix.Summarize));
                    profile.AddRange(SimulationMatrix.RoundProfile(p, reps, 15));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            File.WriteAllText(Path.Combine(folder, "bob_matrix_combinations.csv"), SimulationMatrix.ToCsv(rows));
            File.WriteAllText(Path.Combine(folder, "bob_matrix_round_profiles.csv"), SimulationMatrix.RoundProfileCsv(profile));
            report = $"Matrix complete: {rows.Count} combinations x {games} games in {watch.Elapsed.TotalSeconds:0} seconds.\n" +
                     $"Saved in {folder}:\n- bob_matrix_combinations.csv\n- bob_matrix_round_profiles.csv\n\n" +
                     string.Join("\n", rows.Select(r => $"{r.Players}p  {r.Combo,-44} {r.AvgMin,5:0.0} minutes  {(r.InTarget ? "within target" : "outside target")}"));
            last = null;
            EditorUtility.RevealInFinder(folder);
        }

        TimeModel SyncedTime()
        {
            var t = timeModel.Clone();
            t.TurnThinkLimit = rules.TurnTimeLimitSeconds;
            return t;
        }

        void Calibrate()
        {
            string folder = BiteOrBetrayDebugGame.TelemetryFolder;
            if (!TelemetryRecorder.TryAverageHumanThink(folder, out double avg, out int samples, out int timeouts))
            {
                EditorUtility.DisplayDialog("Calibration", "No human player telemetry data yet in:\n" + folder +
                    "\n\nPlay a few test table games (human seats) with telemetry enabled.", "OK");
                return;
            }
            float old = timeModel.ThinkPerDecision;
            timeModel.ThinkPerDecision = (float)Math.Round(avg, 1);
            EditorUtility.DisplayDialog("Calibration", $"{samples} decisions, {timeouts} timeouts.\n" +
                $"Thinking seconds per decision: {old:0.0} -> {timeModel.ThinkPerDecision:0.0}.\n\n" +
                "Note: the test table has no VR animations or interaction yet, so these values need rechecking in the VR version.", "OK");
        }

        static void Save(string defaultName, string ext, string content)
        {
            string path = EditorUtility.SaveFilePanel("Save", Directory.GetParent(Application.dataPath).FullName, defaultName, ext);
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, content);
            EditorUtility.RevealInFinder(path);
        }
    }
}
