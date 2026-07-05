"""Burst-capture the game view during play and stitch a contact sheet.
Usage: python unity_jobs/burst_capture.py [seconds=6] [interval=0.4]
Output: screenshots/burst_<ts>_sheet.png (grid, newest last) — readable motion strip.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge


def main() -> int:
    seconds = float(sys.argv[1]) if len(sys.argv) > 1 else 6.0
    interval = float(sys.argv[2]) if len(sys.argv) > 2 else 0.4

    b = UnityBridge().connect()
    paths = []
    try:
        t_end = time.time() + seconds
        while time.time() < t_end:
            shot = b.call("manage_screenshot", "capture_game_view")
            p = (shot.get("data") or {}).get("path")
            if p:
                paths.append(p)
            time.sleep(interval)
    finally:
        b.close()

    if not paths:
        print("no frames captured")
        return 1

    from PIL import Image
    cols = 4
    rows = (len(paths) + cols - 1) // cols
    thumb_w, thumb_h = 480, 270
    sheet = Image.new("RGB", (cols * thumb_w, rows * thumb_h), (10, 10, 12))
    for i, p in enumerate(paths):
        try:
            im = Image.open(p).resize((thumb_w, thumb_h), Image.NEAREST)
            sheet.paste(im, ((i % cols) * thumb_w, (i // cols) * thumb_h))
        except Exception:
            pass
    out = os.path.join("screenshots", f"burst_{time.strftime('%H%M%S')}_sheet.png")
    sheet.save(out)
    print(f"frames={len(paths)} sheet={out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
