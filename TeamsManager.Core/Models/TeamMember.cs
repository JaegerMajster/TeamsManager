using System;

namespace TeamsManager.Core.Models
{
    public class TeamMember
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public TeamMemberRole Role { get; set; }
        public DateTime AddedDate { get; set; }

        // Foreign key
        public string TeamId { get; set; } = string.Empty;
        public Team? Team { get; set; }
    }
}