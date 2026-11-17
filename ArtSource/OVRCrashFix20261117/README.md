# Unity / OVRPlugin startup repair, 2026-11-17

Unity 6000.6.3f1, Meta XR Core 207.0.0, OVRPlugin 1.207.0, Windows 11.

## Cause and evidence

- The original minidump `Unity.exe.31956.dmp` records fail-fast `0xc0000409` on a native OVRPlugin worker thread. Its unwound stack reaches the filesystem-error constructor called from OVRPlugin RVA `0x1eba0c`. The operation string is `remove`; the saved Windows error code is **32, sharing violation**.
- The same native function constructs the SDK cache filename `TRiMSchema.json`. The affected file is `C:\Users\acer\AppData\Local\Temp\cache\TRiMSchema.json`.
- A read-only Windows delete-access probe returned error 32. Windows Restart Manager identified the sole lock owner as **adb.exe, PID 11944**, running `adb -L tcp:5037 fork-server server --reply-fd 568`. Its original parent was no longer running.
- Loading the unmodified DLL and calling its version and machine-ID functions in isolated Python children succeeded. Those probes did not print the machine identifier. This repair did not require replacing or downgrading the SDK.

The native trace was reconstructed using `unwind_dump.py`, Windows [RtlVirtualUnwind](https://learn.microsoft.com/en-us/windows/win32/api/winnt/nf-winnt-rtlvirtualunwind), the installed LLVM objdump, and PE metadata. The script maps image bytes as non-executable data in its own process. REA identified the original PE and its digest; deep decompilation was unavailable, so the trace and instruction inspection used the local Windows/LLVM tools instead.

## Repair performed

1. Backed up the cache as `TRiMSchema.before.json`.
2. Stopped the orphan Android Debug Bridge server using the project's installed `adb.exe kill-server`.
3. Renamed the old cache to `C:\Users\acer\AppData\Local\Temp\cache\TRiMSchema.json.before-OVR-fix-20261117`.
4. Started ADB from a clean shell before Unity. Meta's SDK then regenerated its schema cache.

OVRPlugin remains enabled for the Editor. Its DLL was not patched: SHA-256 `6f8e957590187c66cd19d289e0e049bf21e70a8205487b7a4131c961b638ecb6`. Its importer metadata still exactly matches the pre-troubleshooting backup: SHA-256 `414ca13abaf8261e8c3efaacf80a8eaea622678bf7c3939e2de83380bb0a5636`. No SDK version, global OpenXR runtime selection, or XR input binding was changed by this repair.

## Verification

- Normal native-plugin startup and clean shutdown: exit **0**, `Logs/OVRCrashFix20261117-startup.log`.
- Real PlayMode scene smoke with the registered **Meta XR Simulator** runtime: **1/1 passed**, `XRPlaySmoke.xml`. The log records `Runtime Name: Meta XR Simulator`; the simulator process also launched during this run.
- Character-size verification in PlayMode: **11/11 passed**, `CharacterScalePlayMode.xml`. Covers six-character layout, blinking/chewing, finger deformation, palm-centered controller poses, head visibility, menu interaction, full reaction framing with 1–5 bots, and UI overlaps.
- Both gameplay scenes retain exactly eighteen uniform `1.15` head/glove scale records. The current rendered overview is `Screens/characters-115-percent.png`.
- The interactive Unity Editor was reopened successfully and remained responsive. Its current scene is Untitled; use **Bite or Betray > Open Candy Parlour 3D Scene**, then Play. The `-openfile` launch argument did not select the scene in this Editor session.

Logs remain in `../../Logs/OVRCrashFix20261117-*.log`. Physical Quest hardware and extended VR sessions were not tested. This clears the stale ADB/cache lock on this machine; it does not claim a general fix inside Meta's native library. If the same lock recurs, close Unity and restart ADB before restarting Unity; do not disable the XR plugin or delete game assets.
