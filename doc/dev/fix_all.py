import os
import glob
import re

for root, _, files in os.walk('src/AvaloniaEdit_Decompiled'):
    for file in files:
        if file.endswith('.cs'):
            path = os.path.join(root, file)
            with open(path, 'r', encoding='utf-8') as f:
                content = f.read()
                
            orig = content
            
            # fix _002Ector
            content = re.sub(r'([a-zA-Z0-9_]+)\._002Ector\(([^)]*)\);', r'\1 = new (\2);', content)
            
            # fix CS0019 DragDropEffects
            if "DragDropEffects" in content:
                content = content.replace('(e.DragEffects & 2) == 2', 'e.DragEffects.HasFlag(DragDropEffects.Move)')
                content = content.replace('(e.KeyModifiers & 2) != 2', '(!e.KeyModifiers.HasFlag(KeyModifiers.Control))')
                content = content.replace('e.DragEffects & 1', 'e.DragEffects & DragDropEffects.Copy')
                content = content.replace('(allowedEffects & -3)', '(allowedEffects & ~DragDropEffects.Move)')
                content = content.replace('(allowedEffects & 2) != 2', '(!allowedEffects.HasFlag(DragDropEffects.Move))')
            
            # fix CS0019 PointerType
            if "PointerType" in content:
                content = content.replace('type - 1 <= 1', 'type == PointerType.Mouse || type == PointerType.Touch')
            
            # fix VisualChildren and protected members
            content = content.replace('base.VisualChildren', 'this.GetVisualChildren()')
            content = content.replace('VisualChildren', 'GetVisualChildren()')  # rough
            
            if content != orig:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(content)

