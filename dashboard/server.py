#!/usr/bin/env python3
"""Background HTTP server for the Arisen Studio migration dashboard and completion API."""

from __future__ import annotations

import argparse
import copy
import datetime as dt
import json
import os
import re
import signal
import subprocess
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

SCRIPT_DIR = Path(os.path.abspath(__file__)).parent
MILESTONES_PATH = SCRIPT_DIR / "milestones.json"
INDEX_PATH = SCRIPT_DIR / "index.html"
MEASURE_PATH = SCRIPT_DIR / "measure.py"
EVENTS_PATH = SCRIPT_DIR / "events.json"
PID_PATH = SCRIPT_DIR / "server.pid"
LOG_PATH = SCRIPT_DIR / "server.log"

MAX_BODY_BYTES = 1_048_576
MAX_EVENTS = 200

VALID_STATUSES = {"done", "in_progress", "proposed", "planned"}
VALID_GATE_STATUSES = {"pass", "fail", "unknown"}
VALID_CHECKLIST_STATES = {"yes", "no", "unknown"}

DASHBOARD_JSON_RE = re.compile(
    r'<script\s+id=["\']dashboard-data["\']\s+type=["\']application/json["\']\s*>(.*?)</script>',
    re.DOTALL,
)

STATE_LOCK = threading.Lock()
LOG_LOCK = threading.Lock()
START_MONOTONIC = time.monotonic()


def utc_now_str() -> str:
    """Return current UTC timestamp formatted consistently with measure.py."""
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")


def log_line(message: str) -> None:
    """Append a timestamped log line to server.log and stdout."""
    line = f"[{utc_now_str()}] {message}"
    with LOG_LOCK:
        try:
            with LOG_PATH.open("a", encoding="utf-8") as fh:
                fh.write(line + "\n")
        except OSError:
            pass
        print(line, flush=True)


def norm_rel(path_str: str) -> str:
    """Normalize relative path separators to forward slashes."""
    cleaned = path_str.strip().replace("\\", "/")
    while cleaned.startswith("./"):
        cleaned = cleaned[2:]
    return cleaned.lstrip("/")


def extract_embedded_data(index_path: Path) -> dict[str, object]:
    """Extract and parse the inline JSON payload from index.html."""
    html_text = index_path.read_text(encoding="utf-8")
    match = DASHBOARD_JSON_RE.search(html_text)
    if not match:
        raise RuntimeError("index.html is missing #dashboard-data JSON payload")
    return json.loads(match.group(1))


def load_events() -> list[dict[str, object]]:
    """Load persisted work-completed events from events.json."""
    if not EVENTS_PATH.is_file():
        return []
    raw = json.loads(EVENTS_PATH.read_text(encoding="utf-8"))
    if not isinstance(raw, list):
        return []
    return [item for item in raw if isinstance(item, dict)]


def save_events(events: list[dict[str, object]]) -> None:
    """Persist up to MAX_EVENTS most recent events to events.json."""
    trimmed = events[-MAX_EVENTS:]
    EVENTS_PATH.write_text(
        json.dumps(trimmed, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def run_measure() -> tuple[int, str, str]:
    """Run measure.py in isolated mode (-I) and return (returncode, stdout, stderr)."""
    proc = subprocess.run(
        [sys.executable, "-I", str(MEASURE_PATH)],
        cwd=str(SCRIPT_DIR),
        capture_output=True,
        text=True,
        timeout=60,
        check=False,
    )
    return proc.returncode, proc.stdout, proc.stderr


def apply_completion_payload(
    config: dict[str, object], body: dict[str, object]
) -> tuple[dict[str, object], list[str], bool]:
    """Validate and apply completion updates to a copy of milestones config."""
    updated = copy.deepcopy(config)
    changes: list[str] = []
    mutated = False

    milestones = updated.get("milestones")
    if not isinstance(milestones, list):
        raise ValueError("milestones.json 'milestones' must be a list")

    milestone_id = body.get("milestone_id")
    milestone_fields_present = any(
        k in body
        for k in ("status", "approved", "approval_note", "manual_percent", "evidence", "append_evidence")
    )
    if milestone_fields_present and not milestone_id:
        raise ValueError("milestone_id is required when updating milestone fields")

    if milestone_id is not None:
        if not isinstance(milestone_id, str) or not milestone_id.strip():
            raise ValueError("milestone_id must be a non-empty string")
        target_m = next(
            (m for m in milestones if isinstance(m, dict) and m.get("id") == milestone_id),
            None,
        )
        if target_m is None:
            raise ValueError(f"Unknown milestone_id: {milestone_id!r}")

        if "status" in body:
            st = body["status"]
            if not isinstance(st, str) or st not in VALID_STATUSES:
                raise ValueError(f"Invalid status {st!r}; expected one of {sorted(VALID_STATUSES)}")
            if target_m.get("status") != st:
                target_m["status"] = st
                changes.append(f"{milestone_id}.status -> {st}")
                mutated = True

        if "approved" in body:
            app = body["approved"]
            if not isinstance(app, bool):
                raise ValueError("'approved' must be a boolean")
            if target_m.get("approved") != app:
                target_m["approved"] = app
                changes.append(f"{milestone_id}.approved -> {app}")
                mutated = True

        if "approval_note" in body:
            note = body["approval_note"]
            if not isinstance(note, str):
                raise ValueError("'approval_note' must be a string")
            if target_m.get("approval_note") != note:
                target_m["approval_note"] = note
                changes.append(f"{milestone_id}.approval_note updated")
                mutated = True

        if "manual_percent" in body:
            pct = body["manual_percent"]
            if isinstance(pct, bool) or not isinstance(pct, (int, float)) or not (0 <= pct <= 100):
                raise ValueError("'manual_percent' must be a number between 0 and 100")
            if target_m.get("manual_percent") != pct:
                target_m["manual_percent"] = pct
                changes.append(f"{milestone_id}.manual_percent -> {pct}")
                mutated = True

        if "evidence" in body:
            ev = body["evidence"]
            if not isinstance(ev, str):
                raise ValueError("'evidence' must be a string")
            append_ev = body.get("append_evidence", False)
            if not isinstance(append_ev, bool):
                raise ValueError("'append_evidence' must be a boolean")
            existing_ev = str(target_m.get("evidence") or "")
            new_ev = f"{existing_ev}; {ev}" if (append_ev and existing_ev) else ev
            if existing_ev != new_ev:
                target_m["evidence"] = new_ev
                changes.append(f"{milestone_id}.evidence updated")
                mutated = True

    if "port_map_add" in body:
        additions = body["port_map_add"]
        if not isinstance(additions, list):
            raise ValueError("'port_map_add' must be a list of {old, new} objects")
        port_map = updated.setdefault("port_map", [])
        if not isinstance(port_map, list):
            raise ValueError("milestones.json 'port_map' must be a list")
        seen = {
            (norm_rel(str(item.get("old", ""))), norm_rel(str(item.get("new", ""))))
            for item in port_map
            if isinstance(item, dict)
        }
        for idx, item in enumerate(additions):
            if not isinstance(item, dict) or not isinstance(item.get("old"), str) or not isinstance(item.get("new"), str):
                raise ValueError(f"port_map_add[{idx}] must have string 'old' and 'new' fields")
            old_rel = norm_rel(item["old"])
            new_rel = norm_rel(item["new"])
            if not old_rel or not new_rel:
                raise ValueError(f"port_map_add[{idx}] paths must be non-empty")
            if (old_rel, new_rel) not in seen:
                seen.add((old_rel, new_rel))
                port_map.append({"old": old_rel, "new": new_rel})
                changes.append(f"port_map += {old_rel} -> {new_rel}")
                mutated = True

    if "resolve_modules" in body:
        mods_req = body["resolve_modules"]
        if not isinstance(mods_req, list):
            raise ValueError("'resolve_modules' must be a list")
        m_with_mods = next(
            (m for m in milestones if isinstance(m, dict) and isinstance(m.get("modules"), list)),
            None,
        )
        if m_with_mods is None:
            raise ValueError("No milestone with 'modules' found in milestones.json")
        mod_by_name = {
            str(mod.get("name")): mod
            for mod in m_with_mods["modules"]
            if isinstance(mod, dict) and mod.get("name")
        }
        for idx, req in enumerate(mods_req):
            if isinstance(req, str):
                name, resolved, note = req, True, None
            elif isinstance(req, dict) and isinstance(req.get("name"), str):
                name = req["name"]
                resolved = req.get("resolved", True)
                if not isinstance(resolved, bool):
                    raise ValueError(f"resolve_modules[{idx}].resolved must be boolean")
                note = req.get("note")
                if note is not None and not isinstance(note, str):
                    raise ValueError(f"resolve_modules[{idx}].note must be string")
            else:
                raise ValueError(f"resolve_modules[{idx}] must be string or object with 'name'")
            if name not in mod_by_name:
                raise ValueError(f"Unknown module in resolve_modules: {name!r}")
            target_mod = mod_by_name[name]
            if target_mod.get("resolved") != resolved:
                target_mod["resolved"] = resolved
                mutated = True
                changes.append(f"module {name}.resolved -> {resolved}")
            if note is not None and target_mod.get("note") != note:
                target_mod["note"] = note
                mutated = True
                changes.append(f"module {name}.note updated")
            elif note is None and resolved and target_mod.get("note") == "Needs owner decision":
                target_mod["note"] = "Resolved"
                mutated = True
        resolved_cnt = sum(1 for mod in m_with_mods["modules"] if isinstance(mod, dict) and mod.get("resolved"))
        total_cnt = len(m_with_mods["modules"])
        ev_str = str(m_with_mods.get("evidence", ""))
        new_ev_str = re.sub(r"^\d+\s+of\s+\d+\s+resolved", f"{resolved_cnt} of {total_cnt} resolved", ev_str)
        if new_ev_str != ev_str:
            m_with_mods["evidence"] = new_ev_str
            mutated = True

    if "checklist_updates" in body:
        chk_req = body["checklist_updates"]
        if not isinstance(chk_req, list):
            raise ValueError("'checklist_updates' must be a list")
        m_with_chk = next(
            (m for m in milestones if isinstance(m, dict) and isinstance(m.get("checklist"), list)),
            None,
        )
        if m_with_chk is None:
            raise ValueError("No milestone with 'checklist' found in milestones.json")
        chk_by_label = {
            str(item.get("label")): item
            for item in m_with_chk["checklist"]
            if isinstance(item, dict) and item.get("label")
        }
        for idx, req in enumerate(chk_req):
            if not isinstance(req, dict) or not isinstance(req.get("label"), str):
                raise ValueError(f"checklist_updates[{idx}] must be object with 'label'")
            lbl = req["label"]
            if lbl not in chk_by_label:
                raise ValueError(f"Unknown checklist item: {lbl!r}")
            target_chk = chk_by_label[lbl]
            if "state" in req:
                st = req["state"]
                if not isinstance(st, str) or st not in VALID_CHECKLIST_STATES:
                    raise ValueError(f"Invalid checklist state {st!r}")
                if target_chk.get("state") != st:
                    target_chk["state"] = st
                    mutated = True
                    changes.append(f"checklist[{lbl}].state -> {st}")
            if "detail" in req:
                det = req["detail"]
                if not isinstance(det, str):
                    raise ValueError(f"checklist_updates[{idx}].detail must be string")
                if target_chk.get("detail") != det:
                    target_chk["detail"] = det
                    mutated = True
                    changes.append(f"checklist[{lbl}].detail updated")

    if "gate_updates" in body:
        gate_req = body["gate_updates"]
        if not isinstance(gate_req, dict):
            raise ValueError("'gate_updates' must be an object keyed by gate id")
        manual_gates = updated.get("manual_gates")
        if not isinstance(manual_gates, dict):
            raise ValueError("milestones.json 'manual_gates' must be an object")
        for gkey, gpatch in gate_req.items():
            if gkey not in manual_gates or not isinstance(manual_gates[gkey], dict):
                raise ValueError(f"Unknown manual gate key: {gkey!r}")
            if not isinstance(gpatch, dict):
                raise ValueError(f"gate_updates[{gkey!r}] must be an object")
            target_g = manual_gates[gkey]
            if "status" in gpatch:
                st = gpatch["status"]
                if not isinstance(st, str) or st not in VALID_GATE_STATUSES:
                    raise ValueError(f"Invalid gate status {st!r}")
                if target_g.get("status") != st:
                    target_g["status"] = st
                    mutated = True
                    changes.append(f"gate[{gkey}].status -> {st}")
            if "value" in gpatch:
                val = gpatch["value"]
                if not isinstance(val, str):
                    raise ValueError(f"gate_updates[{gkey!r}].value must be string")
                if target_g.get("value") != val:
                    target_g["value"] = val
                    mutated = True
                    changes.append(f"gate[{gkey}].value -> {val}")
            if "detail" in gpatch:
                det = gpatch["detail"]
                if not isinstance(det, str):
                    raise ValueError(f"gate_updates[{gkey!r}].detail must be string")
                if target_g.get("detail") != det:
                    target_g["detail"] = det
                    mutated = True
                    changes.append(f"gate[{gkey}].detail updated")

    return updated, changes, mutated


class DashboardServer(ThreadingHTTPServer):
    allow_reuse_address = True
    daemon_threads = True


class DashboardHandler(BaseHTTPRequestHandler):
    server_version = "ArisenDashboardHTTP/1.0"

    def log_message(self, format: str, *args: object) -> None:
        log_line(f"{self.address_string()} - {format % args}")

    def send_json(self, code: int, payload: object) -> None:
        raw = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(raw)

    def send_html(self, code: int, html_bytes: bytes) -> None:
        self.send_response(code)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(html_bytes)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(html_bytes)

    def read_json_body(self) -> tuple[dict[str, object] | None, tuple[int, str] | None]:
        raw_len = self.headers.get("Content-Length")
        if raw_len is None or raw_len == "":
            return {}, None
        try:
            content_len = int(raw_len)
        except ValueError:
            return None, (400, "Invalid Content-Length header")
        if content_len < 0:
            return None, (400, "Negative Content-Length header")
        if content_len > MAX_BODY_BYTES:
            return None, (413, f"Request body exceeds {MAX_BODY_BYTES} bytes")
        if content_len == 0:
            return {}, None
        raw_bytes = self.rfile.read(content_len)
        if not raw_bytes.strip():
            return {}, None
        try:
            parsed = json.loads(raw_bytes.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            return None, (400, f"Invalid JSON body: {exc}")
        if not isinstance(parsed, dict):
            return None, (400, "JSON request body must be an object")
        return parsed, None

    def do_GET(self) -> None:
        path = urlsplit(self.path).path
        if path in ("/", "/index.html"):
            if not INDEX_PATH.is_file():
                self.send_json(503, {"ok": False, "error": "index.html not generated yet"})
                return
            self.send_html(200, INDEX_PATH.read_bytes())
            return

        if path == "/api/health":
            host, port = self.server.server_address[:2]
            self.send_json(
                200,
                {
                    "ok": True,
                    "pid": os.getpid(),
                    "host": host,
                    "port": port,
                    "uptime_seconds": round(time.monotonic() - START_MONOTONIC, 3),
                },
            )
            return

        if path in ("/api/status", "/api/data"):
            with STATE_LOCK:
                if not INDEX_PATH.is_file():
                    self.send_json(503, {"ok": False, "error": "index.html not generated yet"})
                    return
                data = extract_embedded_data(INDEX_PATH)
            self.send_json(200, data)
            return

        if path == "/api/events":
            with STATE_LOCK:
                events = load_events()
            self.send_json(200, {"ok": True, "count": len(events), "events": events})
            return

        self.send_json(404, {"ok": False, "error": f"Unknown endpoint: {path}"})

    def do_POST(self) -> None:
        path = urlsplit(self.path).path
        if path not in ("/api/complete", "/api/work-completed", "/api/refresh"):
            self.send_json(404, {"ok": False, "error": f"Unknown endpoint: {path}"})
            return

        body, err = self.read_json_body()
        if err is not None:
            self.send_json(err[0], {"ok": False, "error": err[1]})
            return
        assert body is not None

        if path == "/api/refresh":
            source = str(body.get("source") or "main-thread")
            summary = body.get("summary") or body.get("message")
            with STATE_LOCK:
                orig_events_text = EVENTS_PATH.read_text(encoding="utf-8") if EVENTS_PATH.is_file() else None
                event = None
                if isinstance(summary, str) and summary.strip():
                    events = load_events()
                    event = {
                        "timestamp_utc": utc_now_str(),
                        "endpoint": path,
                        "source": source,
                        "summary": summary.strip(),
                        "changes": [],
                    }
                    events.append(event)
                    save_events(events)

                rc, out, err_out = run_measure()
                if rc != 0:
                    if event is not None:
                        EVENTS_PATH.write_text(
                            orig_events_text if orig_events_text is not None else "[]\n",
                            encoding="utf-8",
                        )
                    self.send_json(
                        422,
                        {
                            "ok": False,
                            "measure_exit_code": rc,
                            "stdout": out,
                            "stderr": err_out,
                        },
                    )
                    return
                data = extract_embedded_data(INDEX_PATH)
            self.send_json(
                200,
                {
                    "ok": True,
                    "event": event,
                    "measure_exit_code": rc,
                    "measure_stdout": out,
                    "measurements": {
                        "overall": data["measurements"]["overall"],
                        "dimensions": data["measurements"]["dimensions"],
                    },
                },
            )
            return

        # /api/complete or /api/work-completed
        dry_run = body.get("dry_run", False)
        if not isinstance(dry_run, bool):
            self.send_json(400, {"ok": False, "error": "'dry_run' must be a boolean"})
            return

        source = body.get("source", "main-thread")
        if not isinstance(source, str) or not source.strip():
            self.send_json(400, {"ok": False, "error": "'source' must be a non-empty string"})
            return

        raw_summary = body.get("summary") if "summary" in body else body.get("message")
        if raw_summary is not None and not isinstance(raw_summary, str):
            self.send_json(400, {"ok": False, "error": "'summary'/'message' must be a string"})
            return
        summary = raw_summary.strip() if isinstance(raw_summary, str) and raw_summary.strip() else "Work completed notification"

        with STATE_LOCK:
            orig_milestones_text = MILESTONES_PATH.read_text(encoding="utf-8")
            config = json.loads(orig_milestones_text)
            try:
                updated_config, changes, mutated = apply_completion_payload(config, body)
            except ValueError as exc:
                self.send_json(400, {"ok": False, "error": str(exc)})
                return

            if dry_run:
                self.send_json(
                    200,
                    {
                        "ok": True,
                        "dry_run": True,
                        "source": source.strip(),
                        "summary": summary,
                        "changes": changes,
                        "would_mutate_milestones": mutated,
                    },
                )
                return

            orig_events_text = EVENTS_PATH.read_text(encoding="utf-8") if EVENTS_PATH.is_file() else None
            if mutated:
                MILESTONES_PATH.write_text(
                    json.dumps(updated_config, ensure_ascii=False, indent=2) + "\n",
                    encoding="utf-8",
                )

            events = load_events()
            event = {
                "timestamp_utc": utc_now_str(),
                "endpoint": path,
                "source": source.strip(),
                "summary": summary,
                "changes": changes,
            }
            events.append(event)
            save_events(events)

            rc, out, err_out = run_measure()
            if rc != 0:
                if mutated:
                    MILESTONES_PATH.write_text(orig_milestones_text, encoding="utf-8")
                EVENTS_PATH.write_text(
                    orig_events_text if orig_events_text is not None else "[]\n",
                    encoding="utf-8",
                )
                self.send_json(
                    422,
                    {
                        "ok": False,
                        "measure_exit_code": rc,
                        "stdout": out,
                        "stderr": err_out,
                        "rolled_back": True,
                    },
                )
                return

            data = extract_embedded_data(INDEX_PATH)

        log_line(f"COMPLETION source={source.strip()!r} summary={summary!r} changes={len(changes)}")
        self.send_json(
            200,
            {
                "ok": True,
                "dry_run": False,
                "event": event,
                "changes": changes,
                "measure_exit_code": rc,
                "measure_stdout": out,
                "measurements": {
                    "overall": data["measurements"]["overall"],
                    "dimensions": data["measurements"]["dimensions"],
                },
            },
        )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Serve Arisen Studio dashboard and completion API.")
    parser.add_argument("--host", default="127.0.0.1", help="Bind host (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=8787, help="Bind port (default: 8787)")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not INDEX_PATH.is_file():
        rc, out, err_out = run_measure()
        if rc != 0:
            print(err_out or out, file=sys.stderr)
            return rc

    server = DashboardServer((args.host, args.port), DashboardHandler)
    PID_PATH.write_text(f"{os.getpid()}\n", encoding="utf-8")
    log_line(f"Dashboard server listening on http://{args.host}:{args.port}/ (pid={os.getpid()})")

    def shutdown_handler(signum: int, _frame: object) -> None:
        log_line(f"Received signal {signum}, shutting down.")
        threading.Thread(target=server.shutdown, daemon=True).start()

    signal.signal(signal.SIGTERM, shutdown_handler)
    signal.signal(signal.SIGINT, shutdown_handler)

    try:
        server.serve_forever()
    finally:
        server.server_close()
        log_line("Dashboard server stopped.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
