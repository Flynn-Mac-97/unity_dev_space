#!/usr/bin/env python3
"""Direct client for the Codely Unity TCP bridge (port from .com-unity-codely.json).

Protocol (reversed 2026-06-23):
  - Server greets with line: 'WELCOME UNITY-TCP 1 FRAMING=1 SERVER_VERSION=2\\n'
  - All subsequent messages are framed: 8-byte big-endian length prefix + UTF-8 body.
  - Client announces (framed text): 'CLIENT_VERSION=2', 'PLATFORM=cli'.
  - Command request body (JSON): {"type":<command>,"params":{"action":<action>,...},"request_id":<id>}
  - Response body (JSON): {"success":bool,"message":str,"data":{...}}
  - 'ping' (framed text) -> {"success":true,"message":"pong"}
"""
import json, os, socket, struct, sys

def bridge_port(project_dir="."):
    cfg = os.path.join(project_dir, ".com-unity-codely.json")
    with open(cfg) as f:
        return json.load(f)["unity_port"]

class UnityBridge:
    def __init__(self, host="127.0.0.1", port=None, project_dir=".", timeout=15):
        self.host = host
        self.port = port or bridge_port(project_dir)
        self.timeout = timeout
        self.s = None
        self.buf = b""
        self._rid = 0

    def connect(self):
        self.s = socket.create_connection((self.host, self.port), timeout=self.timeout)
        self.s.settimeout(self.timeout)
        self._readline()  # WELCOME
        self._send_text("CLIENT_VERSION=2")
        self._send_text("PLATFORM=cli")
        return self

    # --- framing ---
    def _send_frame(self, body: bytes):
        self.s.sendall(struct.pack(">Q", len(body)) + body)
    def _send_text(self, t): self._send_frame(t.encode("utf-8"))
    def _fill(self):
        d = self.s.recv(65536)
        if not d: raise EOFError("server closed")
        self.buf += d
    def _readline(self):
        while b"\n" not in self.buf: self._fill()
        ln, _, self.buf = self.buf.partition(b"\n")
        return ln.decode("utf-8", "replace").rstrip("\r")
    def _read_frame(self):
        while len(self.buf) < 8: self._fill()
        n = struct.unpack(">Q", self.buf[:8])[0]
        self.buf = self.buf[8:]
        while len(self.buf) < n: self._fill()
        body, self.buf = self.buf[:n], self.buf[n:]
        return json.loads(body.decode("utf-8"))

    # --- API ---
    def ping(self):
        self._send_text("ping")
        return self._read_frame()
    def call(self, command, action=None, **params):
        self._rid += 1
        if action is not None: params = {"action": action, **params}
        req = {"type": command, "params": params, "request_id": f"py-{self._rid}"}
        self._send_frame(json.dumps(req).encode("utf-8"))
        return self._read_frame()
    def csharp(self, script):
        """Run arbitrary editor C# (escape hatch). `return <expr>;` allowed.
        Returns the inner data dict ({result, logs, ...}) or raises on failure."""
        r = self.call("execute_csharp_script", script=script)
        outer = r.get("data", {})
        if not outer.get("success", False):
            raise RuntimeError(r.get("code") or r.get("error") or outer.get("message") or r)
        return outer.get("data", {})
    def close(self):
        try: self.s.close()
        except: pass

if __name__ == "__main__":
    b = UnityBridge().connect()
    print("ping ->", b.ping())
    print("\nget_project_root ->",
          json.dumps(b.call("manage_editor", "get_project_root"))[:300])
    print("\nmanage_scene get_hierarchy ->")
    r = b.call("manage_scene", "get_hierarchy")
    print(json.dumps(r, indent=2)[:1800])
    b.close()
