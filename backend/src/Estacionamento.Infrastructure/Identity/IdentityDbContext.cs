using Estacionamento.Domain.Operations;
using Estacionamento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Estacionamento.Infrastructure.Identity;

/// <summary>
/// Contexto relacional único do monólito: Identity e dados operacionais compartilham
/// a mesma transação para preservar as invariantes de vaga, cobrança e entrega.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<EmployeeAccount, EmployeeRole, Guid>(options)
{
    public DbSet<ParkingSpot> ParkingSpots => Set<ParkingSpot>();
    public DbSet<ParkingSession> ParkingSessions => Set<ParkingSession>();
    public DbSet<Occupancy> Occupancies => Set<Occupancy>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Adjustment> Adjustments => Set<Adjustment>();
    public DbSet<ExceptionAuthorization> ExceptionAuthorizations => Set<ExceptionAuthorization>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ParkingSpot>(entity =>
        {
            entity.ToTable("ParkingSpots");
            entity.HasKey(spot => spot.Id);
            entity.Property(spot => spot.Code).HasMaxLength(20).IsRequired();
            entity.HasIndex(spot => spot.Code).IsUnique();
            entity.Property(spot => spot.RowVersion).IsRowVersion();
        });

        builder.Entity<Tariff>(entity =>
        {
            entity.ToTable("Tariffs");
            entity.HasKey(tariff => tariff.Id);
            entity.Property(tariff => tariff.DailyRate).HasColumnType("decimal(18,2)");
            entity.HasIndex(tariff => tariff.EffectiveFromUtc);
        });

        builder.Entity<ParkingSession>(entity =>
        {
            entity.ToTable("ParkingSessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.PublicId).HasMaxLength(30).IsRequired();
            entity.HasIndex(session => session.PublicId).IsUnique();
            entity.Property(session => session.Plate).HasMaxLength(10).IsRequired();
            entity.Property(session => session.PlateNormalized).HasMaxLength(10).IsRequired();
            entity.HasIndex(session => new { session.PlateNormalized, session.EntryAtUtc });
            entity.Property(session => session.Model).HasMaxLength(120).IsRequired();
            entity.Property(session => session.HolderName).HasMaxLength(160).IsRequired();
            entity.Property(session => session.HolderDocumentProtected).HasMaxLength(2048).IsRequired();
            entity.Property(session => session.CapturedDailyRate).HasColumnType("decimal(18,2)");
            entity.Property(session => session.RowVersion).IsRowVersion();
            entity.HasIndex(session => new { session.State, session.EntryAtUtc });
            entity.HasOne(session => session.ParkingSpot)
                .WithMany()
                .HasForeignKey(session => session.ParkingSpotId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(session => session.Inspections)
                .WithOne()
                .HasForeignKey(inspection => inspection.ParkingSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(session => session.Occupancies)
                .WithOne(occupancy => occupancy.ParkingSession)
                .HasForeignKey(occupancy => occupancy.ParkingSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(session => session.WithdrawalRequests)
                .WithOne(request => request.ParkingSession)
                .HasForeignKey(request => request.ParkingSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(session => session.Charges)
                .WithOne()
                .HasForeignKey(charge => charge.ParkingSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(session => session.Payments)
                .WithOne()
                .HasForeignKey(payment => payment.ParkingSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(session => session.Adjustments)
                .WithOne()
                .HasForeignKey(adjustment => adjustment.ParkingSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(session => session.Authorizations)
                .WithOne()
                .HasForeignKey(authorization => authorization.ParkingSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Occupancy>(entity =>
        {
            entity.ToTable("Occupancies");
            entity.HasKey(occupancy => occupancy.Id);
            entity.Property(occupancy => occupancy.RowVersion).IsRowVersion();
            entity.HasOne(occupancy => occupancy.ParkingSpot)
                .WithMany(spot => spot.Occupancies)
                .HasForeignKey(occupancy => occupancy.ParkingSpotId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(occupancy => occupancy.ParkingSpotId)
                .IsUnique()
                .HasFilter("[EndedAtUtc] IS NULL");
            entity.HasIndex(occupancy => occupancy.ParkingSessionId)
                .IsUnique()
                .HasFilter("[EndedAtUtc] IS NULL");
        });

        builder.Entity<Inspection>(entity =>
        {
            entity.ToTable("Inspections");
            entity.HasKey(inspection => inspection.Id);
            entity.Property(inspection => inspection.Id).ValueGeneratedNever();
            entity.Property(inspection => inspection.Text).HasMaxLength(Inspection.MaxTextLength).IsRequired();
            entity.HasIndex(inspection => new { inspection.ParkingSessionId, inspection.Type }).IsUnique();
        });

        builder.Entity<WithdrawalRequest>(entity =>
        {
            entity.ToTable("WithdrawalRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.VerificationNotes).HasMaxLength(500);
            entity.Property(request => request.RowVersion).IsRowVersion();
            entity.HasIndex(request => request.ParkingSessionId).IsUnique();
            entity.HasIndex(request => new { request.State, request.RequestedAtUtc });
        });

        builder.Entity<Charge>(entity =>
        {
            entity.ToTable("Charges");
            entity.HasKey(charge => charge.Id);
            entity.Property(charge => charge.DailyRate).HasColumnType("decimal(18,2)");
            entity.Property(charge => charge.TotalAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(charge => charge.ParkingSessionId).IsUnique();
            entity.HasIndex(charge => charge.WithdrawalRequestId).IsUnique();
            entity.HasOne<WithdrawalRequest>()
                .WithMany()
                .HasForeignKey(charge => charge.WithdrawalRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Amount).HasColumnType("decimal(18,2)");
            entity.HasIndex(payment => payment.ParkingSessionId);
        });

        builder.Entity<Adjustment>(entity =>
        {
            entity.ToTable("Adjustments");
            entity.HasKey(adjustment => adjustment.Id);
            entity.Property(adjustment => adjustment.Amount).HasColumnType("decimal(18,2)");
            entity.Property(adjustment => adjustment.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(adjustment => adjustment.ParkingSessionId);
        });

        builder.Entity<ExceptionAuthorization>(entity =>
        {
            entity.ToTable("ExceptionAuthorizations");
            entity.HasKey(authorization => authorization.Id);
            entity.Property(authorization => authorization.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(authorization => new { authorization.ParkingSessionId, authorization.Type })
                .IsUnique();
        });

        builder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("AuditEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Action).HasMaxLength(80).IsRequired();
            entity.Property(entry => entry.Details).HasMaxLength(1000);
            entity.HasIndex(entry => new { entry.ParkingSessionId, entry.OccurredAtUtc });
        });

        builder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Operation).HasMaxLength(50).IsRequired();
            entity.Property(record => record.Key).HasMaxLength(100).IsRequired();
            entity.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(record => record.ResponseJson).IsRequired();
            entity.HasIndex(record => new { record.ActorUserId, record.Operation, record.Key }).IsUnique();
        });
    }
}
