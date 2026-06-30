using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MURO.Domain.Entities;

namespace MURO.Infrastructure.Persistence;

public class AuditEntry
{
    public EntityEntry Entry { get; }
    public EntityState State { get; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string TableName { get; set; } = string.Empty;
    public Dictionary<string, object?> KeyValues { get; } = new();
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();

    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
        State = entry.State;
    }

    public AuditLog ToAuditLog()
    {
        var audit = new AuditLog
        {
            UserId = UserId,
            UserName = UserName,
            IpAddress = IpAddress,
            EntityType = TableName,
            CreatedAt = DateTime.UtcNow
        };

        // Action type
        if (State == EntityState.Added)
        {
            audit.Action = "Create";
        }
        else if (State == EntityState.Deleted)
        {
            audit.Action = "Delete";
        }
        else
        {
            audit.Action = "Update";
        }

        // Retrieve primary keys
        var idList = new List<string>();
        foreach (var key in KeyValues)
        {
            idList.Add($"{key.Value}");
        }
        audit.EntityId = string.Join(",", idList);

        // Retrieve display name of the modified object
        audit.EntityName = GetEntityDisplayName();

        // Build formatted changes log (JSON)
        var changesDict = new Dictionary<string, object>();
        
        if (State == EntityState.Added)
        {
            var properties = new Dictionary<string, object?>();
            foreach (var kvp in NewValues)
            {
                if (kvp.Key == "PasswordHash")
                {
                    properties[kvp.Key] = new { old = (string?)null, @new = "[Şifrelenmiş Değer]" };
                }
                else if (kvp.Value != null)
                {
                    properties[kvp.Key] = new { old = (string?)null, @new = kvp.Value.ToString() };
                }
            }
            changesDict["action"] = "Create";
            changesDict["changes"] = properties;
        }
        else if (State == EntityState.Deleted)
        {
            var properties = new Dictionary<string, object?>();
            foreach (var kvp in OldValues)
            {
                if (kvp.Key == "PasswordHash") continue;
                if (kvp.Value != null)
                {
                    properties[kvp.Key] = new { old = kvp.Value.ToString(), @new = (string?)null };
                }
            }
            changesDict["action"] = "Delete";
            changesDict["changes"] = properties;
        }
        else // Modified
        {
            var properties = new Dictionary<string, object?>();
            foreach (var kvp in NewValues)
            {
                var propName = kvp.Key;
                OldValues.TryGetValue(propName, out var oldValue);
                var newValue = kvp.Value;

                if (Equals(oldValue, newValue)) continue;

                if (propName == "PasswordHash")
                {
                    properties[propName] = new { old = "[Eski Şifre]", @new = "[Şifre Değiştirildi]" };
                }
                else
                {
                    properties[propName] = new { old = oldValue?.ToString(), @new = newValue?.ToString() };
                }
            }
            changesDict["action"] = "Update";
            changesDict["changes"] = properties;
        }

        audit.Details = JsonSerializer.Serialize(changesDict);

        return audit;
    }

    private string? GetEntityDisplayName()
    {
        var entity = Entry.Entity;

        if (entity is User user)
        {
            return $"{user.FirstName} {user.LastName}".Trim();
        }

        var properties = entity.GetType().GetProperties();
        string? nameValue = null;
        string? titleValue = null;
        string? emailValue = null;
        string? auditDisplayName = null;

        foreach (var prop in properties)
        {
            if (prop.Name.Equals("AuditDisplayName", StringComparison.OrdinalIgnoreCase))
                auditDisplayName = prop.GetValue(entity)?.ToString();
            else if (prop.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("Ad", StringComparison.OrdinalIgnoreCase))
                nameValue = prop.GetValue(entity)?.ToString();
            else if (prop.Name.Equals("Title", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("Baslik", StringComparison.OrdinalIgnoreCase))
                titleValue = prop.GetValue(entity)?.ToString();
            else if (prop.Name.Equals("Email", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("Eposta", StringComparison.OrdinalIgnoreCase))
                emailValue = prop.GetValue(entity)?.ToString();
        }

        return auditDisplayName ?? nameValue ?? titleValue ?? emailValue ?? TableName;
    }
}
