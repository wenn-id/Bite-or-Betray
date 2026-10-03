using System;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;
using BiteOrBetray.DebugTools;
using UnityEditor;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    /// <summary>Jalankan ratusan game bot vs bot dan buat laporan balancing.</summary>
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
            EditorGUILayout.LabelField("Simulasi game bot vs bot", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Semua kursi dimainkan bot. Hasilnya dipakai untuk mengecek durasi game, keseimbangan tingkat bot, dan efek angka spawn.", MessageType.Info);

            games = EditorGUILayout.IntSlider("Jumlah game", games, 10, 5000);
            seatCount = EditorGUILayout.IntSlider("Jumlah pemain", seatCount, 2, 6);
            EditorGUI.indentLevel++;
            for (int i = 0; i < seatCount; i++)
                seats[i] = (BotDifficulty)EditorGUILayout.EnumPopup($"Kursi {i + 1}", seats[i]);
            EditorGUI.indentLevel--;
            startSeed = EditorGUILayout.IntField("Seed awal", startSeed);
            var target = SimulationReport.TargetMinutes(seatCount);
            EditorGUILayout.LabelField("Target durasi", $"{target.min:0}-{target.max:0} menit untuk {seatCount} pemain");

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(so.FindProperty("rules"), new GUIContent("Rules"), true);
            EditorGUILayout.PropertyField(so.FindProperty("timeModel"), new GUIContent("Model waktu (detik)", "Perkiraan lama tiap aksi di VR"), true);
            so.ApplyModifiedProperties();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ambil Rules dari scene"))
            {
                var dg = UnityEngine.Object.FindAnyObjectByType<BiteOrBetrayDebugGame>();
                if (dg != null) rules = dg.rules.Clone();
                else EditorUtility.DisplayDialog("Simulator", "Tidak ada BiteOrBetrayDebugGame di scene yang terbuka.", "OK");
            }
            if (GUILayout.Button("Reset Rules ke default")) rules = new RuleConfig();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Kalibrasi dari telemetry", "Ambil rata-rata detik berpikir pemain manusia dari CSV telemetry meja test.")))
                Calibrate();
            if (GUILayout.Button("Reset model waktu")) timeModel = new TimeModel();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox($"Timer giliran mengikuti Rules: {rules.TurnTimeLimitSeconds} detik berpikir per giliran.", MessageType.None);

            EditorGUILayout.Space();
            if (GUILayout.Button("Jalankan simulasi", GUILayout.Height(32))) Run();
            if (GUILayout.Button(new GUIContent("Jalankan matriks semua kombinasi bot (2-6 pemain)",
                    "80 kombinasi Easy/Normal/Hard, memakai Jumlah game per kombinasi. Hasilnya disimpan sebagai 2 file CSV.")))
                RunMatrix();

            if (!string.IsNullOrEmpty(report))
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Salin laporan")) EditorGUIUtility.systemCopyBuffer = report;
                if (GUILayout.Button("Simpan laporan (.md)")) Save("bob_simulasi.md", "md", report);
                GUI.enabled = last != null;
                if (GUILayout.Button("Simpan data per game (.csv)")) Save("bob_simulasi.csv", "csv", last.ToCsv());
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
            Debug.Log($"[Bite or Betray] Simulasi {last.Results.Count} game selesai dalam {watch.Elapsed.TotalSeconds:0.0} detik.");
        }

        void RunMatrix()
        {
            string folder = EditorUtility.SaveFolderPanel("Simpan hasil matriks ke folder", Directory.GetParent(Application.dataPath).FullName, "");
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
                        EditorUtility.DisplayCancelableProgressBar("Matriks simulasi", $"{p} pemain, kombinasi {i + 1}/{n}", (float)(offset + i) / total));
                    done += reps.Count;
                    rows.AddRange(reps.Select(SimulationMatrix.Summarize));
                    profile.AddRange(SimulationMatrix.RoundProfile(p, reps, 15));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            File.WriteAllText(Path.Combine(folder, "bob_matriks_kombinasi.csv"), SimulationMatrix.ToCsv(rows));
            File.WriteAllText(Path.Combine(folder, "bob_matriks_profil_ronde.csv"), SimulationMatrix.RoundProfileCsv(profile));
            report = $"Matriks selesai: {rows.Count} kombinasi x {games} game dalam {watch.Elapsed.TotalSeconds:0} detik.\n" +
                     $"Disimpan di {folder}:\n- bob_matriks_kombinasi.csv\n- bob_matriks_profil_ronde.csv\n\n" +
                     string.Join("\n", rows.Select(r => $"{r.Players}p  {r.Combo,-44} {r.AvgMin,5:0.0} menit  {(r.InTarget ? "masuk target" : "di luar target")}"));
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
                EditorUtility.DisplayDialog("Kalibrasi", "Belum ada data telemetry pemain manusia di:\n" + folder +
                    "\n\nMainkan beberapa game di meja test (kursi manusia) dengan telemetry aktif.", "OK");
                return;
            }
            float old = timeModel.ThinkPerDecision;
            timeModel.ThinkPerDecision = (float)Math.Round(avg, 1);
            EditorUtility.DisplayDialog("Kalibrasi", $"{samples} keputusan, {timeouts} kali waktu habis.\n" +
                $"Detik berpikir per keputusan: {old:0.0} -> {timeModel.ThinkPerDecision:0.0}.\n\n" +
                "Catatan: meja test belum punya animasi dan interaksi VR, jadi angka ini perlu dicek lagi di versi VR.", "OK");
        }

        static void Save(string defaultName, string ext, string content)
        {
            string path = EditorUtility.SaveFilePanel("Simpan", Directory.GetParent(Application.dataPath).FullName, defaultName, ext);
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, content);
            EditorUtility.RevealInFinder(path);
        }
    }
}
