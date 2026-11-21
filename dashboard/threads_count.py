#!/usr/bin/env python3
"""Write threads.json: how many T3 threads are active on this machine.

Reads the T3 state database READ-ONLY and stores only a number and a time.
No titles, ids or links are written, because the page is public.
On any error nothing is written, so the page's "as of" time ages and the
page marks the count stale instead of showing a wrong number.
"""
import datetime as dt
import json
import os
import sqlite3
import sys

ACTIVE = ("running", "preparing", "queued", "starting")


def main() -> int:
    here = os.path.dirname(os.path.abspath(__file__))
    db_path = os.path.expanduser("~/.t3/userdata/statev2.sqlite")
    con = sqlite3.connect("file:" + db_path + "?mode=ro", uri=True, timeout=5)
    try:
        con.execute("PRAGMA query_only=1")
        marks = ",".join("?" for _ in ACTIVE)
        row = con.execute(
            "SELECT COUNT(DISTINCT r.thread_id) "
            "FROM orchestration_v2_projection_runs r "
            "JOIN orchestration_v2_projection_threads t ON t.thread_id = r.thread_id "
            "WHERE r.status IN (" + marks + ") "
            "AND t.deleted_at IS NULL AND t.archived_at IS NULL",
            ACTIVE,
        ).fetchone()
    finally:
        con.close()
    count = int(row[0])
    stamp = dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat()
    tmp = os.path.join(here, "threads.json.tmp")
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump({"active": count, "as_of": stamp}, fh)
        fh.write("\n")
    os.replace(tmp, os.path.join(here, "threads.json"))
    print("active threads:", count)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # leave the old file alone so the page shows it as stale
        print("threads_count failed:", exc, file=sys.stderr)
        raise SystemExit(1)
