using System.IO;
using System.Text;

class Program
{
    static void Main()
    {
        string b64 = File.ReadAllText("blank_169.txt").Trim();
        string filePath = @"src\MURO.Infrastructure\Services\BbbService.cs";
        string content = File.ReadAllText(filePath, new UTF8Encoding(false));

        string oldStr = "<document name=\"blank.pdf\">JVBERi0xLjQKMSAwIG9iaiA8PC9UeXBlL0NhdGFsb2cvUGFnZXMgMiAwIFI+PiBlbmRvYmoKMiAwIG9iaiA8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PiBlbmRvYmoKMyAwIG9iaiA8PC9UeXBlL1BhZ2UvUGFyZW50IDIgMCBSL01lZGlhQm94WzAgMCAxOTIwIDEwODBdPj4gZW5kb2JqCnhyZWYKMCA0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU0IDAwMDAwIG4gCjAwMDAwMDAxMDUgMDAwMDAgbiAKdHJhaWxlciA8PC9TaXplIDQvUm9vdCAxIDAgUj4+CnN0YXJ0eHJlZgoxNzgKJSVFT0YK</document>";
        string newStr = "<document name=\"blank.png\">" + b64 + "</document>";

        content = content.Replace(oldStr, newStr);
        content = content.Replace("16:9 oranýnda boþ bir PDF", "16:9 oranýnda boþ bir PNG");

        File.WriteAllText(filePath, content, new UTF8Encoding(false));
    }
}
