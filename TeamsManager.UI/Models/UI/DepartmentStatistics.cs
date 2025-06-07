namespace TeamsManager.UI.Models.UI
{
    /// <summary>
    /// Model UI dla statystyk działu
    /// </summary>
    public class DepartmentStatistics
    {
        public int DirectUsersCount { get; set; }
        public int TotalUsersCount { get; set; }
        public int SubDepartmentsCount { get; set; }
        public int HierarchyLevel { get; set; }
        public string FullPath { get; set; } = string.Empty;
        public bool HasSubDepartments { get; set; }
    }
} 