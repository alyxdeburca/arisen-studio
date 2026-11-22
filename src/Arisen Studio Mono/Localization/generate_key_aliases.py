#!/usr/bin/env python3
"""Generate Localization/KeyAliases.g.cs: a map from the short keys the Mono code invented to the existing .resx keys.

Reads (never writes): the six DevExpress Languages/*.resx files and Localization/l10n-audit.tsv.
Writes: Localization/KeyAliases.g.cs and, with a second argument, a markdown fragment for the report.

Run:  python3 -I Localization/generate_key_aliases.py "<Arisen Studio Mono folder>" [report-fragment.md]

The alias table below is the source of truth. A target key that is not in en_US.resx is NOT mapped (it goes to the
"not mapped" list with the reason), so the generated map never points at a key that does not exist.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET

LANGS = ["en_US", "es_ES", "pt_BR", "ro_RO", "sv_SE", "tr_TR"]

# short Mono key -> existing .resx key that holds the same English text (from PARALLEL-L10N-RESULT.md table B).
ALIASES = {
    "ADD_TO_FAVORITES": "LABEL_ADD_TO_FAVORITES",
    "ADD_TO_MODULES": "LABEL_ADD_TO_MODULES",
    "CATEGORY": "LABEL_CATEGORY",
    "DASHBOARD": "NAVIGATION_DASHBOARD",
    "DOWNLOAD": "LABEL_DOWNLOAD",
    "DOWNLOADS": "LABEL_DOWNLOADS",  # NAVIGATION_DOWNLOADS has the same en_US text; LABEL_ chosen per the brief
    "EDIT": "LABEL_EDIT",
    "FILE_SIZE": "LABEL_FILE_SIZE",
    "FIRMWARE_VERSION": "LABEL_FW_VERSION",
    "GAME": "LABEL_GAME",
    "GAME_FILE": "LABEL_GAME_FILE",
    "INSTALL": "LABEL_INSTALL",
    "INSTALLED_ON": "LABEL_INSTALLED_ON",
    "MODIFIED_DATE": "LABEL_MODIFIED_DATE",
    "MOD_TYPE": "LABEL_MOD_TYPE",
    "NAME": "LABEL_NAME",
    "NEW_FOLDER": "LABEL_NEW_FOLDER",
    "OPEN_EXPLORER": "LABEL_OPEN_EXPLORER",
    "PACKAGE_FILES_MANAGER": "PACKAGE_FILE_MANAGER",
    "PLATFORM": "LABEL_PLATFORM",
    "REFRESH": "LABEL_REFRESH",
    "REGION": "LABEL_REGION",
    "RENAME": "LABEL_RENAME",
    "SHOW_FAVORITES": "LABEL_SHOW_FAVORITES",  # FAVORITES_SHOW has the same en_US text; LABEL_ chosen per the brief
    "SYSTEM_TYPE": "LABEL_SYSTEM_TYPE",
    "TITLE_ID": "LABEL_TITLE_ID",
    "TOTAL_FILES": "LABEL_TOTAL_FILES",
    "UPLOAD": "LABEL_UPLOAD",
    "VERSION": "LABEL_VERSION",
}


def load_resx(folder):
    result = {}
    for lang in LANGS:
        root = ET.parse(os.path.join(folder, lang + ".resx")).getroot()
        values = {}
        for node in root.findall("data"):
            value = node.find("value")
            values[node.get("name")] = value.text if value is not None and value.text else ""
        result[lang] = values
    return result


def load_audit(path):
    rows = {}
    with open(path, encoding="utf-8") as handle:
        header = handle.readline().rstrip("\n").split("\t")
        for line in handle:
            cells = line.rstrip("\n").split("\t")
            cells += [""] * (len(header) - len(cells))
            rows[cells[0]] = dict(zip(header, cells))
    return rows


def cs_string(text):
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
        else:
            out.append(ch)
    return '"' + "".join(out) + '"'


def placeholders(text):
    return sorted(set(re.findall(r"\{\d+\}", text or "")))


def main():
    mono = sys.argv[1]
    fragment = sys.argv[2] if len(sys.argv) > 2 else None
    resx_dir = os.path.normpath(os.path.join(mono, "..", "Arisen Studio", "Languages"))
    res = load_resx(resx_dir)
    audit = load_audit(os.path.join(mono, "Localization", "l10n-audit.tsv"))

    mapped, not_mapped = [], []
    for short, target in sorted(ALIASES.items()):
        have = [lang for lang in LANGS if target in res[lang]]
        missing = [lang for lang in LANGS if target not in res[lang]]
        short_in = [lang for lang in LANGS if short in res[lang]]
        row = audit.get(short, {})
        fallback = row.get("code_fallback", "")
        english = res["en_US"].get(target)
        if english is None:
            not_mapped.append((short, target, "target key is not in en_US.resx"))
            continue
        notes = []
        if short_in:
            notes.append("short key already defined in " + ",".join(short_in))
        if fallback and fallback.strip(" :").lower() != english.strip(" :").lower():
            notes.append("code fallback '%s' differs from en_US '%s'" % (fallback, english))
        for lang in have:
            if placeholders(res[lang][target]) != placeholders(english):
                notes.append("placeholder set differs in " + lang)
        if not row:
            notes.append("short key not in l10n-audit.tsv")
        mapped.append((short, target, have, missing, english, notes))

    # Mono-only keys with no alias: say why, and whether any en_US value matches loosely.
    en_by_text = {}
    for key, text in res["en_US"].items():
        en_by_text.setdefault(text.strip(" :").lower(), []).append(key)
    for short, row in sorted(audit.items()):
        if row.get("status") != "undefined" or short in ALIASES:
            continue
        fallback = row.get("code_fallback", "").split(" / ")[0]
        loose = en_by_text.get(fallback.strip(" :").lower(), [])
        if row.get("notes", "").find("also looked up by DevExpress code") >= 0 or "USED BUT NEVER DEFINED" in row.get("notes", ""):
            reason = "also looked up by the DevExpress code and defined nowhere; no en_US key has this text"
        else:
            reason = "Mono-only key; no en_US key has this text"
        if short in ("LABEL_OPEN_FILE",):
            reason += " (two different fallbacks in use, 'Keyvault File' and 'Open File'; mapping to OPEN_FILE would change the XboxHDKey caption)"
        if short.endswith("_APPLICATION") or short == "INSTALLING_PACKAGE":
            reason = "dynamic TransferType key defined in no .resx; no en_US key has this text"
        if loose:
            reason += "; loose text match (not mapped): " + ",".join(loose)
        not_mapped.append((short, "-", reason))

    lines = []
    lines.append("// <auto-generated>")
    lines.append("// Generated by Localization/generate_key_aliases.py. Do not edit by hand; rerun the script.")
    lines.append("// Sources: DevExpress Languages/*.resx (which target keys exist, per language) and Localization/l10n-audit.tsv.")
    lines.append("// NOT COMPILED OR RUN when this file was produced.")
    lines.append("// </auto-generated>")
    lines.append("using System.Collections.Generic;")
    lines.append("")
    lines.append("namespace ArisenStudio.Localization")
    lines.append("{")
    lines.append("    /// <summary>")
    lines.append("    /// Short keys the Mono code invented, mapped to the existing .resx key that holds the same text.")
    lines.append("    /// Every target exists in en_US.resx. <see cref=\"MissingIn\"/> lists the languages whose .resx lacks the target;")
    lines.append("    /// for those, <see cref=\"AliasedText\"/> shows <see cref=\"EnglishText\"/> (verbatim en_US).")
    lines.append("    /// </summary>")
    lines.append("    internal static class KeyAliases")
    lines.append("    {")
    lines.append("        /// <summary>Short Mono key to existing .resx key.</summary>")
    lines.append("        public static readonly IDictionary<string, string> Targets = new Dictionary<string, string>")
    lines.append("        {")
    for short, target, have, missing, english, notes in mapped:
        lines.append("            { %s, %s }," % (cs_string(short), cs_string(target)))
    lines.append("        };")
    lines.append("")
    lines.append("        /// <summary>Target .resx key to its en_US text (verbatim), used when the active language lacks the target.</summary>")
    lines.append("        public static readonly IDictionary<string, string> EnglishText = new Dictionary<string, string>")
    lines.append("        {")
    seen = set()
    for short, target, have, missing, english, notes in mapped:
        if target in seen:
            continue
        seen.add(target)
        lines.append("            { %s, %s }," % (cs_string(target), cs_string(english)))
    lines.append("        };")
    lines.append("")
    lines.append("        /// <summary>Short key to the resx language names (en_US, es_ES, ...) whose file lacks the target key. Only keys with a gap are listed.</summary>")
    lines.append("        public static readonly IDictionary<string, string[]> MissingIn = new Dictionary<string, string[]>")
    lines.append("        {")
    for short, target, have, missing, english, notes in mapped:
        if missing:
            lines.append("            { %s, new[] { %s } }," % (cs_string(short), ", ".join(cs_string(m) for m in missing)))
    lines.append("        };")
    lines.append("    }")
    lines.append("}")
    out_path = os.path.join(mono, "Localization", "KeyAliases.g.cs")
    with open(out_path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")

    if fragment:
        md = ["| Short key | Target key | Present in | Lacks (falls back to en_US) | Notes |", "|---|---|---|---|---|"]
        for short, target, have, missing, english, notes in mapped:
            present = "all 6" if not missing else ", ".join(have)
            md.append("| `%s` | `%s` | %s | %s | %s |" % (short, target, present, ", ".join(missing) or "-", "; ".join(notes) or ""))
        md += ["", "NOT MAPPED:", "", "| Key | Target | Reason |", "|---|---|---|"]
        for short, target, reason in not_mapped:
            md.append("| `%s` | %s | %s |" % (short, target if target == "-" else "`%s`" % target, reason))
        with open(fragment, "w", encoding="utf-8") as handle:
            handle.write("\n".join(md) + "\n")

    print("mapped %d, not mapped %d, wrote %s" % (len(mapped), len(not_mapped), out_path))
    for short, target, have, missing, english, notes in mapped:
        if missing or notes:
            print("  %s -> %s  missing=%s notes=%s" % (short, target, missing, notes))


if __name__ == "__main__":
    main()
