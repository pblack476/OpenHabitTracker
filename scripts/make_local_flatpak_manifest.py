#!/usr/bin/env python3
"""Create a local-source Flatpak manifest without altering the upstream manifest."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = root / "net.openhabittracker.OpenHabitTracker.yaml"
destination = root / "net.openhabittracker.OpenHabitTracker.local.yaml"
text = source.read_text(encoding="utf-8")
before = """      - type: git
        url: https://github.com/Jinjinov/OpenHabitTracker.git
        tag: 1.2.5
        commit: fff3bcd4bf9bd805e53aced0ae4c8d731cc2fc68"""
after = """      - type: dir
        path: ."""
if text.count(before) != 1:
    raise SystemExit("Manifesto mudou: revise a origem antes de compilar. Nenhum arquivo modificado.")
destination.write_text(text.replace(before, after), encoding="utf-8")
print(f"Manifesto local criado: {destination}")
print("ID Flatpak preservado: net.openhabittracker.OpenHabitTracker")
