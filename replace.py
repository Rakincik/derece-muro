# -*- coding: utf-8 -*-
import io

b64 = io.open('blank_169.txt', 'r', encoding='utf-8').read().strip()
with io.open('src/MURO.Infrastructure/Services/BbbService.cs', 'r', encoding='utf-8') as f:
    content = f.read()

old_str = '<document name="blank.pdf">JVBERi0xLjQKMSAwIG9iaiA8PC9UeXBlL0NhdGFsb2cvUGFnZXMgMiAwIFI+PiBlbmRvYmoKMiAwIG9iaiA8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PiBlbmRvYmoKMyAwIG9iaiA8PC9UeXBlL1BhZ2UvUGFyZW50IDIgMCBSL01lZGlhQm94WzAgMCAxOTIwIDEwODBdPj4gZW5kb2JqCnhyZWYKMCA0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU0IDAwMDAwIG4gCjAwMDAwMDAxMDUgMDAwMDAgbiAKdHJhaWxlciA8PC9TaXplIDQvUm9vdCAxIDAgUj4+CnN0YXJ0eHJlZgoxNzgKJSVFT0YK</document>'
new_str = '<document name="blank.png">' + b64 + '</document>'

content = content.replace(old_str, new_str)
content = content.replace('16:9 oranında boş bir PDF', '16:9 oranında boş bir PNG')

with io.open('src/MURO.Infrastructure/Services/BbbService.cs', 'w', encoding='utf-8', newline='') as f:
    f.write(content)
