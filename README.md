# DevOps Triage Console (Version 1)

A C# console application that uses Microsoft Semantic Kernel and local Ollama models to triage software issue reports with three cooperating AI agents: **Intake**, **Research**, and **Resolver**.

Everything runs locally: local LLM inference through Ollama, local SQLite storage for known issues, and local mock ticket data. No API key or cloud service is required.

For background on the design decisions behind this project, see `project-overview.md`. For the planned next iteration, see `version-2.md`.

---

## Features

- Three specialized `ChatCompletionAgent` instances running in a fixed, explicit sequence.
- Two read-only Semantic Kernel plugins exposed as native C# tools:
  - `KnownIssuesPlugin` — searches a local SQLite known-issues table.
  - `TicketApiPlugin` — searches local mock open-ticket data.
- Local-only inference via the official `Microsoft.SemanticKernel.Connectors.Ollama` connector.
- Automatically seeded SQLite knowledge base on first run.
- Console logging of each agent step for easy debugging.

---

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/) matching the project's target framework (see `SkDevOpsTriage.Console.csproj`).
- [Ollama](https://ollama.com/) installed and running locally.
- A local Ollama model that explicitly supports **tool/function calling**, since the Research Agent depends on it.

Verify your environment before running the project:

```bash
dotnet --version
ollama --version
ollama list
```

Confirm Ollama is reachable:

```bash
curl http://localhost:11434/api/tags
```

If you don't have a suitable model yet, pull one:

```bash
ollama pull <your-model-name>
```

---

## Project Structure

```text
SkDevOpsTriage.Console/
├── Agents/
│   └── AgentFactory.cs        # Builds Intake, Research, and Resolver agents
├── Data/
│   └── KnownIssueRepository.cs # SQLite queries for known issues
├── Models/
│   ├── KnownIssue.cs
│   └── OpenTicket.cs
├── Plugins/
│   ├── KnownIssuesPlugin.cs   # [KernelFunction] wrapper around KnownIssueRepository
│   └── TicketApiPlugin.cs     # [KernelFunction] wrapper around MockTicketApiClient
├── Services/
│   ├── DatabaseInitializer.cs # Creates and seeds triage.db on first run
│   ├── MockTicketApiClient.cs # In-memory sample ticket data
│   └── TriageWorkflow.cs      # Orchestrates the Intake → Research → Resolver pipeline
├── appsettings.json           # Ollama endpoint/model + SQLite connection string
├── Program.cs                 # Composition root and console loop
└── SkDevOpsTriage.Console.csproj
```

---

## Configuration

All configuration lives in `appsettings.json`, in the same folder as `Program.cs` and the `.csproj` file:

```json
{
  "Ollama": {
    "ModelId": "qwen2.5-coder:7b",
    "Endpoint": "http://localhost:11434"
  },
  "ConnectionStrings": {
    "KnowledgeBase": "Data Source=triage.db"
  }
}
```

| Key | Description |
|---|---|
| `Ollama:ModelId` | The exact model tag as shown by `ollama list` |
| `Ollama:Endpoint` | The local Ollama server address (default `http://localhost:11434`) |
| `ConnectionStrings:KnowledgeBase` | SQLite connection string; the database file is created automatically on first run |

**Important:** `appsettings.json` must be copied to the build output directory or the app will fail with `Ollama:ModelId is missing`. Confirm your `.csproj` includes:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </None>
</ItemGroup>
```

---

## Setup

```bash
git clone <your-repository-url>
cd SkDevOpsTriage/SkDevOpsTriage.Console
dotnet restore
```

Update `appsettings.json` with your installed model's exact tag before running.

---

## Running the App

```bash
dotnet run
```

You should see:

```text
Semantic Kernel DevOps Triage Console — Ollama local mode
Model: qwen2.5-coder:7b
Endpoint: http://localhost:11434
Enter an issue report. Submit an empty line to quit.

>
```

Paste an issue report, for example:

```text
The desktop application freezes for 20-30 seconds when users export more than 50,000 records to Excel. It happens on Windows 11. Severity seems high because finance cannot complete month-end reporting.
```

The console will print the Intake analysis, Research findings, and final Resolution plan in sequence. Press Enter on an empty line to exit.

---

## How a Request Flows Through the App

```text
1. User enters an issue report in the console.
2. IntakeAgent classifies category, severity, product area, and escalation need.
3. ResearchAgent receives the report + Intake analysis, then calls:
      - SearchKnownIssuesAsync (SQLite)
      - SearchOpenTicketsAsync (mock data)
4. ResolverAgent receives the report + Intake analysis + Research findings,
   and produces the final triage summary and action plan.
5. All three outputs are printed together to the console.
```

---

## Troubleshooting

### `Ollama:ModelId is missing in appsettings.json`

- Confirm `appsettings.json` sits next to `Program.cs` and the `.csproj` file.
- Confirm the `CopyToOutputDirectory` setting shown above is present.
- Run `dotnet clean && dotnet build` and check that `appsettings.json` exists in `bin/Debug/<tfm>/`.

### Cannot connect to Ollama / `HttpRequestException`

- Make sure Ollama is running: `ollama list` should return without error.
- Confirm the endpoint in `appsettings.json` matches your Ollama server address, with no `/v1` suffix.
- Test directly: `curl http://localhost:11434/api/tags`.

### Model responds but never calls a tool

- Confirm the configured model explicitly supports tool/function calling — not all models do.
- Test `KnownIssuesPlugin` and `TicketApiPlugin` by calling their underlying repository/client methods directly in C# to rule out a data problem.
- Try a different, known tool-capable model before assuming the code is at fault.

### SQLite has no results

- Delete `triage.db` and re-run if you changed the seed data or schema — `DatabaseInitializer` only seeds an empty table.
- Confirm `DatabaseInitializer.InitializeAsync()` runs before the workflow starts.

---

## Safety Notes

- Both plugins are strictly **read-only**. Neither can create, modify, or delete any data.
- No plugin executes arbitrary SQL — all queries are parameterized.
- No agent has access to the file system, network writes, or external services beyond the local Ollama endpoint and local SQLite file.
- This project is intended for local learning and experimentation, not production incident management.

---

## Roadmap

Version 1 is intentionally minimal: no persistence between runs, Markdown-only agent output, keyword-only search, and no UI beyond the console. The planned Version 2 adds structured JSON agent output, persistent triage history in SQLite, a local RAG knowledge base backed by Ollama embeddings, evidence-based research records, and a policy/approval layer. See `version-2.md` for the full design.
