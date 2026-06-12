import os
import re

def fix_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    orig = content
    
    # 1. _003F error
    content = content.replace('(_003F?)', '(Avalonia.Media.FontStyle?)')
    content = content.replace('(_003F)', '(Avalonia.Media.FontStyle)')
    content = content.replace('_003F', 'Avalonia.Media.FontStyle') # rough fix
    
    # 2. GetVisualChildren missing
    content = content.replace('.GetVisualChildren()', '.VisualChildren')
    content = content.replace('((Visual)val).VisualChildren', '((Avalonia.Controls.Control)val).VisualChildren')
    
    # 3. CS0104 ITextSource ambiguity
    content = content.replace(' ITextSource ', ' AvaloniaEdit.Document.ITextSource ')
    content = content.replace('(ITextSource ', '(AvaloniaEdit.Document.ITextSource ')
    
    # 4. OnPropertyChanged / OnGotFocus
    content = content.replace('((Control)this).OnPropertyChanged', 'base.OnPropertyChanged')
    content = content.replace('((Control)this).OnGotFocus', 'base.OnGotFocus')
    content = content.replace('((Control)this).OnLostFocus', 'base.OnLostFocus')
    content = content.replace('((InputElement)this).OnTextInput', 'base.OnTextInput')
    content = content.replace('((InputElement)this).OnKeyDown', 'base.OnKeyDown')
    content = content.replace('((Control)this).OnKeyUp', 'base.OnKeyUp')
    content = content.replace('((TemplatedControl)this).OnApplyTemplate', 'base.OnApplyTemplate')

    # 5. CS1503 System.Drawing.Point to Avalonia.Point
    if "ExtensionMethods.cs" in path:
        content = content.replace('(Avalonia.Point)base.TextView.GetVisualPosition', 'base.TextView.GetVisualPosition')
        # Actually it's probably missing 'new Avalonia.Point(p.X, p.Y)'
    
    # 6. TranslatePoint missing 3 args
    content = content.replace('TranslatePoint(new Point(0.0, 0.0), this, null)', 'TranslatePoint(new Point(0.0, 0.0), null)')
    
    # 7. TransformToVisual 2 args
    content = content.replace('TransformToVisual(this, ', 'TransformToVisual(')
    
    # 8. Point.implicit operator Vector
    if "SelectionMouseHandler.cs" in path:
        content = content.replace('(Vector)(Point)', 'new Vector(')

    # 9. _002Ector on Point
    content = re.sub(r'([a-zA-Z0-9_]+)\._002Ector\(([^)]+)\);', r'\1 = new (\2);', content)
    
    # 10. TextWrapping
    content = content.replace('(TextWrapping)true', 'Avalonia.Media.TextWrapping.Wrap')
    content = content.replace('(TextWrapping)false', 'Avalonia.Media.TextWrapping.NoWrap')

    if content != orig:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)

for root, _, files in os.walk('src/AvaloniaEdit_Decompiled'):
    for file in files:
        if file.endswith('.cs'):
            fix_file(os.path.join(root, file))

