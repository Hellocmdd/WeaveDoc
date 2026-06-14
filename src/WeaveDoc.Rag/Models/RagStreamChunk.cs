namespace WeaveDoc.Rag.Models;

/// <summary>
/// One fragment emitted by a streaming RAG answer (<see cref="LocalAiService.AskStreamAsync"/>).
/// </summary>
/// <remarks>
/// When <see cref="Replace"/> is <see langword="false"/>, <see cref="Text"/> is an incremental
/// token delta to append to the in-progress assistant bubble. When <see cref="Replace"/> is
/// <see langword="true"/>, <see cref="Text"/> is the canonical final answer (produced after
/// citation normalization or an off-topic repair) and must replace everything streamed so far,
/// so the displayed bubble settles to the corrected text.
/// </remarks>
public readonly record struct RagStreamChunk(string Text, bool Replace);
