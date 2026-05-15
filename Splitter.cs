using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System;

class Program {
    static void Main() {
        var sourceFile = @"c:\Users\Rüstem\.gemini\antigravity\scratch\muro-v2\src\MURO.Infrastructure\Services\AdminService.cs";
        var content = File.ReadAllText(sourceFile);
        
        var methods = new Dictionary<string, string>();
        
        // Match public async Task... and public (int, object?) ...
        var matches = Regex.Matches(content, @"(public\s+(?:async\s+)?(?:Task<\(int,\s*object\?\)>|\(int,\s*object\?\))\s+(\w+).*?\n    })", RegexOptions.Singleline);
        
        foreach(Match match in matches) {
            methods[match.Groups[2].Value] = match.Groups[1].Value;
        }

        Console.WriteLine("Found " + methods.Count + " methods.");
        foreach(var m in methods.Keys) {
            Console.WriteLine(m);
        }
    }
}
