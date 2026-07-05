"""Tweak pass 1: compile, force scene/asset serialized values to the new tuning
(existing components keep stale values otherwise), save, screencap ring state."""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge


def connect(retries=20, delay=2.0):
    for _ in range(retries):
        try:
            return UnityBridge(timeout=60).connect()
        except Exception:
            time.sleep(delay)
    raise SystemExit("BRIDGE DOWN")


def wait_idle(b, timeout=180):
    t0 = time.time()
    while time.time() - t0 < timeout:
        try:
            d = (b.call("manage_editor", "get_state").get("data") or {}).get("data") or {}
            if not d.get("isCompiling") and not d.get("isUpdating"):
                return b
        except Exception:
            try: b.close()
            except Exception: pass
            time.sleep(2)
            b = connect()
        time.sleep(1.5)
    raise SystemExit("compile timeout")


def cs_retry(b, code, tries=15):
    """csharp with retry across domain reloads (bridge reports 'reload in progress')."""
    for i in range(tries):
        try:
            return b, b.csharp(code)
        except (RuntimeError, OSError, EOFError) as e:
            if i == tries - 1:
                raise
            time.sleep(3)
            if isinstance(e, (OSError, EOFError)):
                try: b.close()
                except Exception: pass
                b = connect()
    return b, None


def main():
    receipt = []
    b = connect()
    try:
        b.csharp('UnityEditor.AssetDatabase.Refresh(); return "ok";')
    except Exception:
        pass
    try: b.close()
    except Exception: pass
    time.sleep(3)
    b = connect()
    b = wait_idle(b)

    b, r = cs_retry(b, 'return UnityEditor.EditorUtility.scriptCompilationFailed ? "FAILED" : "clean";')
    if r.get("result") != "clean":
        c = b.call("read_console", "get", types=["error"], count=5)
        print("COMPILE FAILED:", str(c.get("data"))[:800])
        return 1
    receipt.append("compile: clean")

    # Force new tuning onto the already-serialized scene component + settings asset.
    b, r = cs_retry(b, '''
        var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.Combat.PowerBuildupSettings>(
            "Assets/Flynn/Configs/Player/PowerBuildupSettings.asset");
        s.holdToChargeDelay = 0.3f; s.throwSpeed = 6.5f; s.throwAnimSpeed = 0.75f;
        UnityEditor.EditorUtility.SetDirty(s);

        var fx = UnityEngine.Object.FindObjectOfType<Flynn.Player.Combat.WrenchChargeFX>();
        if (fx == null) return "NO_FX";
        var so = new UnityEditor.SerializedObject(fx);
        so.FindProperty("_ringRadius").floatValue = 0.26f;
        so.FindProperty("_ringOffset").vector2Value = new UnityEngine.Vector2(0f, 0.3f);
        so.FindProperty("_ringWidth").floatValue = 0.018f;
        so.FindProperty("_fillColor").colorValue = new UnityEngine.Color(1,1,1,0.9f);
        so.FindProperty("_zoneIdleColor").colorValue = new UnityEngine.Color(1,1,1,0.3f);
        so.FindProperty("_zoneHotColor").colorValue = new UnityEngine.Color(1,1,1,1);
        so.FindProperty("_reticleColor").colorValue = new UnityEngine.Color(1,1,1,0.9f);
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        return "values forced + saved";
    ''')
    receipt.append(f"tuning: {r.get('result')}")

    # Visual check: ring at sweetspot state.
    b.call("manage_editor", "play")
    for _ in range(30):
        time.sleep(1)
        d = (b.call("manage_editor", "get_state").get("data") or {}).get("data") or {}
        if d.get("isPlaying"):
            break
    time.sleep(2)

    r = b.csharp('var bus = Flynn.Core.GameEventBus.Instance; if (bus == null) return "NO_BUS";'
                 'bus.Publish(new Flynn.Events.PowerBuildupStarted(Flynn.Events.ActionType.Swing));'
                 'bus.Publish(new Flynn.Events.PowerBuildupChanged(0.82f, true));'
                 'return "ok";')
    time.sleep(0.4)
    shot = b.call("manage_screenshot", "capture_game_view")
    receipt.append(f"ring shot: {(shot.get('data') or {}).get('path')}")

    b.call("manage_editor", "stop")
    b.close()
    print("\n".join(receipt))
    return 0


if __name__ == "__main__":
    sys.exit(main())
