using System;
using System.Collections.Generic;
using System.Reflection;

namespace TeamsManager.Core.Helpers
{
    /// <summary>
    /// Klasa pomocnicza do mapowania właściwości z obiektów Microsoft Graph API
    /// </summary>
    public static class GraphModelMapper
    {
        /// <summary>
        /// Pobiera wartość string z obiektu Graph
        /// </summary>
        /// <param name="graphObject">Obiekt Graph</param>
        /// <param name="propertyName">Nazwa właściwości</param>
        /// <param name="defaultValue">Wartość domyślna</param>
        /// <returns>Wartość string lub wartość domyślna</returns>
        public static string? GetString(object? graphObject, string propertyName, string? defaultValue = null)
        {
            if (graphObject == null) return defaultValue;

            try
            {
                var property = graphObject.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    var value = property.GetValue(graphObject);
                    return value?.ToString() ?? defaultValue;
                }
            }
            catch
            {
                // Ignoruj błędy i zwróć wartość domyślną
            }

            return defaultValue;
        }

        /// <summary>
        /// Pobiera wartość boolean z obiektu Graph
        /// </summary>
        /// <param name="graphObject">Obiekt Graph</param>
        /// <param name="propertyName">Nazwa właściwości</param>
        /// <param name="defaultValue">Wartość domyślna</param>
        /// <returns>Wartość boolean lub wartość domyślna</returns>
        public static bool GetBoolean(object? graphObject, string propertyName, bool defaultValue = false)
        {
            if (graphObject == null) return defaultValue;

            try
            {
                var property = graphObject.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    var value = property.GetValue(graphObject);
                    if (value is bool boolValue)
                        return boolValue;
                    if (value != null && bool.TryParse(value.ToString(), out var parsedValue))
                        return parsedValue;
                }
            }
            catch
            {
                // Ignoruj błędy i zwróć wartość domyślną
            }

            return defaultValue;
        }

        /// <summary>
        /// Pobiera wartość DateTime z obiektu Graph
        /// </summary>
        /// <param name="graphObject">Obiekt Graph</param>
        /// <param name="propertyName">Nazwa właściwości</param>
        /// <returns>Wartość DateTime lub null</returns>
        public static DateTime? GetDateTime(object? graphObject, string propertyName)
        {
            if (graphObject == null) return null;

            try
            {
                var property = graphObject.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    var value = property.GetValue(graphObject);
                    if (value is DateTime dateValue)
                        return dateValue;
                    if (value is DateTimeOffset dateOffsetValue)
                        return dateOffsetValue.DateTime;
                    if (value != null && DateTime.TryParse(value.ToString(), out var parsedValue))
                        return parsedValue;
                }
            }
            catch
            {
                // Ignoruj błędy i zwróć null
            }

            return null;
        }

        /// <summary>
        /// Pobiera wartość nullable int z obiektu Graph
        /// </summary>
        /// <param name="graphObject">Obiekt Graph</param>
        /// <param name="propertyName">Nazwa właściwości</param>
        /// <returns>Wartość int lub null</returns>
        public static int? GetNullableInt32(object? graphObject, string propertyName)
        {
            if (graphObject == null) return null;

            try
            {
                var property = graphObject.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    var value = property.GetValue(graphObject);
                    if (value is int intValue)
                        return intValue;
                    if (value != null && int.TryParse(value.ToString(), out var parsedValue))
                        return parsedValue;
                }
            }
            catch
            {
                // Ignoruj błędy i zwróć null
            }

            return null;
        }
    }
} 