using System;
using System.IO;
using UnityEditor;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Langkah Unity untuk runner otomatis di laptop (Tools/Autopilot/unity_runner.py): Budget Audit lalu build APK
    /// sesuai Docs/Release/build-mode.txt atau -bobBuildMode. Hasil ditulis ke Logs/ci/ (Temp/ dihapus saat Unity batch keluar).
    /// Unity -batchmode -quit -buildTarget Android -executeMethod BiteOrBetray.EditorTools.CIRunner.Run
    /// </summary>
    public static class CIRunner
    {
        public static void Run()
        {
            Directory.CreateDirectory("Logs/ci");
            if (File.Exists("Temp/QuestBudgetAudit.txt")) File.Delete("Temp/QuestBudgetAudit.txt");
            bool auditPassed = true;
            try
            {
                GameplayPerformanceBuilder.PrepareScenes();
                QuestPerfTestBuilder.Audit();
                File.Copy("Temp/QuestBudgetAudit.txt", "Logs/ci/audit.txt", true);
            }
            catch (Exception exception)
            {
                auditPassed = false;
                if (File.Exists("Temp/QuestBudgetAudit.txt"))
                    File.Copy("Temp/QuestBudgetAudit.txt", "Logs/ci/audit.txt", true);
                else File.WriteAllText("Logs/ci/audit.txt", "AUDIT GAGAL\n" + exception);
            }
            try
            {
                AvatarValidation.BuildAndroid(AvatarValidation.RequestedBuildMode() == "Release");
            }
            catch (Exception exception)
            {
                File.WriteAllText("Logs/ci/build-exception.txt", exception.ToString());
                EditorApplication.Exit(1);
            }
            if (!auditPassed) EditorApplication.Exit(1);
        }
    }
}
