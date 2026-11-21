#!/usr/bin/env python3
"""Measure Arisen Studio -> Mono porting progress and generate static index.html."""

from __future__ import annotations

import datetime as dt
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

MSBUILD_NS = "http://schemas.microsoft.com/developer/msbuild/2003"

MAINWINDOW_REL = "Forms/Windows/MainWindow.Designer.cs"
REPORT_ISSUE_REL = "Forms/Dialogs/ReportIssueDialog.Designer.cs"

EXPECTED_COUNTS = {
    "old_compile_items": 165,
    "layout_designers": 54,
    "layout_designer_lines": 33505,
    "ui_free_files": 42,
    "ui_coupled_files": 66,
    "linked_ui_free_files": 30,  # 24 at milestone 1 + 6 old-tree links registered in the data-layer batch
}

WINDOWS_PATH_RE = re.compile(r"(?:[A-Za-z]:\\|\\\\|[^\s\\\"']\\[^\s\\\"'])")
SEGOE_UI_RE = re.compile(r"segoe\s+ui", re.IGNORECASE)


def norm_rel(path_str: str) -> str:
    """Normalize MSBuild or JSON relative paths to forward-slash relative form."""
    cleaned = path_str.strip().replace("\\", "/")
    while cleaned.startswith("./"):
        cleaned = cleaned[2:]
    return cleaned


def count_lines(path: Path) -> int:
    """Count newline characters (exact `wc -l` semantics)."""
    return path.read_bytes().count(b"\n")


def calc_pct(numerator: int, denominator: int) -> dict[str, object]:
    """Return floor-truncated 1-decimal percentage dict (never rounded up)."""
    if denominator <= 0 or numerator <= 0:
        tenths = 0
    else:
        tenths = min(1000, (numerator * 1000) // denominator)
    return {
        "numerator": numerator,
        "denominator": denominator,
        "percent": tenths / 10.0,
        "percent_str": f"{tenths // 10}.{tenths % 10}%",
    }


def parse_compile_items(csproj_path: Path) -> list[tuple[str, str | None]]:
    """Extract (Include, optional Link) from MSBuild <Compile> elements."""
    tree = ET.parse(csproj_path)
    root = tree.getroot()
    items: list[tuple[str, str | None]] = []
    for elem in root.iter():
        if elem.tag == f"{{{MSBUILD_NS}}}Compile" or elem.tag == "Compile" or elem.tag.endswith("}Compile"):
            inc = elem.attrib.get("Include")
            if not inc:
                continue
            link_text: str | None = None
            for child in elem:
                if child.tag == f"{{{MSBUILD_NS}}}Link" or child.tag == "Link" or child.tag.endswith("}Link"):
                    link_text = child.text.strip() if child.text else ""
                    break
            items.append((norm_rel(inc), norm_rel(link_text) if link_text is not None else None))
    return items


def unescape_csharp_regular_string(raw_body: str) -> str:
    """Decode C# regular string escapes so `\"` is not mistaken for a path backslash."""
    out: list[str] = []
    i = 0
    n = len(raw_body)
    simple_escapes = {
        "\\": "\\",
        '"': '"',
        "'": "'",
        "0": "\0",
        "a": "\a",
        "b": "\b",
        "f": "\f",
        "n": "\n",
        "r": "\r",
        "t": "\t",
        "v": "\v",
    }
    while i < n:
        ch = raw_body[i]
        if ch != "\\" or i + 1 >= n:
            out.append(ch)
            i += 1
            continue
        nxt = raw_body[i + 1]
        if nxt in simple_escapes:
            out.append(simple_escapes[nxt])
            i += 2
        elif nxt == "x":
            m = re.match(r"x([0-9A-Fa-f]{1,4})", raw_body[i + 1 :])
            if m:
                out.append(chr(int(m.group(1), 16)))
                i += 1 + len(m.group(0))
            else:
                out.append(nxt)
                i += 2
        elif nxt == "u" and i + 6 <= n and re.fullmatch(r"[0-9A-Fa-f]{4}", raw_body[i + 2 : i + 6]):
            out.append(chr(int(raw_body[i + 2 : i + 6], 16)))
            i += 6
        elif nxt == "U" and i + 10 <= n and re.fullmatch(r"[0-9A-Fa-f]{8}", raw_body[i + 2 : i + 10]):
            out.append(chr(int(raw_body[i + 2 : i + 10], 16)))
            i += 10
        else:
            out.append(nxt)
            i += 2
    return "".join(out)


def scan_csharp_file_for_windows_artifacts(rel_path: str, text: str) -> list[str]:
    """Scan non-comment C# code and string literals for Windows backslash paths or Segoe UI."""
    findings: list[str] = []
    non_comment_chunks: list[str] = []
    n = len(text)
    i = 0
    line = 1

    while i < n:
        ch = text[i]
        if ch == "\n":
            non_comment_chunks.append(ch)
            line += 1
            i += 1
            continue

        # Line comment
        if ch == "/" and i + 1 < n and text[i + 1] == "/":
            i += 2
            while i < n and text[i] != "\n":
                i += 1
            continue

        # Block comment
        if ch == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                if text[i] == "\n":
                    line += 1
                    non_comment_chunks.append("\n")
                i += 1
            i = min(n, i + 2)
            continue

        # Verbatim string @"..." or $@"..." or @$"..."
        if text.startswith('@"', i) or text.startswith('$@"', i) or text.startswith('@$"', i):
            prefix_len = 2 if text.startswith('@"', i) else 3
            start_line = line
            i += prefix_len
            body: list[str] = []
            while i < n:
                c = text[i]
                if c == '"':
                    if i + 1 < n and text[i + 1] == '"':
                        body.append('"')
                        i += 2
                        continue
                    i += 1
                    break
                if c == "\n":
                    line += 1
                body.append(c)
                i += 1
            decoded = "".join(body)
            non_comment_chunks.append(decoded)
            if WINDOWS_PATH_RE.search(decoded):
                findings.append(f"{rel_path}:{start_line} verbatim string backslash path")
            continue

        # Regular or interpolated string "..." / $"..."
        if ch == '"' or text.startswith('$"', i):
            start_line = line
            i += 2 if text.startswith('$"', i) else 1
            raw_body: list[str] = []
            while i < n:
                c = text[i]
                if c == "\\":
                    if i + 1 < n:
                        if text[i + 1] == "\n":
                            line += 1
                        raw_body.append(c)
                        raw_body.append(text[i + 1])
                        i += 2
                        continue
                if c == '"':
                    i += 1
                    break
                if c == "\n":
                    line += 1
                raw_body.append(c)
                i += 1
            decoded = unescape_csharp_regular_string("".join(raw_body))
            non_comment_chunks.append(decoded)
            if WINDOWS_PATH_RE.search(decoded):
                findings.append(f"{rel_path}:{start_line} string literal backslash path")
            continue

        # Char literal '...'
        if ch == "'":
            i += 1
            while i < n:
                c = text[i]
                if c == "\\":
                    i += 2
                    continue
                if c == "'":
                    i += 1
                    break
                if c == "\n":
                    line += 1
                i += 1
            continue

        non_comment_chunks.append(ch)
        i += 1

    code_no_comments = "".join(non_comment_chunks)
    for idx, code_line in enumerate(code_no_comments.splitlines(), start=1):
        if SEGOE_UI_RE.search(code_line):
            findings.append(f"{rel_path}:{idx} contains 'Segoe UI'")

    return findings


def resolve_port_map(
    raw_port_map: object,
    new_dir: Path,
    ui_coupled: dict[str, int],
    layout_designers: dict[str, int],
) -> tuple[set[str], set[str], list[dict[str, object]]]:
    """Validate explicit port_map entries against existing new files and exact old paths."""
    if not isinstance(raw_port_map, list):
        raise ValueError("milestones.json 'port_map' must be a list")

    new_root = new_dir.resolve()
    ported_b: set[str] = set()
    ported_c: set[str] = set()
    records: list[dict[str, object]] = []

    for idx, entry in enumerate(raw_port_map):
        if not isinstance(entry, dict) or "old" not in entry or "new" not in entry:
            raise ValueError(f"port_map[{idx}] must be an object with 'old' and 'new'")
        old_rel = norm_rel(str(entry["old"])).lstrip("/")
        new_rel = norm_rel(str(entry["new"])).lstrip("/")
        candidate = (new_dir / new_rel).resolve()
        new_exists = candidate.is_relative_to(new_root) and candidate.is_file()

        category = None
        if new_exists:
            if old_rel in ui_coupled:
                ported_b.add(old_rel)
                category = "B"
            elif old_rel in layout_designers:
                ported_c.add(old_rel)
                category = "C"

        records.append(
            {
                "old": old_rel,
                "new": new_rel,
                "new_exists": new_exists,
                "counted_dimension": category,
            }
        )

    return ported_b, ported_c, records


def render_html(payload: dict[str, object]) -> str:
    """Build self-contained index.html from template with JSON payload embedded."""
    raw_json = json.dumps(payload, ensure_ascii=False, indent=2)
    safe_json = (
        raw_json.replace("</", "<\\/")
        .replace("\u2028", "\\u2028")
        .replace("\u2029", "\\u2029")
    )

    template = """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta http-equiv="refresh" content="60">
  <title>Arisen Studio Linux/Mono Migration Dashboard</title>
  <style>
    :root {
      --bg: #f6f7f9;
      --surface: #ffffff;
      --surface-alt: #f0f2f6;
      --border: #d8dee6;
      --text: #161b22;
      --muted: #57606a;
      --accent: #0969da;
      --accent-soft: #ddf4ff;
      --accent-files: #8250df;
      --ok: #1a7f37;
      --ok-bg: #dafbe1;
      --warn: #9a6700;
      --warn-bg: #fff8c5;
      --fail: #cf222e;
      --fail-bg: #ffebe9;
      --neutral-bg: #eaeef2;
      --bar-track: #e1e6ed;
      --radius: 10px;
      --font-sans: system-ui, -apple-system, "Segoe UI", Roboto, Ubuntu, Cantarell, sans-serif;
      --font-mono: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace;
    }

    @media (prefers-color-scheme: dark) {
      :root {
        --bg: #0d1117;
        --surface: #161b22;
        --surface-alt: #1f2630;
        --border: #30363d;
        --text: #e6edf3;
        --muted: #9198a1;
        --accent: #58a6ff;
        --accent-soft: #132c47;
        --accent-files: #a371f7;
        --ok: #3fb950;
        --ok-bg: #132e1e;
        --warn: #d29922;
        --warn-bg: #2e2208;
        --fail: #f85149;
        --fail-bg: #3b1719;
        --neutral-bg: #262c36;
        --bar-track: #262c36;
      }
    }

    *, *::before, *::after {
      box-sizing: border-box;
    }

    body {
      margin: 0;
      padding: 16px 12px 32px;
      background: var(--bg);
      color: var(--text);
      font-family: var(--font-sans);
      font-size: 14px;
      line-height: 1.5;
    }

    .container {
      max-width: 960px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .card {
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 16px;
      min-width: 0;
    }

    .notice-banner {
      background: var(--warn-bg);
      border: 2px solid var(--warn);
      border-radius: var(--radius);
      padding: 14px 16px;
      color: var(--text);
    }

    .notice-banner strong {
      display: block;
      margin-bottom: 4px;
      color: var(--warn);
      font-size: 13px;
      letter-spacing: 0.02em;
      text-transform: uppercase;
    }

    h1 {
      margin: 0 0 6px;
      font-size: 20px;
      line-height: 1.25;
    }

    h2 {
      margin: 0 0 10px;
      font-size: 16px;
      line-height: 1.3;
    }

    p {
      margin: 0 0 8px;
    }

    p:last-child {
      margin-bottom: 0;
    }

    .muted {
      color: var(--muted);
    }

    .mono {
      font-family: var(--font-mono);
      font-size: 12px;
      overflow-wrap: anywhere;
      word-break: break-word;
    }

    .meta-row {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 14px;
      margin-top: 10px;
      font-size: 12px;
      color: var(--muted);
    }

    .hero-top {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      justify-content: space-between;
      gap: 8px 16px;
      margin-bottom: 8px;
    }

    .hero-pct-wrap {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 2px;
    }

    .hero-pct {
      font-size: clamp(22px, 5vw, 32px);
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      line-height: 1.1;
      white-space: nowrap;
    }

    .hero-pct-sub {
      font-size: 11px;
      font-weight: 600;
      color: var(--muted);
      letter-spacing: 0.02em;
    }

    .hero-bars {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .hero-bar-row {
      display: grid;
      grid-template-columns: 42px 1fr;
      align-items: center;
      gap: 8px;
    }

    .hero-bar-label {
      font-size: 11px;
      font-weight: 600;
      color: var(--muted);
    }

    .bar-track {
      width: 100%;
      height: 10px;
      background: var(--bar-track);
      border-radius: 999px;
      overflow: hidden;
    }

    .bar-fill {
      height: 100%;
      background: var(--accent);
      border-radius: 999px;
      transition: width 0.2s ease;
    }

    .bar-fill.done {
      background: var(--ok);
    }

    .bar-fill.files {
      background: var(--accent-files);
    }

    .badge-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }

    .badge {
      display: inline-flex;
      align-items: center;
      padding: 2px 8px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 600;
      line-height: 1.4;
      white-space: nowrap;
      border: 1px solid transparent;
    }

    .badge-done, .badge-pass {
      background: var(--ok-bg);
      color: var(--ok);
    }

    .badge-proposed, .badge-unknown {
      background: var(--warn-bg);
      color: var(--warn);
    }

    .badge-planned {
      background: var(--neutral-bg);
      color: var(--muted);
    }

    .badge-fail {
      background: var(--fail-bg);
      color: var(--fail);
    }

    .badge-computed {
      background: var(--accent-soft);
      color: var(--accent);
    }

    .badge-manual {
      background: var(--surface-alt);
      color: var(--muted);
      border-color: var(--border);
    }

    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 12px;
    }

    .dim-card {
      background: var(--surface-alt);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 12px;
      display: flex;
      flex-direction: column;
      gap: 6px;
      min-width: 0;
    }

    .dim-header {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: 8px;
    }

    .dim-pct {
      font-size: 18px;
      font-weight: 700;
      font-variant-numeric: tabular-nums;
    }

    .table-wrap {
      width: 100%;
      overflow-x: auto;
      margin-top: 12px;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 12.5px;
    }

    th, td {
      text-align: left;
      padding: 8px 8px;
      border-bottom: 1px solid var(--border);
      vertical-align: top;
    }

    th {
      color: var(--muted);
      font-weight: 600;
      font-size: 11.5px;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }

    td.num, th.num {
      text-align: right;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    .milestone-list, .gate-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .milestone-item, .gate-item {
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 12px;
      background: var(--surface-alt);
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: 0;
    }

    .item-top {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      align-items: flex-start;
      gap: 8px;
    }

    .item-title {
      font-weight: 600;
      min-width: 0;
      flex: 1 1 220px;
      overflow-wrap: anywhere;
    }

    .item-pct {
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    .sublist {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
      gap: 6px;
      margin-top: 2px;
    }

    .subpill {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 8px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 6px 8px;
      font-size: 12px;
      min-width: 0;
    }

    details {
      margin-top: 12px;
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 8px 12px;
      background: var(--surface-alt);
    }

    summary {
      cursor: pointer;
      font-weight: 600;
      font-size: 13px;
    }

    .file-columns {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 12px;
      margin-top: 10px;
    }

    .file-list {
      margin: 6px 0 0;
      padding-left: 18px;
      font-family: var(--font-mono);
      font-size: 11.5px;
      overflow-wrap: anywhere;
    }

    footer {
      text-align: center;
      font-size: 12px;
      color: var(--muted);
      padding-top: 4px;
    }

    @media (max-width: 600px) {
      body {
        padding: 10px 8px 24px;
      }
      .card {
        padding: 12px;
      }
      .hero-pct {
        font-size: 26px;
      }
    }
  </style>
</head>
<body>
  <main class="container" id="app">
    <noscript>
      <section class="card">
        <h1>JavaScript Required</h1>
        <p>Enable JavaScript to render the dashboard data stored inside this file.</p>
      </section>
    </noscript>
  </main>

  <script id="dashboard-data" type="application/json">__JSON_DATA__</script>
  <script>
    (function () {
      "use strict";

      function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = String(text);
        return node;
      }

      function fmtNum(n) {
        return Number(n).toLocaleString("en-US");
      }

      function badge(text, cls) {
        return el("span", "badge " + cls, text);
      }

      function progressBar(percent, isDone, extraClass) {
        var track = el("div", "bar-track");
        var cls = "bar-fill" + (isDone ? " done" : (extraClass ? " " + extraClass : ""));
        var fill = el("div", cls);
        var clamped = Math.max(0, Math.min(100, Number(percent) || 0));
        fill.style.width = clamped + "%";
        track.appendChild(fill);
        return track;
      }

      function sourceBadge(source) {
        return source === "computed"
          ? badge("Computed", "badge-computed")
          : badge("Manual", "badge-manual");
      }

      function statusBadge(status) {
        if (status === "done") return badge("done", "badge-done");
        if (status === "proposed") return badge("proposed", "badge-proposed");
        return badge(status || "planned", "badge-planned");
      }

      function gateBadge(status, value) {
        if (status === "pass") return badge("PASS (" + value + ")", "badge-pass");
        if (status === "fail") return badge("FAIL (" + value + ")", "badge-fail");
        return badge("UNKNOWN (" + value + ")", "badge-unknown");
      }

      var dataEl = document.getElementById("dashboard-data");
      var app = document.getElementById("app");
      if (!dataEl || !app) return;

      var data = JSON.parse(dataEl.textContent);
      app.textContent = "";

      // 1. Header + Definition
      var headerCard = el("section", "card");
      var at = data.active_threads || {};
      var atText;
      if (typeof at.count === "number") {
        atText = "Active threads: " + at.count + " (as of " + at.as_of + (at.stale ? ", STALE: counter not updating" : "") + ")";
      } else {
        atText = "Active threads: - (no count available)";
      }
      var atEl = el("p", at.stale ? "muted" : null, atText);
      atEl.style.fontWeight = "700";
      atEl.style.fontSize = "1.1em";
      headerCard.appendChild(atEl);
      headerCard.appendChild(el("h1", null, data.project));
      headerCard.appendChild(
        el("p", "muted", "Target: Windows DevExpress WinForms rewritten for Linux on Mono using only stock System.Windows.Forms (no DevExpress).")
      );
      var defP = el("p", null);
      var defStrong = el("strong", null, "Full completion definition: ");
      defP.appendChild(defStrong);
      defP.appendChild(document.createTextNode(data.completion_definition));
      headerCard.appendChild(defP);
      var meta = el("div", "meta-row");
      meta.appendChild(el("span", null, "Generated (UTC): " + data.generated_at_utc));
      meta.appendChild(el("span", null, "Percentages: 1 decimal place, floor-truncated (never rounded up)"));
      headerCard.appendChild(meta);
      app.appendChild(headerCard);

      // 2. Proposal disclaimer banner
      var notice = el("section", "notice-banner");
      notice.setAttribute("role", "alert");
      notice.appendChild(el("strong", null, "Unapproved Proposal Notice"));
      notice.appendChild(el("p", null, data.proposal_notice));
      app.appendChild(notice);

      // 3. Overall Codebase Progress Hero
      var ov = data.measurements.overall;
      var heroCard = el("section", "card");
      var heroTop = el("div", "hero-top");
      var heroLeft = el("div", "badge-row");
      heroLeft.appendChild(el("h2", null, "Overall Codebase Progress"));
      heroLeft.appendChild(sourceBadge("computed"));
      heroTop.appendChild(heroLeft);
      var heroPctWrap = el("div", "hero-pct-wrap");
      var heroPctText = ov.files_percent_str ? ov.percent_str + " / " + ov.files_percent_str : ov.percent_str;
      heroPctWrap.appendChild(el("div", "hero-pct", heroPctText));
      if (ov.files_percent_str) {
        heroPctWrap.appendChild(el("div", "hero-pct-sub", "LOC / Files"));
      }
      heroTop.appendChild(heroPctWrap);
      heroCard.appendChild(heroTop);
      var heroBars = el("div", "hero-bars");
      var locRow = el("div", "hero-bar-row");
      locRow.appendChild(el("span", "hero-bar-label", "LOC"));
      locRow.appendChild(progressBar(ov.percent, ov.percent >= 100));
      heroBars.appendChild(locRow);
      var filesPct = typeof ov.files_percent === "number" ? ov.files_percent : (ov.files_total > 0 ? (ov.files_ported * 100) / ov.files_total : 0);
      var filesRow = el("div", "hero-bar-row");
      filesRow.appendChild(el("span", "hero-bar-label", "Files"));
      filesRow.appendChild(progressBar(filesPct, filesPct >= 100, "files"));
      heroBars.appendChild(filesRow);
      heroCard.appendChild(heroBars);
      var heroDetails = el("div", "meta-row");
      heroDetails.appendChild(el("span", null, "Lines (LOC): " + fmtNum(ov.numerator) + " / " + fmtNum(ov.denominator) + " (" + ov.percent_str + ")"));
      heroDetails.appendChild(el("span", null, "Files: " + fmtNum(ov.files_ported) + " / " + fmtNum(ov.files_total) + (ov.files_percent_str ? " (" + ov.files_percent_str + ")" : "")));
      heroDetails.appendChild(el("span", "mono", "Formula: " + ov.formula));
      heroCard.appendChild(heroDetails);
      app.appendChild(heroCard);

      // 4. Measured Codebase Progress (Dimensions A, B, C, Overall + Excluded Properties)
      var dimSection = el("section", "card");
      var dimHead = el("div", "hero-top");
      var dimTitleRow = el("div", "badge-row");
      dimTitleRow.appendChild(el("h2", null, "Measured Codebase Dimensions"));
      dimTitleRow.appendChild(sourceBadge("computed"));
      dimHead.appendChild(dimTitleRow);
      dimHead.appendChild(el("span", "muted", "Line count = newline characters (wc -l)"));
      dimSection.appendChild(dimHead);

      var dimGrid = el("div", "grid");
      var dims = data.measurements.dimensions;
      dims.forEach(function (d) {
        var box = el("div", "dim-card");
        var dh = el("div", "dim-header");
        dh.appendChild(el("strong", null, d.label));
        dh.appendChild(el("span", "dim-pct", d.percent_str));
        box.appendChild(dh);
        box.appendChild(progressBar(d.percent, d.percent >= 100));
        box.appendChild(el("div", "muted", "Lines: " + fmtNum(d.numerator) + " / " + fmtNum(d.denominator)));
        box.appendChild(el("div", "muted", "Files: " + fmtNum(d.files_ported) + " / " + fmtNum(d.files_total)));
        box.appendChild(el("div", "mono muted", d.formula));
        dimGrid.appendChild(box);
      });
      dimSection.appendChild(dimGrid);

      // Visible formula & numerator/denominator summary table
      var tableWrap = el("div", "table-wrap");
      var tbl = el("table");
      var thead = el("thead");
      var hr = el("tr");
      ["Category / Dimension", "Files", "Ported / Linked Lines", "Total Lines", "%", "Formula"].forEach(function (h, i) {
        var th = el("th", i >= 1 && i <= 4 ? "num" : null, h);
        hr.appendChild(th);
      });
      thead.appendChild(hr);
      tbl.appendChild(thead);

      var tbody = el("tbody");
      dims.concat([ov]).forEach(function (row) {
        var tr = el("tr");
        tr.appendChild(el("td", null, row.label));
        tr.appendChild(el("td", "num", fmtNum(row.files_ported) + " / " + fmtNum(row.files_total)));
        tr.appendChild(el("td", "num", fmtNum(row.numerator)));
        tr.appendChild(el("td", "num", fmtNum(row.denominator)));
        tr.appendChild(el("td", "num", row.percent_str));
        tr.appendChild(el("td", "mono", row.formula));
        tbody.appendChild(tr);
      });

      var excl = data.measurements.excluded_properties;
      var exclTr = el("tr");
      exclTr.appendChild(el("td", null, "Excluded: Properties/*.cs (generated/metadata)"));
      exclTr.appendChild(el("td", "num", fmtNum(excl.file_count) + " (excluded)"));
      exclTr.appendChild(el("td", "num", "—"));
      exclTr.appendChild(el("td", "num", fmtNum(excl.total_lines)));
      exclTr.appendChild(el("td", "num", "excluded"));
      exclTr.appendChild(el("td", "mono", "Excluded from every denominator"));
      tbody.appendChild(exclTr);

      tbl.appendChild(tbody);
      tableWrap.appendChild(tbl);
      dimSection.appendChild(tableWrap);

      var portNote = el("p", "muted", "port_map entries active: " + fmtNum(data.measurements.port_map_summary.active_count) + " (of " + fmtNum(data.measurements.port_map_summary.total_entries) + " entries). Dimensions B and C count only explicit {old, new} relative-path entries whose new file exists; basename matching is never used.");
      portNote.style.marginTop = "10px";
      dimSection.appendChild(portNote);

      // Collapsible file breakdown
      var details = el("details");
      details.appendChild(el("summary", null, "Inspect file lists: " + fmtNum(data.measurements.linked_ui_free_files.length) + " linked UI-free, " + fmtNum(data.measurements.unlinked_ui_free_files.length) + " unlinked UI-free, " + fmtNum(excl.files.length) + " excluded Properties/*.cs"));
      var cols = el("div", "file-columns");

      function makeList(title, items) {
        var wrap = el("div");
        wrap.appendChild(el("strong", null, title));
        var ul = el("ul", "file-list");
        items.forEach(function (it) {
          ul.appendChild(el("li", null, it.path + " (" + fmtNum(it.lines) + " lines)"));
        });
        wrap.appendChild(ul);
        return wrap;
      }

      cols.appendChild(makeList("Linked UI-free files (" + fmtNum(data.measurements.linked_ui_free_files.length) + ")", data.measurements.linked_ui_free_files));
      cols.appendChild(makeList("Remaining UI-free files (" + fmtNum(data.measurements.unlinked_ui_free_files.length) + ")", data.measurements.unlinked_ui_free_files));
      cols.appendChild(makeList("Excluded Properties/*.cs (" + fmtNum(excl.files.length) + ")", excl.files));
      details.appendChild(cols);
      dimSection.appendChild(details);
      app.appendChild(dimSection);

      // 5. Milestones
      var msCard = el("section", "card");
      msCard.appendChild(el("h2", null, "Milestones (M0 – M8)"));
      msCard.appendChild(el("p", "muted", "M0–M1b are approved and done (100.0%). M2–M8 are unapproved proposals: M4 (other layouts), M5 (MainWindow alone), and M6 (code-behind) are measured from the repo without double counting; planned/proposed unmeasured milestones stay at 0.0%."));
      var msList = el("div", "milestone-list");
      msList.style.marginTop = "10px";

      data.milestones.forEach(function (m) {
        var item = el("div", "milestone-item");
        var top = el("div", "item-top");

        var left = el("div", "item-title");
        left.appendChild(document.createTextNode(m.id + " — " + m.title));
        top.appendChild(left);

        var right = el("div", "badge-row");
        right.appendChild(statusBadge(m.status));
        if (!m.approved && m.approval_note) {
          right.appendChild(badge(m.approval_note, m.status === "proposed" ? "badge-proposed" : "badge-planned"));
        }
        right.appendChild(sourceBadge(m.progress_source));
        right.appendChild(el("span", "item-pct", m.percent_str));
        top.appendChild(right);
        item.appendChild(top);

        item.appendChild(progressBar(m.percent, m.status === "done" || m.percent >= 100));

        if (m.measurement) {
          var mMeta = el("div", "meta-row");
          mMeta.appendChild(el("span", null, "Lines: " + fmtNum(m.measurement.numerator) + " / " + fmtNum(m.measurement.denominator)));
          if (m.measurement.files_total !== undefined) {
            mMeta.appendChild(el("span", null, "Files: " + fmtNum(m.measurement.files_ported) + " / " + fmtNum(m.measurement.files_total)));
          }
          if (m.measurement.layout_share_percent_str) {
            mMeta.appendChild(el("span", null, "Share of all layout lines: " + m.measurement.layout_share_percent_str));
          }
          mMeta.appendChild(el("span", "mono", "Formula: " + m.measurement.formula));
          item.appendChild(mMeta);
        }

        if (m.modules && m.modules.length) {
          var modSummary = el("div", "muted", "Resolved: " + fmtNum(m.resolved_count) + " of " + fmtNum(m.total_count));
          item.appendChild(modSummary);
          var modGrid = el("div", "sublist");
          m.modules.forEach(function (mod) {
            var pill = el("div", "subpill");
            pill.appendChild(el("span", null, mod.name));
            pill.appendChild(badge(mod.resolved ? "resolved" : mod.note, mod.resolved ? "badge-done" : "badge-planned"));
            modGrid.appendChild(pill);
          });
          item.appendChild(modGrid);
        }

        if (m.checklist && m.checklist.length) {
          var chkGrid = el("div", "sublist");
          m.checklist.forEach(function (c) {
            var pill = el("div", "subpill");
            pill.appendChild(el("span", null, c.label));
            var cls = c.state === "yes" ? "badge-pass" : (c.state === "no" ? "badge-fail" : "badge-unknown");
            pill.appendChild(badge(c.state.toUpperCase(), cls));
            pill.title = c.detail || "";
            chkGrid.appendChild(pill);
          });
          item.appendChild(chkGrid);
        }

        item.appendChild(el("p", "muted", "Evidence: " + m.evidence));
        msList.appendChild(item);
      });

      msCard.appendChild(msList);
      app.appendChild(msCard);

      // 6. Extensibility Gates
      var gateCard = el("section", "card");
      gateCard.appendChild(el("h2", null, "Extensibility Gates (Separate Pass/Fail Checks — Not Included in %)"));
      gateCard.appendChild(el("p", "muted", "These gates track architectural readiness and packaging constraints separately from codebase line percentages."));
      var gateList = el("div", "gate-list");
      gateList.style.marginTop = "10px";

      data.extensibility_gates.forEach(function (g) {
        var gi = el("div", "gate-item");
        var top = el("div", "item-top");
        top.appendChild(el("div", "item-title", g.label));
        var badges = el("div", "badge-row");
        badges.appendChild(sourceBadge(g.source));
        badges.appendChild(gateBadge(g.status, g.value));
        top.appendChild(badges);
        gi.appendChild(top);
        gi.appendChild(el("p", "muted", g.detail));
        gateList.appendChild(gi);
      });

      gateCard.appendChild(gateList);
      app.appendChild(gateCard);

      if (data.recent_events && data.recent_events.length > 0) {
        var evCard = el("section", "card");
        evCard.appendChild(el("h2", null, "Recent Work-Completed Notifications"));
        var evList = el("div", "gate-list");
        data.recent_events.forEach(function (ev) {
          var row = el("div", "gate-item");
          var top = el("div", "item-top");
          top.appendChild(el("div", "item-title", ev.summary || "Work completed"));
          var br = el("div", "badge-row");
          br.appendChild(badge(ev.source || "main-thread", "badge-manual"));
          br.appendChild(el("span", "muted mono", ev.timestamp_utc || ""));
          top.appendChild(br);
          row.appendChild(top);
          if (ev.changes && ev.changes.length) {
            row.appendChild(el("div", "muted mono", "Changes: " + ev.changes.join("; ")));
          }
          evList.appendChild(row);
        });
        evCard.appendChild(evList);
        app.appendChild(evCard);
      }

      var footer = el("footer", null, "Generated by measure.py from milestones.json and the local repo checkout. Do not hand-edit index.html.");
      app.appendChild(footer);
    })();
  </script>
</body>
</html>
"""
    return template.replace("__JSON_DATA__", safe_json)


def main() -> int:
    script_dir = Path(os.path.abspath(__file__)).parent
    milestones_path = script_dir / "milestones.json"
    output_html_path = script_dir / "index.html"

    candidate_repo = script_dir.parent
    if (candidate_repo / "src" / "Arisen Studio" / "Arisen Studio.csproj").is_file():
        repo_dir = candidate_repo
    else:
        repo_dir = Path("~/github.com/ohhsodead/arisen-studio").expanduser()
    old_dir = repo_dir / "src" / "Arisen Studio"
    new_dir = repo_dir / "src" / "Arisen Studio Mono"
    old_csproj = old_dir / "Arisen Studio.csproj"
    new_csproj = new_dir / "Arisen Studio Mono.csproj"

    for required in (milestones_path, old_csproj, new_csproj):
        if not required.is_file():
            print(f"ERROR: required file missing: {required}", file=sys.stderr)
            return 1

    with milestones_path.open("r", encoding="utf-8") as fh:
        config = json.load(fh)

    old_compile_items = parse_compile_items(old_csproj)
    new_compile_items = parse_compile_items(new_csproj)

    layout_designers: dict[str, int] = {}
    properties_files: dict[str, int] = {}
    ui_free: dict[str, int] = {}
    ui_coupled: dict[str, int] = {}

    for rel, _ in old_compile_items:
        fpath = old_dir / rel
        if not fpath.is_file():
            print(f"ERROR: missing compile item on disk: {rel}", file=sys.stderr)
            return 1
        lines = count_lines(fpath)
        if rel.startswith("Properties/"):
            properties_files[rel] = lines
        elif rel.endswith(".Designer.cs"):
            layout_designers[rel] = lines
        else:
            text = fpath.read_text(encoding="utf-8", errors="replace")
            if "DevExpress" not in text and "System.Windows.Forms" not in text:
                ui_free[rel] = lines
            else:
                ui_coupled[rel] = lines

    linked_old_paths: set[str] = set()
    link_prefix = "../Arisen Studio/"
    for inc, link in new_compile_items:
        if link is not None and inc.startswith(link_prefix):
            linked_old_paths.add(inc[len(link_prefix) :])

    linked_ui_free = sorted(p for p in linked_old_paths if p in ui_free)
    # A local Compile item (no Link) whose path equals an old UI-free file is a compiled copy of that file.
    copied_ui_free = sorted(inc for inc, link in new_compile_items if link is None and inc in ui_free and inc not in linked_old_paths)
    unlinked_ui_free = sorted(p for p in ui_free if p not in linked_old_paths and p not in copied_ui_free)

    actual_counts = {
        "old_compile_items": len(old_compile_items),
        "layout_designers": len(layout_designers),
        "layout_designer_lines": sum(layout_designers.values()),
        "ui_free_files": len(ui_free),
        "ui_coupled_files": len(ui_coupled),
        "linked_ui_free_files": len(linked_ui_free),
    }

    failures = []
    for key, expected in EXPECTED_COUNTS.items():
        actual = actual_counts[key]
        if actual != expected:
            failures.append(f"CHECK FAILED: {key}: expected {expected}, got {actual}")

    if MAINWINDOW_REL not in layout_designers:
        failures.append(f"CHECK FAILED: missing expected layout designer: {MAINWINDOW_REL}")
    if REPORT_ISSUE_REL not in layout_designers:
        failures.append(f"CHECK FAILED: missing expected layout designer: {REPORT_ISSUE_REL}")

    if failures:
        for msg in failures:
            print(msg, file=sys.stderr)
        return 1

    try:
        ported_b, ported_c, port_records = resolve_port_map(
            config.get("port_map", []), new_dir, ui_coupled, layout_designers
        )
    except ValueError as exc:
        print(f"ERROR: invalid port_map: {exc}", file=sys.stderr)
        return 1

    a_num = sum(ui_free[p] for p in linked_ui_free) + sum(ui_free[p] for p in copied_ui_free)
    a_den = sum(ui_free.values())
    b_num = sum(ui_coupled[p] for p in ported_b)
    b_den = sum(ui_coupled.values())
    c_num = sum(layout_designers[p] for p in ported_c)
    c_den = sum(layout_designers.values())

    overall_num = a_num + b_num + c_num
    overall_den = a_den + b_den + c_den

    dim_a = {
        "id": "A",
        "label": "Dimension A — UI-free code",
        **calc_pct(a_num, a_den),
        "files_ported": len(linked_ui_free) + len(copied_ui_free),
        "files_total": len(ui_free),
        "formula": f"(linked + locally copied and compiled) UI-free lines / all {len(ui_free)} UI-free lines",
    }
    dim_b = {
        "id": "B",
        "label": "Dimension B — UI-coupled code-behind & logic",
        **calc_pct(b_num, b_den),
        "files_ported": len(ported_b),
        "files_total": len(ui_coupled),
        "formula": f"ported UI-coupled lines (port_map) / all {len(ui_coupled)} UI-coupled lines",
    }
    dim_c = {
        "id": "C",
        "label": "Dimension C — Layout designers",
        **calc_pct(c_num, c_den),
        "files_ported": len(ported_c),
        "files_total": len(layout_designers),
        "formula": f"ported layout designer lines (port_map) / all {len(layout_designers)} layout designer lines",
    }
    overall_files_ported = len(linked_ui_free) + len(copied_ui_free) + len(ported_b) + len(ported_c)
    overall_files_total = len(ui_free) + len(ui_coupled) + len(layout_designers)
    overall_files_pct = calc_pct(overall_files_ported, overall_files_total)
    overall = {
        "id": "overall",
        "label": "Overall Codebase (A + B + C)",
        **calc_pct(overall_num, overall_den),
        "files_ported": overall_files_ported,
        "files_total": overall_files_total,
        "files_percent": overall_files_pct["percent"],
        "files_percent_str": overall_files_pct["percent_str"],
        "formula": "(A_ported + B_ported + C_ported) / (A_total + B_total + C_total); Properties/*.cs excluded",
    }

    # M4 (excludes MainWindow and ReportIssueDialog) and M5 (MainWindow alone)
    m4_keys = [k for k in layout_designers if k not in (MAINWINDOW_REL, REPORT_ISSUE_REL)]
    m4_num = sum(layout_designers[k] for k in ported_c if k in m4_keys)
    m4_den = sum(layout_designers[k] for k in m4_keys)
    m4_files_ported = sum(1 for k in ported_c if k in m4_keys)
    m4_pct = calc_pct(m4_num, m4_den)

    m5_num = layout_designers[MAINWINDOW_REL] if MAINWINDOW_REL in ported_c else 0
    m5_den = layout_designers[MAINWINDOW_REL]
    m5_pct = calc_pct(m5_num, m5_den)
    m5_layout_share = calc_pct(m5_den, c_den)

    # Scan new project's own .cs files outside linked files (exclude bin/ and obj/)
    own_cs_files: list[Path] = []
    win_findings: list[str] = []
    new_root_resolved = new_dir.resolve()
    for cs_path in sorted(new_dir.rglob("*.cs")):
        rel_parts = cs_path.relative_to(new_dir).parts
        if any(part in ("bin", "obj") for part in rel_parts):
            continue
        if not cs_path.resolve().is_relative_to(new_root_resolved):
            continue
        rel_cs = cs_path.relative_to(new_dir).as_posix()
        own_cs_files.append(cs_path)
        text = cs_path.read_text(encoding="utf-8", errors="replace")
        win_findings.extend(scan_csharp_file_for_windows_artifacts(rel_cs, text))

    # Relative existence check for smoke scripts (never read contents, never emit absolute paths)
    scripts_dir = script_dir.parent / "scripts"
    if not scripts_dir.is_dir():
        scripts_dir = Path("~/arisen-build/scripts").expanduser()
    cross_exists = (scripts_dir / "check-cross.sh").is_file()
    elf_exists = (scripts_dir / "check-elf.sh").is_file()
    smoke_ok = cross_exists and elf_exists

    manual_gates = config.get("manual_gates", {})
    g_shared = manual_gates.get("shared_control_layer", {})
    g_containers = manual_gates.get("layout_uses_containers", {})
    g_self_elf = manual_gates.get("self_contained_elf", {})

    gates = [
        {
            "id": "shared_control_layer",
            "label": g_shared.get("label", "Shared control layer exists in the new project"),
            "status": g_shared.get("status", "fail"),
            "value": g_shared.get("value", "NO"),
            "source": "manual",
            "detail": g_shared.get("detail", "None yet."),
        },
        {
            "id": "no_windows_paths_or_fonts",
            "label": "No hard-coded Windows paths or fonts in the new project's own .cs files",
            "status": "pass" if not win_findings else "fail",
            "value": "YES" if not win_findings else "NO",
            "source": "computed",
            "detail": (
                f"Scanned {len(own_cs_files)} own .cs files ("
                + ", ".join(p.relative_to(new_dir).as_posix() for p in own_cs_files)
                + "): 0 backslash path literals and 0 'Segoe UI' references."
                if not win_findings
                else f"Found {len(win_findings)} issue(s): " + "; ".join(win_findings)
            ),
        },
        {
            "id": "layout_uses_containers",
            "label": g_containers.get("label", "Layout uses containers, not copied pixel coordinates"),
            "status": g_containers.get("status", "unknown"),
            "value": g_containers.get("value", "unknown"),
            "source": "manual",
            "detail": g_containers.get("detail", "No forms converted yet."),
        },
        {
            "id": "automated_smoke_check",
            "label": "Automated smoke check exists",
            "status": "pass" if smoke_ok else "fail",
            "value": "YES" if smoke_ok else "NO",
            "source": "computed",
            "detail": (
                f"../scripts/check-cross.sh: {'exists' if cross_exists else 'missing'}; "
                f"../scripts/check-elf.sh: {'exists' if elf_exists else 'missing'}."
            ),
        },
        {
            "id": "self_contained_elf",
            "label": g_self_elf.get("label", "Self-contained ELF without installed Mono"),
            "status": g_self_elf.get("status", "fail"),
            "value": g_self_elf.get("value", "NO"),
            "source": "manual",
            "detail": g_self_elf.get("detail", "Requires system Mono libraries."),
        },
    ]

    enriched_milestones: list[dict[str, object]] = []
    for raw_m in config.get("milestones", []):
        m = dict(raw_m)
        mode = m.get("progress_mode", "manual")
        if mode == "measured_c_other":
            m["progress_source"] = "computed"
            m["percent"] = m4_pct["percent"]
            m["percent_str"] = m4_pct["percent_str"]
            m["measurement"] = {
                **m4_pct,
                "files_ported": m4_files_ported,
                "files_total": len(m4_keys),
                "formula": f"ported lines in Dimension C excluding MainWindow & ReportIssueDialog / {m4_den:,} lines",
            }
        elif mode == "measured_c_mainwindow":
            m["progress_source"] = "computed"
            m["percent"] = m5_pct["percent"]
            m["percent_str"] = m5_pct["percent_str"]
            m["measurement"] = {
                **m5_pct,
                "files_ported": 1 if MAINWINDOW_REL in ported_c else 0,
                "files_total": 1,
                "layout_share_percent_str": m5_layout_share["percent_str"],
                "formula": f"ported lines in {MAINWINDOW_REL} / {m5_den:,} lines",
            }
        elif mode == "measured_b":
            m["progress_source"] = "computed"
            m["percent"] = dim_b["percent"]
            m["percent_str"] = dim_b["percent_str"]
            m["measurement"] = {
                **calc_pct(b_num, b_den),
                "files_ported": len(ported_b),
                "files_total": len(ui_coupled),
                "formula": dim_b["formula"],
            }
        else:
            m["progress_source"] = "manual"
            if m.get("status") == "done":
                pct_val = 100.0
                pct_str = "100.0%"
            else:
                manual_val = float(m.get("manual_percent", 0) or 0)
                tenths = max(0, min(1000, int(manual_val * 10)))
                pct_val = tenths / 10.0
                pct_str = f"{tenths // 10}.{tenths % 10}%"
            m["percent"] = pct_val
            m["percent_str"] = pct_str

        if "modules" in m and isinstance(m["modules"], list):
            m["resolved_count"] = sum(1 for mod in m["modules"] if mod.get("resolved"))
            m["total_count"] = len(m["modules"])

        enriched_milestones.append(m)

    events_path = script_dir / "events.json"
    recent_events: list[dict[str, object]] = []
    if events_path.is_file():
        try:
            loaded_events = json.loads(events_path.read_text(encoding="utf-8"))
            if isinstance(loaded_events, list):
                recent_events = [e for e in loaded_events if isinstance(e, dict)]
        except Exception as exc:
            print(f"WARNING: ignoring unreadable events.json: {exc}", file=sys.stderr)

    # Optional live count written by threads_count.py: only a number and a time.
    # Any problem here shows a dash on the page; it must never stop generation.
    active_threads: dict[str, object] = {"count": None, "as_of": None, "stale": False}
    try:
        with (script_dir / "threads.json").open("r", encoding="utf-8") as fh:
            tj = json.load(fh)
        cnt = tj.get("active")
        stamp = str(tj.get("as_of", ""))
        if isinstance(cnt, int) and not isinstance(cnt, bool) and cnt >= 0:
            when = dt.datetime.fromisoformat(stamp)
            if when.tzinfo is None:
                when = when.replace(tzinfo=dt.timezone.utc)
            age = (dt.datetime.now(dt.timezone.utc) - when).total_seconds()
            active_threads = {
                "count": cnt,
                "as_of": when.astimezone(dt.timezone.utc).strftime("%H:%M:%S UTC"),
                "stale": age > 180,
            }
    except Exception as exc:
        print(f"WARNING: ignoring unreadable threads.json: {exc}", file=sys.stderr)

    payload: dict[str, object] = {
        "active_threads": active_threads,
        "project": config.get("project", "Arisen Studio"),
        "completion_definition": config.get("completion_definition", ""),
        "proposal_notice": config.get("proposal_notice", ""),
        "generated_at_utc": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC"),
        "measurements": {
            "overall": overall,
            "dimensions": [dim_a, dim_b, dim_c],
            "excluded_properties": {
                "file_count": len(properties_files),
                "total_lines": sum(properties_files.values()),
                "files": [
                    {"path": p, "lines": properties_files[p]} for p in sorted(properties_files)
                ],
            },
            "linked_ui_free_files": [
                {"path": p, "lines": ui_free[p]} for p in sorted(set(linked_ui_free) | set(copied_ui_free))
            ],
            "unlinked_ui_free_files": [
                {"path": p, "lines": ui_free[p]} for p in unlinked_ui_free
            ],
            "port_map_summary": {
                "total_entries": len(port_records),
                "active_count": sum(1 for r in port_records if r["counted_dimension"] is not None),
                "entries": port_records,
            },
        },
        "milestones": enriched_milestones,
        "extensibility_gates": gates,
        "recent_events": recent_events[-20:][::-1],
    }

    html_output = render_html(payload)
    tmp_html_path = output_html_path.with_name(output_html_path.name + ".tmp")
    tmp_html_path.write_text(html_output, encoding="utf-8")
    os.replace(tmp_html_path, output_html_path)

    pass_cnt = sum(1 for g in gates if g["status"] == "pass")
    fail_cnt = sum(1 for g in gates if g["status"] == "fail")
    unk_cnt = sum(1 for g in gates if g["status"] == "unknown")

    print("CHECKS: PASS (6/6)")
    print(f"Overall: {overall_num:,} / {overall_den:,} lines ({overall['percent_str']}) [{overall_files_ported}/{overall_files_total} files ({overall_files_pct['percent_str']})]")
    print(
        f"Dimension A (UI-free): {a_num:,} / {a_den:,} lines ({dim_a['percent_str']}) "
        f"[{len(linked_ui_free) + len(copied_ui_free)}/{len(ui_free)} files]"
    )
    print(
        f"Dimension B (UI-coupled): {b_num:,} / {b_den:,} lines ({dim_b['percent_str']}) "
        f"[{len(ported_b)}/{len(ui_coupled)} files]"
    )
    print(
        f"Dimension C (Layouts): {c_num:,} / {c_den:,} lines ({dim_c['percent_str']}) "
        f"[{len(ported_c)}/{len(layout_designers)} files]"
    )
    print(
        f"Excluded Properties/*.cs: {len(properties_files)} files, "
        f"{sum(properties_files.values()):,} lines"
    )
    print(
        f"M4 other layouts: {m4_num:,} / {m4_den:,} lines ({m4_pct['percent_str']}) "
        f"[{m4_files_ported}/{len(m4_keys)} files]"
    )
    print(
        f"M5 MainWindow: {m5_num:,} / {m5_den:,} lines ({m5_pct['percent_str']}) "
        f"[{m5_layout_share['percent_str']} of layout lines]"
    )
    print(f"Gates: {pass_cnt} pass, {fail_cnt} fail, {unk_cnt} unknown")
    print("Wrote index.html")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
