using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Services.Dashboard
{
    /// <summary>
    /// Uproszczona implementacja IUserService tylko dla potrzeb Dashboard
    /// </summary>
    public class SimpleDashboardUserService : IUserService
    {
        private readonly List<User> _mockUsers;

        public SimpleDashboardUserService()
        {
            _mockUsers = new List<User>
            {
                new User 
                { 
                    Id = "1", 
                    UPN = "jan.kowalski@contoso.com",
                    FirstName = "Jan",
                    LastName = "Kowalski"
                },
                new User 
                { 
                    Id = "2", 
                    UPN = "anna.nowak@contoso.com",
                    FirstName = "Anna",
                    LastName = "Nowak"
                },
                new User 
                { 
                    Id = "3", 
                    UPN = "piotr.wisniewski@contoso.com",
                    FirstName = "Piotr",
                    LastName = "Wiśniewski"
                },
                new User 
                { 
                    Id = "4", 
                    UPN = "maria.kowalczyk@contoso.com",
                    FirstName = "Maria",
                    LastName = "Kowalczyk"
                }
            };
        }

        public Task<IEnumerable<User>> GetAllActiveUsersAsync(bool forceRefresh = false, string? accessToken = null)
        {
            var activeUsers = _mockUsers.Where(u => u.IsActive);
            return Task.FromResult<IEnumerable<User>>(activeUsers);
        }

        // Pozostałe metody - implementacje zaślepkowe
        public Task<User?> GetUserByIdAsync(string userId, bool forceRefresh = false, string? accessToken = null)
        {
            var user = _mockUsers.FirstOrDefault(u => u.Id == userId);
            return Task.FromResult(user);
        }

        public Task<User?> GetUserByUpnAsync(string upn, bool forceRefresh = false, string? accessToken = null)
        {
            var user = _mockUsers.FirstOrDefault(u => u.UPN == upn);
            return Task.FromResult(user);
        }

        public Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role, bool forceRefresh = false, string? accessToken = null)
        {
            return Task.FromResult<IEnumerable<User>>(new List<User>());
        }

        public Task<User?> CreateUserAsync(string firstName, string lastName, string upn, UserRole role, string departmentId, string password, string accessToken, bool sendWelcomeEmail = false)
        {
            return Task.FromResult<User?>(null);
        }

        public Task<bool> UpdateUserAsync(User userToUpdate, string accessToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> DeactivateUserAsync(string userId, string accessToken, bool deactivateM365Account = true)
        {
            return Task.FromResult(true);
        }

        public Task<bool> ActivateUserAsync(string userId, string accessToken, bool activateM365Account = true)
        {
            return Task.FromResult(true);
        }

        public Task<UserSchoolType?> AssignUserToSchoolTypeAsync(string userId, string schoolTypeId, DateTime assignedDate, DateTime? endDate = null, decimal? workloadPercentage = null, string? notes = null)
        {
            return Task.FromResult<UserSchoolType?>(null);
        }

        public Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId)
        {
            return Task.FromResult(true);
        }

        public Task<UserSubject?> AssignTeacherToSubjectAsync(string teacherId, string subjectId, DateTime assignedDate, string? notes = null)
        {
            return Task.FromResult<UserSubject?>(null);
        }

        public Task<bool> RemoveTeacherFromSubjectAsync(string userSubjectId)
        {
            return Task.FromResult(true);
        }

        public Task RefreshCacheAsync()
        {
            return Task.CompletedTask;
        }
    }
} 