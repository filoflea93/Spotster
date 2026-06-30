using Spotster.Domain.Enums;
using Spotster.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Spotster.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ParkingReport> ParkingReports => Set<ParkingReport>();
    public DbSet<ParkingRequest> ParkingRequests => Set<ParkingRequest>();
    public DbSet<ParkingRequestMessage> ParkingRequestMessages => Set<ParkingRequestMessage>();
    public DbSet<ReportVote> ReportVotes => Set<ReportVote>();
    public DbSet<UserReview> UserReviews => Set<UserReview>();
    public DbSet<ParkingRequestBlock> ParkingRequestBlocks => Set<ParkingRequestBlock>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.UserName).HasColumnName("Username").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.DeviceFingerprintHash).HasMaxLength(128);
            entity.Property(e => e.LastIpHash).HasMaxLength(128);
            entity.Property(e => e.ProfilePhotoUrl).HasMaxLength(500);
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DateOfBirth).HasColumnType("date");
            entity.Property(e => e.AccuracyRate).HasPrecision(5, 2);
            entity.HasIndex(e => e.ReputationScore);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("AspNetRoles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("AspNetUserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("AspNetUserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("AspNetUserTokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("AspNetRoleClaims");

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt });
            entity.Property(e => e.Token).HasMaxLength(128).IsRequired();
            entity.Property(e => e.ReplacedByToken).HasMaxLength(128);
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParkingReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VirtualZoneKey);
            entity.HasIndex(e => new { e.Status, e.ExpiresAt });
            entity.Property(e => e.Location).HasColumnType("geography");
            entity.Property(e => e.PhotoUrl).HasMaxLength(500);
            entity.Property(e => e.VirtualZoneKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 2);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany(u => u.ParkingReports)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReportVote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ParkingReportId, e.UserId }).IsUnique();
            entity.HasOne(e => e.ParkingReport)
                .WithMany(p => p.Votes)
                .HasForeignKey(e => e.ParkingReportId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany(u => u.Votes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParkingRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.ExpiresAt });
            entity.Property(e => e.Location).HasColumnType("geography");
            entity.Property(e => e.Address).HasMaxLength(500).IsRequired();
            entity.Property(e => e.VirtualZoneKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.RewardAmount).HasPrecision(10, 2);
            entity.Property(e => e.PaymentMethodsJson).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.CreatedByUser)
                .WithMany(u => u.ParkingRequests)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReservedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReservedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        modelBuilder.Entity<ParkingRequestMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ParkingRequestId, e.GuestUserId, e.CreatedAt });
            entity.HasIndex(e => new { e.ParkingRequestId, e.CreatedAt });
            entity.Property(e => e.Content).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.ParkingRequest)
                .WithMany(r => r.Messages)
                .HasForeignKey(e => e.ParkingRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SenderUser)
                .WithMany()
                .HasForeignKey(e => e.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ReviewerUserId, e.ReviewedUserId }).IsUnique();
            entity.HasIndex(e => e.ReviewedUserId);
            entity.HasIndex(e => e.ParkingRequestId);
            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.HasOne(e => e.ReviewerUser)
                .WithMany(u => u.ReviewsGiven)
                .HasForeignKey(e => e.ReviewerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReviewedUser)
                .WithMany(u => u.ReviewsReceived)
                .HasForeignKey(e => e.ReviewedUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParkingRequest)
                .WithMany(r => r.Reviews)
                .HasForeignKey(e => e.ParkingRequestId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        modelBuilder.Entity<ParkingRequestBlock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ParkingRequestId, e.GuestUserId }).IsUnique();
            entity.HasOne(e => e.ParkingRequest)
                .WithMany(r => r.Blocks)
                .HasForeignKey(e => e.ParkingRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GuestUser)
                .WithMany()
                .HasForeignKey(e => e.GuestUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
