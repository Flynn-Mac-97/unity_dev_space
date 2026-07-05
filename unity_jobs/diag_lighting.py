"""Diagnose pink-outline / Shadow2D lighting issues. Read-only + screenshot."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge


def main() -> int:
    b = UnityBridge().connect()
    receipt: list[str] = []
    try:
        if not b.ping().get("success"):
            print("BRIDGE DOWN — open the Unity editor")
            return 1

        r = b.csharp("""
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            string[] names = {
                "Flynn/ProjectedShadow2D", "Flynn/SpriteOutline", "Flynn/CharacterSpriteLit",
                "Flynn/PixelLitSprite", "Flynn/FlynnSprite", "Flynn/SpriteLit3D"
            };
            foreach (var n in names) {
                var s = Shader.Find(n);
                if (s == null) { sb.AppendLine(n + " = NOT FOUND"); continue; }
                bool err = UnityEditor.ShaderUtil.ShaderHasError(s);
                sb.AppendLine(n + " = " + (err ? "COMPILE ERROR" : "ok") + (s.isSupported ? "" : " UNSUPPORTED"));
            }

            var lights = UnityEngine.Object.FindObjectsOfType<UnityEngine.Rendering.Universal.Light2D>();
            foreach (var l in lights)
                sb.AppendLine("Light2D " + l.name + " type=" + l.lightType + " shadows=" + l.shadowsEnabled + " int=" + l.intensity.ToString("F2"));

            var mgrType = System.Type.GetType("Flynn.Shadow2D.Shadow2DManager, Flynn.Runtime");
            var mgr = mgrType != null ? UnityEngine.Object.FindObjectOfType(mgrType) as MonoBehaviour : null;
            sb.AppendLine("Shadow2DManager " + (mgr == null ? "MISSING from scene" : "on " + mgr.gameObject.name));

            var castType = System.Type.GetType("Flynn.Shadow2D.Shadow2DCaster, Flynn.Runtime");
            int nCast = castType != null ? UnityEngine.Object.FindObjectsOfType(castType).Length : -1;
            sb.AppendLine("Shadow2DCaster count=" + nCast);

            int shadowChildren = 0;
            foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>(true))
                if (sr.gameObject.name == "Shadow2D") shadowChildren++;
            sb.AppendLine("Shadow2D child sprites=" + shadowChildren);
            return sb.ToString();
        """)
        receipt.append(str(r.get("result")))

        con = b.call("read_console", "get", types=["error"], count=15)
        data = (con.get("data") or {}).get("data") or con.get("data") or {}
        entries = data.get("entries") if isinstance(data, dict) else data
        if entries:
            seen = set()
            for e in entries:
                msg = (e.get("message") if isinstance(e, dict) else str(e)) or ""
                line = msg.splitlines()[0][:160]
                if line not in seen:
                    seen.add(line)
                    receipt.append("ERR: " + line)
        else:
            receipt.append("console errors: none")

        shot = b.call("manage_screenshot", "capture_game_view")
        sdata = shot.get("data") or {}
        receipt.append("shot: " + str(sdata.get("path")))
    finally:
        b.close()

    print("\n".join(receipt[:40]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
