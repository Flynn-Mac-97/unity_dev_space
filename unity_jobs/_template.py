"""Job script template — ONE batched flush of Unity editor mutations.

Doctrine (root .claude/CLAUDE.md):
- Accumulate ALL editor mutations for a task here; run ONCE at task end.
- Swallow every bridge response; print ONLY a compact receipt (<=15 lines).
- Run from repo root:
    NO_PROXY="localhost,127.0.0.1" HTTP_PROXY="" HTTPS_PROXY="" python unity_jobs/<task>.py
- Batch at the wire: create_batch/edit_batch, or one execute_csharp_script
  (param key 'script' — use b.csharp(...)) doing N mutations editor-side.
- Verify once at the end: wait_for_compile (if scripts changed) + read_console.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # repo root
from unity_bridge import UnityBridge


def main() -> int:
    b = UnityBridge().connect()  # one connection for the whole job
    receipt: list[str] = []
    errors = 0
    try:
        # --- health ---
        if not b.ping().get("success"):
            print("BRIDGE DOWN — open the Unity editor")
            return 1

        # --- mutations (examples; replace) ---
        # r = b.call("manage_gameobject", "create", name="Foo", parent="MANAGERS")
        # data = (r.get("data") or {}).get("data") or {}   # payload nested here
        # receipt.append(f"create Foo — id {data.get('instanceID')}")
        #
        # N-object wiring in ONE round trip.
        # NOTE: b.csharp() returns ALREADY UNWRAPPED {result, logs, log_count}.
        # r = b.csharp("""
        #     var go = GameObject.Find("Foo");
        #     go.AddComponent<UnityEngine.Rigidbody2D>();
        #     UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        #     return "wired Foo";
        # """)
        # receipt.append(str(r.get("result")))

        # --- verify once ---
        # b.call("manage_editor", "wait_for_compile")     # only if scripts changed
        con = b.call("read_console", "error")
        errs = ((con.get("data") or {}).get("data") or {}) or {}
        receipt.append(f"console: {errs if errs else 'clean'}")
    finally:
        b.close()

    print("\n".join(receipt[:15]))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
