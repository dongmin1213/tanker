#!/usr/bin/env python3
"""Unity MCP relay 범용 클라이언트.

  python3 mcp_unity.py list                     # 도구 이름 나열
  python3 mcp_unity.py schema <tool>            # 입력 스키마 출력
  python3 mcp_unity.py call <tool> '<json>'     # 도구 호출 (이미지 결과는 파일 저장)
"""
import base64
import json
import selectors
import subprocess
import sys
import time
from pathlib import Path

RELAY = str(Path.home() / ".unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64")
OUTDIR = Path.cwd() / "captures"


class Client:
    def __init__(self):
        self.proc = subprocess.Popen([RELAY, "--mcp"], stdin=subprocess.PIPE,
                                     stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        self.sel = selectors.DefaultSelector()
        self.sel.register(self.proc.stdout, selectors.EVENT_READ)
        self.buf = b""
        self.send({"jsonrpc": "2.0", "id": 0, "method": "initialize",
                   "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                              "clientInfo": {"name": "claude-bash", "version": "0.1"}}})
        if self.recv(0) is None:
            sys.exit("FAIL: relay initialize 응답 없음")
        self.send({"jsonrpc": "2.0", "method": "notifications/initialized"})

    def send(self, obj):
        self.proc.stdin.write((json.dumps(obj) + "\n").encode())
        self.proc.stdin.flush()

    def recv(self, want_id, timeout=120):
        deadline = time.time() + timeout
        while time.time() < deadline:
            if not self.sel.select(timeout=0.5):
                continue
            chunk = self.proc.stdout.read1(1 << 20)
            if not chunk:
                return None
            self.buf += chunk
            while b"\n" in self.buf:
                line, self.buf = self.buf.split(b"\n", 1)
                if not line.strip():
                    continue
                try:
                    msg = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if msg.get("id") == want_id:
                    return msg
        return None

    def tools(self):
        self.send({"jsonrpc": "2.0", "id": 1, "method": "tools/list"})
        resp = self.recv(1) or {}
        return resp.get("result", {}).get("tools", [])

    def call(self, name, args):
        self.send({"jsonrpc": "2.0", "id": 2, "method": "tools/call",
                   "params": {"name": name, "arguments": args}})
        return self.recv(2)


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "list"
    c = Client()
    if mode == "list":
        for t in c.tools():
            print(t["name"])
    elif mode == "schema":
        target = sys.argv[2]
        for t in c.tools():
            if t["name"] == target:
                print(json.dumps(t, indent=2, ensure_ascii=False))
                break
        else:
            sys.exit(f"도구 없음: {target}")
    elif mode in ("call", "run"):
        if mode == "run":  # run <file.cs>: 파일 내용을 Unity_RunCommand Code로 전달
            name = "Unity_RunCommand"
            args = {"Code": Path(sys.argv[2]).read_text()}
        else:
            name = sys.argv[2]
            args = json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
        resp = c.call(name, args)
        if resp is None:
            sys.exit("FAIL: 응답 없음 (timeout)")
        result = resp.get("result", resp.get("error"))
        if not isinstance(result, dict):
            print(json.dumps(resp, ensure_ascii=False)); return
        for i, item in enumerate(result.get("content", [])):
            if item.get("type") == "text":
                print(item["text"])
            elif item.get("type") == "image":
                OUTDIR.mkdir(exist_ok=True)
                ext = "png" if "png" in item.get("mimeType", "png") else "jpg"
                p = OUTDIR / f"capture_{int(time.time())}_{i}.{ext}"
                p.write_bytes(base64.b64decode(item["data"]))
                print(f"[이미지 저장: {p}]")
        if result.get("isError"):
            sys.exit("도구 에러 (위 메시지 참조)")
    c.proc.kill()


if __name__ == "__main__":
    main()
