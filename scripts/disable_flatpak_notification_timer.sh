#!/usr/bin/env bash
set -Eeuo pipefail
UNIT="openhabittracker-notifications"
systemctl --user disable --now "$UNIT.timer" || true
systemctl --user stop "$UNIT.service" || true
echo "Verificação em segundo plano desativada. Notas e tarefas não foram alteradas."
