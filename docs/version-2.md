# DevOps Triage Console — Version 2 Design Guide

## Local-First Incident Intelligence Workbench

Version 1 proved that Semantic Kernel agents, Ollama, plugins, and a sequential Intake → Research → Resolver workflow can work together in a C# console app. Version 2 turns that proof of concept into a small, reliable local application: structured agent output, persistent triage history, a local RAG knowledge base, evidence-backed research, a policy/approval gate, and a real command-line interface.

The guiding principle for Version 2: **the language model proposes, and ordinary C# code decides.** Agents interpret language, search knowledge, and draft plans. Deterministic code validates output, applies policy, persists history, and controls anything resembling a side effect.

---

## 1. Why Move Beyond Version 1

Version 1 works as a learning demo, but it has real limitations if you want to use it as a foundation for a genuine tool:

- Agent output is free-form Markdown, so code cannot safely branch on severity, category, or escalation.
- Nothing is persisted. Every run disappears once the console prints it.
- Knowledge search is keyword-only, so differently worded reports about the same known issue can be missed.
- The Research Agent’s tool results are summarized in prose instead of being kept as structured, auditable evidence.
- There is no policy layer, so “should this be escalated” lives only inside a prompt.
- There is no history, comparison, or evaluation across runs.
- The console only supports “paste one report, get one answer.”

Version 2 fixes each of these while keeping the same domain (software issue triage) and the same core technology choices (Semantic Kernel, Ollama, SQLite).

---

## 2. Version 2 Goals

1. Every agent decision that matters to application logic must be structured and validated, not inferred from prose.
2. Every triage run must be saved locally with enough detail to debug or audit it later.
3. Knowledge search must combine exact keyword search with local semantic search (RAG) over Markdown documents.
4. Evidence must be tracked as discrete, source-attributed records, not just summarized text.
5. Escalation and review decisions must be made by a deterministic C# policy layer, not by the model alone.
6. The console must support multiple commands: creating a new triage run, viewing history, viewing a specific run, importing knowledge, checking system health, and running evaluation fixtures.
7. The core logic must be separated from the console so a future Avalonia UI can reuse it without rewriting it.

---

## 3. High-Level Architecture

```text
Issue report
    ↓
Intake Agent           → validated structured IntakeAnalysis (JSON)
    ↓
Research Agent         → SQL keyword search + local semantic search (RAG)
    ↓
Evidence collection     → structured EvidenceItem records with source and relevance
    ↓
Resolver Agent          → grounded resolution plan referencing evidence IDs
    ↓
Policy evaluator        → approve / needs-human-review / reject
    ↓
Persistence             → SQLite triage history + JSON evidence + Markdown report
```

The model stays local through Ollama end-to-end. Documents, embeddings, triage history, and generated reports can all remain on your machine, which fits a local-first design.

---

## 4. Solution Structure

Split the single console project into layered projects. This mirrors patterns you already use in ASP.NET Core/Avalonia work, and it prepares the intelligence layer to be reused by a future desktop UI.

```text
DevOpsTriage.sln
│
├── DevOpsTriage.Domain/
│   ├── Models/
│   │   ├── Incident.cs
│   │   ├── IntakeAnalysis.cs
│   │   ├── EvidenceItem.cs
│   │   ├── TriageRun.cs
│   │   ├── AgentRun.cs
│   │   ├── ToolInvocation.cs
│   │   └── TriageDecision.cs
│   └── Enums/
│       ├── Severity.cs
│       ├── IncidentCategory.cs
│       └── TriageStatus.cs
│
├── DevOpsTriage.Application/
│   ├── Workflows/
│   │   └── TriageWorkflow.cs
│   ├── Agents/
│   │   ├── IntakeAgentFactory.cs
│   │   ├── ResearchAgentFactory.cs
│   │   └── ResolverAgentFactory.cs
│   ├── Policies/
│   │   └── TriagePolicyEvaluator.cs
│   └── Contracts/
│       ├── ITriageRepository.cs
│       ├── IKnowledgeSearchService.cs
│       └── IReportWriter.cs
│
├── DevOpsTriage.Infrastructure/
│   ├── Ollama/
│   │   ├── SemanticKernelRegistration.cs
│   │   └── OllamaHealthCheck.cs
│   ├── Persistence/
│   │   ├── TriageDbContext.cs
│   │   ├── SqliteTriageRepository.cs
│   │   └── Migrations/
│   ├── KnowledgeBase/
│   │   ├── MarkdownDocumentImporter.cs
│   │   ├── ChunkingService.cs
│   │   ├── OllamaEmbeddingService.cs
│   │   └── HybridKnowledgeSearchService.cs
│   └── Reports/
│       └── MarkdownReportWriter.cs
│
├── DevOpsTriage.Console/
│   ├── Program.cs
│   ├── Commands/
│   │   ├── TriageCommand.cs
│   │   ├── ImportKnowledgeCommand.cs
│   │   ├── HistoryCommand.cs
│   │   ├── ShowRunCommand.cs
│   │   └── HealthCommand.cs
│   └── appsettings.json
│
└── DevOpsTriage.Tests/
    ├── Application/
    ├── Infrastructure/
    └── EvaluationCases/
```

You do not need to build all five projects on day one. A practical path is: keep the existing console project working, then peel out `Domain` and `Infrastructure` once the new features start to need real separation.

---

## 5. Feature 1 — Structured Agent Output

This is the most important change and should be implemented first. Everything else in Version 2 depends on having a trustworthy structured `IntakeAnalysis` instead of free-form Markdown.

### Domain model

```csharp
namespace DevOpsTriage.Domain.Models;

public sealed record IntakeAnalysis(
    IncidentCategory Category,
    Severity Severity,
    string ProductArea,
    IReadOnlyList<string> Symptoms,
    IReadOnlyList<string> MissingInformation,
    string SearchQuery,
    bool RequiresEscalation,
    string EscalationReason);
```

```csharp
namespace DevOpsTriage.Domain.Enums;

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

public enum IncidentCategory
{
    Bug,
    FeatureRequest,
    Question,
    Incident
}
```

### Agent instructions

Ask the Intake Agent to return JSON only, matching the shape above:

```text
Return only a JSON object with these exact fields:
category, severity, productArea, symptoms, missingInformation,
searchQuery, requiresEscalation, escalationReason.

Use only these values for category: Bug, FeatureRequest, Question, Incident.
Use only these values for severity: Low, Medium, High, Critical.
Do not include any text before or after the JSON object.
Do not invent logs, ticket IDs, customer names, versions, or test results.
```

### Validation in C#

```csharp
var json = ExtractJsonObject(agentRawOutput);

IntakeAnalysis? intake;

try
{
    intake = JsonSerializer.Deserialize<IntakeAnalysis>(
        json,
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
}
catch (JsonException)
{
    intake = null;
}

if (intake is null || string.IsNullOrWhiteSpace(intake.SearchQuery))
{
    // Retry once with a stricter reminder prompt, then fall back
    // to a "needs human review" status rather than guessing.
}
```

Local models vary in how reliably they produce clean JSON. Plan for:

- A small helper that extracts the first valid JSON object from the raw text, in case the model adds stray words.
- One bounded retry with a stricter reminder before giving up.
- A safe fallback: mark the run as `NeedsHumanReview` instead of continuing with unclear data.

---

## 6. Feature 2 — Persistent Triage History

Every run should be saved locally, independent of whether the console window stays open. This is what turns the project from a demo into a debuggable, auditable tool.

### Suggested tables

```text
TriageRuns
├── Id (Guid)
├── CreatedUtc
├── OriginalReport
├── ModelId
├── PromptVersion
├── Status
├── FinalSeverity
├── RequiresHumanReview
└── FinalResolutionMarkdown

AgentRuns
├── Id
├── TriageRunId
├── AgentName
├── StartedUtc
├── CompletedUtc
├── InputText
├── OutputText
├── Success
└── ErrorMessage

EvidenceItems
├── Id
├── TriageRunId
├── SourceType
├── SourceReference
├── Title
├── ContentExcerpt
├── RelevanceScore
└── AddedUtc

ToolInvocations
├── Id
├── TriageRunId
├── AgentName
├── PluginName
├── FunctionName
├── ArgumentsJson
├── ResultSummary
├── StartedUtc
├── CompletedUtc
└── Success
```

### Example entity

```csharp
namespace DevOpsTriage.Domain.Models;

public sealed class TriageRun
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public required string OriginalReport { get; init; }

    public required string ModelId { get; init; }

    public required string PromptVersion { get; init; }

    public string Status { get; set; } = "Started";

    public string? FinalResolutionMarkdown { get; set; }

    public bool RequiresHumanReview { get; set; }
}
```

### Technology choice

Use **EF Core with SQLite**. It gives you migrations, relationship mapping between `TriageRuns`, `AgentRuns`, `EvidenceItems`, and `ToolInvocations`, and a data layer that can later bind directly into an Avalonia UI. Raw `Microsoft.Data.Sqlite` is fine if you specifically want to minimize dependencies, but EF Core will save time as the schema grows.

---

## 7. Feature 3 — Local RAG Knowledge Base

This is the highest-value new capability in Version 2: semantic search over your own Markdown documents, running fully through local Ollama models.

### Folder layout

```text
Knowledge/
├── known-issues/
│   ├── large-export-ui-freeze.md
│   ├── token-refresh-failure.md
│   └── duplicate-questionnaire-answers.md
├── postmortems/
│   ├── incident-2026-01-export-memory-pressure.md
│   └── incident-2026-02-api-timeouts.md
├── runbooks/
│   ├── desktop-export-diagnostics.md
│   └── authentication-diagnostics.md
└── architecture/
    ├── export-pipeline.md
    └── authentication-flow.md
```

### Document metadata convention

```markdown
---
id: KB-EXPORT-001
title: Large export freezes the desktop UI
area: Export
severity: High
tags:
  - avalonia
  - export
  - ui-thread
  - excel
---

# Symptoms

The desktop client becomes unresponsive during exports above approximately
50,000 rows.

# Root cause

The export work runs on the UI thread and materializes all results in memory.

# Recommended remediation

Move export generation to a background operation, stream source rows,
report progress, support cancellation, and test large datasets.
```

### Retrieval pipeline

```text
Markdown files
    ↓
Parse front-matter metadata
    ↓
Chunk into ~300–700 token sections
    ↓
Generate embeddings locally through Ollama's embedding API
    ↓
Store chunk text + metadata + vector locally
    ↓
Intake search query
    ↓
Generate query embedding through Ollama
    ↓
Cosine similarity ranking
    ↓
Return top-N matching chunks as evidence
```

Ollama exposes a dedicated embeddings capability intended for semantic search and retrieval-augmented generation, returning numeric vectors through its `/api/embed` endpoint. This lets the whole retrieval pipeline stay local, alongside your existing chat model.

### Why keep keyword search too

Do not replace SQL keyword search with vector search. Combine them:

| Use case | Best method |
|---|---|
| Exact ticket ID, error code, version number | SQL keyword search |
| Known issue with matching terminology | SQL keyword search |
| Incident described in different words than the documents | Semantic search |
| Postmortems, runbooks, architecture notes | Semantic search |
| Broad "similar past incidents" queries | Semantic search |

---

## 8. Feature 4 — Evidence-First Research

The Research Agent should stop summarizing tool output in prose and instead produce structured, source-attributed evidence records.

```csharp
namespace DevOpsTriage.Domain.Models;

public sealed record EvidenceItem(
    string SourceType,
    string SourceId,
    string Title,
    string Excerpt,
    double RelevanceScore,
    string WhyRelevant);
```

Example evidence set for an export-freeze report:

```json
[
  {
    "sourceType": "KnownIssue",
    "sourceId": "KI-001",
    "title": "Large export blocks the UI thread",
    "excerpt": "Desktop client becomes unresponsive during large exports.",
    "relevanceScore": 0.94,
    "whyRelevant": "Matches the reported UI freeze during a 50,000-record export."
  },
  {
    "sourceType": "KnowledgeDocument",
    "sourceId": "KB-EXPORT-001#chunk-3",
    "title": "Large export freezes the desktop UI",
    "excerpt": "Move export generation to a background operation and stream source rows.",
    "relevanceScore": 0.89,
    "whyRelevant": "Provides a documented remediation for the same symptom pattern."
  }
]
```

The Resolver Agent should only receive:

- The original user report.
- The validated `IntakeAnalysis`.
- The evidence list.
- Any relevant policy constraints.

It should not receive raw, unfiltered SQLite rows, entire source documents, unrelated historical triage runs, or internal system prompts. Keeping its input narrow makes its output easier to trust and easier to test.

---

## 9. Feature 5 — Policy and Approval Gate

Insert a deterministic C# policy layer between the Resolver Agent’s output and anything that resembles a decision or action.

```csharp
namespace DevOpsTriage.Application.Policies;

public sealed class TriagePolicyEvaluator
{
    public TriageDecision Evaluate(IntakeAnalysis intake)
    {
        if (intake.Severity == Severity.Critical)
        {
            return new TriageDecision(
                RequiresHumanReview: true,
                Reason: "Critical severity requires human review.");
        }

        if (intake.RequiresEscalation)
        {
            return new TriageDecision(
                RequiresHumanReview: true,
                Reason: intake.EscalationReason);
        }

        return new TriageDecision(
            RequiresHumanReview: false,
            Reason: "No mandatory manual-review rule matched.");
    }
}
```

### Suggested starter policy table

| Condition | System action |
|---|---|
| Low or Medium severity | Save final report automatically |
| High severity | Save report and flag "engineering review recommended" |
| Critical severity | Pause before escalation; require human decision |
| Suspected credential or security exposure | Require human review |
| Suspected data loss | Require human review |
| Agent or tool failure | Mark run incomplete; never present a fabricated result as complete |

The core idea: the model can **recommend** severity and escalation, but your code makes the final routing decision and is the only thing allowed to trigger a stored outcome or a future external action.

---

## 10. Feature 6 — A Real Command-Line Interface

Replace the "paste one report, get one answer" loop with named commands, so the tool behaves like something you would actually keep using.

```text
triage new
triage import ./Knowledge
triage history
triage show <run-id>
triage report <run-id>
triage evaluate ./EvaluationCases/test-cases.json
triage health
```

### Example session

```text
> triage new

Describe the issue:
> Export freezes the desktop app when finance exports more than 50,000 rows.

Run ID: 629dc47c-077e-4ecf-904a-0432fad7d23a
Model: qwen2.5-coder:7b
Knowledge search: 2 SQL matches, 3 semantic matches
Status: Completed
Human review: Recommended

Saved:
- SQLite run history
- JSON evidence file
- Markdown triage report
```

### Suggested output layout

```text
Data/
├── triage.db
└── vectors.db

Reports/
├── 2026-09-14_629dc47c_triage-report.md
└── 2026-09-14_629dc47c_evidence.json

Knowledge/
└── ...
```

---

## 11. Feature 7 — Health Checks and Diagnostics

Because everything runs against a local Ollama instance and local files, make diagnostics a first-class command rather than an afterthought.

`triage health` should verify:

- The Ollama endpoint responds.
- The configured chat model is installed.
- The configured embedding model is installed.
- The SQLite database opens successfully.
- Known issues were seeded.
- Knowledge documents were imported and chunked.
- Stored vector dimensions are consistent.
- Required Semantic Kernel plugins are registered.
- No write-capable tools are accidentally enabled.

Example output:

```text
Ollama endpoint: Healthy
Chat model: qwen2.5-coder:7b — available
Embedding model: embeddinggemma — available
SQLite database: Healthy
Known issues: 4 records
Knowledge chunks: 82 records
Vector dimension: 768
Read-only tool policy: Enabled
```

This becomes especially useful once you start switching between machines, Ollama versions, or model tags, since local-model setups are more prone to silent drift than a fixed cloud API.

---

## 12. Recommended Implementation Order

1. **Extract layers.** Keep the console app as the composition root; move models into `Domain` and infrastructure concerns into `Infrastructure`.
2. **Add SQLite run history.** Persist the raw report, agent outputs, timing, model ID, and status before adding anything else. This alone makes debugging dramatically easier.
3. **Implement structured Intake JSON.** Parse and validate `IntakeAnalysis`; add one bounded retry; fall back to `NeedsHumanReview` on failure.
4. **Add the policy evaluator.** Move escalation/review logic into C#, out of prompt text.
5. **Add Markdown report export.** Save the run ID, model metadata, evidence, and final plan as a reviewable file.
6. **Add document importing.** Parse and store Markdown knowledge files and chunks in SQLite, even before embeddings exist.
7. **Add local Ollama embeddings.** Choose an embedding model, generate and store vectors, and implement cosine similarity search.
8. **Implement hybrid evidence retrieval.** Merge SQL and semantic results, deduplicate by source ID, and pass only top matches to the Resolver.
9. **Create evaluation fixtures.** Track whether expected evidence appears, and compare results across different local models.
10. **Prepare for a UI.** Expose `ITriageWorkflow`, `ITriageRepository`, and `IKnowledgeSearchService` so a future Avalonia client can reuse the same core without changes.

---

## 13. What to Avoid in Version 2

- Do not give any agent unrestricted shell access.
- Do not expose arbitrary SQL execution as a plugin.
- Do not let an agent automatically create or modify external tickets.
- Do not add more agents just to make the project look more "multi-agent."
- Do not add vector search without a way to evaluate whether it actually improves results.
- Do not forward entire files or unbounded tool results into a prompt.
- Do not rely on free-form Markdown when code must decide severity, escalation, or routing.
- Do not start a UI before the console workflow has a persistent, reproducible audit trail.

---

## 14. Definition of Done for Version 2

Version 2 can be considered complete when this full sequence works end to end, locally, with Ollama:

1. A user submits an incident report through the console.
2. The Intake Agent returns valid structured JSON that passes validation.
3. The system immediately persists the run to SQLite.
4. The Research Agent retrieves both exact SQLite matches and semantic Markdown matches.
5. The Resolver Agent produces a grounded plan that references specific evidence IDs.
6. The C# policy layer decides whether the run needs human review.
7. The system writes a Markdown report and a JSON evidence file locally.
8. `triage history` and `triage show <run-id>` can replay the full audit trail for any past run.

That outcome is a genuinely useful local AI engineering-assistant foundation, and a solid base for a future Version 3 that adds an Avalonia incident dashboard, a document-management screen, an evidence viewer, and an explicit human-approval UI.
