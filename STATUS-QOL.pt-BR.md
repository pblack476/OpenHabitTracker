# Entrega QoL — OpenHabitTracker pessoal (Linux/Flatpak)

## Escopo implementado na branch `personal/dev`

| Código | Descrição | Implementação |
|---|---|---|
| Q02 | Atalhos de teclado | Navegação, pesquisa, captura e desfazer |
| Q03 | Edição diretamente na lista | Títulos, prioridades e data/hora da tarefa |
| Q04 | Duplicação com um clique | Notas, tarefas e recorrentes, sem reutilização de IDs |
| Q05 | Colagem de múltiplas linhas | Tarefas e checklists, com limites de quantidade e tamanho |
| Q06 | Ações em lote | Conclusão, reagendamento, prioridade, categoria, exclusão lógica |
| Q07 | Desfazer | Histórico por sessão, por conta, inclusive lote |
| Q08 | Central Hoje | Atrasadas, hoje, próximos dias e recorrentes |
| Q09 | Reagendamento rápido | Hoje, amanhã e próxima segunda-feira |
| Q10 | Calendário | Mês, semana e dia com abertura e conclusão |
| Q11 | Filtros e visualizações salvos | Salvar, aplicar e excluir visualizações no workspace |
| Q12 | Favoritos/fixados | Fixação de itens nas listas |
| Q13 | Ordenação manual | Arrastar no modo manual por lista |
| Q14 | Recorrências de calendário | Dias da semana, dia do mês e dias úteis |
| Q15 | Pular/adiar ocorrência | Exceções locais por data |
| Q16 | Lembretes por compromisso | Sobrescrita de lead time por tarefa, incluindo desligar |
| Q17 | Agir pela notificação | Botões Adiar 10 min e Concluir no Linux quando o aplicativo está ativo |
| Q18 | Alertas com a janela fechada | Verificação sem interface gráfica com timer systemd --user |
| Q19 | Densidade da interface | Compacta/confortável |
| Q20 | Menu de ações por item | Copiar, duplicar, editar, reagendar, fixar e modelos |
| Q21 | Modelos reutilizáveis | Tarefas e checklists recorrentes |
| Q22 | Backup e restauração | SQLite verificado e arquivos de preferências da versão pessoal |

## Compatibilidade

- Identificador Flatpak mantido como `net.openhabittracker.OpenHabitTracker`.
- A estrutura de dados SQLite existente não foi renomeada, recriada ou apagada.
- Backups automáticos usam a API de backup do SQLite; o instalador recusa
  atualizar sem um backup verificado.
- Regras, modelos, favoritos e lembretes específicos são dados do *workspace*
  local; no Linux/Photino são salvos em arquivos JSON separados. O aplicativo
  migra preferências antigas do localStorage do WebView de forma não destrutiva.
- O backup do SQLite acompanha os arquivos JSON de workspace quando já
  existem; a restauração verifica seus checksums.

## Validação necessária no computador do usuário

Mesmo quando testes e compilação nativa forem aprovados no CI, isso **não
valida integralmente** o aplicativo gráfico no Flatpak instalado nem o portal de
notificações do desktop específico. Depois do update, confirmar:

1. Notas, tarefas, checklists, prioridades e históricos intactos.
2. Reabrir o aplicativo e conferir as listas no final da rolagem.
3. Testar ações, edição rápida, desfazer, atalhos e colagem em lote.
4. Testar agenda e regras em datas de virada do mês.
5. Configurar tarefa com horário e lembrete em 2 minutos; fechar a janela,
   conferir status do timer systemd e validar a notificação.
6. Testar os botões de ação em uma notificação *enquanto a janela está aberta*.

## Limitações reconhecidas

- Timer depende de `systemd --user` e sessão gráfica ativa; não desperta
  computador desligado. Após suspensão pode haver atraso de até a próxima
  execução do verificador.
- O portal de notificações não confirma se o aviso foi visualizado.
- Os botões de adiar/concluir dependem de um processo que receba o sinal do
  portal. O verificador temporário envia avisos, mas não exporta um serviço de
  ativação D-Bus permanente para cliques de botões após encerrar.
- As preferências pessoais do workspace ainda não são sincronizadas entre
  dispositivos pelo servidor remoto. O SQLite e a sincronização original
  continuam independentes delas.
- A validação visual/integração em uma instalação Flatpak real precisa ser
  realizada no computador de destino.

## Diagnóstico

```bash
systemctl --user status openhabittracker-notifications.timer
journalctl --user -u openhabittracker-notifications.service -n 50 --no-pager
flatpak run --user net.openhabittracker.OpenHabitTracker --notification-scan
flatpak run --user net.openhabittracker.OpenHabitTracker --list-backups
```

Para instalar/atualizar com backup, use
`scripts/update_flatpak_safe.sh`; para detalhes, consulte
`UPGRADE.pt-BR.md` e `BACKGROUND-NOTIFICATIONS.pt-BR.md`.
