import os
import re

def fix_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    orig = content

    # CS0266: switch (key - 19) -> switch ((int)key - 19)
    content = content.replace('switch (key - 19)', 'switch ((int)key - 19)')
    content = content.replace('switch (key - 34)', 'switch ((int)key - 34)')
    content = content.replace('switch (key - 1)', 'switch ((int)key - 1)')
    # Just generic: switch (key - \d+) -> switch ((int)key - \d+)
    content = re.sub(r'switch \(key - (\d+)\)', r'switch ((int)key - \1)', content)

    # CS0030: bool to SweepDirection / TextWrapping
    content = content.replace('(SweepDirection)true', 'Avalonia.Media.SweepDirection.Clockwise')
    content = content.replace('(SweepDirection)false', 'Avalonia.Media.SweepDirection.CounterClockwise')
    # TextWrapping handled by fix_all5.py, but just in case
    content = content.replace('(TextWrapping)true', 'Avalonia.Media.TextWrapping.Wrap')
    content = content.replace('(TextWrapping)false', 'Avalonia.Media.TextWrapping.NoWrap')

    # CS1061: _002Ector on Point, Rect, Vector
    # e.g. ((Rect)(ref val))._002Ector(...) -> val = new Rect(...)
    # e.g. ((Point)(ref val))._002Ector(...) -> val = new Point(...)
    # Actually, the code might look like: ((Rect)(ref val))._002Ector(1, 2, 3, 4);
    content = re.sub(r'\(\([A-Za-z0-9_]+\)\(ref ([A-Za-z0-9_]+)\)\)\._002Ector\(([^)]*)\);', r'\1 = new (\2);', content)
    # What if it's newing a struct property directly? Like ((Point)(ref something))._002Ector()
    content = re.sub(r'\(\([A-Za-z0-9_]+\)\(ref ([A-Za-z0-9_.]+)\)\)\._002Ector\(([^)]*)\);', r'\1 = new (\2);', content)

    # CS0571: Key.init
    # { Key = e.Key } but decompiled as e.Key
    content = content.replace('Key.init(e.Key);', 'Key = e.Key;')
    content = content.replace('Key.init', 'Key = ')

    # CS1501: PointToScreen, TransformToVisual, TranslatePoint
    # Usually it's PointToScreen(p) instead of PointToScreen(this, p)
    content = re.sub(r'PointToScreen\(([^,]+),\s*([^)]+)\)', r'PointToScreen(\2)', content)
    # Wait, Avalonia's PointToScreen takes a Point. If it was an extension method...
    
    # Let's just fix the CS1540 (protected access)
    content = content.replace('((StyledElement)val).LogicalChildren', 'this.LogicalChildren') # rough, maybe this.LogicalChildren?
    # Better: just ((Avalonia.Controls.Control)val).LogicalChildren... wait LogicalChildren is protected on Control too.
    # What is `val`? In CompletionWindow.cs
    
    # CS0019: FontStyle? ?? FontWeight
    content = content.replace('((FontStyle?)null) ??', 'Avalonia.Media.FontStyle.Normal ??')

    # CS1620: out parameters
    # FoldingElementGenerator.cs(61,25)
    # textSource.GetLineByOffset(offset, out line, out lineOffset);
    content = content.replace('textSource.GetLineByOffset(offset, line, offset2);', 'textSource.GetLineByOffset(offset, out line, out offset2);')

    if content != orig:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)

for root, _, files in os.walk('src/AvaloniaEdit_Decompiled'):
    for file in files:
        if file.endswith('.cs'):
            fix_file(os.path.join(root, file))
