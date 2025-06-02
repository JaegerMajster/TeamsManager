using System;
using System.Collections.Generic;

namespace TeamsManager.UI.Models
{
    public class TestCase
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new();
        public string ExpectedResult { get; set; } = string.Empty;
        public TestResult Result { get; set; } = TestResult.NotRun;
        public string Notes { get; set; } = string.Empty;
        public DateTime? ExecutedAt { get; set; }
        public string? ErrorDetails { get; set; }
        public bool IsAutomatable { get; set; } = false;
        public string? ApiEndpoint { get; set; }
        public string Priority { get; set; } = "Medium"; // High, Medium, Low
        
        // Nowe właściwości dla automatycznych testów
        public bool HasAutomaticExecution { get; set; } = false;
        public string? AutoExecuteButtonText { get; set; }
    }

    public enum TestResult
    {
        NotRun,
        Pass,
        Fail,
        Skip,
        Warning
    }

    public class TestSuite
    {
        public string Name { get; set; } = "TeamsManager Manual Tests";
        public string Version { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<TestCase> TestCases { get; set; } = new();
        
        public int TotalTests => TestCases.Count;
        public int PassedTests => TestCases.Count(t => t.Result == TestResult.Pass);
        public int FailedTests => TestCases.Count(t => t.Result == TestResult.Fail);
        public int SkippedTests => TestCases.Count(t => t.Result == TestResult.Skip);
        public int WarningTests => TestCases.Count(t => t.Result == TestResult.Warning);
        public int NotRunTests => TestCases.Count(t => t.Result == TestResult.NotRun);
        
        public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
    }
} 