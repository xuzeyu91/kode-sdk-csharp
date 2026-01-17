# Kode.Agent WebApi Assistant (OpenAI Compatible)

这个示例是一个 ASP.NET WebAPI 应用，对外暴露 OpenAI Chat Completions 兼容接口，并支持 SSE 流式输出。

## 运行

```bash
cd csharp/examples/Kode.Agent.WebApiAssistant

cp .env.example .env
# 编辑 .env，至少设置 DEFAULT_PROVIDER + 对应的 API KEY

dotnet run
```

默认监听地址以控制台输出为准（通常是 `http://localhost:5xxx`）。

## 接口

- `POST /v1/chat/completions`（兼容 OpenAI）
  - `stream=false`：返回 JSON
  - `stream=true`：返回 `text/event-stream`（SSE），以 `data: ...\n\n` 形式输出，并以 `data: [DONE]` 结束
- `GET /healthz`

关于取消/断开连接：
- 客户端中断 SSE 连接时，服务端会停止写入流并退出 handler，但 **不会自动 `Interrupt` Agent**（对齐 TS assistant：断连不等于取消任务）。

## 会话与记忆

本服务会把对话状态存到 `KODE_STORE_DIR` 下的 JSON 文件中。

- 如果请求体里带 `user` 字段，会使用它作为 `agentId`（推荐）
- 否则服务端会生成一个新的 `agentId`，并通过响应头 `X-Kode-Agent-Id` 返回给客户端

同时会创建每个 agent 的数据目录：`<workDir>/data/<agentId>`（`workDir` 默认是应用的 ContentRootPath，可用 `Kode:WorkDir` / `KODE_WORK_DIR` 覆盖），并初始化：

- `data/<agentId>/.memory/profile.json`
- `data/<agentId>/.config/notify.json`
- `data/<agentId>/.config/email.json`

## 工具白名单（allowlist）

本服务会把允许的工具列表作为白名单（allowlist）下发给 Agent：不在白名单中的工具会被直接拒绝（不会进入审批暂停）。

- 默认白名单取 `KODE_TOOLS` / `Kode:Tools`（即你暴露给模型的那批工具）
- 显式拒绝：`PermissionConfig.DenyTools`
- 必须审批：`PermissionConfig.RequireApprovalTools`

## 示例请求

注意：当前版本不允许客户端覆盖 `model`，如需携带 `model` 字段，请与服务端配置保持一致。

非流式：

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model":"gpt-4o",
    "user":"demo",
    "messages":[
      {"role":"system","content":"You are a helpful personal assistant."},
      {"role":"user","content":"你好，介绍一下你自己"}
    ],
    "stream":false
  }'
```

流式（SSE）：

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d '{
    "model":"gpt-4o",
    "user":"demo",
    "messages":[
      {"role":"user","content":"用 3 句话总结一下今天的计划"}
    ],
    "stream":true
  }'
```
