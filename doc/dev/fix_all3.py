import os
import re

def fix_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    orig = content
    
    # 1. _002Ector
    # For Vector: val._002Ector(A, B) -> val = new Avalonia.Vector(A, B)
    # The regex r'([a-zA-Z0-9_]+)\._002Ector\(([^)]+)\);' was fine, but let's make it more robust
    content = re.sub(r'([a-zA-Z0-9_]+)\._002Ector\(([^)]+)\);', r'\1 = new (\2);', content)
    # wait, if I use `new (\2)` C# might not infer the type if it's an assignment to an existing struct. 
    # C# 9.0 target-typed new is `new(\2)`. Wait, we are LangVersion 13.0, so `\1 = new(\2);` works PERFECTLY!
    
    # 2. OnPointer / OnKey / OnPropertyChanged etc
    content = content.replace('((InputElement)this).OnPointerMoved(e)', 'base.OnPointerMoved(e)')
    content = content.replace('((InputElement)this).OnPointerPressed(e)', 'base.OnPointerPressed(e)')
    content = content.replace('((Control)this).OnPointerReleased(e)', 'base.OnPointerReleased(e)')
    content = content.replace('((Control)this).OnPropertyChanged(e)', 'base.OnPropertyChanged(e)')
    content = content.replace('((Layoutable)this).MeasureOverride', 'base.MeasureOverride')
    content = content.replace('((Layoutable)this).ArrangeCore', 'base.ArrangeCore')
    content = content.replace('((Control)this).OnKeyDown', 'base.OnKeyDown')
    content = content.replace('((Control)this).OnKeyUp', 'base.OnKeyUp')
    
    # 3. ITextSource ambiguity
    content = content.replace(' ITextSource ', ' AvaloniaEdit.Document.ITextSource ')
    content = content.replace('(ITextSource ', '(AvaloniaEdit.Document.ITextSource ')
    content = content.replace('<ITextSource>', '<AvaloniaEdit.Document.ITextSource>')
    content = content.replace('AvaloniaEdit.Document.AvaloniaEdit.Document.ITextSource', 'AvaloniaEdit.Document.ITextSource')
    content = content.replace('Avalonia.Media.TextFormatting.AvaloniaEdit.Document.ITextSource', 'Avalonia.Media.TextFormatting.ITextSource')
    
    # 4. GetVisualChildren missing on Visual
    content = content.replace('((Visual)(object)this).VisualChildren', 'this.GetVisualChildren()')
    content = content.replace('((Visual)this).VisualChildren', 'this.GetVisualChildren()')
    content = re.sub(r'\(\(Visual\)\(([a-zA-Z0-9_]+)\)\)\.VisualChildren', r'((Avalonia.Controls.Control)\1).GetVisualChildren()', content)
    content = re.sub(r'\(\(Visual\)([a-zA-Z0-9_]+)\)\.VisualChildren', r'((Avalonia.Controls.Control)\1).GetVisualChildren()', content)

    # 5. CS0571: implicit operator Vector
    if "implicit operator Vector" in content or "SelectionMouseHandler.cs" in path:
        content = content.replace('(Vector)(Point)', 'new Vector')
        
    # 6. CS1501 TranslatePoint and TransformToVisual
    content = content.replace('TranslatePoint(new Point(0.0, 0.0), this, null)', 'this.TranslatePoint(new Point(0.0, 0.0), null)')
    content = content.replace('TranslatePoint(new Point(0.0, 0.0), this, ', 'this.TranslatePoint(new Point(0.0, 0.0), ')
    content = content.replace('TransformToVisual(this, ', 'this.TransformToVisual(')
    
    # 7. CS1620 out keyword
    if 'SingleCharacterElementGenerator.cs' in path:
        content = content.replace('GetVisualColumn(textLineVisualXPosition, textLineVisualXPosition, ', 'GetVisualColumn(textLineVisualXPosition, out _, out _); //')
        # wait, if there are actually variables passed: GetVisualColumn(p1, isRtl, isLtr)
        # We can just change the signature of the call.
        content = re.sub(r'GetVisualColumn\(([^,]+), ([^,]+), ([^)]+)\)', r'GetVisualColumn(\1, out _, out _)', content)
    
    if content != orig:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)

for root, _, files in os.walk('src/AvaloniaEdit_Decompiled'):
    for file in files:
        if file.endswith('.cs'):
            fix_file(os.path.join(root, file))

