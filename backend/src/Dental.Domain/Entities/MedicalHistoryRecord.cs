using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Bản chụp tiền sử bệnh và dị ứng tại một lần khám.</summary>
public sealed class MedicalHistoryRecord : BaseEntity
{
    public int RecordId { get; set; }
    public int PatientId { get; set; }
    public int VisitId { get; set; }
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
    public string? ItemsJson { get; set; }

    public Patient Patient { get; set; } = null!;
    public Visit Visit { get; set; } = null!;
    public User RecordedByUser { get; set; } = null!;
    [NotMapped]
    public ICollection<MedicalHistoryItem> Items { get; set; } = [];

    public List<MedicalHistoryItem> GetItems()
    {
        if (string.IsNullOrWhiteSpace(ItemsJson))
            return [.. Items];

        var list = JsonSerializer.Deserialize<List<MedicalHistoryItem>>(ItemsJson) ?? [];
        foreach (var item in list)
            item.RecordId = RecordId;
        return list;
    }

    public MedicalHistoryRecord HydrateItems()
    {
        Items = GetItems();
        return this;
    }

    public void SyncItemsJson()
    {
        var list = Items.ToList();
        var nextId = 1;
        foreach (var item in list)
        {
            item.RecordId = RecordId;
            if (item.ItemId <= 0)
                item.ItemId = nextId++;
            else
                nextId = Math.Max(nextId, item.ItemId + 1);
        }

        Items = list;
        ItemsJson = JsonSerializer.Serialize(list);
    }
}
