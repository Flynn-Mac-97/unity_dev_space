"""M0 feel flush: refresh/compile new code, set scene-side feel values, save.
Player _moveSpeed 0.7->1.8, vcam damping 1.0->0.25, HitStop 0.04/0.1->0.1/0.19.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

MUTATE = r"""
    var sb = new System.Text.StringBuilder();

    var player = GameObject.Find("Player");
    var pc = player.GetComponent<Flynn.Player.PlayerController2D>();
    var so = new UnityEditor.SerializedObject(pc);
    var sp = so.FindProperty("_moveSpeed");
    sb.AppendLine("moveSpeed " + sp.floatValue + " -> 1.8");
    sp.floatValue = 1.8f;
    so.ApplyModifiedPropertiesWithoutUndo();

    var vcamGO = GameObject.Find("CM vcam1");
    var vcam = vcamGO.GetComponent<Cinemachine.CinemachineVirtualCamera>();
    var ft = vcam.GetCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
    if (ft != null) {
        sb.AppendLine("vcam damping " + ft.m_XDamping + "/" + ft.m_YDamping + " -> 0.25");
        ft.m_XDamping = 0.25f; ft.m_YDamping = 0.25f; ft.m_ZDamping = 0.25f;
        UnityEditor.EditorUtility.SetDirty(vcam);
    } else sb.AppendLine("WARN: no FramingTransposer on CM vcam1");

    var hitStop = UnityEngine.Object.FindObjectOfType<Flynn.Effects.HitStop>(true);
    if (hitStop != null) {
        var hso = new UnityEditor.SerializedObject(hitStop);
        var b = hso.FindProperty("_baseDuration"); var m = hso.FindProperty("_maxDuration");
        sb.AppendLine("hitstop " + b.floatValue + "/" + m.floatValue + " -> 0.1/0.19");
        b.floatValue = 0.1f; m.floatValue = 0.19f;
        hso.ApplyModifiedPropertiesWithoutUndo();
    } else sb.AppendLine("WARN: no HitStop in scene");

    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    sb.AppendLine("scene saved");
    return sb.ToString();
"""


def connect_retry(tries: int = 20, delay: float = 1.5) -> UnityBridge:
    last = None
    for _ in range(tries):
        try:
            b = UnityBridge().connect()
            if b.ping().get("success"):
                return b
        except Exception as e:
            last = e
            time.sleep(delay)
    raise RuntimeError(f"bridge unreachable: {last}")


def main() -> int:
    b = connect_retry()
    try:
        b.csharp("UnityEditor.AssetDatabase.Refresh(); return \"refreshed\";")
    except Exception:
        pass
    finally:
        try:
            b.close()
        except Exception:
            pass

    time.sleep(3)
    receipt = []
    b = connect_retry()
    try:
        for _ in range(8):
            try:
                b.call("manage_editor", "wait_for_compile")
                r = b.csharp(MUTATE)
                receipt.append(str(r.get("result")))
                break
            except Exception as e:
                if "reload in progress" in str(e) or "unreachable" in str(e) or "Connection" in str(e):
                    time.sleep(5)
                    try:
                        b.close()
                    except Exception:
                        pass
                    b = connect_retry()
                else:
                    raise

        con = b.call("read_console", "get", types=["error"], count=10)
        d = (con.get("data") or {}).get("data") or con.get("data") or {}
        ent = d.get("entries") if isinstance(d, dict) else d
        msgs = []
        for e in ent or []:
            m = (e.get("message") if isinstance(e, dict) else str(e)) or ""
            line = m.splitlines()[0][:140]
            if line not in msgs:
                msgs.append(line)
        receipt.append("console errs: " + ("; ".join(msgs[:6]) if msgs else "none"))
    finally:
        b.close()

    print("\n".join(receipt[:20]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
