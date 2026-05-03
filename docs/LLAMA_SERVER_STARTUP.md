# 后端LLM服务启动指南

本文档说明如何启动Chat LLM和Reranker LLM两个后端服务。

## 脚本位置

所有启动脚本位于 `./scripts/` 目录：
- `scripts/start_backend_llm.sh` - Linux/Mac基础启动脚本
- `scripts/start_backend_llm_advanced.sh` - Linux/Mac高级启动脚本（支持自定义参数）
- `scripts/start_backend_llm.ps1` - Windows PowerShell启动脚本

## 系统要求

- llama.cpp 已编译到 `./llama.cpp/build/bin/llama-server`
- 模型文件已下载到 `./models/` 目录：
  - `Qwen3.5-4B-Q4_K_M.gguf` (Chat模型)
  - `bge-reranker-v2-m3.gguf` (Reranker模型)

## 服务配置

### Chat LLM (端口 8080)
- **模型**: Qwen3.5-4B-Q4_K_M.gguf
- **启动参数**:
  - `-port 8080` - 监听端口
  - `-ngl 99` - GPU层数（99表示全部使用GPU）
  - `--flash-attn on` - 启用Flash Attention优化
  - `-c 32438` - 上下文大小（32K tokens）

### Reranker LLM (端口 8081)
- **模型**: bge-reranker-v2-m3.gguf
- **启动参数**:
  - `--embedding` - 启用Embedding模式
  - `--pooling rank` - 使用rank pooling
  - `-port 8081` - 监听端口

## Linux/Mac 启动

```bash
# 在项目根目录运行基础版本
bash scripts/start_backend_llm.sh

# 或运行高级版本（支持自定义参数）
bash scripts/start_backend_llm_advanced.sh

# CPU模式运行
bash scripts/start_backend_llm_advanced.sh --cpu-only

# 自定义端口
bash scripts/start_backend_llm_advanced.sh --chat-port 9000 --reranker-port 9001
```

## Windows 启动

```powershell
# 在项目根目录运行基础版本
powershell -ExecutionPolicy Bypass -File ".\scripts\start_backend_llm.ps1"

# 如果遇到权限问题，设置执行策略
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope CurrentUser
# 然后运行
.\scripts\start_backend_llm.ps1
```

## 服务检查

启动后，可以通过以下方式验证服务是否正常运行：

```bash
# Chat LLM 健康检查
curl http://localhost:8080/health

# Reranker LLM 健康检查
curl http://localhost:8081/health
```

## 关闭服务

在终端按 `Ctrl+C` 将会优雅地关闭两个服务。

## 故障排查

### llama-server不存在
需要先编译llama.cpp：
```bash
cd llama.cpp
cmake -B build
cmake --build build
```

### 模型文件不存在
确保模型文件已正确下载到 `./models/` 目录中。

### 端口已被占用
如果8080或8081端口已被占用，可能需要：
1. 修改脚本中的端口号
2. 关闭占用这些端口的其他进程

### GPU支持
如果没有GPU或CUDA环境，可以修改 `-ngl 99` 参数：
- `-ngl 0` - 完全使用CPU
- `-ngl 10` - 部分层使用GPU

## 与C# RAG应用集成

在C#应用中配置这两个服务的地址：

```csharp
var chatClient = new LlamaServerChatClient("http://localhost:8080");
var reranker = new LlamaServerEmbedding("http://localhost:8081");
```

详见 `csharp-rag-avalonia/Services/LlamaServerChatClient.cs`
