using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.Serialization;

namespace TeamsManager.Core.Exceptions.PowerShell
{
    /// <summary>
    /// Wyjątek rzucany gdy występują problemy z połączeniem PowerShell lub sesją.
    /// </summary>
    [Serializable]
    public class PowerShellConnectionException : PowerShellException
    {
        /// <summary>
        /// URI do którego próbowano się połączyć.
        /// </summary>
        public string? ConnectionUri { get; init; }

        /// <summary>
        /// Metoda uwierzytelniania używana podczas połączenia.
        /// </summary>
        public string? AuthenticationMethod { get; init; }

        /// <summary>
        /// Timeout połączenia jeśli został określony.
        /// </summary>
        public TimeSpan? ConnectionTimeout { get; init; }

        /// <summary>
        /// Czy błąd wystąpił podczas próby odnowienia połączenia.
        /// </summary>
        public bool IsReconnectionAttempt { get; init; }

        /// <summary>
        /// Liczba prób połączenia przed wystąpieniem błędu.
        /// </summary>
        public int AttemptCount { get; init; } = 1;

        public PowerShellConnectionException(string message)
            : base(message)
        {
        }

        public PowerShellConnectionException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        public PowerShellConnectionException(
            string message,
            string? connectionUri = null,
            string? authenticationMethod = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            ConnectionUri = connectionUri;
            AuthenticationMethod = authenticationMethod;
        }

        /// <summary>
        /// Konstruktor z pełnym kontekstem połączenia.
        /// </summary>
        public PowerShellConnectionException(
            string message,
            IEnumerable<ErrorRecord>? errorRecords,
            IDictionary<string, object?>? contextData,
            string? connectionUri = null,
            string? authenticationMethod = null,
            Exception? innerException = null)
            : base(message, errorRecords, contextData, innerException)
        {
            ConnectionUri = connectionUri;
            AuthenticationMethod = authenticationMethod;
        }

        protected PowerShellConnectionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu tokenów.
        /// </summary>
        public static PowerShellConnectionException ForTokenError(string message, Exception? innerException = null)
        {
            return new PowerShellConnectionException(
                $"Błąd uwierzytelniania tokenu: {message}",
                innerException)
            {
                AuthenticationMethod = "AccessToken"
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu timeout.
        /// </summary>
        public static PowerShellConnectionException ForTimeout(TimeSpan timeout, string? connectionUri = null)
        {
            return new PowerShellConnectionException(
                $"Przekroczono timeout połączenia ({timeout.TotalSeconds}s)")
            {
                ConnectionTimeout = timeout,
                ConnectionUri = connectionUri
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu ponownego połączenia.
        /// </summary>
        public static PowerShellConnectionException ForReconnection(string message, int attemptCount, Exception? innerException = null)
        {
            return new PowerShellConnectionException(message, innerException)
            {
                IsReconnectionAttempt = true,
                AttemptCount = attemptCount
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu połączenia.
        /// </summary>
        public static PowerShellConnectionException ForConnectionFailed(
            string message, 
            Exception? innerException = null,
            string? connectionUri = null,
            string? authenticationMethod = null)
        {
            return new PowerShellConnectionException(message, innerException)
            {
                ConnectionUri = connectionUri,
                AuthenticationMethod = authenticationMethod
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla utraconego połączenia.
        /// </summary>
        public static PowerShellConnectionException ForConnectionLost(
            string message,
            Exception? innerException = null,
            string? connectionUri = null)
        {
            return new PowerShellConnectionException(message, innerException)
            {
                ConnectionUri = connectionUri,
                AuthenticationMethod = "AccessToken",
                IsReconnectionAttempt = false
            };
        }
    }
} 