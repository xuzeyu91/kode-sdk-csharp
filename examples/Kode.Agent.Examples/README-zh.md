# Kode.Agent.Examples (C#)

> **English version**: [Read the English README](./README.md)

这个项目用于演示 C# SDK 的核心链路，整体示例结构参考仓库根目录的 TypeScript `examples/`：

- `examples/getting-started.ts` ↔ `GettingStarted.cs`
- `examples/01-agent-inbox.ts` ↔ `AgentInbox.cs`
- `examples/02-approval-control.ts` ↔ `ApprovalControl.cs`
- `examples/03-room-collab.ts` ↔ `RoomCollab.cs`
- `examples/04-scheduler-watch.ts` ↔ `SchedulerWatch.cs`

## 运行方式

1. 准备环境变量
   - 复制 `csharp/examples/Kode.Agent.Examples/.env.example` 为 `csharp/examples/Kode.Agent.Examples/.env`
   - 填写 `ANTHROPIC_API_KEY` 或 `OPENAI_API_KEY`
   - 如需切换提供商：设置 `DEFAULT_PROVIDER=openai|anthropic`，并对应填写 `OPENAI_MODEL_ID` / `ANTHROPIC_MODEL_ID`

2. 运行示例菜单
   - `dotnet run --project csharp/examples/Kode.Agent.Examples/Kode.Agent.Examples.csproj`

## 说明

- 示例优先展示 TS 对齐用法：`TemplateRegistry + templateId + sandbox.workDir + events.subscribe/on + permission_required.respond(...)`
- `SchedulerWatch` 会创建 `./workspace` 目录；你可以手动修改其中任意文件来观察 `file_changed` 事件。
- 建议不要把真实 API Key 提交到仓库；如误提交请及时轮换密钥并清理历史。
