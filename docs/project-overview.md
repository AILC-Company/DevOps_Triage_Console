# Project Overview — DevOps Triage Console (Version 1)

## What This Project Is

DevOps Triage Console is a C# console application built to learn Semantic Kernel by implementing a small, realistic multi-agent workflow. It simulates a software engineering support desk: a user describes a bug or issue in plain language, and three specialized AI agents work together to classify it, research related history, and produce a concrete resolution plan.

The project runs entirely on local infrastructure. Language-model inference happens through **Ollama**, running on the developer's own machine, and all supporting data lives in a local **SQLite** database. No cloud AI service or API key is required.

This is a learning project first, and a reusable architectural pattern second. It is intentionally scoped to be small enough to fully understand end to end, while still demonstrating the core building blocks that any real Semantic Kernel agent application needs: kernel configuration, native plugins, function/tool calling, multiple specialized agents, and an explicit orchestration flow between them.

---

## Problem It Simulates

Support and engineering teams routinely receive issue reports that need to be triaged before anyone can act on them. That triage work typically involves three separate mental steps:

1. Understanding what kind of problem this is and how urgent it is.
2. Checking whether this has happened before, either as a known issue or an existing ticket.
3. Turning that understanding into a concrete, prioritized action plan.

This project models each step as its own agent, so the responsibilities stay clearly separated instead of being handled by one large, ambiguous prompt.

---

## The Three Agents

| Agent | Responsibility | Uses tools? |
|---|---|---|
| Intake Agent | Reads the raw issue report and classifies category, severity, product area, and escalation need | No |
| Research Agent | Searches the local known-issues database and local mock ticket data for related history | Yes — two read-only plugins |
| Resolver Agent | Combines the intake analysis and research findings into a final, evidence-based action plan | No |

The agents run in a fixed, predictable order: **Intake → Research → Resolver**. Each agent's output is explicitly passed forward as input to the next agent. This sequential, explicit handoff was chosen deliberately over an autonomous multi-agent conversation, because it is far easier to debug, log, and reason about while learning the underlying framework.

---

## Example Walkthrough

**Input** (typed into the console):

```text
The desktop application freezes for 20–30 seconds when users export more than 50,000 records to Excel. It happens on Windows 11. Severity seems high because finance cannot complete month-end reporting.
```

**What happens internally:**

1. The **Intake Agent** reads this and determines it is a Bug, High severity, in the Export area, and drafts a short search query such as "export freeze large dataset."
2. The **Research Agent** receives that analysis and calls two local tools:
   - `SearchKnownIssuesAsync`, which queries the SQLite knowledge base and finds `KI-001` (large export blocks the UI thread) and `KI-004` (export has high memory pressure).
   - `SearchOpenTicketsAsync`, which checks local mock ticket data and finds `T-1042`, a matching finance-reported export slowness ticket.
3. The **Resolver Agent** receives the original report, the intake analysis, and the research findings, then produces a structured plan: probable cause, recommended next actions, a suggested technical direction, a verification plan, and an escalation decision.

**Output:** all three agents' responses are printed to the console as a combined Markdown-formatted report.

---

## Why Ollama and Local Models

The project uses Ollama instead of a cloud LLM provider so that:

- No API key, billing account, or internet dependency is required to run or learn from the project.
- All issue data, tool results, and generated reports stay on the local machine.
- It's possible to compare how different local models handle both plain chat responses and structured tool/function calling — a meaningfully different capability that not every model handles well.

The trade-off is that local models vary significantly in reliability, especially for function calling and structured output, so the project is built around explicitly testable, small steps rather than assuming any model will "just work."

---

## Core Technology Choices

| Concern | Choice | Why |
|---|---|---|
| AI orchestration | Microsoft Semantic Kernel | Provides the `Kernel`, plugin system, and agent abstractions used throughout |
| Model runtime | Ollama (local) | Runs open models locally with no API key or cloud dependency |
| SK/Ollama bridge | `Microsoft.SemanticKernel.Connectors.Ollama` (prerelease) | Official connector exposing `AddOllamaChatCompletion` |
| Local data | SQLite via `Microsoft.Data.Sqlite` | Lightweight, file-based, no server setup, good fit for a console app |
| App hosting/DI | `Microsoft.Extensions.Hosting` | Standard .NET dependency-injection and configuration pipeline |
| Logging | `Microsoft.Extensions.Logging.Console` | Simple visibility into agent and tool execution during development |

---

## Architecture at a Glance

```text
Console UI (Program.cs)
        │
        v
TriageWorkflow (Services/TriageWorkflow.cs)
        │
        ├── IntakeAgent     (no tools)
        ├── ResearchAgent   (KnownIssuesPlugin + TicketApiPlugin)
        └── ResolverAgent   (no tools)
                │
    ┌───────────┴────────────┐
    v                        v
KnownIssuesPlugin      TicketApiPlugin
    │                        │
    v                        v
SQLite (triage.db)     In-memory mock ticket data
                │
                v
        Ollama (localhost:11434)
```

Each agent gets its own cloned `Kernel` instance. Only the Research Agent's kernel has plugins registered, which keeps its tool-calling responsibility isolated from the other two agents.

---

## Design Principles Behind the Project

- **Read-only tools only.** Both plugins only search local data; neither can write, delete, or modify anything. This keeps the first learning project safe regardless of what the model decides to do.
- **Deterministic data access, non-deterministic language.** SQLite queries and mock ticket lookups are ordinary, parameterized, fully deterministic C# code. The LLM is only responsible for language understanding, tool selection, and synthesis — not data access logic.
- **Explicit over autonomous.** The three-agent handoff is coded explicitly in `TriageWorkflow`, rather than delegated to an autonomous group chat. This makes behavior predictable and much easier to debug while learning.
- **Small, testable steps.** The project is designed to be built and verified incrementally — first a kernel connectivity check, then one plugin, then one agent, then the full pipeline — rather than assembled all at once and debugged as a whole.

---

## What Version 1 Deliberately Does Not Include

These are intentional scope boundaries for the first version, not oversights:

- No persistence of triage runs between executions — each run only exists in the console output.
- No structured (JSON) agent output — agents currently return Markdown text.
- No semantic/vector search — knowledge lookup is keyword-based SQL only.
- No write-capable tools of any kind — nothing can create tickets, modify records, or send messages.
- No graphical UI — this is a console-only application.
- No autonomous multi-agent conversation — the workflow is a fixed, sequential pipeline.

These limitations define the intended scope for a Version 2 iteration, which introduces structured output, persistent history, local retrieval-augmented generation (RAG) over Markdown documents, and a policy/approval layer.

---

## Who This Project Is For

This project is aimed at a developer who already knows C#, .NET, SQL, and API design, and wants a hands-on, non-trivial introduction to Semantic Kernel — specifically: how kernels are configured, how native plugins become callable tools, how tool/function calling actually behaves with a local model, and how to structure multiple cooperating agents without immediately reaching for fully autonomous orchestration.
