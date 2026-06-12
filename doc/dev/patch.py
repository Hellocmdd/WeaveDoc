import sys
path = 'src/AvaloniaEdit_Decompiled/AvaloniaEdit.Rendering/VisualLineTextSource.cs'
with open(path, 'r') as f:
    text = f.read()

# Replace my LastIndexOf to use StringComparison
text = text.replace('public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType) => throw new NotImplementedException();', 'public int LastIndexOf(string searchText, int startIndex, int count, System.StringComparison comparisonType) => throw new NotImplementedException();')

# Explicitly implement ITextSource.GetText
text = text.replace('public StringSegment GetText(int offset, int length)', 'string AvaloniaEdit.Document.ITextSource.GetText(int offset, int length) => throw new NotImplementedException();\n        public StringSegment GetText(int offset, int length)')

with open(path, 'w') as f:
    f.write(text)

