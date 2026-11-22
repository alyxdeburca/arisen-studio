#!/usr/bin/env python3
"""
Localization audit generator for the Arisen Studio Mono port.

Reads (never writes) the Mono .cs sources and the DevExpress .resx files, then writes:
  - Localization/EnglishKeyTable.g.cs   generated C# table (used keys -> en_US text, and code-fallback text)
  - Localization/l10n-audit.tsv         one row per used key
  - <markdown fragment path>            the audit tables used by PARALLEL-L10N-RESULT.md (optional 2nd argument)

Run (from anywhere; -I keeps the script from importing modules out of the current directory):
    python3 -I generate_l10n_audit.py "<path to src>" [markdown-fragment-output]

"<path to src>" is the folder holding "Arisen Studio", "Arisen Studio Mono" and "Arisen Studio.sln".

What counts as a used key:
  1. A string literal matching [A-Z][A-Z0-9_]+ that is followed by a comma and the English fallback,
     i.e. the "KEY", "English text" pair every Text_/Lookup/GetText/PageUi.Text/layout.Column call passes.
  2. The [Description("...")] values of the TransferType enum, because TransferDialog.LoadTransferFilesAsync
     looks the title up with GetText(TransferType.Humanize(), ...), a key that is not a literal at the call site.
  Known false positives are dropped (see FALSE_POSITIVE_LINES).
"""
import collections
import os
import re
import sys
import xml.etree.ElementTree as ET

SRC = sys.argv[1]
MD_OUT = sys.argv[2] if len(sys.argv) > 2 else None
MONO = os.path.join(SRC, "Arisen Studio Mono")
DX = os.path.join(SRC, "Arisen Studio")
LANG_DIR = os.path.join(DX, "Languages")
OUT_DIR = os.path.join(MONO, "Localization")

# Files / folders the audit covers (relative to the Mono project folder).
SCOPES = [
    "Forms/Tools/PS3/New",
    "Forms/Tools/XBOX/New",
    "Forms/Dialogs/Transfer",
    "Forms/Pages",
    "Forms/Pages/Library",
    "Forms/Tools/XBOX/XboxHDKey.cs",
    "Forms/Dialogs/XboxHDKey.cs",        # older location of the same tool, still in the tree
    "Forms/Tools/PS3/GameRegions.cs",
    "Forms/Dialogs/GameRegions.cs",      # older location of the same tool, still in the tree
]

# Lines where a "KB", "MB" pair is a units array and not a resource key.
FALSE_POSITIVE_LINES = re.compile(r'string\[\]\s+units\s*=')

PAIR = re.compile(r'"([A-Z][A-Z0-9_]{1,})"\s*,\s*(@?"(?:[^"\\]|\\.)*"|[^,)]{1,60})')
PLACEHOLDER = re.compile(r'\{(\d+)(?:[,:][^}]*)?\}')


def decode_cs_literal(token):
    """C# string literal token -> text, or None when the token is not a plain literal."""
    token = token.strip()
    if token.startswith('@"') and token.endswith('"'):
        return token[2:-1].replace('""', '"')
    if not (token.startswith('"') and token.endswith('"')):
        return None
    body = token[1:-1]
    out, i = [], 0
    table = {"n": "\n", "r": "\r", "t": "\t", '"': '"', "\\": "\\", "'": "'", "0": "\0"}
    while i < len(body):
        c = body[i]
        if c == "\\" and i + 1 < len(body):
            n = body[i + 1]
            if n == "u" and i + 5 < len(body) + 0:
                out.append(chr(int(body[i + 2:i + 6], 16)))
                i += 6
                continue
            out.append(table.get(n, n))
            i += 2
            continue
        out.append(c)
        i += 1
    return "".join(out)


def cs_escape(text):
    out = []
    for ch in text:
        if ch == "\\":
            out.append("\\\\")
        elif ch == '"':
            out.append('\\"')
        elif ch == "\n":
            out.append("\\n")
        elif ch == "\r":
            out.append("\\r")
        elif ch == "\t":
            out.append("\\t")
        elif ord(ch) < 32 or ord(ch) > 126:
            out.append("\\u%04x" % ord(ch))
        else:
            out.append(ch)
    return '"' + "".join(out) + '"'


def placeholders(text):
    return tuple(sorted(set(int(m.group(1)) for m in PLACEHOLDER.finditer(text or ""))))


def collect_files():
    files = []
    for scope in SCOPES:
        path = os.path.join(MONO, scope)
        if os.path.isfile(path):
            files.append(path)
        elif os.path.isdir(path):
            for name in sorted(os.listdir(path)):
                if name.endswith(".cs"):
                    files.append(os.path.join(path, name))
    return sorted(set(files))


def load_resx():
    langs = {}
    for name in sorted(os.listdir(LANG_DIR)):
        if not name.endswith(".resx"):
            continue
        table = {}
        for node in ET.parse(os.path.join(LANG_DIR, name)).getroot().findall("data"):
            value = node.find("value")
            table[node.get("name")] = (value.text or "") if value is not None else ""
        langs[name[:-5]] = table
    return langs


def transfer_type_keys():
    path = os.path.join(DX, "Enums", "Enums.cs")
    text = open(path, encoding="utf-8-sig").read()
    match = re.search(r'enum\s+TransferType\s*\{(.*?)\n\s*\}', text, re.S)
    return re.findall(r'\[Description\("([A-Z0-9_]+)"\)\]', match.group(1)) if match else []


def main():
    files = collect_files()
    uses = collections.OrderedDict()  # key -> list of (relfile, line, fallback_text_or_None, raw)
    for path in files:
        rel = os.path.relpath(path, MONO).replace(os.sep, "/")
        with open(path, encoding="utf-8-sig") as handle:
            lines = handle.read().split("\n")
        for number, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith("//") or FALSE_POSITIVE_LINES.search(line):
                continue
            for match in PAIR.finditer(line):
                uses.setdefault(match.group(1), []).append(
                    (rel, number, decode_cs_literal(match.group(2)), match.group(2).strip()))
    dynamic = transfer_type_keys()
    for key in dynamic:
        uses.setdefault(key, []).append(
            ("Forms/Dialogs/Transfer/TransferDialog.cs", 222, None, "(dynamic: TransferType.Humanize())"))

    langs = load_resx()
    en = langs["en_US"]
    # Keys the DevExpress code looks up as GetString("KEY") (literal call sites only).
    dx_used = set()
    for folder, _, names in os.walk(DX):
        for name in names:
            if name.endswith(".cs"):
                with open(os.path.join(folder, name), encoding="utf-8-sig", errors="replace") as handle:
                    dx_used.update(re.findall(r'GetString\("([A-Z][A-Z0-9_]+)"\)', handle.read()))
    rows = []
    for key in sorted(uses):
        sites = uses[key]
        defined_in = [l for l in langs if key in langs[l]]
        fallbacks = []
        for _, _, fb, _ in sites:
            if fb is not None and fb not in fallbacks:
                fallbacks.append(fb)
        used_in = collections.OrderedDict()
        for rel, number, _, _ in sites:
            used_in.setdefault(rel, []).append(number)
        notes = []
        if len(fallbacks) > 1:
            notes.append("code fallbacks differ: " + " | ".join(repr(f) for f in fallbacks))
        fb_ph = set()
        for f in fallbacks:
            fb_ph.update(placeholders(f))
        lang_ph = {l: placeholders(langs[l][key]) for l in defined_in}
        distinct_lang_ph = set(lang_ph.values())
        status = "defined" if "en_US" in defined_in else ("defined-other-lang" if defined_in else "undefined")
        if status == "undefined":
            if fb_ph:
                notes.append("PLACEHOLDER UNVERIFIED: code fallback uses %s but no resx defines the key"
                             % ", ".join("{%d}" % n for n in sorted(fb_ph)))
        else:
            if len(distinct_lang_ph) > 1:
                notes.append("placeholders differ between languages: " + "; ".join(
                    "%s=%s" % (l, ",".join("{%d}" % n for n in p) or "none") for l, p in sorted(lang_ph.items())))
            en_ph = lang_ph.get("en_US", ())
            if fallbacks and set(en_ph) != fb_ph:
                notes.append("placeholder mismatch: en_US %s, code fallback %s" % (
                    ",".join("{%d}" % n for n in en_ph) or "none",
                    ",".join("{%d}" % n for n in sorted(fb_ph)) or "none"))
            elif en_ph and not notes:
                notes.append("placeholders %s match the code fallback and every language" %
                             ",".join("{%d}" % n for n in en_ph))
            if "<a " in en.get(key, "").lower() or "href=" in en.get(key, "").lower():
                notes.append("VALUE CONTAINS HTML ANCHOR (needs ResourceText.ToPlainText for a MessageBox)")
        if status == "undefined" and fallbacks:
            norm = lambda s: s.strip().rstrip(":").strip().lower()
            same = sorted(k for k, v in en.items() if norm(v) == norm(fallbacks[0]) and "\n" not in v)
            if same:
                notes.append("en_US has the same English text under: " + ", ".join(same[:4]))
        if status == "undefined" and not fallbacks and key not in dynamic:
            notes.append("code fallback is a variable, not a literal")
        if status == "undefined":
            if key in dx_used or key in dynamic:
                notes.append("USED BUT NEVER DEFINED: the DevExpress original also looks this key up (GetString returns null there too)")
            else:
                notes.append("Mono-only key: the DevExpress code never looks it up, so no resx has it")
        if key in dynamic:
            notes.append("dynamic key from the TransferType enum [Description]; code fallback is the enum name split into words")
        rows.append({
            "key": key, "used_in": used_in, "status": status, "defined_in": defined_in,
            "en": en.get(key), "fallbacks": fallbacks, "notes": notes, "dynamic": key in dynamic,
        })

    # ---- C# table -------------------------------------------------------------------------------------------
    os.makedirs(OUT_DIR, exist_ok=True)
    defined_rows = [r for r in rows if r["en"] is not None]
    undefined_rows = [r for r in rows if r["en"] is None]
    cs = []
    cs.append("// <auto-generated>")
    cs.append("// Generated by Localization/generate_l10n_audit.py. Do not edit by hand; rerun the script.")
    cs.append("// Sources: DevExpress Languages/en_US.resx (text) and the Mono .cs files (which keys are used).")
    cs.append("// NOT COMPILED OR RUN when this file was produced.")
    cs.append("// </auto-generated>")
    cs.append("using System.Collections.Generic;")
    cs.append("")
    cs.append("namespace ArisenStudio.Localization")
    cs.append("{")
    cs.append("    /// <summary>")
    cs.append("    /// English text for every resource key the Mono tree uses. The Mono tree ships no .resx of its own, so these are")
    cs.append("    /// the keys it is missing. <see cref=\"DefinedInEnUs\"/> holds text copied verbatim from the DevExpress en_US.resx;")
    cs.append("    /// <see cref=\"CodeFallbackOnly\"/> holds keys that en_US does not define at all, with the English text the Mono code")
    cs.append("    /// passes as its fallback (so it is NOT from en_US; a translator has to add these keys to a resx).")
    cs.append("    /// </summary>")
    cs.append("    internal static class EnglishKeyTable")
    cs.append("    {")
    cs.append("        /// <summary>Used keys that en_US.resx defines (%d). Text is verbatim, including {0} placeholders and any HTML.</summary>" % len(defined_rows))
    cs.append("        public static readonly IDictionary<string, string> DefinedInEnUs = new Dictionary<string, string>")
    cs.append("        {")
    for r in defined_rows:
        cs.append("            { %s, %s }," % (cs_escape(r["key"]), cs_escape(r["en"])))
    cs.append("        };")
    cs.append("")
    cs.append("        /// <summary>Used keys that no DevExpress .resx defines (%d), with the code's own English fallback.</summary>" % len([r for r in undefined_rows if r["fallbacks"]]))
    cs.append("        public static readonly IDictionary<string, string> CodeFallbackOnly = new Dictionary<string, string>")
    cs.append("        {")
    for r in undefined_rows:
        if r["fallbacks"]:
            cs.append("            { %s, %s }," % (cs_escape(r["key"]), cs_escape(r["fallbacks"][0])))
    cs.append("        };")
    cs.append("    }")
    cs.append("}")
    with open(os.path.join(OUT_DIR, "EnglishKeyTable.g.cs"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(cs) + "\n")

    # ---- TSV ------------------------------------------------------------------------------------------------
    with open(os.path.join(OUT_DIR, "l10n-audit.tsv"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write("key\tstatus\tdefined_in\tused_in\tcode_fallback\ten_US\tnotes\n")
        for r in rows:
            handle.write("\t".join([
                r["key"], r["status"], ",".join(r["defined_in"]) or "-",
                "; ".join("%s:%s" % (f, ",".join(map(str, n))) for f, n in r["used_in"].items()),
                " | ".join(r["fallbacks"]).replace("\n", "\\n"),
                (r["en"] or "").replace("\n", "\\n"),
                " ; ".join(r["notes"]).replace("\n", "\\n"),
            ]) + "\n")

    # ---- Markdown fragment ----------------------------------------------------------------------------------
    def short(path):
        return path.split("/")[-1]

    def cell(text):
        return (text or "").replace("|", "\\|").replace("\n", "\\n")

    md = []
    md.append("Files scanned: %d. Distinct keys used: %d (%d literal, %d dynamic). Defined in en_US: %d. Never defined: %d."
              % (len(files), len(rows), len(rows) - len(dynamic), len(dynamic), len(defined_rows), len(undefined_rows)))
    md.append("")
    md.append("| Key | Used in (file:lines) | Defined in resx | Code fallback (English) | Placeholder / other notes |")
    md.append("|---|---|---|---|---|")
    for r in rows:
        used = "; ".join("%s:%s" % (short(f), ",".join(map(str, n[:4])) + ("…" if len(n) > 4 else ""))
                         for f, n in r["used_in"].items())
        defined = "all 6" if len(r["defined_in"]) == len(langs) else (", ".join(r["defined_in"]) or "**MISSING**")
        md.append("| `%s` | %s | %s | %s | %s |" % (
            r["key"], cell(used), defined, cell(" / ".join(r["fallbacks"])) or "(variable or enum name)", cell("; ".join(r["notes"]))))
    md.append("")
    md.append("<!-- MISSING-KEY TABLES -->")
    md.append("")
    md.append("**A. Used keys that en_US defines (%d). Mono has no .resx, so all of these are missing from the Mono tree.** English text is verbatim from the DevExpress en_US.resx." % len(defined_rows))
    md.append("")
    md.append("| Key | English text (en_US) | Defined in |")
    md.append("|---|---|---|")
    for r in defined_rows:
        md.append("| `%s` | %s | %s |" % (r["key"], cell(r["en"]),
                  "all 6" if len(r["defined_in"]) == len(langs) else ", ".join(r["defined_in"])))
    md.append("")
    md.append("**B. Used keys that no DevExpress .resx defines (%d).** There is no en_US text. The English shown is the Mono code's own fallback; a resx entry has to be authored." % len(undefined_rows))
    md.append("")
    md.append("| Key | Code fallback (English) | Origin | Existing en_US key with the same text |")
    md.append("|---|---|---|---|")
    for r in undefined_rows:
        origin = "also looked up by DevExpress code" if (r["key"] in dx_used or r["dynamic"]) else "Mono-only key"
        same = ""
        for n in r["notes"]:
            if n.startswith("en_US has the same"):
                same = n.split(": ", 1)[1]
        md.append("| `%s` | %s | %s | %s |" % (r["key"], cell(" / ".join(r["fallbacks"])) or "(variable or enum name)", origin, same or "-"))
    md_text = "\n".join(md) + "\n"
    if MD_OUT:
        with open(MD_OUT, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(md_text)
    print("files=%d keys=%d defined_en=%d undefined=%d dynamic=%d" % (
        len(files), len(rows), len(defined_rows), len(undefined_rows), len(dynamic)))
    # a few diagnostics for the report
    unusual = [r["key"] for r in rows if not r["fallbacks"] and not r["dynamic"]]
    print("keys with no literal fallback:", unusual)
    for r in rows:
        if any(("PLACEHOLDER" in n or "mismatch" in n or "differ" in n or "ANCHOR" in n) for n in r["notes"]):
            print(r["key"], "|", "; ".join(r["notes"]))


if __name__ == "__main__":
    main()
