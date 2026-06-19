using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var secret = "DJcEz4jbbLaA4WeBdGQ3tva0n8UXCrdm0ZVU42Ubc";
        var bbbUrl = "https://canli.monopoluzem.com.tr/bigbluebutton/api";
        var callbackUrl = "https://online.monopoluzem.com.tr/api/v1/bbb/webhook";
        var qs = "callbackURL=" + Uri.EscapeDataString(callbackUrl);
        var data = "hooks/create" + qs + secret;
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);
        var checksum = Convert.ToHexString(hash).ToLowerInvariant();
        var url = $"{bbbUrl}/hooks/create?{qs}&checksum={checksum}";
        
        Console.WriteLine($"URL: {url}");
        using var client = new HttpClient();
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {content}");
    }
}
