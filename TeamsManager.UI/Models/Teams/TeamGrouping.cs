using System.Collections.ObjectModel;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.Models.Teams
{
    public class TeamGrouping
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public int TeamCount => Teams.Count;
        public ObservableCollection<Team> Teams { get; set; } = new();
    }
} 