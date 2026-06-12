import os
import re

def fix_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    orig = content

    # 1. Protected methods accessed via cast
    methods = ['OnGotFocus', 'OnLostFocus', 'OnTextInput', 'OnKeyDown', 'OnKeyUp', 'OnApplyTemplate', 'ArrangeCore']
    for m in methods:
        content = re.sub(r'\(\([a-zA-Z0-9_]+\)this\)\.' + m, r'base.' + m, content)
        content = re.sub(r'this\.' + m + r'\(', r'base.' + m + '(', content)

    # 2. VisualChildren
    content = content.replace('this.GetVisualChildren()', 'this.VisualChildren')
    content = content.replace('((Avalonia.Controls.Control)this).GetVisualChildren()', 'this.VisualChildren')
    
    # fix earlier bad replacement: ((Avalonia.Controls.Control)val).GetVisualChildren() -> val.VisualChildren
    content = re.sub(r'\(\(Avalonia\.Controls\.Control\)([a-zA-Z0-9_]+)\)\.GetVisualChildren\(\)', r'\1.VisualChildren', content)

    # 3. ToAvalonia
    content = content.replace('.ToAvalonia()', '')
    
    # 4. TranslatePoint
    content = content.replace('TranslatePoint(new Point(0.0, 0.0), this, null)', 'this.TranslatePoint(new Point(0.0, 0.0), null)')

    # 5. TransformToVisual
    content = content.replace('TransformToVisual(this, ', 'this.TransformToVisual(')
    
    # 6. CS1620 out keyword in SingleCharacterElementGenerator
    if 'SingleCharacterElementGenerator.cs' in path:
        content = re.sub(r'GetVisualColumn\(([^,]+), ([^,]+), ([^)]+)\)', r'GetVisualColumn(\1, out _, out _)', content)
        
    # 7. ITextSource ambiguity (again)
    content = content.replace(' ITextSource ', ' AvaloniaEdit.Document.ITextSource ')
    content = content.replace('(ITextSource ', '(AvaloniaEdit.Document.ITextSource ')
    content = content.replace('AvaloniaEdit.Document.AvaloniaEdit.Document.ITextSource', 'AvaloniaEdit.Document.ITextSource')
    content = content.replace('Avalonia.Media.TextFormatting.AvaloniaEdit.Document.ITextSource', 'Avalonia.Media.TextFormatting.ITextSource')

    # 8. Point/Vector conversions
    if "SelectionMouseHandler.cs" in path:
        content = content.replace('((Vector)(Point)', 'new Vector(') # this is rough but might work

    if content != orig:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)

for root, _, files in os.walk('src/AvaloniaEdit_Decompiled'):
    for file in files:
        if file.endswith('.cs'):
            fix_file(os.path.join(root, file))

