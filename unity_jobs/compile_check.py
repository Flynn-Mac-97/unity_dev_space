"""Reusable: refresh -> wait for compile/domain reload -> report clean/failed (+first errors).
Run after any batch of .cs edits: python unity_jobs/compile_check.py"""
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
    raise SystemExit("BRIDGE DOWN — open the Unity editor")


def main():
    b = connect()
    try:
        b.csharp('UnityEditor.AssetDatabase.Refresh(); return "ok";')
    except Exception:
        pass
    try:
        b.close()
    except Exception:
        pass
    time.sleep(3)
    b = connect()

    for _ in range(60):
        try:
            d = (b.call("manage_editor", "get_state").get("data") or {}).get("data") or {}
            if not d.get("isCompiling") and not d.get("isUpdating"):
                break
        except Exception:
            try: b.close()
            except Exception: pass
            time.sleep(2)
            b = connect()
        time.sleep(1.5)

    r = None
    for i in range(15):
        try:
            r = b.csharp('return UnityEditor.EditorUtility.scriptCompilationFailed ? "COMPILE_FAILED" : "clean";')
            break
        except (RuntimeError, OSError, EOFError) as e:
            time.sleep(3)
            if isinstance(e, (OSError, EOFError)):
                try: b.close()
                except Exception: pass
                b = connect()

    result = r.get("result") if r else "UNKNOWN"
    print("compile:", result)
    if result != "clean":
        c = b.call("read_console", "get", types=["error"], count=5)
        print(str(c.get("data"))[:800])
    b.close()
    return 0 if result == "clean" else 1


if __name__ == "__main__":
    sys.exit(main())
