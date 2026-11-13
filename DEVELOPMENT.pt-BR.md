# Desenvolvimento pessoal do OpenHabitTracker

## Ponto de partida

Preparação iniciada em 13/11/2026 a partir do projeto
[Jinjinov/OpenHabitTracker](https://github.com/Jinjinov/OpenHabitTracker), branch
`main`, commit `77f1d208b14799be9e905ff2dcaee4e059cb8461`.
A versão declarada nesse commit é `1.2.5`.

A branch `personal/dev` é o ponto de trabalho para as adaptações pessoais.
Esta preparação adiciona comandos de desenvolvimento, torna os caminhos do
VS Code portáveis e ajusta um teste de formatação para respeitar os dados
culturais de cada plataforma. As funcionalidades do aplicativo ainda são as do
projeto original. A licença original está em `LICENSE`.

## Requisitos

- SDK do .NET 10. Confira com `dotnet --list-sdks`.
- Git.
- Acesso ao NuGet para restaurar as dependências dos projetos.
- Para abrir o aplicativo desktop no Linux: sessão gráfica e bibliotecas nativas
  exigidas pelo Photino/WebKitGTK. Compilar o projeto não valida essas dependências
  de execução.

Instalação oficial do SDK: <https://dotnet.microsoft.com/download/dotnet/10.0>.

Execute os comandos abaixo na raiz do repositório. No Linux, compile os projetos
individualmente: a solução completa também inclui MAUI, WPF e WinForms, com
requisitos de outras plataformas.

## Começar pela versão de navegador

```bash
dotnet run --project OpenHabitTracker.Blazor.Wasm/OpenHabitTracker.Blazor.Wasm.csproj --launch-profile http
```

O perfil HTTP existente usa `http://localhost:5173`. Essa versão guarda os dados
localmente no navegador, em IndexedDB. Use sempre a mesma origem e o mesmo perfil
de navegador para acessar os mesmos dados. `Ctrl+C` encerra o servidor de
desenvolvimento.

## Compilar o desktop Linux

```bash
dotnet build OpenHabitTracker.Blazor.Photino/OpenHabitTracker.Blazor.Photino.csproj --configuration Debug
dotnet run --project OpenHabitTracker.Blazor.Photino/OpenHabitTracker.Blazor.Photino.csproj
```

O projeto Photino usa a mesma interface Blazor compartilhada. A localização
padrão do banco é `LocalApplicationData/.OpenHabitTracker/OpenHT.db`.
Para uma sessão de desenvolvimento com dados separados, o código existente
também aceita um diretório definido pela variável `SNAP_USER_COMMON`:

```bash
SNAP_USER_COMMON="${TMPDIR:-/tmp}/openhabittracker-dev-$(id -u)" \
  dotnet run --project OpenHabitTracker.Blazor.Photino/OpenHabitTracker.Blazor.Photino.csproj
```

Esse diretório temporário serve apenas para testes; não o use como armazenamento
permanente de hábitos.

## Validar a base

```bash
dotnet test OpenHabitTracker.UnitTests/OpenHabitTracker.UnitTests.csproj --configuration Debug
dotnet build OpenHabitTracker.Blazor.Wasm/OpenHabitTracker.Blazor.Wasm.csproj --configuration Debug
dotnet build OpenHabitTracker.Blazor.Web/OpenHabitTracker.Blazor.Web.csproj --configuration Debug
```

Os comandos restauram as dependências automaticamente. Os testes unitários
incluem testes de componentes com bUnit.

### Resultado da preparação em 13/11/2026

Validação realizada em Linux x64 com SDK .NET `10.0.404`:

- Núcleo, Backup, Blazor, Auth, API Client, EntityFrameworkCore, Blazor.Web e
  UnitTests compilaram.
- A suíte registrou 979 casos: 978 executados e aprovados, zero falhas e um
  `NotExecuted` (`Read_AllDayReminder_PlansTheDateWithoutATime`).
- O teste de preservação de cultura foi corrigido para capturar a formatação
  original antes da chamada. A comparação continua exata, incluindo espaços
  Unicode. O código de produção não precisou de alteração.
- O build específico de WASM restaurou as dependências e compilou as bibliotecas,
  mas não terminou: o MSBuild falhou ao criar/conectar o processo auxiliar de
  `ComputeWasmBuildAssets` (`MSB4216`, seguido de `MSB4027`). Esse alvo ainda
  precisa de validação completa em um ambiente onde o processo auxiliar execute.
- A execução gráfica do Photino e os testes de interface não foram validados.

Neste executor, a inicialização multiprocessada padrão do MSBuild falhou com
`SocketException (13): Permission denied`. A suíte de testes conseguiu executar
em modo serial, com `DOTNET_CLI_USE_MSBUILD_SERVER=0` e as opções `-m:1 -nr:false
-p:UseSharedCompilation=false`. Essas opções não resolveram o build WASM.

Os testes de interface em `OpenHabitTracker.EndToEndTests` exigem uma aplicação
já em execução. Na base original, `BaseTest.cs` usa `http://localhost` na porta
80, enquanto os perfis de desenvolvimento usam 5173 (WASM) e 5260 (Web).
Alinhe esse endereço antes de usar os testes de interface como verificação.

## VS Code

Abra a pasta do repositório e execute `Tasks: Run Task`:

| Tarefa | Finalidade |
| --- | --- |
| `build: WASM` | Compilar a versão de navegador; tarefa de build padrão |
| `build: Server` | Compilar o servidor Blazor |
| `build: Photino` | Compilar o aplicativo desktop |
| `test: UnitTests` | Executar os testes unitários e de componentes |
| `run: WASM (HTTP)` | Iniciar a versão de navegador em localhost:5173 |
| `maui: Build Windows` | Tarefa existente para Windows com MAUI |

As configurações de depuração continuam em `.vscode/launch.json`. A depuração
requer as extensões .NET/C# correspondentes; a configuração MAUI exige seu
ambiente específico.

## Onde desenvolver

| Área | Diretório |
| --- | --- |
| Modelos, regras, serviços e localização | `OpenHabitTracker` |
| Interface compartilhada | `OpenHabitTracker.Blazor` |
| Aplicativo Linux | `OpenHabitTracker.Blazor.Photino` |
| Aplicativo de navegador/PWA | `OpenHabitTracker.Blazor.Wasm` |
| Servidor e autenticação | `OpenHabitTracker.Blazor.Web`, `OpenHabitTracker.Blazor.Auth` |
| Persistência SQLite | `OpenHabitTracker.EntityFrameworkCore` |
| Persistência no navegador | `OpenHabitTracker.IndexedDB` |
| Importação, exportação e backups | `OpenHabitTracker.Backup` |

## Relação com o projeto original

O remoto `upstream` aponta para o projeto original. Quando o fork estiver
criado na conta pessoal, o remoto `origin` deve apontar para ele.

Confira os remotos com `git remote -v`. Para consultar alterações do original:

```bash
git fetch upstream
git log --oneline HEAD..upstream/main
```

Integre atualizações deliberadamente na branch de desenvolvimento e execute
os testes antes de adotá-las. O `docker-compose.yml` original baixa a imagem
publicada pelo autor; ele não compila automaticamente as alterações locais.
