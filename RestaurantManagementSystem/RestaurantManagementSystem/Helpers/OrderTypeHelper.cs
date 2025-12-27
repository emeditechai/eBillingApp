using System;
using System.Collections.Generic;
using System.Linq;

namespace RestaurantManagementSystem.Helpers
{
    public static class OrderTypeHelper
    {
        public const string AllowedOrderTypesCacheKey = "RestaurantSettings:SelectedOrderTypeIds:v1";
        public const string AllowedOrderTypesCacheVersionKey = "RestaurantSettings:SelectedOrderTypeIds:ver";

        // Must match the dropdown values in Views/Order/Create.cshtml
        public static IReadOnlyList<(int Id, string Name)> GetOrderTypes() => new List<(int, string)>
        {
            (0, "Dine-In"),
            (1, "Takeout"),
            (2, "Delivery"),
            (4, "Room Service"),
        };

        public static string GetOrderTypeName(int id)
        {
            var match = GetOrderTypes().FirstOrDefault(x => x.Id == id);
            return match == default ? id.ToString() : match.Name;
        }

        public static List<int> ParseCsvIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<int>();

            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Select(x => int.TryParse(x, out var id) ? (int?)id : null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        public static string ToCsv(IEnumerable<int>? ids)
        {
            if (ids == null) return string.Empty;

            return string.Join(",", ids
                .Distinct()
                .OrderBy(x => x)
                .Select(x => x.ToString()));
        }
    }
}
