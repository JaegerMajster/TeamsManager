using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Helpers.PowerShell
{
    /// <summary>
    /// Builder do bezpiecznego tworzenia poleceń PowerShell dla Microsoft.Graph
    /// </summary>
    public class PowerShellCommandBuilder
    {
        private readonly StringBuilder _scriptBuilder;
        private readonly List<string> _parameters;
        private readonly List<string> _variables;
        private string? _commandName;
        private bool _includeErrorHandling;
        private bool _returnRawResult;

        public PowerShellCommandBuilder()
        {
            _scriptBuilder = new StringBuilder();
            _parameters = new List<string>();
            _variables = new List<string>();
            _includeErrorHandling = true;
            _returnRawResult = false;
        }

        /// <summary>
        /// Rozpoczyna budowanie komendy Microsoft.Graph
        /// </summary>
        public PowerShellCommandBuilder WithCommand(string commandName)
        {
            _commandName = PSParameterValidator.ValidateAndSanitizeString(commandName, nameof(commandName));
            return this;
        }

        /// <summary>
        /// Dodaje parametr do komendy
        /// </summary>
        public PowerShellCommandBuilder AddParameter(string parameterName, object? value)
        {
            if (value == null) return this;

            var sanitizedName = PSParameterValidator.ValidateAndSanitizeString(
                parameterName, 
                nameof(parameterName), 
                allowSpecialChars: false);

            string paramValue = value switch
            {
                string s => $"'{s}'", // Stringi już sanityzowane
                bool b => $"${b.ToString().ToLowerInvariant()}",
                int i => i.ToString(),
                long l => l.ToString(),
                string[] arr => $"@({string.Join(",", arr.Select(s => $"'{s}'"))})",
                Enum e => $"'{e}'",
                _ => $"'{value}'"
            };

            _parameters.Add($"-{sanitizedName} {paramValue}");
            return this;
        }

        /// <summary>
        /// Dodaje zmienną PowerShell
        /// </summary>
        public PowerShellCommandBuilder AddVariable(string variableName, string variableDefinition)
        {
            var sanitizedName = PSParameterValidator.ValidateAndSanitizeString(
                variableName, 
                nameof(variableName), 
                allowSpecialChars: false);
            
            _variables.Add($"${sanitizedName} = {variableDefinition}");
            return this;
        }

        /// <summary>
        /// Dodaje hashtable jako zmienną (np. dla -BodyParameter)
        /// </summary>
        public PowerShellCommandBuilder AddHashtableVariable(string variableName, Dictionary<string, object?> properties)
        {
            var hashtableBuilder = new StringBuilder();
            hashtableBuilder.AppendLine("@{");
            
            foreach (var (key, value) in properties)
            {
                if (value == null) continue;
                
                var sanitizedKey = PSParameterValidator.ValidateAndSanitizeString(key, "hashtableKey");
                
                string valueString = value switch
                {
                    string s => $"'{s}'",
                    bool b => $"${b.ToString().ToLowerInvariant()}",
                    int i => i.ToString(),
                    long l => l.ToString(),
                    string[] arr => $"@({string.Join(",", arr.Select(s => $"'{s}'"))})",
                    Dictionary<string, object?> dict => BuildNestedHashtable(dict),
                    _ => $"'{value}'"
                };
                
                hashtableBuilder.AppendLine($"    {sanitizedKey} = {valueString}");
            }
            
            hashtableBuilder.Append("}");
            
            return AddVariable(variableName, hashtableBuilder.ToString());
        }

        /// <summary>
        /// Dodaje obiekt Microsoft.Graph.AadUserConversationMember
        /// </summary>
        public PowerShellCommandBuilder AddGraphTeamMember(string variableName, string userId, TeamMemberRole role)
        {
            var memberDefinition = new Dictionary<string, object?>
            {
                ["@odata.type"] = "#microsoft.graph.aadUserConversationMember",
                ["roles"] = role == TeamMemberRole.Owner ? new[] { "owner" } : new[] { "member" },
                ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{userId}')"
            };
            
            return AddHashtableVariable(variableName, memberDefinition);
        }

        /// <summary>
        /// Włącza/wyłącza automatyczną obsługę błędów
        /// </summary>
        public PowerShellCommandBuilder WithErrorHandling(bool include = true)
        {
            _includeErrorHandling = include;
            return this;
        }

        /// <summary>
        /// Określa czy zwrócić surowy wynik czy przetworzyć
        /// </summary>
        public PowerShellCommandBuilder ReturnRawResult(bool raw = true)
        {
            _returnRawResult = raw;
            return this;
        }

        /// <summary>
        /// Dodaje warunek warunkowego wykonania
        /// </summary>
        public PowerShellCommandBuilder AddConditionalExecution(string condition, string trueScript, string? falseScript = null)
        {
            _scriptBuilder.AppendLine($"if ({condition}) {{");
            _scriptBuilder.AppendLine($"    {trueScript}");
            _scriptBuilder.AppendLine("}");
            
            if (falseScript != null)
            {
                _scriptBuilder.AppendLine("else {");
                _scriptBuilder.AppendLine($"    {falseScript}");
                _scriptBuilder.AppendLine("}");
            }
            
            return this;
        }

        /// <summary>
        /// Buduje pełny skrypt PowerShell
        /// </summary>
        public string Build()
        {
            if (string.IsNullOrWhiteSpace(_commandName))
            {
                throw new InvalidOperationException("Command name must be specified using WithCommand()");
            }

            var finalScript = new StringBuilder();

            // Dodaj zmienne
            foreach (var variable in _variables)
            {
                finalScript.AppendLine(variable);
            }

            if (_variables.Any())
            {
                finalScript.AppendLine(); // Pusta linia dla czytelności
            }

            // Dodaj główną komendę
            if (_includeErrorHandling)
            {
                finalScript.AppendLine("try {");
                finalScript.Append("    ");
            }

            finalScript.Append(_commandName);
            
            foreach (var parameter in _parameters)
            {
                finalScript.Append(" ").Append(parameter);
            }

            if (_includeErrorHandling)
            {
                finalScript.AppendLine(" -ErrorAction Stop");
                finalScript.AppendLine("}");
                finalScript.AppendLine("catch {");
                finalScript.AppendLine("    throw $_");
                finalScript.AppendLine("}");
            }
            else
            {
                finalScript.AppendLine();
            }

            // Dodaj custom script jeśli jest
            if (_scriptBuilder.Length > 0)
            {
                finalScript.AppendLine();
                finalScript.Append(_scriptBuilder);
            }

            // Obsługa wyniku
            if (!_returnRawResult && !_commandName.StartsWith("Remove-") && !_commandName.StartsWith("Update-"))
            {
                // Dla komend Get zwróć id lub cały obiekt
                if (_commandName.StartsWith("Get-"))
                {
                    finalScript.AppendLine();
                    finalScript.AppendLine("# Return the result");
                    finalScript.AppendLine("$result");
                }
                else if (_commandName.StartsWith("New-"))
                {
                    // Dla New- zwróć ID utworzonego obiektu
                    finalScript.AppendLine();
                    finalScript.AppendLine("# Return the ID of created object");
                    finalScript.AppendLine("if ($result.Id) { $result.Id } else { $result }");
                }
            }

            return finalScript.ToString();
        }

        /// <summary>
        /// Buduje komendę jako jednolinijkową (dla ExecuteCommandWithRetryAsync)
        /// </summary>
        public string BuildCommand()
        {
            if (string.IsNullOrWhiteSpace(_commandName))
            {
                throw new InvalidOperationException("Command name must be specified using WithCommand()");
            }

            var command = new StringBuilder(_commandName);
            
            foreach (var parameter in _parameters)
            {
                command.Append(" ").Append(parameter);
            }

            if (_includeErrorHandling)
            {
                command.Append(" -ErrorAction Stop");
            }

            return command.ToString();
        }

        /// <summary>
        /// Buduje słownik parametrów dla ExecuteCommandWithRetryAsync
        /// </summary>
        public Dictionary<string, object> BuildParameters()
        {
            var parameters = new Dictionary<string, object>();
            
            foreach (var param in _parameters)
            {
                var parts = param.TrimStart('-').Split(' ', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0];
                    var value = parts[1].Trim('\'', '"', '$');
                    
                    // Konwersja wartości
                    if (bool.TryParse(value, out var boolValue))
                        parameters[key] = boolValue;
                    else if (int.TryParse(value, out var intValue))
                        parameters[key] = intValue;
                    else
                        parameters[key] = value;
                }
            }
            
            return parameters;
        }

        // Metody pomocnicze dla często używanych komend Microsoft.Graph

        /// <summary>
        /// Tworzy builder dla New-MgTeam
        /// </summary>
        public static PowerShellCommandBuilder CreateNewTeamCommand(
            string displayName, 
            string description,
            TeamVisibility visibility,
            string ownerId,
            string? templateId = null)
        {
            var builder = new PowerShellCommandBuilder()
                .WithCommand("New-MgTeam");

            var teamBody = new Dictionary<string, object?>
            {
                ["displayName"] = PSParameterValidator.ValidateAndSanitizeString(displayName, "displayName"),
                ["description"] = PSParameterValidator.ValidateAndSanitizeString(description, "description", allowEmpty: true),
                ["visibility"] = visibility.ToString().ToLowerInvariant()
            };

            // Dodaj właściciela
            teamBody["members"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["@odata.type"] = "#microsoft.graph.aadUserConversationMember",
                    ["roles"] = new[] { "owner" },
                    ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{ownerId}')"
                }
            };

            // Dodaj szablon jeśli podany
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                teamBody["template@odata.bind"] = $"https://graph.microsoft.com/v1.0/teamsTemplates('{templateId}')";
            }

            builder.AddHashtableVariable("teamBody", teamBody)
                   .AddParameter("BodyParameter", "$teamBody");

            return builder;
        }

        /// <summary>
        /// Tworzy builder dla Get-MgUser z filtrem
        /// </summary>
        public static PowerShellCommandBuilder CreateGetUsersCommand(string? filter = null, int top = 999)
        {
            var builder = new PowerShellCommandBuilder()
                .WithCommand("Get-MgUser")
                .AddParameter("All", true)
                .AddParameter("PageSize", top);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                // Graph API wymaga specjalnego formatowania filtrów
                var sanitizedFilter = PSParameterValidator.ValidateAndSanitizeString(filter, "filter");
                builder.AddParameter("Filter", sanitizedFilter);
            }

            return builder;
        }

        private string BuildNestedHashtable(Dictionary<string, object?> dict)
        {
            var sb = new StringBuilder("@{");
            foreach (var (key, value) in dict)
            {
                if (value == null) continue;
                
                var valueString = value switch
                {
                    string s => $"'{s}'",
                    bool b => $"${b.ToString().ToLowerInvariant()}",
                    _ => $"'{value}'"
                };
                
                sb.Append($"{key}={valueString};");
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
} 