# CI Unity (runner laptop)

Commit `389425c` · 2026-10-17 08:37 WIB · **GAGAL**

| Cek | Hasil |
|---|---|
| EditMode tests | 111/111 lulus, 0 gagal |
| PlayMode tests (scene sungguhan) | 1/9 lulus, 8 gagal |
| Build APK Development | Succeeded; mode=Development; errors=0; warnings=5; bytes=1784374222; durasi=217.7s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Development.apk; laporan=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Development_20261017-083404_389425c-dirty.txt |
| Error shader | 0 |
| Error compile | 0 |

## Tes gagal

- BiteOrBetray.Tests.PlayMode.GameplayPerformanceTests.Gameplay_idle_gc_render_stats_and_automated_match_are_recorded: SetUp :   Expected: False
  But was:  True
- BiteOrBetray.Tests.PlayMode.OfflineMatchTests.Application_pause_freezes_turn_timer_and_returns_held_candy: SetUp :   Suite ini dijalankan Editor tanpa perangkat XR.
  Expected: False
  But was:  True

TearDown : Unhandled log message: '[Log] [OVRManager] OnDestroy'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OfflineMatchTests.Bot_counts_one_to_five_start_with_selected_seats_and_difficulties: SetUp :   Suite ini dijalankan Editor tanpa perangkat XR.
  Expected: False
  But was:  True

TearDown : Unhandled log message: '[Log] [OVRManager] OnDestroy'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OfflineMatchTests.Eliminated_human_is_locked_transparent_spectator_until_winner_and_replay: SetUp :   Suite ini dijalankan Editor tanpa perangkat XR.
  Expected: False
  But was:  True

TearDown : Unhandled log message: '[Log] [OVRManager] OnDestroy'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OfflineMatchTests.Full_match_via_table_actions_items_grapple_results_replay_and_menu: SetUp :   Suite ini dijalankan Editor tanpa perangkat XR.
  Expected: False
  But was:  True

TearDown : Unhandled log message: '[Log] [OVRManager] OnDestroy'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OfflineMatchTests.Lost_application_focus_freezes_turn_timer_and_returns_held_candy: SetUp :   Suite ini dijalankan Editor tanpa perangkat XR.
  Expected: False
  But was:  True

TearDown : Unhandled log message: '[Log] [OVRManager] OnDestroy'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OfflineMatchTests.Tracking_loss_with_grip_held_recovers_without_accidental_give: SetUp :   Suite ini dijalankan Editor tanpa perangkat XR.
  Expected: False
  But was:  True

TearDown : Unhandled log message: '[Log] [OVRManager] OnDestroy'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OfflineMatchTests.Tracking_loss_with_trigger_held_recovers_without_accidental_give: SetUp :   Suite ini dijalankan Editor tanpa perangkat XR.
  Expected: False
  But was:  True

TearDown : Unhandled log message: '[Log] [OVRManager] OnDestroy'. Use UnityEngine.TestTools.LogAssert.Expect

## Budget Audit

```
AUDIT GAGAL
System.ArgumentException: Invalid SceneManagerSetup:
 No loaded scene found.
 One active scene is required. Number of active scenes: 0
  at (wrapper managed-to-native) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(UnityEditor.SceneManagement.SceneSetup[])
  at BiteOrBetray.EditorTools.GameplayPerformanceBuilder.PrepareScenes () [0x0011c] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\GameplayPerformanceBuilder.cs:43 
  at BiteOrBetray.EditorTools.CIRunner.Run () [0x00023] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\CIRunner.cs:21
```

## Performa PlayMode (Logs/ci/perf.txt)

```
BOB-022 | Editor 6000.6.3f1 | NVIDIA GeForce RTX 3050 Laptop GPU | 2026-10-17T01:13:10.2902896Z
GC = ProfilerRecorder Memory/GC Allocated In Frame (byte, seluruh frame termasuk Editor/Test Runner).
Draw/SetPass = UnityStats, seluruh render Editor mono, BUKAN draw/mata XR atau bukti 72 FPS Quest.
Timer input mati, tempo=4, timeScale=1, 5 bot; isi dispenser/inventory dikontrol fixture. Animasi/komponen gameplay tetap aktif.
BoB_Art3D sebelum/sesudah: 138/100 slot warna, 137/11 slot shadow.
Shadow mesh kaku/dekor mati; shadow skinned tetap. Furnitur digabung per material/lightmap/layer/probe; collider dan karakter tetap.
status=incomplete (lihat hasil tes)
```
