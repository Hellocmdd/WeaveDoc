using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Extensions.Mathematics;

namespace WeaveDoc.MarkdownEditor.Services;

public class RelaxedMathInlineParser : InlineParser
{
    public RelaxedMathInlineParser()
    {
        OpeningCharacters = new[] { '$' };
    }

    public override bool Match(InlineProcessor processor, ref Markdig.Helpers.StringSlice slice)
    {
        var match = slice.CurrentChar;
        var pc = slice.PeekCharExtra(-1);
        if (pc == match || pc == '\\')
        {
            return false;
        }

        var startPosition = slice.Start;
        int openDollars = 1;
        var c = slice.NextChar();
        if (c == match)
        {
            openDollars++;
            c = slice.NextChar();
        }

        var start = slice.Start;
        int closeDollars = 0;
        int end = 0;

        while (c != '\0')
        {
            if (c == '\r' || c == '\n') return false; // Default math inline does not support newlines

            if (c == match && slice.PeekCharExtra(-1) != '\\')
            {
                closeDollars++;
                if (closeDollars == openDollars)
                {
                    end = slice.Start - 1;
                    slice.NextChar(); // Consume the last $
                    break;
                }
            }
            else
            {
                closeDollars = 0;
            }
            c = slice.NextChar();
        }

        if (closeDollars >= openDollars)
        {
            var inline = new MathInline()
            {
                Span = new Markdig.Syntax.SourceSpan(processor.GetSourcePosition(startPosition, out int line, out int column), processor.GetSourcePosition(slice.Start - 1)),
                Line = line,
                Column = column,
                Delimiter = match,
                DelimiterCount = openDollars,
                Content = new Markdig.Helpers.StringSlice(slice.Text, start, end - openDollars)
            };
            processor.Inline = inline;
            return true;
        }

        return false;
    }
}
