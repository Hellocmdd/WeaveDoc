namespace RagAvalonia.Models;

public sealed record ChatTurn(string Role, string Content, bool IsUser);
