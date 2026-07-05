"""Play-mode visual check for wrench FX. Small csharp calls only (big snippets
return empty). Publishes buildup events directly so no input simulation needed.
Bails fast on first failure — no churn."""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

SHOTS = []


def cs(b, code):
    r = b.csharp(code)
    return r.get("result")


def shot(b, tag):
    r = b.call("manage_screenshot", "capture_game_view")
    p = (r.get("data") or {}).get("path")
    SHOTS.append((tag, p))
    return p


PUB = (
    'var bus = Flynn.Core.GameEventBus.Instance;'
    'if (bus == null) return "NO_BUS";'
)


def main():
    b = UnityBridge(timeout=60).connect()

    # enter play
    b.call("manage_editor", "play")
    for _ in range(30):
        time.sleep(1)
        d = (b.call("manage_editor", "get_state").get("data") or {}).get("data") or {}
        if d.get("isPlaying"):
            break
    else:
        print("FAIL: never entered play")
        return 1
    time.sleep(2)  # let Awake/managers settle

    # 1. swing charge mid
    r = cs(b, PUB + 'bus.Publish(new Flynn.Events.PowerBuildupStarted(Flynn.Events.ActionType.Swing));'
                    'bus.Publish(new Flynn.Events.PowerBuildupChanged(0.5f, false));'
                    'return "ok";')
    if r != "ok":
        print(f"BAIL: event publish failed ({r}) — verify manually in play mode")
        b.call("manage_editor", "stop")
        return 1
    time.sleep(0.3)
    shot(b, "charge_mid")

    # 2. sweetspot
    cs(b, PUB + 'bus.Publish(new Flynn.Events.PowerBuildupChanged(0.82f, true)); return "ok";')
    time.sleep(0.2)
    shot(b, "sweetspot")

    # 3. perfect release burst (capture fast, burst is 0.3s)
    cs(b, PUB + 'bus.Publish(new Flynn.Events.PowerBuildupReleased(0.82f, true, 4, 6f, Flynn.Events.ActionType.Swing)); return "ok";')
    shot(b, "perfect_burst")

    # 4. throw aim line
    cs(b, PUB + 'bus.Publish(new Flynn.Events.PowerBuildupStarted(Flynn.Events.ActionType.Throw));'
                'bus.Publish(new Flynn.Events.PowerBuildupChanged(0.6f, false));'
                'return "ok";')
    time.sleep(0.3)
    shot(b, "aim_line")
    cs(b, PUB + 'bus.Publish(new Flynn.Events.PowerBuildupReleased(0.6f, false, 2, 9f, Flynn.Events.ActionType.Throw)); return "ok";')

    # 5. real projectile via WrenchController's own spawn path (reflection Launch)
    r = cs(b, 'var pc = UnityEngine.Object.FindObjectOfType<Flynn.Player.Combat.WrenchController>();'
              'if (pc == null) return "NO_WC";'
              'var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.Combat.PowerBuildupSettings>("Assets/Flynn/Configs/Player/PowerBuildupSettings.asset");'
              'Flynn.Player.Combat.ThrownWrench.Launch(pc.transform, UnityEngine.Vector2.right, 0.8f, 3, false, s, null);'
              'return "thrown";')
    if r == "thrown":
        time.sleep(0.35)
        shot(b, "wrench_flight")
        time.sleep(1.2)
        shot(b, "wrench_return")
    else:
        SHOTS.append(("throw", f"skipped ({r})"))

    b.call("manage_editor", "stop")
    b.close()
    for tag, p in SHOTS:
        print(f"{tag}: {p}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
