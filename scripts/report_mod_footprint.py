#!/usr/bin/env python3
"""Report mod package volume and rough SQLite footprint estimates.

Usage:
  python scripts/report_mod_footprint.py --input _references/sample_mods
"""
from __future__ import annotations

import argparse
import sqlite3
import tempfile
from pathlib import Path
from zipfile import ZipFile, BadZipFile


def iter_zip_files(root: Path):
    return sorted([p for p in root.rglob("*.zip") if p.is_file()])


def summarize_zip(path: Path):
    files = 0
    comp = 0
    uncomp = 0
    with ZipFile(path, "r") as zf:
        for info in zf.infolist():
            if info.is_dir():
                continue
            files += 1
            comp += info.compress_size
            uncomp += info.file_size
    return files, comp, uncomp


def estimate_sqlite_bytes(total_files: int, total_uncompressed: int) -> int:
    """Populate a temp sqlite file with file-ish rows and return resulting DB size."""
    with tempfile.TemporaryDirectory(prefix="dmm-footprint-") as td:
        db_path = Path(td) / "estimate.db"
        con = sqlite3.connect(db_path)
        cur = con.cursor()
        cur.executescript(
            """
            PRAGMA journal_mode = OFF;
            PRAGMA synchronous = OFF;
            CREATE TABLE FileInfo (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL,
              DTStamp TEXT NOT NULL,
              Size INTEGER NOT NULL,
              RelativePath TEXT NOT NULL
            );
            CREATE INDEX IX_FileInfo_RelativePath ON FileInfo(RelativePath);
            """
        )

        # Keep insertion cost bounded for huge samples while preserving estimate shape.
        sample_rows = min(total_files, 200000)
        avg_size = (total_uncompressed // total_files) if total_files else 0
        rows = [
            (f"file_{i}.bin", "2026-01-01T00:00:00Z", avg_size, f"Data\\Path\\file_{i}.bin")
            for i in range(sample_rows)
        ]
        cur.executemany(
            "INSERT INTO FileInfo(Name, DTStamp, Size, RelativePath) VALUES (?, ?, ?, ?)",
            rows,
        )
        con.commit()

        cur.execute("PRAGMA page_count")
        page_count = cur.fetchone()[0]
        cur.execute("PRAGMA page_size")
        page_size = cur.fetchone()[0]
        con.close()

        raw = page_count * page_size
        if sample_rows == 0:
            return 0
        # Scale up if sampled.
        return int(raw * (total_files / sample_rows))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Folder containing mod zip outputs")
    args = parser.parse_args()

    root = Path(args.input)
    if not root.exists():
        print(f"ERROR: input folder does not exist: {root}")
        return 2

    zips = iter_zip_files(root)
    if not zips:
        print(f"No zip files found under: {root}")
        return 0

    total_files = 0
    total_comp = 0
    total_uncomp = 0

    print("Zip summary:")
    skipped = 0
    for z in zips:
        try:
            files, comp, uncomp = summarize_zip(z)
        except BadZipFile:
            skipped += 1
            print(f"- {z.name}: skipped (invalid zip format)")
            continue
        total_files += files
        total_comp += comp
        total_uncomp += uncomp
        print(f"- {z.name}: files={files:,} compressed={comp:,} bytes uncompressed={uncomp:,} bytes")

    est_db = estimate_sqlite_bytes(total_files, total_uncomp)

    print("\nTotals:")
    print(f"- Zip count: {len(zips):,}")
    print(f"- Skipped invalid zips: {skipped:,}")
    print(f"- File count: {total_files:,}")
    print(f"- Compressed bytes: {total_comp:,}")
    print(f"- Uncompressed bytes: {total_uncomp:,}")
    print(f"- Approx SQLite footprint (FileInfo-like table + path index): {est_db:,} bytes")
    print("\nNote: current onboarding path persists mod-level records (ManagedModCatalog), not per-file rows.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
