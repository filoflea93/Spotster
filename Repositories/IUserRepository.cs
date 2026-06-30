using Spotster.Domain.Enums;
using Spotster.Entities;

namespace Spotster.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string normalizedEmail);
    Task AddAsync(User user);
    Task<(List<User> Items, int Total)> GetLeaderboardAsync(int page, int pageSize);
    Task<List<User>> SearchByUsernameAsync(string query, Guid excludeUserId, int limit);
    Task<int> CountByStatusAsync(UserStatus status);
    Task<int> CountAllAsync();
    Task<int> CountActiveSinceAsync(DateTime since);
    Task SaveChangesAsync();
}
