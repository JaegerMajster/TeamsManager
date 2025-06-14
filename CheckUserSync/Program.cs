using System;
using System.IO;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamsManager", "teamsmanager.db");
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Baza danych nie istnieje: {dbPath}");
            return;
        }
        
        Console.WriteLine($"Sprawdzam bazę danych: {dbPath}");
        
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        
        // Sprawdź wszystkich użytkowników
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, UPN, FirstName, LastName, Role, IsActive, CreatedBy, CreatedDate FROM Users ORDER BY CreatedDate DESC";
        
        using var reader = command.ExecuteReader();
        
        Console.WriteLine("\n=== WSZYSCY UŻYTKOWNICY ===");
        Console.WriteLine("UPN | FirstName | LastName | Role | IsActive | CreatedBy | CreatedDate");
        Console.WriteLine(new string('-', 100));
        
        while (reader.Read())
        {
            Console.WriteLine($"{reader["UPN"]} | {reader["FirstName"]} | {reader["LastName"]} | {reader["Role"]} | {reader["IsActive"]} | {reader["CreatedBy"]} | {reader["CreatedDate"]}");
        }
        
        reader.Close();
        
        // Sprawdź operacje historii związane z użytkownikami
        command.CommandText = "SELECT Type, TargetEntityType, TargetEntityName, Status, CreatedBy, CreatedDate, OperationDetails FROM OperationHistory WHERE TargetEntityType = 'User' OR OperationDetails LIKE '%synchroniz%' OR OperationDetails LIKE '%Synchroniz%' ORDER BY CreatedDate DESC LIMIT 10";
        
        using var historyReader = command.ExecuteReader();
        
        Console.WriteLine("\n=== HISTORIA OPERACJI (User/Synchronization) ===");
        Console.WriteLine("Type | TargetEntityType | TargetEntityName | Status | CreatedBy | CreatedDate | Details");
        Console.WriteLine(new string('-', 150));
        
        while (historyReader.Read())
        {
            Console.WriteLine($"{historyReader["Type"]} | {historyReader["TargetEntityType"]} | {historyReader["TargetEntityName"]} | {historyReader["Status"]} | {historyReader["CreatedBy"]} | {historyReader["CreatedDate"]} | {historyReader["OperationDetails"]}");
        }
    }
} 