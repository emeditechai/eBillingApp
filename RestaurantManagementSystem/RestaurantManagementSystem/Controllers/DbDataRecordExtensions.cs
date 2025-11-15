using System;
using System.Data;

namespace RestaurantManagementSystem.Controllers
{
    public static class DbDataRecordExtensions
    {
        public static bool ColumnExists(this IDataRecord reader, string columnName)
        {
            if (reader == null || string.IsNullOrWhiteSpace(columnName)) return false;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
