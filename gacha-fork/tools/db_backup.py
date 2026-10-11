#!/usr/bin/env python3
"""Hot backup + inspect for Starward-family SQLite databases.

Why not a plain file copy: Starward runs the DB in WAL mode with a live writer.
Copying .db/.db-wal/.db-shm by hand can capture a torn tail. This tool uses
sqlite3's online backup API, which is the supported way to snapshot a live DB.

Usage:
  python db_backup.py --src "<user data folder>" --label <name>
  python db_backup.py --src "<user data folder>" --label <name> --inspect-only
  python db_backup.py --src "<user data folder>" --compare "<other folder>"
"""

from __future__ import annotations

import argparse
import datetime as dt
import os
import shutil
import sqlite3
import sys

DB_NAME = "StarwardDatabase.db"

GACHA_TABLES = [
    "GenshinGachaItem",
    "StarRailGachaItem",
    "ZZZGachaItem",
    "GenshinBeyondGachaItem",
    "GachaLogUrl",
    "GenshinGachaInfo",
    "StarRailGachaInfo",
    "ZZZGachaInfo",
    "GenshinBeyondGachaInfo",
]


def connect_rw(db_path: str) -> sqlite3.Connection:
    """Open the live DB read-write (as the app itself does) so WAL is readable."""
    con = sqlite3.connect(db_path, timeout=15.0)
    con.execute("PRAGMA busy_timeout = 15000;")
    return con


def table_counts(con: sqlite3.Connection) -> dict[str, int]:
    have = {
        row[0]
        for row in con.execute("SELECT name FROM sqlite_master WHERE type='table';")
    }
    out: dict[str, int] = {}
    for t in GACHA_TABLES:
        if t in have:
            out[t] = con.execute(f"SELECT COUNT(*) FROM {t};").fetchone()[0]
    return out


def gacha_summary(con: sqlite3.Connection) -> str:
    have = {
        row[0]
        for row in con.execute("SELECT name FROM sqlite_master WHERE type='table';")
    }
    lines: list[str] = []
    for t in GACHA_TABLES:
        if t not in have:
            continue
        n = con.execute(f"SELECT COUNT(*) FROM {t};").fetchone()[0]
        extra = ""
        if t.endswith("GachaItem"):
            try:
                uids = con.execute(
                    f"SELECT COUNT(DISTINCT Uid), MIN(Time), MAX(Time) FROM {t};"
                ).fetchone()
                extra = f"  uids={uids[0]}  {uids[1]} .. {uids[2]}"
            except sqlite3.Error:
                pass
        lines.append(f"  {t:<28} {n:>8}{extra}")
    ver = con.execute("PRAGMA USER_VERSION;").fetchone()[0]
    lines.insert(0, f"  schema USER_VERSION = {ver}")
    return "\n".join(lines)


def do_backup(src_folder: str, label: str, out_root: str) -> str:
    db = os.path.join(src_folder, DB_NAME)
    if not os.path.isfile(db):
        raise SystemExit(f"no database at {db}")

    stamp = dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    out_dir = os.path.join(out_root, f"{stamp}-{label}")
    os.makedirs(out_dir, exist_ok=True)
    dst = os.path.join(out_dir, DB_NAME)

    src = connect_rw(db)
    try:
        src.execute("PRAGMA wal_checkpoint(PASSIVE);")
    except sqlite3.Error:
        pass
    dst_con = sqlite3.connect(dst)
    try:
        src.backup(dst_con)
    finally:
        dst_con.close()
        src.close()

    # side files the app may also need (config.ini holds UserDataFolder)
    for extra in ("config.ini", "version.ini", "StarwardDatabase.db-wal", "StarwardDatabase.db-shm"):
        p = os.path.join(src_folder, extra)
        if os.path.isfile(p):
            try:
                shutil.copy2(p, os.path.join(out_dir, extra))
            except OSError as ex:
                print(f"  (skip {extra}: {ex})")

    chk = sqlite3.connect(dst)
    try:
        integrity = chk.execute("PRAGMA integrity_check;").fetchone()[0]
        counts = table_counts(chk)
    finally:
        chk.close()

    size = os.path.getsize(dst)
    print(f"backup -> {dst}  ({size/1024/1024:.2f} MB)")
    print(f"  integrity_check = {integrity}")
    if counts:
        print("  rows: " + ", ".join(f"{k}={v}" for k, v in counts.items()))
    return dst


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", help="user data folder (holds StarwardDatabase.db)")
    ap.add_argument("--label", default="starward")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "backup"))
    ap.add_argument("--inspect-only", action="store_true")
    ap.add_argument("--compare", help="second user data folder to compare against")
    args = ap.parse_args()

    if args.src:
        src_db = os.path.join(args.src, DB_NAME)
        con = connect_rw(src_db)
        print(f"=== {args.label}: {args.src} ===")
        try:
            print(gacha_summary(con))
        finally:
            con.close()
        if not args.inspect_only:
            do_backup(args.src, args.label, os.path.abspath(args.out))

    if args.compare:
        other = os.path.join(args.compare, DB_NAME)
        con = connect_rw(other)
        print(f"=== compare: {args.compare} ===")
        try:
            print(gacha_summary(con))
        finally:
            con.close()

    return 0


if __name__ == "__main__":
    sys.exit(main())
