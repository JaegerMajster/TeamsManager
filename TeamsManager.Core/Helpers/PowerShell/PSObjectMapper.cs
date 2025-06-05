using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using Microsoft.Extensions.Logging;

namespace TeamsManager.Core.Helpers.PowerShell
{
    /// <summary>
    /// Pomocnik do bezpiecznego mapowania właściwości PSObject na typy .NET
    /// </summary>
    public static class PSObjectMapper
    {
        /// <summary>
        /// Bezpiecznie pobiera wartość string z PSObject
        /// </summary>
        public static string? GetString(PSObject psObject, string propertyName, string? defaultValue = null)
        {
            try
            {
                var value = psObject.Properties[propertyName]?.Value;
                if (value == null) return defaultValue;
                
                var stringValue = value.ToString();
                return string.IsNullOrWhiteSpace(stringValue) ? defaultValue : stringValue.Trim();
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Bezpiecznie pobiera wartość int z PSObject
        /// </summary>
        public static int GetInt32(PSObject psObject, string propertyName, int defaultValue = 0)
        {
            try
            {
                var value = psObject.Properties[propertyName]?.Value;
                if (value == null) return defaultValue;

                return value switch
                {
                    int intValue => intValue,
                    long longValue => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, longValue)),
                    string strValue when int.TryParse(strValue, out var parsed) => parsed,
                    _ => defaultValue
                };
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Bezpiecznie pobiera wartość nullable int z PSObject
        /// </summary>
        public static int? GetNullableInt32(PSObject psObject, string propertyName)
        {
            try
            {
                var value = psObject.Properties[propertyName]?.Value;
                if (value == null) return null;

                return value switch
                {
                    int intValue => intValue,
                    long longValue => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, longValue)),
                    string strValue when int.TryParse(strValue, out var parsed) => parsed,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Bezpiecznie pobiera wartość long z PSObject
        /// </summary>
        public static long GetInt64(PSObject psObject, string propertyName, long defaultValue = 0)
        {
            try
            {
                var value = psObject.Properties[propertyName]?.Value;
                if (value == null) return defaultValue;

                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    string strValue when long.TryParse(strValue, out var parsed) => parsed,
                    _ => defaultValue
                };
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Bezpiecznie pobiera wartość nullable long z PSObject
        /// </summary>
        public static long? GetNullableInt64(PSObject psObject, string propertyName)
        {
            try
            {
                var value = psObject.Properties[propertyName]?.Value;
                if (value == null) return null;

                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    string strValue when long.TryParse(strValue, out var parsed) => parsed,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Bezpiecznie pobiera wartość bool z PSObject
        /// </summary>
        public static bool GetBoolean(PSObject psObject, string propertyName, bool defaultValue = false)
        {
            try
            {
                var value = psObject.Properties[propertyName]?.Value;
                if (value == null) return defaultValue;

                return value switch
                {
                    bool boolValue => boolValue,
                    string strValue => strValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                      strValue.Equals("1", StringComparison.Ordinal),
                    int intValue => intValue != 0,
                    _ => defaultValue
                };
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Bezpiecznie pobiera wartość DateTime z PSObject
        /// </summary>
        public static DateTime? GetDateTime(PSObject psObject, string propertyName)
        {
            try
            {
                var value = psObject.Properties[propertyName]?.Value;
                if (value == null) return null;

                return value switch
                {
                    DateTime dateTime => dateTime,
                    DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
                    string strValue when DateTime.TryParse(strValue, CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out var parsed) => parsed,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sprawdza czy właściwość istnieje w PSObject
        /// </summary>
        public static bool HasProperty(PSObject psObject, string propertyName)
        {
            return psObject.Properties[propertyName] != null;
        }

        /// <summary>
        /// Pobiera wszystkie nazwy właściwości z PSObject
        /// </summary>
        public static IEnumerable<string> GetPropertyNames(PSObject psObject)
        {
            return psObject.Properties.Select(p => p.Name);
        }

        /// <summary>
        /// Loguje wszystkie właściwości PSObject (do debugowania)
        /// </summary>
        public static void LogProperties(PSObject psObject, ILogger logger, string context)
        {
            logger.LogDebug("PSObject properties for {Context}:", context);
            foreach (var prop in psObject.Properties)
            {
                logger.LogDebug("  {PropertyName}: {Value} ({Type})", 
                    prop.Name, 
                    prop.Value?.ToString() ?? "<null>",
                    prop.Value?.GetType().Name ?? "null");
            }
        }
    }
} 