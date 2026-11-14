# Lembretes Flatpak com o aplicativo fechado (Q17/Q18)

## Como funciona

O Flatpak utiliza o portal org.freedesktop.portal.Notification para **mostrar**
notificações. Ele não disponibiliza um agendador de alarmes. Por isso, o fork
oferece `OpenHT --notification-scan`, que funciona sem janela e consulta o
SQLite de forma não destrutiva, e um timer de usuário do systemd para executar
a verificação aproximadamente uma vez por minuto.

O instalador seguro `scripts/update_flatpak_safe.sh` pode instalar esse timer
após atualizar o aplicativo. Manualmente:

```bash
bash scripts/install_flatpak_notification_timer.sh
systemctl --user status openhabittracker-notifications.timer
journalctl --user -u openhabittracker-notifications.service -n 50 --no-pager
```

Desativar sem alterar nenhuma tarefa:

```bash
bash scripts/disable_flatpak_notification_timer.sh
```

Os lembretes com o aplicativo aberto continuam sendo tratados pelo relógio
interno. O verificador identifica quando a janela está aberta e evita duplicar
avisos. Eventos já disparados são registrados separadamente no diretório do
aplicativo; o histórico de tarefas não é alterado por uma mera verificação.

As preferências da UI criadas em versões anteriores no localStorage são
migradas, sem exclusão da cópia antiga, para arquivos de workspace na pasta
de dados do aplicativo. Isso permite que os lembretes individuais sejam
interpretados pelo processo sem interface. O banco SQLite mantém o mesmo
esquema e identificadores.

## Limitações

- É necessário um **systemd --user** ativo e o Flatpak instalado no escopo do
  usuário. Sem ele, o timer não pode ser ativado automaticamente.
- A máquina precisa estar ligada e a sessão do usuário precisa estar ativa;
  o processo não desperta computadores desligados nem força execução após
  logout. Após suspensão, até 30 minutos de avisos atrasados podem ser
  processados no próximo disparo.
- A hora não é exata: o timer é executado aproximadamente a cada minuto.
- Notificações podem ser bloqueadas pelo ambiente gráfico. O portal não confirma
  se o usuário viu o aviso.
- Ações de **concluir** e **adiar** via botões precisam de uma aplicação
  escutando os eventos do portal; nem todos os ambientes entregam cliques
  quando o app está fechado. O clique padrão pode reabrir o aplicativo, mas
  a disponibilidade depende do portal do desktop.
- Metadados de workspace são locais ao dispositivo. Sincronização remota
  de regras e lembretes personalizados ainda não foi implementada.

As rotinas de backup e restauração do SQLite estão disponíveis separadamente.
Não use `flatpak uninstall --delete-data`.
