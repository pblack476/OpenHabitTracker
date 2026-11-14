# Atualização segura do OpenHabitTracker pessoal — Linux

O patch desta versão modifica UI, ordenação e ações de copiar/colar; **não remove
dados, não adiciona migrações de banco, não renomeia tabelas e não altera o
identificador do aplicativo**. Os nomes `Habit` e `Habits` permanecem na
persistência e no código, aparecendo como “Tarefas recorrentes” na UI.
**A ausência de migração neste patch não garante compatibilidade com versões
instaladas mais antigas que o commit-base do fork**: faça backup antes.

## 1. Identificar a instalação

```bash
flatpak info net.openhabittracker.OpenHabitTracker 2>/dev/null || true
snap list openhabittracker 2>/dev/null || true
command -v OpenHT || true
```

Se o aplicativo é **web/PWA**, o banco é **IndexedDB do navegador**; o
script de SQLite não serve. Use o comando de exportação do próprio app
(Data → Export) para salvar **JSON** antes de trocar de origem, navegador ou PWA.
Não limpe os dados do site, não remova o perfil e não troque a origem.

## 2. Backup verificado antes de qualquer instalação

Feche o OpenHabitTracker e, na pasta do checkout desta branch, execute:

```bash
python3 scripts/backup_openhabittracker.py --list
python3 scripts/backup_openhabittracker.py
```

O script procura os diretórios nativos, Flatpak e Snap conhecidos. Se o banco
estiver fora deles, passe `--database /CAMINHO/COMPLETO/OpenHT.db`.
Ele faz backup consistente pelo SQLite, verifica `PRAGMA integrity_check`,
salva um `manifest.json` com origem e contagem dos registros, e não escreve no
arquivo original. Os backups ficam em `~/OpenHabitTracker-backups/`, com
permissões restritas. **Não avance se o backup falhar.**

Se usa sincronização remota, exporte também um JSON pelo app: o banco local pode
não conter todos os dados hospedados no servidor.

## 3. Aplicar o patch ao código-fonte

Se estiver no fork na branch `personal/dev`, as alterações já estão presentes;
não aplique o patch duas vezes. Em outra cópia na mesma base, use:

```bash
git apply --check OpenHabitTracker-pessoal.patch
git apply OpenHabitTracker-pessoal.patch
```

O patch de código-fonte **não** modifica automaticamente o Flatpak, Snap,
executável distribuído, nem o banco do aplicativo instalado.

## 4. Atualizar sem alterar a localização dos dados

### Flatpak

**Importante:** atualizar o Flatpak exige recompilar usando o **mesmo ID**
`net.openhabittracker.OpenHabitTracker`. Executar um binário nativo isolado
criaria outra área de dados e pareceria que suas notas desapareceram.

Com `flatpak-builder` e os runtimes/SDKs Flatpak instalados:

```bash
python3 scripts/make_local_flatpak_manifest.py
flatpak-builder --user --install --install-deps-from=flathub --force-clean \
  .flatpak-build net.openhabittracker.OpenHabitTracker.local.yaml
flatpak run net.openhabittracker.OpenHabitTracker
```

O manifesto local usa **os arquivos deste checkout** e o mesmo ID. O manifesto
original continua intocado. A instalação em escopo de usuário pode sobrepor
uma instalação de sistema; não execute `flatpak uninstall --delete-data`.
As dependências são baixadas durante a compilação; o processo depende da
configuração Flatpak/Flathub no seu Linux.

### Snap

Mantenha o nome `openhabittracker` e o mesmo `SNAP_USER_COMMON`. Depois do
backup, é possível construir o snap com `snapcraft`; a substituição de um snap
instalado exige instalar o pacote localmente com as permissões necessárias.
**Não remova o snap existente com `--purge`** e não copie o banco para a pasta
de versão `~/snap/openhabittracker/<revisão>`. O caminho persistente é
`~/snap/openhabittracker/common/OpenHT.db`.

### Execução nativa (.NET/Photino)

Compile com o .NET 10, e substitua os **binários** do aplicativo, não o banco:

```bash
dotnet publish OpenHabitTracker.Blazor.Photino/OpenHabitTracker.Blazor.Photino.csproj \
  -c Release -f net10.0 -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish-personal
```

Para ARM64, troque `linux-x64` por `linux-arm64`. Instale o conteúdo de
`publish-personal` no diretório de binários da sua instalação existente,
guardando cópia da versão anterior. **Não substitua** o diretório de dados,
por padrão `${XDG_DATA_HOME:-~/.local/share}/.OpenHabitTracker/`, nem
`OpenHT.db`, arquivos `-wal` ou `-shm`.

## 5. Verificar e reverter

Abra a nova versão e confirme as contagens de notas, tarefas, recorrentes,
categorias e históricos, além de abrir alguns itens com checklist. Teste
fechar/reabrir. Se necessário, reinstale os binários anteriores e, somente
se houve alteração indesejada no banco, restaure a cópia de `OpenHT-*.db`
do backup identificado em `manifest.json`, **com o aplicativo fechado**,
respeitando o caminho `source`. Nunca copie por cima de um banco aberto.

## Lembretes

O código original já oferece lembrete antecipado configurável, resumo diário
e configurações de permissão. A nova barra com ícone de sino facilita o
acesso. No Linux/Photino e no navegador, os avisos locais precisam que o
aplicativo esteja aberto; este patch não promete agendamento com app fechado.

## Validação

A fonte de verdade dos dados continua sendo o `OpenHT.db` original. O patch
não altera o modelo persistido, as entidades, o `DbContext`, nem cria nova
migração. Ao compilar, execute:

```bash
dotnet test OpenHabitTracker.UnitTests/OpenHabitTracker.UnitTests.csproj
```

A suíte cobre também clonagem sem IDs/histórico e ordenação por urgência.

## Verificador de notificações sem a janela aberta

Depois da atualização, se `systemd --user` estiver disponível, o instalador configura
`openhabittracker-notifications.timer`. Veja o estado:

```bash
systemctl --user status openhabittracker-notifications.timer
journalctl --user -u openhabittracker-notifications.service -n 30 --no-pager
```

O timer atua aproximadamente uma vez por minuto durante a sessão gráfica.
Ele não funciona com o computador desligado e pode entregar lembretes atrasados
após suspensão. Se o sistema não tiver `systemd --user`, a atualização do
aplicativo não falha, mas os avisos continuam dependentes da janela aberta.
O programa usa o portal de notificações, e a confirmação de visualização não
é observável pelo aplicativo.

Veja `BACKGROUND-NOTIFICATIONS.pt-BR.md` para detalhes e desativação.
