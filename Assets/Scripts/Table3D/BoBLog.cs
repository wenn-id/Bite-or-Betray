using System.Diagnostics;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Central verbose-log switch (M7, second half of BOB-009). Verbose calls are compiled out of
    /// release builds entirely: [Conditional] removes the call site, so the interpolated message
    /// string is never built and no log write happens on Quest. LogWarning / LogError /
    /// LogException stay on Debug directly and are unaffected.
    /// </summary>
    public static class BoBLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Verbose(string message) => Debug.Log(message);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Verbose(string message, Object context) => Debug.Log(message, context);
    }
}
