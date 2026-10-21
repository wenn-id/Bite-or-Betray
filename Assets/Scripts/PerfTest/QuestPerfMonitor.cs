using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace BiteOrBetray.PerfTest
{
    /// <summary>
    /// Automated benchmark for QuestPerfTest: run several render scale configurations in sequence
    /// with gameplay characters still active, measure FPS + CPU/GPU times, then display results on the board
    /// above the table and write to the log ([PerfTest] tag) and persistentDataPath/perf_results.txt.
    /// Button A = repeat benchmark, button B = change configuration manually.
    /// </summary>
    public class QuestPerfMonitor : MonoBehaviour
    {
        public TextMesh label;
        public float warmupSeconds = 4f;
        public float measureSeconds = 12f;

        struct Config { public string name; public float scale; }
        static readonly Config[] Configs =
        {
            new Config { name = "A  default scale, gameplay", scale = -1f },
            new Config { name = "B  scale 1.0, gameplay", scale = 1.0f },
            new Config { name = "C  scale 1.25, gameplay", scale = 1.25f },
        };

        UniversalRenderPipelineAsset urp;
        float originalScale = 1f;
        readonly List<string> results = new List<string>();
        readonly FrameTiming[] timing = new FrameTiming[1];
        int manualIndex;
        bool running;
        string status = "";

        void Start()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) originalScale = urp.renderScale;
            StartCoroutine(Benchmark());
        }

        void OnDisable()
        {
            StopAllCoroutines();
            running = false;
            if (urp != null) urp.renderScale = originalScale;
        }

        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (running) return;
            if (OVRInput.GetDown(OVRInput.Button.One)) StartCoroutine(Benchmark());
            else if (OVRInput.GetDown(OVRInput.Button.Two))
            {
                manualIndex = (manualIndex + 1) % Configs.Length;
                Apply(Configs[manualIndex]);
                status = "Manual: " + Configs[manualIndex].name;
            }
            if (!running) UpdateLabel(LiveLine());
        }

        float RefreshRate()
        {
            float hz = 0;
            try { hz = OVRPlugin.systemDisplayFrequency; } catch { }
            return hz > 1 ? hz : 72f;
        }

        void Apply(Config c)
        {
            if (urp != null) urp.renderScale = c.scale < 0 ? originalScale : c.scale;
        }

        IEnumerator Benchmark()
        {
            running = true;
            results.Clear();
            string header = $"Quest perf test | {SystemInfo.deviceModel} | {RefreshRate():0} Hz | pipeline {(urp ? urp.name : "?")} | " +
                            $"gameplay characters/animations active | default scale {originalScale:0.##} | MSAA {(urp ? urp.msaaSampleCount : 0)}x | Unity {Application.unityVersion}";
            Debug.Log("[PerfTest] " + header);
            foreach (var c in Configs)
            {
                Apply(c);
                for (float t = 0; t < warmupSeconds; t += Time.unscaledDeltaTime)
                {
                    UpdateLabel($"Benchmark: {c.name}\nWarming up... {warmupSeconds - t:0}s\nKeep the headset on");
                    yield return null;
                }
                var dts = new List<float>();
                double cpu = 0, gpu = 0; int timed = 0;
                for (float t = 0; t < measureSeconds; t += Time.unscaledDeltaTime)
                {
                    dts.Add(Time.unscaledDeltaTime);
                    if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0)
                    { cpu += timing[0].cpuFrameTime; gpu += timing[0].gpuFrameTime; timed++; }
                    UpdateLabel($"Benchmark: {c.name}\nMeasuring... {measureSeconds - t:0}s");
                    yield return null;
                }
                float hz = RefreshRate(), budget = 1f / hz;
                float avgFps = dts.Count / dts.Sum();
                var sorted = dts.OrderByDescending(d => d).ToList();
                float low1 = 1f / sorted.Take(Mathf.Max(1, sorted.Count / 100)).Average();
                float missed = 100f * dts.Count(d => d > budget * 1.5f) / dts.Count;
                string eye = $"{XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}";
                string line = $"{c.name}: {avgFps:0.0} fps (1% low {low1:0}), late frames {missed:0.0}%" +
                              (timed > 0 ? $", CPU {cpu / timed:0.0} ms, GPU {gpu / timed:0.0} ms" : ", CPU/GPU n/a") + $", eye {eye}";
                results.Add(line);
                Debug.Log("[PerfTest] " + line);
            }
            Apply(Configs[0]);
            manualIndex = 0;
            string report = header + "\n" + string.Join("\n", results);
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "perf_results.txt"), report); } catch { }
            status = "Done. A = repeat, B = change configuration";
            running = false;
        }

        string LiveLine()
        {
            float fps = 1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f);
            var sb = new StringBuilder();
            sb.AppendLine($"Now: {fps:0} fps / target {RefreshRate():0}  |  scale {(urp ? urp.renderScale : 0):0.##}");
            sb.AppendLine(status);
            foreach (var r in results) sb.AppendLine(r.Replace(", eye", "\n     eye"));
            return sb.ToString();
        }

        void UpdateLabel(string text)
        {
            if (label != null) label.text = text;
        }
    }
}
