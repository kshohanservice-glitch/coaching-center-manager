using System.Text.Json;
using System.Text.Json.Serialization;
using ShikkhaDesk.Core;

namespace ShikkhaDesk.Lan;

/// <summary>
/// Money crosses the wire as an integer number of minor units. A decimal or textual form would
/// reintroduce exactly the rounding drift the ledger is designed to avoid.
/// </summary>
public sealed class MoneyConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            System.Text.Json.JsonTokenType.Number => Money.FromMinorUnits(reader.GetInt64()),
            System.Text.Json.JsonTokenType.String => Money.Parse(reader.GetString()),
            _ => Money.Zero
        };
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.MinorUnits);
}
