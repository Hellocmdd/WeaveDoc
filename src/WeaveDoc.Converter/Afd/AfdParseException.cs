namespace WeaveDoc.Converter.Afd;

/// <summary>
/// AFD 模板解析或校验失败时抛出的异常
/// </summary>
public class AfdParseException : Exception
{
    public AfdParseException(string message) : base(message) { }

    public AfdParseException(string message, Exception inner) : base(message, inner) { }
}
