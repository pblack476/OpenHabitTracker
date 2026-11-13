#!/usr/bin/env python3
"""Consistent, read-only backup of OpenHabitTracker's local SQLite data (Linux)."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import sys
from datetime import datetime
from urllib.parse import quote


def candidates() -> list[Path]:
    home = Path.home()
    xdg = Path(os.environ.get("XDG_DATA_HOME", home / ".local/share"))
    flatpak = home / ".var/app/net.openhabittracker.OpenHabitTracker"
    return [
        xdg / ".OpenHabitTracker/OpenHT.db",
        home / ".local/share/.OpenHabitTracker/OpenHT.db",
        flatpak / "data/.OpenHabitTracker/OpenHT.db",
        flatpak / "data/OpenHT.db",
        flatpak / "data/.local/share/.OpenHabitTracker/OpenHT.db",
        home / "snap/openhabittracker/common/OpenHT.db",
        home / "snap/openhabittracker/current/.local/share/.OpenHabitTracker/OpenHT.db",
    ]


def read_only(path: Path) -> sqlite3.Connection:
    return sqlite3.connect(f"file:{quote(str(path), safe='/')}?mode=ro", uri=True, timeout=15)


def database_counts(db: sqlite3.Connection) -> dict[str, int]:
    existing = {row[0].lower() for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    counts = {}
    for name in ("Notes", "Tasks", "Habits", "Items", "Categories", "Times"):
        if name.lower() in existing:
            counts[name] = db.execute(f'SELECT COUNT(*) FROM "{name}"').fetchone()[0]
    return counts


def backup_one(source_path: Path, destination: Path) -> dict:
    temporary = destination.with_suffix(".partial")
    try:
        with read_only(source_path) as source:
            with sqlite3.connect(temporary) as target:
                source.backup(target)
        with read_only(temporary) as verified:
            check = verified.execute("PRAGMA integrity_check").fetchone()[0]
            if check != "ok":
                raise RuntimeError(f"Falha na integridade do backup: {check}")
            counts = database_counts(verified)
        temporary.replace(destination)
        sha256 = hashlib.sha256(destination.read_bytes()).hexdigest()
        return {"source": str(source_path), "backup": str(destination), "sha256": sha256, "counts": counts}
    finally:
        temporary.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="Backup SQLite do OpenHabitTracker sem modificar o banco original.")
    parser.add_argument("--database", action="append", type=Path, help="Caminho adicional ou específico do OpenHT.db")
    parser.add_argument("--output", type=Path, default=Path.home() / "OpenHabitTracker-backups", help="Diretório dos backups")
    parser.add_argument("--list", action="store_true", help="Apenas listar bancos encontrados; não criar backup")
    args = parser.parse_args()

    sources = args.database if args.database else candidates()
    available = []
    seen = set()
    for path in sources:
        path = path.expanduser().resolve()
        if path.is_file() and path not in seen:
            available.append(path)
            seen.add(path)

    if not available:
        print("Nenhum OpenHT.db encontrado. Se usa a versão web, exporte os dados em JSON pelo aplicativo.", file=sys.stderr)
        return 2

    for source in available:
        print(f"Banco identificado: {source}")
    if args.list:
        return 0

    os.umask(0o077)
    folder = args.output.expanduser() / ("backup-" + datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
    folder.mkdir(parents=True, exist_ok=False, mode=0o700)
    records = []
    try:
        for index, source in enumerate(available, start=1):
            destination = folder / f"OpenHT-{index}.db"
            record = backup_one(source, destination)
            records.append(record)
            print(f"Backup verificado: {destination} | {record['counts']}")
        manifest = {"created_at": datetime.now().isoformat(), "databases": records}
        (folder / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"Backup concluído: {folder}")
        return 0
    except Exception as exc:
        print(f"ERRO: backup incompleto. NENHUM banco de origem foi modificado. {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
