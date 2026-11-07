"""A private stand-in for saverepo.kingdomsandcastles.com, for testing Kingdom Share.

The game's real server has no way to delete an upload, so a test kingdom must never go there. This one listens on
this computer only (127.0.0.1) and keeps everything in memory, so it is gone when the script stops.
"""
import base64
import json
import sys
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = 8765
codes = {}  # code -> {"auth": str, "save": str or None, "shot": bytes or None}
next_code = [1000]


def log(text):
    print(text, flush=True)


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):
        pass

    def reply(self, code, body=b"", kind="text/plain"):
        if isinstance(body, str):
            body = body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def form(self):
        length = int(self.headers.get("Content-Length") or 0)
        data = self.rfile.read(length).decode("ascii", "replace")
        return {k: v[0] for k, v in urllib.parse.parse_qs(data, keep_blank_values=True).items()}

    def do_GET(self):
        if self.path == "/alive":
            return self.reply(200, "alive")
        parts = self.path.strip("/").split("/")
        if len(parts) == 2 and parts[1] == "screenshot":
            entry = codes.get(parts[0])
            if entry and entry["shot"]:
                log("screenshot " + parts[0] + " sent")
                return self.reply(200, entry["shot"], "image/png")
        return self.reply(404, "not found")

    def do_POST(self):
        form = self.form()
        code = form.get("code", "")
        if self.path == "/getnewcode":
            next_code[0] += 1
            new = "T" + str(next_code[0])
            codes[new] = {"auth": "auth" + new, "save": None, "shot": None}
            log("new code " + new)
            return self.reply(200, new + ":" + codes[new]["auth"])
        if self.path == "/checkcode":
            known = code in codes and codes[code]["save"] is not None
            log("check " + code + " = " + str(known))
            return self.reply(200, "true" if known else "false")
        if self.path == "/checkcodeownership":
            entry = codes.get(code)
            ok = entry is not None and entry["auth"] == form.get("editauthcode")
            body = {"exists": "true" if ok else "false", "lastupload": "2026-11-07T10:00:00Z" if ok and entry["save"] else ""}
            log("ownership " + code + " = " + str(ok))
            return self.reply(200, json.dumps(body), "application/json")
        if self.path == "/upload":
            entry = codes.get(code)
            if entry is None or entry["auth"] != form.get("editAuthCode"):
                log("upload refused for " + code)
                return self.reply(403, "no")
            entry["save"] = form.get("base64FileData", "")
            entry["shot"] = base64.b64decode(form.get("screenshotFileData", ""))
            log("upload " + code + ": save " + str(len(entry["save"])) + " chars, picture " + str(len(entry["shot"])) + " bytes")
            return self.reply(200, "ok")
        if self.path == "/retrieve":
            entry = codes.get(code)
            if entry is None or not entry["save"]:
                return self.reply(404, "")
            log("retrieve " + code)
            return self.reply(200, entry["save"])
        return self.reply(404, "unknown")


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    log("share test server on 127.0.0.1:" + str(PORT))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        sys.exit(0)
