using System.Data;
using System.Globalization;
using System.Reflection;
using Dapper;
using ShikkhaDesk.Core;

namespace ShikkhaDesk.Infrastructure.Persistence;

/// <summary>
/// Dapper type handlers for the value types SQLite has no native mapping for. Registering these
/// keeps money in integer minor units and keeps GUID/date round-tripping exact and culture-invariant.
/// </summary>
internal static class DapperConfiguration
{
    private static int _configured;

    public static void EnsureConfigured()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 0)
        {
            // Map snake_case columns onto PascalCase properties deterministically.
            SqlMapper.TypeMapProvider = type => new CustomPropertyTypeMap(
                type,
                (target, columnName) => target.GetProperty(
                    columnName.Replace("_", string.Empty),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new InvalidOperationException(
                        $"কলাম '{columnName}' এর জন্য '{target.Name}' ক্লাসে কোনো প্রপার্টি পাওয়া যায়নি।"));

            SqlMapper.AddTypeHandler(new MoneyTypeHandler());
            SqlMapper.AddTypeHandler(new GuidTypeHandler());
            SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
            SqlMapper.AddTypeHandler(new NullableDateTimeTypeHandler());
        }
    }

    private sealed class MoneyTypeHandler : SqlMapper.TypeHandler<Money>
    {
        public override void SetValue(IDbDataParameter parameter, Money value)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = value.MinorUnits;
        }

        public override Money Parse(object value) => value is null or DBNull ? Money.Zero : value switch
        {
            long minor => Money.FromMinorUnits(minor),
            int intValue => Money.FromMinorUnits(intValue),
            _ => Money.FromMinorUnits(Convert.ToInt64(value, CultureInfo.InvariantCulture))
        };
    }

    private sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("D");
        }

        public override Guid Parse(object value) => value is null or DBNull ? Guid.Empty : value switch
        {
            Guid guid => guid,
            byte[] bytes => new Guid(bytes),
            _ => Guid.Parse(value.ToString() ?? string.Empty)
        };
    }

    private sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
    {
        private static readonly string[] Formats =
        [
            "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd"
        ];

        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        public override DateTime Parse(object value)
        {
            if (value is null or DBNull)
            {
                return default;
            }

            string? text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return default;
            }

            return DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified)
                : DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None);
        }
    }

    private sealed class NullableDateTimeTypeHandler : SqlMapper.TypeHandler<DateTime?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.HasValue
                ? value.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                : null;
        }

        public override DateTime? Parse(object value)
        {
            if (value is null or DBNull)
            {
                return null;
            }

            string? text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified)
                : null;
        }
    }
}
