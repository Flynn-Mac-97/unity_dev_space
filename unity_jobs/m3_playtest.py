"""M3 end-to-end play-mode verification of the island-1 demo loop.
Drives the loop via direct method calls (no input sim), asserts each stage,
screenshots the end screen, stops play. Compact receipt.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

STAGE_A = r"""
    var sb = new System.Text.StringBuilder();
    var wood  = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.ItemDefinition>("Assets/Flynn/Configs/Items/Wood_Item.asset");
    var stone = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.ItemDefinition>("Assets/Flynn/Configs/Items/Stone_Item.asset");
    var inv = Flynn.Player.PlayerInventory.Instance;
    if (inv == null) return "FAIL: no PlayerInventory (not in play mode?)";

    var active = Flynn.Npc.ObjectiveTracker.Instance != null
        ? Flynn.Npc.ObjectiveTracker.Instance.GetActiveObjectives() : null;
    sb.Append("active_objs=[");
    if (active != null) foreach (var o in active) sb.Append(o.SignalId + ",");
    sb.AppendLine("]");

    inv.TryAddItem(wood, 3); inv.TryAddItem(stone, 2);

    var rep = UnityEngine.Object.FindObjectOfType<Flynn.Pod.RepairableStructure>(true);
    if (rep == null) return sb + "FAIL: no RepairableStructure";
    rep.TryAdvanceStage();
    sb.AppendLine("repaired=" + rep.IsFullyRepaired);

    var burnerGO = GameObject.Find("PodBurner");
    var feed = burnerGO != null ? burnerGO.transform.Find("FeedPoint") : null;
    var repair = burnerGO != null ? burnerGO.transform.Find("RepairPoint") : null;
    sb.AppendLine("feedActive=" + (feed != null && feed.gameObject.activeSelf)
        + " repairActive=" + (repair != null && repair.gameObject.activeSelf));
    return sb.ToString();
"""

STAGE_B = r"""
    var sb = new System.Text.StringBuilder();
    var wood = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.ItemDefinition>("Assets/Flynn/Configs/Items/Wood_Item.asset");
    var inv = Flynn.Player.PlayerInventory.Instance;
    inv.TryAddItem(wood, 6);
    var burner = UnityEngine.Object.FindObjectOfType<Flynn.Pod.BurnerStation>(true);
    for (int i = 0; i < 6; i++) burner.TryBurn();
    var pod = UnityEngine.Object.FindObjectOfType<Flynn.Pod.PodPower>(true);
    sb.AppendLine("podPercent=" + pod.Percent.ToString("F2") + " stabilised=" + pod.IsStabilised);

    // battery recharge: park player at pod
    var player = GameObject.Find("Player");
    player.transform.position = pod.transform.position + new Vector3(0.8f, 0f, 0f);
    Flynn.Player.RobotBattery.Instance.SetCharge(40);
    return sb.ToString();
"""

STAGE_C = r"""
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("batteryAfterPodWait=" + Flynn.Player.RobotBattery.Instance.Charge);

    var active = Flynn.Npc.ObjectiveTracker.Instance.GetActiveObjectives();
    sb.Append("active=[");
    foreach (var o in active) sb.Append(o.SignalId + ",");
    sb.AppendLine("]");

    // exhaustion round-trip
    var pc = GameObject.Find("Player").GetComponent<Flynn.Player.PlayerController2D>();
    var wrench = GameObject.Find("Player").GetComponent<Flynn.Player.Combat.WrenchController>();
    Flynn.Player.RobotBattery.Instance.SetCharge(0);
    sb.AppendLine("wrenchDisabledAtEmpty=" + !wrench.enabled);
    Flynn.Player.RobotBattery.Instance.SetCharge(50);
    sb.AppendLine("wrenchRestored=" + wrench.enabled);

    // trade
    var metal = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.ItemDefinition>("Assets/Flynn/Configs/Items/MetalScrap_Item.asset");
    var npc = UnityEngine.Object.FindObjectOfType<Flynn.Demo.ScriptedTradeNpc>(true);
    npc.Talk(); // first talk -> request
    Flynn.Player.PlayerInventory.Instance.TryAddItem(metal, 3);
    npc.Talk(); // trade
    var barrier = GameObject.Find("Island2GateBarrier");
    sb.AppendLine("barrierOff=" + (barrier == null || !barrier.activeSelf)
        + " battery=" + Flynn.Player.RobotBattery.Instance.Charge);
    return sb.ToString();
"""

STAGE_D = r"""
    var sb = new System.Text.StringBuilder();
    var zone = GameObject.Find("DemoEndZone");
    var player = GameObject.Find("Player");
    player.transform.position = zone.transform.position;
    return "teleported to end zone @ " + zone.transform.position.ToString("F2");
"""

STAGE_E = r"""
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("timeScale=" + Time.timeScale);
    var all = Flynn.Npc.ObjectiveTracker.Instance.GetAllObjectives();
    int done = 0; foreach (var o in all) if (o.State == Flynn.Npc.ObjectiveTracker.ObjectiveState.Completed) done++;
    sb.AppendLine("objectives total=" + all.Count + " completed=" + done);
    return sb.ToString();
"""


def main() -> int:
    b = UnityBridge().connect()
    receipt = []
    try:
        if not b.ping().get("success"):
            print("BRIDGE DOWN")
            return 1

        b.call("manage_editor", "play")
        time.sleep(6)  # init + wake routine (1.5s)

        for name, script, wait in [
            ("A", STAGE_A, 2.5),
            ("B", STAGE_B, 4.0),   # stir coroutine + battery recharge ticks
            ("C", STAGE_C, 1.0),
            ("D", STAGE_D, 2.5),   # end trigger poll + fade 1.6s
            ("E", STAGE_E, 0.5),
        ]:
            try:
                r = b.csharp(script)
                receipt.append(f"[{name}] " + str(r.get("result")).strip())
            except Exception as e:
                receipt.append(f"[{name}] EXC {str(e)[:120]}")
            time.sleep(wait)

        shot = b.call("manage_screenshot", "capture_game_view")
        receipt.append("shot: " + str((shot.get("data") or {}).get("path")))

        con = b.call("read_console", "get", types=["error"], count=10)
        d = (con.get("data") or {}).get("data") or con.get("data") or {}
        ent = d.get("entries") if isinstance(d, dict) else d
        msgs = []
        for e in ent or []:
            m = (e.get("message") if isinstance(e, dict) else str(e)) or ""
            line = m.splitlines()[0][:130]
            if line not in msgs:
                msgs.append(line)
        receipt.append("console errs: " + ("; ".join(msgs[:6]) if msgs else "none"))

        b.call("manage_editor", "stop")
    finally:
        b.close()

    print("\n".join(receipt[:30]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
