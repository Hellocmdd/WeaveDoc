---
name: csharp-llama-instructions
description: |
  在处理 C# 与本地 llama.cpp 集成相关问题时启用本指令。适用于 `RagStore.cs`, `Program.cs`, `RagStore` 相关请求以及涉及 P/Invoke、native interop、性能优化或崩溃排查的场景。
applyTo:
  - "**/RagStore.cs"
  - "**/Program.cs"
  - "**/*.cs"
---

使用说明：

- 当你提出与 C# 调用本地库（llama.cpp）相关的问题时，本指令会帮助 Agent 提供：
  - P/Invoke 签名建议与 marshalling 注意事项
  - 最小可复现案例补丁（可直接应用）
  - 本地调试与性能分析脚本（Linux/Windows）

示例提示（可直接在聊天中使用）：

- 我的程序在调用本地库时 segfault，堆栈在 `RagStore.cs`，帮我定位并给出补丁。
- 请给出在 Windows 上通过 P/Invoke 调用 llama.cpp 的 `DllImport` 签名与示例。
