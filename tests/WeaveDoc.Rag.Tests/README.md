# WeaveDoc.Rag.Tests

`WeaveDoc.Rag.Tests` covers retrieval and chat service behavior for `WeaveDoc.Rag` without requiring local GGUF models for the normal unit-test path.

## Scope

| Test class | Coverage |
| --- | --- |
| `QueryUnderstandingServiceTests` | Query normalization and intent/profile extraction |
| `LocalAiQuestionAnalyzerTests` | Follow-up and question-analysis helper behavior |
| `LocalAiFallbackAnswerBuilderTests` | Fallback answer generation and citation-style output |
| `RagPipelineHeuristicsTests` | Retrieval scoring heuristics, sparse/vector mix, context selection behavior |
| `CorpusFileSelectionTests` | Corpus file/path selection rules |
| `LlamaServerChatClientStreamingTests` | OpenAI-compatible streaming response parsing, continuation, timeout/error behavior |

## Test Stack

- .NET 10
- xUnit 2
- Microsoft.NET.Test.Sdk
- Project reference to `src/WeaveDoc.Rag`

## Files

```text
WeaveDoc.Rag.Tests/
├── Services/
│   ├── LlamaServerChatClientStreamingTests.cs
│   └── Rag/
│       ├── CorpusFileSelectionTests.cs
│       ├── LocalAiFallbackAnswerBuilderTests.cs
│       ├── LocalAiQuestionAnalyzerTests.cs
│       ├── QueryUnderstandingServiceTests.cs
│       └── RagPipelineHeuristicsTests.cs
└── WeaveDoc.Rag.Tests.csproj
```

## Run

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj -nologo
```

Targeted examples:

```bash
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj --filter "QueryUnderstandingServiceTests" -nologo
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj --filter "RagPipelineHeuristicsTests" -nologo
dotnet test tests/WeaveDoc.Rag.Tests/WeaveDoc.Rag.Tests.csproj --filter "LlamaServerChatClientStreamingTests" -nologo
```

## Notes

- Normal tests should not require `llama.cpp`, GGUF models, or a live endpoint.
- Streaming client tests should use controlled HTTP handlers/fixtures rather than real network services.
- Evaluation workflows are documented in `src/WeaveDoc.Rag/README.md` and are separate from the unit-test project.
