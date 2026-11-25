using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Danh mục dịch vụ nha khoa và lịch sử giá dịch vụ.</summary>
public sealed class DentalService : BaseEntity
{
    public int DentalServiceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal UnitPrice { get; set; }
    public DateTime? PriceEffectiveFrom { get; set; }
    public string? PriceHistoryJson { get; set; }

    [NotMapped]
    public ICollection<ServicePrice> Prices { get; set; } = [];

    public List<ServicePrice> GetPriceHistory()
    {
        if (string.IsNullOrWhiteSpace(PriceHistoryJson))
            return [.. Prices];

        var list = JsonSerializer.Deserialize<List<ServicePrice>>(PriceHistoryJson) ?? [];
        foreach (var item in list)
            item.ServiceId = DentalServiceId;
        return list;
    }

    public void HydratePrices()
    {
        Prices = GetPriceHistory();
    }

    public void SyncPriceHistory(IEnumerable<ServicePrice> prices, DateTime? asOfUtc = null)
    {
        var list = prices.ToList();
        var nextId = list.Count == 0 ? 1 : list.Max(x => x.ServicePriceId) + 1;
        if (nextId <= 0) nextId = 1;
        foreach (var item in list)
        {
            item.ServiceId = DentalServiceId;
            if (item.ServicePriceId <= 0)
                item.ServicePriceId = nextId++;
        }

        Prices = list;
        PriceHistoryJson = JsonSerializer.Serialize(list);

        var now = asOfUtc ?? DateTime.UtcNow;
        var current = list
            .Where(x => x.EffectiveFrom <= now)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.ServicePriceId)
            .FirstOrDefault()
            ?? list.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.ServicePriceId).FirstOrDefault();

        if (current is not null)
        {
            UnitPrice = current.Amount;
            PriceEffectiveFrom = current.EffectiveFrom;
        }
    }

    public ServicePrice AppendPrice(ServicePrice price, DateTime? asOfUtc = null)
    {
        var list = GetPriceHistory();
        if (price.ServicePriceId <= 0)
            price.ServicePriceId = list.Count == 0 ? 1 : Math.Max(1, list.Max(x => x.ServicePriceId) + 1);
        price.ServiceId = DentalServiceId;
        list.Add(price);
        SyncPriceHistory(list, asOfUtc);
        return price;
    }
}
