using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LLama;
using LLama.Common;

namespace TestApp
{
    public partial class MainWindow : Window
    {
        private static readonly string[] RoleStopMarkers = new[]
        {
            "\nUser:",
            "\n用户:",
            "\n用户：",
            "\nHuman:"
        };

        private const string DefaultChatModelPath = "models/gemma-3-4b-it.gguf";
        private const string DefaultEmbeddingModelPath = "models/sentence-transformers--all-MiniLM-L6-v2.gguf";

        private LLamaWeights? _weights;
        private LLamaContext? _context;
        private InteractiveExecutor? _executor;
        private ChatSession? _chatSession;
        private string? _loadedModelPath;

        private LLamaWeights? _embeddingWeights;
        private LLamaEmbedder? _embedder;
        private RagStore? _ragStore;
        private bool _ragReady;
        private readonly object _ragLock = new();

        public MainWindow()
        {
            InitializeComponent();
            // Controls
            var input = this.FindControl<TextBox>("InputBox");
            var modelBox = this.FindControl<TextBox>("ModelBox");
            var button = this.FindControl<Button>("RunButton");
            var clearBtn = this.FindControl<Button>("ClearButton");
            var sourceBox = this.FindControl<TextBox>("SourceBox");
            var pickSourceBtn = this.FindControl<Button>("PickSourceButton");
            var ragReadBtn = this.FindControl<Button>("RagReadButton");
            var ragUpsertBtn = this.FindControl<Button>("RagUpsertButton");
            var ragDeleteBtn = this.FindControl<Button>("RagDeleteButton");
            var ragRebuildBtn = this.FindControl<Button>("RagRebuildButton");
            var ragCountBtn = this.FindControl<Button>("RagCountButton");
            var output = this.FindControl<TextBox>("OutputBlock");

            if (button == null) return;

            this.Opened += (_, __) => { input?.Focus(); };

            button.Click += async (_, __) =>
            {
                if (input == null || output == null) return;

                var prompt = input.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(prompt)) return;

                // Append user message
                var prev = output.Text ?? string.Empty;
                var sb = new StringBuilder(prev);
                if (!string.IsNullOrWhiteSpace(prev)) sb.AppendLine();
                sb.AppendLine($"User: {prompt}");
                sb.Append("Assistant: ");
                await Dispatcher.UIThread.InvokeAsync(() => output.Text = sb.ToString());

                var modelPathInput = modelBox?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(modelPathInput))
                {
                    modelPathInput = DefaultChatModelPath;
                }

                button.IsEnabled = false;

                try
                {
                    var (ragPrompt, ragNote) = await BuildRagPromptAsync(prompt);

                    if (!string.IsNullOrWhiteSpace(ragNote))
                    {
                        var noteText = (output.Text ?? string.Empty) + "\n" + ragNote + "\n";
                        await Dispatcher.UIThread.InvokeAsync(() => output.Text = noteText);
                    }

                    var modelPath = ResolveModelPath(modelPathInput);
                    var assistant = await RunWithLocalModelAsync(modelPath, ragPrompt, output);
                    if (string.IsNullOrWhiteSpace(assistant))
                    {
                        assistant = "[无输出]";
                        var newText = (output.Text ?? string.Empty) + assistant + "\n";
                        await Dispatcher.UIThread.InvokeAsync(() => output.Text = newText);
                    }
                }
                catch (Exception ex)
                {
                    var msg = "本地推理错误: " + ex.Message;
                    await Dispatcher.UIThread.InvokeAsync(() => output.Text = (output.Text ?? string.Empty) + msg + "\n");
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };

            if (clearBtn != null)
            {
                clearBtn.Click += (_, __) =>
                {
                    if (input != null) input.Text = string.Empty;
                    if (output != null) output.Text = string.Empty;
                };
            }

            if (ragCountBtn != null)
            {
                ragCountBtn.Click += async (_, __) =>
                {
                    if (output == null) return;
                    await RunRagActionAsync(ragCountBtn, () => HandleRagCountAsync(output), output);
                };
            }

            if (pickSourceBtn != null)
            {
                pickSourceBtn.Click += async (_, __) =>
                {
                    if (output == null || sourceBox == null) return;
                    await RunRagActionAsync(pickSourceBtn, () => HandlePickSourceFileAsync(sourceBox, output), output);
                };
            }

            if (ragReadBtn != null)
            {
                ragReadBtn.Click += async (_, __) =>
                {
                    if (output == null || sourceBox == null) return;
                    await RunRagActionAsync(ragReadBtn, () => HandleRagReadSourceAsync(sourceBox, output), output);
                };
            }

            if (ragDeleteBtn != null)
            {
                ragDeleteBtn.Click += async (_, __) =>
                {
                    if (output == null || sourceBox == null) return;
                    await RunRagActionAsync(ragDeleteBtn, () => HandleRagDeleteSourceAsync(sourceBox, output), output);
                };
            }

            if (ragUpsertBtn != null)
            {
                ragUpsertBtn.Click += async (_, __) =>
                {
                    if (output == null || sourceBox == null) return;
                    await RunRagActionAsync(ragUpsertBtn, () => HandleRagUpsertSourceAsync(sourceBox, output), output);
                };
            }

            if (ragRebuildBtn != null)
            {
                ragRebuildBtn.Click += async (_, __) =>
                {
                    if (output == null) return;
                    await RunRagActionAsync(ragRebuildBtn, () => HandleRagRebuildAllAsync(output), output);
                };
            }
        }

        private async Task RunRagActionAsync(Button actionButton, Func<Task> action, TextBox output)
        {
            actionButton.IsEnabled = false;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                await AppendOutputAsync(output, "[RAG] 操作失败: " + ex.Message);
            }
            finally
            {
                actionButton.IsEnabled = true;
            }
        }

        private static string NormalizeSource(string source)
        {
            var value = (source ?? string.Empty).Trim();
            value = value.Replace('\\', '/');
            while (value.StartsWith("./", StringComparison.Ordinal))
            {
                value = value.Substring(2);
            }

            return value;
        }

        private static bool IsSupportedSourceFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".txt" || ext == ".md" || ext == ".markdown" || ext == ".csv" || ext == ".json";
        }

        private async Task HandlePickSourceFileAsync(TextBox sourceBox, TextBox output)
        {
            var top = TopLevel.GetTopLevel(this);
            var storage = top?.StorageProvider;
            if (storage == null)
            {
                await AppendOutputAsync(output, "[RAG] 当前环境不支持文件选择器，请手动输入来源路径。");
                return;
            }

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择要导入到 RAG 的文件",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("文本文档") { Patterns = new[] { "*.txt", "*.md", "*.markdown", "*.csv", "*.json" } },
                    new("所有文件") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count == 0)
            {
                await AppendOutputAsync(output, "[RAG] 已取消选择文件。");
                return;
            }

            var localPath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                await AppendOutputAsync(output, "[RAG] 仅支持本地文件。请重新选择本地磁盘上的文件。");
                return;
            }

            if (!IsSupportedSourceFile(localPath))
            {
                await AppendOutputAsync(output, "[RAG] 当前仅支持 txt/md/markdown/csv/json 文件。");
                return;
            }

            var root = Directory.GetCurrentDirectory();
            var source = Path.GetRelativePath(root, localPath).Replace('\\', '/');
            await Dispatcher.UIThread.InvokeAsync(() => sourceBox.Text = source);
            await AppendOutputAsync(output, "[RAG] 已选择来源: " + source);
        }

        private static string ResolveCanonicalSource(string rootDirectory, string sourceInput)
        {
            var normalized = NormalizeSource(sourceInput);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

            var fullPath = Path.IsPathRooted(normalized)
                ? normalized
                : Path.GetFullPath(Path.Combine(rootDirectory, normalized));

            if (File.Exists(fullPath))
            {
                return Path.GetRelativePath(rootDirectory, fullPath).Replace('\\', '/');
            }

            return normalized;
        }

        private static string? ResolveSourceFilePath(string rootDirectory, string sourceInput)
        {
            var normalized = NormalizeSource(sourceInput);
            if (string.IsNullOrWhiteSpace(normalized)) return null;

            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            {
                return normalized;
            }

            var direct = Path.GetFullPath(Path.Combine(rootDirectory, normalized));
            if (File.Exists(direct)) return direct;

            var underDoc = Path.GetFullPath(Path.Combine(rootDirectory, "doc", normalized));
            if (File.Exists(underDoc)) return underDoc;

            return null;
        }

        private static string SafeReadAllText(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task AppendOutputAsync(TextBox output, string message)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var current = output.Text ?? string.Empty;
                output.Text = string.IsNullOrEmpty(current)
                    ? message + "\n"
                    : current + "\n" + message + "\n";
            });
        }

        private static string BuildChunkTable(IReadOnlyList<RagChunkRecord> rows)
        {
            const int idWidth = 8;
            const int idxWidth = 7;
            const int previewWidth = 72;

            var sb = new StringBuilder();
            sb.AppendLine($"{"ID",-idWidth} | {"Chunk",-idxWidth} | Preview");
            sb.AppendLine(new string('-', idWidth + idxWidth + previewWidth + 6));

            foreach (var row in rows)
            {
                var preview = (row.Content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
                if (preview.Length > previewWidth)
                {
                    preview = preview.Substring(0, previewWidth - 3) + "...";
                }

                sb.AppendLine($"{row.Id,-idWidth} | {row.ChunkIndex,-idxWidth} | {preview}");
            }

            return sb.ToString().TrimEnd();
        }

        private static string BuildSourceSummaryTable(IReadOnlyList<RagSourceSummary> rows)
        {
            const int chunkWidth = 7;
            const int timeWidth = 19;

            var sb = new StringBuilder();
            sb.AppendLine($"{"Chunk",-chunkWidth} | {"UpdatedAt",-timeWidth} | Source");
            sb.AppendLine(new string('-', chunkWidth + timeWidth + 64));

            foreach (var row in rows)
            {
                var source = row.Source ?? string.Empty;
                if (source.Length > 80)
                {
                    source = source.Substring(0, 77) + "...";
                }

                var time = string.IsNullOrWhiteSpace(row.LastUpdatedAt) ? "-" : row.LastUpdatedAt;
                sb.AppendLine($"{row.ChunkCount,-chunkWidth} | {time,-timeWidth} | {source}");
            }

            return sb.ToString().TrimEnd();
        }

        private async Task HandleRagCountAsync(TextBox output)
        {
            await EnsureRagReadyAsync();
            if (_ragStore == null)
            {
                await AppendOutputAsync(output, "[RAG] 未初始化。");
                return;
            }

            var count = _ragStore.CountChunks();
            var sources = _ragStore.ListSourceSummaries();
            if (sources.Count == 0)
            {
                await AppendOutputAsync(output, "[RAG] 当前索引为空（0 条）。");
                return;
            }

            var table = BuildSourceSummaryTable(sources);
            await AppendOutputAsync(output, $"[RAG] 当前 chunk 总数: {count}，来源文件数: {sources.Count}\n{table}");
        }

        private async Task HandleRagReadSourceAsync(TextBox sourceBox, TextBox output)
        {
            var sourceInput = sourceBox.Text ?? string.Empty;
            var root = Directory.GetCurrentDirectory();
            var source = ResolveCanonicalSource(root, sourceInput);
            if (string.IsNullOrWhiteSpace(source))
            {
                await AppendOutputAsync(output, "[RAG] 请先点“浏览文件”选择来源。\n如果你愿意，也可以直接输入路径（例如 doc/faq.md）。");
                return;
            }

            await EnsureRagReadyAsync();
            if (_ragStore == null)
            {
                await AppendOutputAsync(output, "[RAG] 未初始化。");
                return;
            }

            var rows = _ragStore.ListChunksBySource(source);
            if (rows.Count == 0)
            {
                await AppendOutputAsync(output, "[RAG] 未找到来源: " + source);
                return;
            }

            var table = BuildChunkTable(rows);
            await AppendOutputAsync(output, $"[RAG] 来源 {source} 共有 {rows.Count} 条:\n{table}");
        }

        private async Task HandleRagDeleteSourceAsync(TextBox sourceBox, TextBox output)
        {
            var sourceInput = sourceBox.Text ?? string.Empty;
            var root = Directory.GetCurrentDirectory();
            var source = ResolveCanonicalSource(root, sourceInput);
            if (string.IsNullOrWhiteSpace(source))
            {
                await AppendOutputAsync(output, "[RAG] 请先选择要删除的来源（点“浏览文件”）。");
                return;
            }

            await EnsureRagReadyAsync();
            if (_ragStore == null)
            {
                await AppendOutputAsync(output, "[RAG] 未初始化。");
                return;
            }

            var deleted = _ragStore.DeleteChunksBySource(source);
            await AppendOutputAsync(output, $"[RAG] 删除来源 {source} 完成，删除 {deleted} 条。");
        }

        private async Task HandleRagUpsertSourceAsync(TextBox sourceBox, TextBox output)
        {
            var sourceInput = sourceBox.Text ?? string.Empty;
            var root = Directory.GetCurrentDirectory();
            var filePath = ResolveSourceFilePath(root, sourceInput);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                await AppendOutputAsync(output, "[RAG] 找不到该来源文件。请先点“浏览文件”选择，或确认路径存在。");
                return;
            }

            await EnsureRagReadyAsync();
            if (_ragStore == null || _embedder == null)
            {
                await AppendOutputAsync(output, "[RAG] 未初始化。");
                return;
            }

            var source = Path.GetRelativePath(root, filePath).Replace('\\', '/');
            var content = SafeReadAllText(filePath);
            var chunks = RagStore.ChunkText(content);
            if (chunks.Count == 0)
            {
                var deleted = _ragStore.DeleteChunksBySource(source);
                await AppendOutputAsync(output, $"[RAG] 文件为空，已删除来源 {source} 的 {deleted} 条旧数据。");
                return;
            }

            var inserted = await Task.Run(() =>
            {
                lock (_ragLock)
                {
                    if (_ragStore == null || _embedder == null)
                    {
                        throw new InvalidOperationException("RAG 尚未就绪");
                    }

                    _ragStore.DeleteChunksBySource(source);
                    var count = 0;
                    for (var i = 0; i < chunks.Count; i++)
                    {
                        var emb = ExtractSingleEmbedding(_embedder.GetEmbeddings(chunks[i]).GetAwaiter().GetResult());
                        _ragStore.InsertChunk(source, i, chunks[i], emb);
                        count++;
                    }

                    return count;
                }
            });

            await AppendOutputAsync(output, $"[RAG] 来源重建完成: {source}，写入 {inserted} 条。");
        }

        private async Task HandleRagRebuildAllAsync(TextBox output)
        {
            await EnsureRagReadyAsync();

            var root = Directory.GetCurrentDirectory();
            var count = await Task.Run(() =>
            {
                lock (_ragLock)
                {
                    if (_ragStore == null || _embedder == null)
                    {
                        throw new InvalidOperationException("RAG 尚未就绪");
                    }

                    _ragStore.ClearAll();
                    SeedRagFromWorkspaceDocs(_ragStore, root);
                    return _ragStore.CountChunks();
                }
            });

            await AppendOutputAsync(output, "[RAG] 全量重建完成，当前 chunk 总数: " + count);
        }

        private async Task<string> RunWithLocalModelAsync(string modelPath, string prompt, TextBox output)
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("找不到模型文件", modelPath);
            }

            EnsureModelLoaded(modelPath);
            if (_chatSession == null)
            {
                throw new InvalidOperationException("模型会话初始化失败");
            }

            var inferenceParams = new InferenceParams
            {
                // Keep a larger token budget to reduce half-sentence truncation.
                MaxTokens = 4096,
                // Stop once the model starts writing the next user turn.
                AntiPrompts = new List<string>(RoleStopMarkers)
            };

            var sb = new StringBuilder();
            var message = new ChatHistory.Message(AuthorRole.User, prompt);
            var outputPrefix = output.Text ?? string.Empty;

            await foreach (var text in _chatSession.ChatAsync(message, inferenceParams))
            {
                sb.Append(text);
                var newText = (output.Text ?? string.Empty) + text;
                await Dispatcher.UIThread.InvokeAsync(() => output.Text = newText);
            }

            var rawText = sb.ToString();
            var cleanText = TrimAtRoleMarker(rawText);
            if (!string.Equals(rawText, cleanText, StringComparison.Ordinal))
            {
                await Dispatcher.UIThread.InvokeAsync(() => output.Text = outputPrefix + cleanText);
            }

            await Dispatcher.UIThread.InvokeAsync(() => output.Text = (output.Text ?? string.Empty) + "\n");
            return cleanText;
        }

        private async Task<(string ragPrompt, string note)> BuildRagPromptAsync(string userQuestion)
        {
            try
            {
                await EnsureRagReadyAsync();
                if (_embedder == null || _ragStore == null)
                {
                    return (userQuestion, "[RAG] 未初始化，已使用直答模式。");
                }

                var queryEmbedding = ExtractSingleEmbedding(await _embedder.GetEmbeddings(userQuestion));
                var hits = _ragStore.SearchByCosine(queryEmbedding, topK: 4);
                if (hits.Count == 0)
                {
                    return (userQuestion, "[RAG] 未检索到上下文，已使用直答模式。");
                }

                var contextSb = new StringBuilder();
                for (var i = 0; i < hits.Count; i++)
                {
                    var h = hits[i];
                    contextSb.AppendLine($"[#{i + 1}] 来源: {h.Source} (chunk={h.ChunkIndex}, score={h.Score:F3})");
                    contextSb.AppendLine(h.Content);
                    contextSb.AppendLine();
                }

                var ragPrompt =
                    "你是一个严谨的 RAG 助手。请严格依据 Context 回答，不要编造。" +
                    "如果 Context 信息不足，请明确说不知道。优先中文，回答简洁。\n\n" +
                    "Context:\n" + contextSb +
                    "Question:\n" + userQuestion + "\n\nAnswer:";

                var note = "[RAG] 命中: " + string.Join(", ", hits.Select(h => $"{h.Source}#{h.ChunkIndex}({h.Score:F2})"));
                return (ragPrompt, note);
            }
            catch (Exception ex)
            {
                return (userQuestion, "[RAG] 发生错误，已降级为直答: " + ex.Message);
            }
        }

        private async Task EnsureRagReadyAsync()
        {
            if (_ragReady) return;

            await Task.Run(() =>
            {
                lock (_ragLock)
                {
                    if (_ragReady) return;

                    var root = Directory.GetCurrentDirectory();
                    var dbPath = Path.GetFullPath(Path.Combine(root, "rag_store.db"));

                    _ragStore = new RagStore(dbPath);
                    _ragStore.EnsureSchema();

                    var embeddingModelPath = ResolveModelPath(DefaultEmbeddingModelPath);
                    if (!File.Exists(embeddingModelPath))
                    {
                        throw new FileNotFoundException("找不到 embedding 模型文件", embeddingModelPath);
                    }

                    var embedParams = new ModelParams(embeddingModelPath)
                    {
                        ContextSize = 1024,
                        Embeddings = true,
                        GpuLayerCount = 0
                    };

                    _embeddingWeights = LLamaWeights.LoadFromFile(embedParams);
                    _embedder = new LLamaEmbedder(_embeddingWeights, embedParams, null);

                    if (_ragStore.CountChunks() == 0)
                    {
                        SeedRagFromWorkspaceDocs(_ragStore, root);
                    }

                    _ragReady = true;
                }
            });
        }

        private void SeedRagFromWorkspaceDocs(RagStore store, string rootDirectory)
        {
            if (_embedder == null) return;

            var docDirectory = Path.Combine(rootDirectory, "doc");
            foreach (var (source, content) in RagStore.EnumerateKnowledgeFiles(docDirectory, rootDirectory))
            {
                var chunks = RagStore.ChunkText(content);
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var emb = ExtractSingleEmbedding(_embedder.GetEmbeddings(chunk).GetAwaiter().GetResult());
                    store.InsertChunk(source, i, chunk, emb);
                }
            }
        }

        private static float[] ExtractSingleEmbedding(object? raw)
        {
            if (raw == null) throw new InvalidOperationException("embedding 结果为空");

            if (raw is float[] f) return NormalizeL2(f);
            if (raw is IReadOnlyList<float> rof) return NormalizeL2(rof.ToArray());
            if (raw is IEnumerable<float> ef) return NormalizeL2(ef.ToArray());
            if (raw is ReadOnlyMemory<float> rom) return NormalizeL2(rom.ToArray());
            if (raw is Memory<float> mem) return NormalizeL2(mem.ToArray());

            if (raw is IEnumerable<float[]> many)
            {
                return NormalizeL2(AverageVectors(many.ToList()));
            }

            if (raw is IEnumerable<ReadOnlyMemory<float>> manyRom)
            {
                return NormalizeL2(AverageVectors(manyRom.Select(x => x.ToArray()).ToList()));
            }

            if (raw is IEnumerable<Memory<float>> manyMem)
            {
                return NormalizeL2(AverageVectors(manyMem.Select(x => x.ToArray()).ToList()));
            }

            if (raw is IEnumerable sequence)
            {
                var arrays = new List<float[]>();
                foreach (var item in sequence)
                {
                    if (item is float[] arr)
                    {
                        arrays.Add(arr);
                    }
                    else if (item is ReadOnlyMemory<float> itemRom)
                    {
                        arrays.Add(itemRom.ToArray());
                    }
                    else if (item is Memory<float> itemMem)
                    {
                        arrays.Add(itemMem.ToArray());
                    }
                }

                if (arrays.Count > 0)
                {
                    return NormalizeL2(AverageVectors(arrays));
                }
            }

            throw new InvalidOperationException("无法识别 embedding 返回类型: " + raw.GetType().FullName);
        }

        private static float[] AverageVectors(List<float[]> vectors)
        {
            if (vectors.Count == 0)
            {
                throw new InvalidOperationException("embedding 向量为空");
            }

            var dim = vectors[0].Length;
            var sum = new double[dim];
            var count = 0;

            foreach (var vec in vectors)
            {
                if (vec.Length != dim) continue;
                for (var i = 0; i < dim; i++) sum[i] += vec[i];
                count++;
            }

            if (count == 0)
            {
                throw new InvalidOperationException("embedding 向量维度不一致");
            }

            var avg = new float[dim];
            for (var i = 0; i < dim; i++) avg[i] = (float)(sum[i] / count);
            return avg;
        }

        private static float[] NormalizeL2(float[] vector)
        {
            double norm = 0;
            for (var i = 0; i < vector.Length; i++) norm += vector[i] * vector[i];
            norm = Math.Sqrt(norm);
            if (norm <= 1e-12) return vector;

            var outVec = new float[vector.Length];
            for (var i = 0; i < vector.Length; i++) outVec[i] = (float)(vector[i] / norm);
            return outVec;
        }

        private static string TrimAtRoleMarker(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var cut = -1;
            foreach (var marker in RoleStopMarkers)
            {
                var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && (cut < 0 || idx < cut))
                {
                    cut = idx;
                }
            }

            if (cut < 0)
            {
                // Fallback for outputs that start directly with role tags without line break.
                var userIdx = text.IndexOf("User:", StringComparison.OrdinalIgnoreCase);
                var cnIdx = text.IndexOf("用户:", StringComparison.OrdinalIgnoreCase);
                var cnFullIdx = text.IndexOf("用户：", StringComparison.OrdinalIgnoreCase);

                cut = userIdx;
                if (cnIdx >= 0 && (cut < 0 || cnIdx < cut)) cut = cnIdx;
                if (cnFullIdx >= 0 && (cut < 0 || cnFullIdx < cut)) cut = cnFullIdx;
            }

            return cut >= 0 ? text.Substring(0, cut).TrimEnd() : text;
        }

        private void EnsureModelLoaded(string modelPath)
        {
            if (_chatSession != null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DisposeModelResources();

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 4096,
                GpuLayerCount = 0
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _context = _weights.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);
            _chatSession = new ChatSession(_executor);
            _loadedModelPath = modelPath;
        }

        private string ResolveModelPath(string modelPathInput)
        {
            if (Path.IsPathRooted(modelPathInput))
            {
                return modelPathInput;
            }

            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), modelPathInput)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelPathInput)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", modelPathInput)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", modelPathInput))
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            return candidates[0];
        }

        private void DisposeModelResources()
        {
            _chatSession = null;
            _executor = null;
            _context?.Dispose();
            _weights?.Dispose();

            _embedder?.Dispose();
            _embeddingWeights?.Dispose();

            _context = null;
            _weights = null;
            _loadedModelPath = null;

            _embedder = null;
            _embeddingWeights = null;
            _ragStore = null;
            _ragReady = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            DisposeModelResources();
            base.OnClosed(e);
        }
    }
}
