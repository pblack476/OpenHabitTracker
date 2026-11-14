#!/usr/bin/env bash
# Atualizador Flatpak do fork pessoal do OpenHabitTracker.
# Não desinstala o app, não exclui dados e só instala após backup SQLite validado.
set -Eeuo pipefail
umask 077

APP_ID="net.openhabittracker.OpenHabitTracker"
REVISION="29b41c9da22a0bcd0dfc4090c59ca8b286383d78"
BACKUP_ROOT="$HOME/OpenHabitTracker-backups"
CACHE_ROOT="${XDG_CACHE_HOME:-$HOME/.cache}"

step() { printf '\n==> %s\n' "$*"; }
die() { printf '\nERRO: %s\n' "$*" >&2; exit 1; }

[[ "$EUID" -ne 0 ]] || die "Execute como usuário normal (sem sudo). O script solicita sudo só se precisar instalar flatpak-builder."
for tool in flatpak curl python3 tar find; do
    command -v "$tool" >/dev/null 2>&1 || die "Comando obrigatório ausente: $tool"
done

if ! flatpak info --user "$APP_ID" >/dev/null 2>&1 && \
   ! flatpak info --system "$APP_ID" >/dev/null 2>&1; then
    die "O Flatpak $APP_ID não foi encontrado. Nenhuma atualização foi feita."
fi

if flatpak ps --columns=application 2>/dev/null | grep -Fxq "$APP_ID"; then
    die "Feche o OpenHabitTracker antes de atualizar. Não é seguro atualizar enquanto ele está aberto."
fi

DATA_ROOT="$HOME/.var/app/$APP_ID"
[[ -d "$DATA_ROOT" ]] || die "Diretório de dados Flatpak ausente: $DATA_ROOT. Nenhuma atualização foi feita."

mapfile -d '' DATABASES < <(find "$DATA_ROOT" -type f -name 'OpenHT.db' -print0)
(("${#DATABASES[@]}" > 0)) || die "Nenhum banco OpenHT.db foi encontrado em $DATA_ROOT. Não vou instalar sem confirmar um backup."

step "Instalação Flatpak identificada; ${#DATABASES[@]} banco(s) encontrado(s)"
mkdir -p "$CACHE_ROOT" "$BACKUP_ROOT"
WORK="$(mktemp -d "$CACHE_ROOT/openhabittracker-update.XXXXXXXX")"
SOURCE="$WORK/source"
mkdir -p "$SOURCE"

flatpak info --user "$APP_ID" >"$WORK/flatpak-original-user.txt" 2>&1 || true
flatpak info --system "$APP_ID" >"$WORK/flatpak-original-system.txt" 2>&1 || true
printf 'Versão do fork: %s\n' "$REVISION" >"$WORK/revision.txt"

step "Baixando código validado do fork pessoal (commit $REVISION)"
curl --fail --location --silent --show-error --retry 3 \
    "https://codeload.github.com/pblack476/OpenHabitTracker/tar.gz/$REVISION" \
    --output "$WORK/source.tar.gz"
tar -xzf "$WORK/source.tar.gz" -C "$SOURCE" --strip-components=1

[[ -f "$SOURCE/scripts/backup_openhabittracker.py" ]] || die "Script de backup não encontrado no código baixado."
[[ -f "$SOURCE/net.openhabittracker.OpenHabitTracker.yaml" ]] || die "Manifesto Flatpak não encontrado."

step "Criando e verificando o backup ANTES de alterar a instalação"
BACKUP_ARGS=()
for database in "${DATABASES[@]}"; do
    printf '  Banco original: %s\n' "$database"
    BACKUP_ARGS+=(--database "$database")
done
python3 "$SOURCE/scripts/backup_openhabittracker.py" \
    --output "$BACKUP_ROOT" "${BACKUP_ARGS[@]}" | tee "$WORK/resultado-backup.txt"

step "Verificando ferramentas de compilação"
if ! command -v flatpak-builder >/dev/null 2>&1; then
    if ! command -v sudo >/dev/null 2>&1; then
        die "flatpak-builder ausente. Instale-o pelo gerenciador de pacotes e execute novamente."
    fi
    if command -v apt-get >/dev/null 2>&1; then
        sudo apt-get update
        sudo apt-get install -y flatpak-builder
    elif command -v dnf >/dev/null 2>&1; then
        sudo dnf install -y flatpak-builder
    elif command -v pacman >/dev/null 2>&1; then
        sudo pacman -S --needed --noconfirm flatpak-builder
    elif command -v zypper >/dev/null 2>&1; then
        sudo zypper --non-interactive install flatpak-builder
    else
        die "flatpak-builder ausente; não reconheci o gerenciador de pacotes. Dados preservados."
    fi
fi
command -v flatpak-builder >/dev/null 2>&1 || die "Não foi possível instalar flatpak-builder."

step "Configurando Flathub (somente para este usuário)"
flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo

step "Preparando manifesto do fork, com o mesmo ID da instalação original"
python3 "$SOURCE/scripts/make_local_flatpak_manifest.py"
MANIFEST="$SOURCE/net.openhabittracker.OpenHabitTracker.local.yaml"
grep -Fxq 'id: net.openhabittracker.OpenHabitTracker' "$MANIFEST" || die "ID do aplicativo inesperado. Instalação cancelada."
grep -Fq 'path: .' "$MANIFEST" || die "Manifesto não aponta para o código local. Instalação cancelada."
if grep -Fq 'url: https://github.com/Jinjinov/OpenHabitTracker.git' "$MANIFEST"; then
    die "O manifesto tenta baixar o projeto original em vez do fork. Instalação cancelada."
fi

step "Compilando e instalando Flatpak sem desinstalar nem limpar dados"
# O build e o cache ficam fora de SOURCE para evitar copiar o diretório de build para dentro dele.
(
    cd "$WORK"
    flatpak-builder --user --install --install-deps-from=flathub --force-clean \
        "$WORK/flatpak-build" "$MANIFEST" 2>&1 | tee "$BACKUP_ROOT/flatpak-build-$(date +%Y%m%d-%H%M%S).log"
)

flatpak info --user "$APP_ID" >/dev/null 2>&1 || die "Build finalizou, mas a instalação do Flatpak do usuário não foi localizada."

step "ATUALIZAÇÃO CONCLUÍDA"
printf 'Dados preservados em: %s\n' "$DATA_ROOT"
printf 'Backups verificados em: %s\n' "$BACKUP_ROOT"
printf 'Log e informações da versão anterior em: %s\n' "$WORK"
printf '\nAbrindo a versão nova...\n'
flatpak run --user "$APP_ID"
