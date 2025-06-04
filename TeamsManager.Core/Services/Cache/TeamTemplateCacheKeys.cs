using System;

namespace TeamsManager.Core.Services.Cache
{
    /// <summary>
    /// Klasa definiująca klucze cache dla szablonów zespołów.
    /// Centralizuje konwencje nazewnictwa kluczy cache.
    /// </summary>
    public static class TeamTemplateCacheKeys
    {
        private const string TeamTemplatePrefix = "TeamTemplate";
        
        /// <summary>
        /// Klucz dla konkretnego szablonu według ID
        /// </summary>
        public static string TeamTemplateById(string id) 
            => $"{TeamTemplatePrefix}_Id_{id}";
            
        /// <summary>
        /// Klucz dla listy wszystkich aktywnych szablonów
        /// </summary>
        public static string AllActiveTeamTemplates 
            => $"TeamTemplates_AllActive";
            
        /// <summary>
        /// Klucz dla listy szablonów uniwersalnych
        /// </summary>
        public static string UniversalTeamTemplates 
            => $"TeamTemplates_UniversalActive";
            
        /// <summary>
        /// Klucz dla szablonów według typu szkoły
        /// </summary>
        public static string TeamTemplatesBySchoolType(string schoolTypeId) 
            => $"TeamTemplates_BySchoolType_Id_{schoolTypeId}";
            
        /// <summary>
        /// Klucz dla domyślnego szablonu według typu szkoły
        /// </summary>
        public static string DefaultTeamTemplateBySchoolType(string schoolTypeId) 
            => $"TeamTemplate_Default_BySchoolType_Id_{schoolTypeId}";
    }
} 