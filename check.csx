using System;
using Microsoft.EntityFrameworkCore;
using MURO.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var services = new ServiceCollection();
var config = new ConfigurationBuilder().AddJsonFile("src/MURO.API/appsettings.json").Build();
services.AddDbContext<MuroDbContext>(opts => opts.UseNpgsql(config.GetConnectionString("DefaultConnection")));
var provider = services.BuildServiceProvider();
var db = provider.GetRequiredService<MuroDbContext>();

var assets = db.MediaAssets.AsNoTracking().ToList();
foreach(var a in assets) {
    Console.WriteLine($"Asset: {a.Title} | Status: {a.Status} | Path: {a.FilePath}");
}
