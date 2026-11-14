# Correção do calendário — OpenHabitTracker pessoal

## Corrigido

- Tarefas recorrentes antigas, que utilizam apenas `RepeatInterval`,
  `RepeatPeriod` e `RepeatCount`, agora aparecem na agenda. Antes só eram
  exibidas recorrências com regra semanal/mensal explícita.
- Recorrências com regra calendárica continuam prevalecendo sobre o intervalo
  antigo, e os dias em que houve execução real permanecem visíveis mesmo
  quando a regra posteriormente é alterada.
- As datas de recorrências baseadas somente em intervalo são **estimativas**,
  identificadas na interface. A repetição é projetada a partir da data de
  início (ou criação) e distribuída ao longo de cada intervalo. Para dias
  exatos, configure uma regra de calendário no editor da tarefa recorrente.
- As tarefas agendadas agora continuam visíveis em seu dia, inclusive as
  concluídas. As excluídas permanecem ocultas.
- Tarefas novas **sem data** aparecem no quadro «Tarefas sem data», sem
  receber uma data arbitrária. O botão «Agendar para o dia selecionado»
  utiliza o serviço de reagendamento existente.
- O mês e a semana mostram títulos abreviados das tarefas e recorrências
  (até três por dia, com indicador para mais entradas). Em telas estreitas,
  fica só a contagem; clicar no dia mostra todas as entradas.

## Compatibilidade

Somente código de consultas, interface, tradução e testes foram alterados.
Nenhuma migração, tabela ou campo do SQLite foi criado ou removido, nem houve
reescrita de IDs, notas, checklists ou históricos de execução.

## Verificações ao atualizar o Flatpak

1. Conferir uma tarefa recorrente já existente, sem regra nova.
2. Criar uma tarefa sem data: deve aparecer no quadro de tarefas sem data.
3. Selecionar uma data e agendar a tarefa: ela deve sair do quadro sem data e
   aparecer no dia escolhido, mantendo o mesmo ID.
4. Concluir uma tarefa agendada: deve continuar visível na agenda com estado
   «Concluído».
5. Verificar tarefas recorrentes com regra semanal específica e histórico.
6. Conferir as listas e notas antigas, inclusive o último item na rolagem.

Use o instalador seguro `scripts/update_flatpak_safe.sh`, que efetua backup
verificado antes da atualização. Não desinstale o Flatpak nem apague seus dados.
