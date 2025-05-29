using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Data.Repositories
{
    public class OperationHistoryRepository : GenericRepository<OperationHistory>, IOperationHistoryRepository
    {
        public OperationHistoryRepository(TeamsManagerDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<OperationHistory>> GetHistoryForEntityAsync(string targetEntityType, string targetEntityId, int? count = null)
        {
            var query = _dbSet
                .Where(oh => oh.TargetEntityType == targetEntityType && oh.TargetEntityId == targetEntityId)
                .OrderByDescending(oh => oh.StartedAt);

            if (count.HasValue)
            {
                return await query.Take(count.Value).ToListAsync();
            }
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<OperationHistory>> GetHistoryByUserAsync(string userUpn, int? count = null)
        {
            var query = _dbSet
               .Where(oh => oh.CreatedBy == userUpn) // Zakładamy, że CreatedBy to UPN użytkownika wykonującego
               .OrderByDescending(oh => oh.StartedAt);

            if (count.HasValue)
            {
                return await query.Take(count.Value).ToListAsync();
            }
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<OperationHistory>> GetHistoryByDateRangeAsync(
            DateTime startDate, DateTime endDate, OperationType? operationType = null, OperationStatus? operationStatus = null)
        {
            var query = _dbSet.Where(oh => oh.StartedAt >= startDate && oh.StartedAt <= endDate);

            if (operationType.HasValue)
            {
                query = query.Where(oh => oh.Type == operationType.Value);
            }
            if (operationStatus.HasValue)
            {
                query = query.Where(oh => oh.Status == operationStatus.Value);
            }

            return await query.OrderByDescending(oh => oh.StartedAt).ToListAsync();
        }
    }
}