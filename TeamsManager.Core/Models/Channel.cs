using System;

namespace TeamsManager.Core.Models
{
    public class Channel
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        // Foreign key
        public string TeamId { get; set; } = string.Empty;
        public Team? Team { get; set; }
    }
}