# Kode.Agent.Examples (C#)

> **中文版**: [查看中文 README](./README-zh.md)

This project demonstrates the core workflows of the C# SDK. The overall example structure mirrors the TypeScript `examples/` in the repository root:

- `examples/getting-started.ts` ↔ `GettingStarted.cs`
- `examples/01-agent-inbox.ts` ↔ `AgentInbox.cs`
- `examples/02-approval-control.ts` ↔ `ApprovalControl.cs`
- `examples/03-room-collab.ts` ↔ `RoomCollab.cs`
- `examples/04-scheduler-watch.ts` ↔ `SchedulerWatch.cs`

## Running

1. Prepare environment variables
   - Copy `csharp/examples/Kode.Agent.Examples/.env.example` to `csharp/examples/Kode.Agent.Examples/.env`
   - Fill in `ANTHROPIC_API_KEY` or `OPENAI_API_KEY`
   - To switch providers: set `DEFAULT_PROVIDER=openai|anthropic`, and fill in corresponding `OPENAI_MODEL_ID` / `ANTHROPIC_MODEL_ID`

2. Run example menu
   - `dotnet run --project csharp/examples/Kode.Agent.Examples/Kode.Agent.Examples.csproj`

## Notes

- Examples prioritize aligned usage patterns: `TemplateRegistry + templateId + sandbox.workDir + events.subscribe/on + permission_required.respond(...)`
- `SchedulerWatch` creates a `./workspace` directory; you can manually modify any file in it to observe `file_changed` events.
- It's recommended not to commit real API keys to the repository; if accidentally committed, rotate the key promptly and clean up history.
