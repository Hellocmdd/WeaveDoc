import os
import re

def fix_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    orig = content
    
    content = content.replace('VisualExtensions.', 'Avalonia.VisualTree.VisualExtensions.')
    content = content.replace(' ITextSource ', ' AvaloniaEdit.Document.ITextSource ')
    content = content.replace('(ITextSource ', '(AvaloniaEdit.Document.ITextSource ')
    content = content.replace('<ITextSource>', '<AvaloniaEdit.Document.ITextSource>')
    content = content.replace('AvaloniaEdit.Document.AvaloniaEdit.Document.ITextSource', 'AvaloniaEdit.Document.ITextSource')
    content = content.replace('Avalonia.Media.TextFormatting.AvaloniaEdit.Document.ITextSource', 'Avalonia.Media.TextFormatting.ITextSource')

    content = re.sub(r'([a-zA-Z0-9_]+)\._002Ector\(([^)]+)\);', r'\1 = new (\2);', content)
    
    content = re.sub(r'\(\(Visual\)\(([a-zA-Z0-9_]+)\)\)\.VisualChildren', r'((Avalonia.Controls.Control)\1).GetVisualChildren()', content)
    content = re.sub(r'\(\(Visual\)([a-zA-Z0-9_]+)\)\.VisualChildren', r'((Avalonia.Controls.Control)\1).GetVisualChildren()', content)
    content = content.replace('((Visual)this).VisualChildren', 'this.GetVisualChildren()')

    content = content.replace('(TextWrapping)true', 'Avalonia.Media.TextWrapping.Wrap')
    content = content.replace('(TextWrapping)false', 'Avalonia.Media.TextWrapping.NoWrap')

    content = content.replace('((InputElement)this).OnPointerMoved(e)', 'base.OnPointerMoved(e)')
    content = content.replace('((InputElement)this).OnPointerPressed(e)', 'base.OnPointerPressed(e)')
    content = content.replace('((Control)this).OnPointerReleased(e)', 'base.OnPointerReleased(e)')
    content = content.replace('((Control)this).OnPropertyChanged(e)', 'base.OnPropertyChanged(e)')
    content = content.replace('((Layoutable)this).MeasureOverride', 'base.MeasureOverride')

    if content != orig:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)

for root, _, files in os.walk('src/AvaloniaEdit_Decompiled'):
    for file in files:
        if file.endswith('.cs'):
            fix_file(os.path.join(root, file))

