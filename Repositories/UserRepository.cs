using Spotster.Data;
using Spotster.Domain.Enums;
using Spotster.Entities;
using Microsoft.EntityFrameworkCore;

namespace Spotster.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(Guid id) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByUsernameAsync(string username) =>
        _db.Users.FirstOrDefaultAsync(u => u.UserName == username);

    public Task<User?> GetByEmailAsync(string normalizedEmail) =>
        _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

    public async Task AddAsync(User user) => await _db.Users.AddAsync(user);

    public async Task<(List<User> Items, int Total)> GetLeaderboardAsync(int page, int pageSize)
    {
        var query = _db.Users
            .Where(u => u.Status != UserStatus.Banned)
            .OrderByDescending(u => u.ReputationScore)
            .ThenByDescending(u => u.AccuracyRate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, total);
    }

    public Task<List<User>> SearchByUsernameAsync(string query, Guid excludeUserId, int limit)
    {
        query = query.Trim();
        return _db.Users
            .AsNoTracking()
            .Where(u =>
                u.Id != excludeUserId &&
                u.Status != UserStatus.Banned &&
                u.UserName!.Contains(query))
            .OrderBy(u => u.UserName)
            .Take(limit)
            .ToListAsync();
    }

    public Task<int> CountByStatusAsync(UserStatus status) =>
        _db.Users.CountAsync(u => u.Status == status);

    public Task<int> CountAllAsync() => _db.Users.CountAsync();

    public Task<int> CountActiveSinceAsync(DateTime since) =>
        _db.Users.CountAsync(u => u.LastActivityAt >= since);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
