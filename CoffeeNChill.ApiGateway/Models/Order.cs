using System.Text.Json.Serialization;

namespace CoffeeNChill.ApiGateway.Models;

// Microsoft Learn - JSON serialization and deserialization in .NET - overview
// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview
// The queue message contract shared with the Part 2 producer and the queue trigger. These
// [JsonPropertyName] values are the wire names the consumer parses, so renaming any of them
// breaks the trigger that Team Member 2 wrote.
public sealed class Order
{
    [JsonPropertyName("OrderId")]
    public string OrderId { get; set; } = default!;

    [JsonPropertyName("CustomerName")]
    public string CustomerName { get; set; } = default!;

    [JsonPropertyName("SelectedItemSKUs")]
    public List<string> SelectedItemSKUs { get; set; } = new();

    [JsonPropertyName("TotalPrice")]
    public double TotalPrice { get; set; }

    [JsonPropertyName("OrderTimestamp")]
    public DateTime OrderTimestamp { get; set; }
}
