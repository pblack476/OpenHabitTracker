#!/usr/bin/env bash
# Instala um timer apenas para o usuário atual; não modifica o banco.
set -Eeuo pipefail
APP="net.openhabittracker.OpenHabitTracker"
UNIT="openhabittracker-notifications"
BASE="${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user"

if ! command -v systemctl >/dev/null 2>&1 || ! systemctl --user show-environment >/dev/null 2>&1; then
    echo "systemd --user indisponível: lembretes com app fechado não serão ativados." >&2
    exit 2
fi
if ! flatpak info --user "$APP" >/dev/null 2>&1; then
    echo "Flatpak de usuário não instalado: não configurei o timer." >&2
    exit 2
fi

mkdir -p "$BASE"
cat > "$BASE/$UNIT.service" <<'SERVICE'
[Unit]
Description=OpenHabitTracker reminder scan (Flatpak, user session)
After=graphical-session.target

[Service]
Type=oneshot
ExecStart=/usr/bin/flatpak run --user net.openhabittracker.OpenHabitTracker --notification-scan
TimeoutStartSec=45
SERVICE

cat > "$BASE/$UNIT.timer" <<'TIMER'
[Unit]
Description=Check OpenHabitTracker reminders each minute while logged in

[Timer]
OnCalendar=*-*-* *:*:00
Persistent=true
AccuracySec=15s
Unit=openhabittracker-notifications.service

[Install]
WantedBy=timers.target
TIMER

systemctl --user daemon-reload
systemctl --user enable --now "$UNIT.timer"
systemctl --user start "$UNIT.service"
printf 'Ativo: systemctl --user status %s.timer\n' "$UNIT"
printf 'Desativar: systemctl --user disable --now %s.timer\n' "$UNIT"
