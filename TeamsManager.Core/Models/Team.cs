using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models
{
    public class Team
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public bool IsArchived { get; set; }

        // Navigation properties
        public List<TeamMember> Members { get; set; } = new List<TeamMember>();
        public List<Channel> Channels { get; set; } = new List<Channel>();
    }
}