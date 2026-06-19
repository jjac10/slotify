using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;

namespace Slotify.Infrastructure.Data;

public class SlotifyDbContext(DbContextOptions<SlotifyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PricingTier> PricingTiers => Set<PricingTier>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<BusinessHoliday> BusinessHolidays => Set<BusinessHoliday>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigurePricingTiers(modelBuilder);
        ConfigureBusinesses(modelBuilder);
        ConfigureStaff(modelBuilder);
        ConfigureServices(modelBuilder);
        ConfigureGuests(modelBuilder);
        ConfigureReservations(modelBuilder);
        ConfigureBusinessHours(modelBuilder);
        ConfigureBusinessHolidays(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        SeedPricingTiers(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            e.Property(u => u.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(20);
            e.Property(u => u.Type).HasColumnName("type").HasMaxLength(50).HasDefaultValue("customer");
            e.Property(u => u.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("active");
            e.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            e.Property(u => u.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            e.Property(u => u.DeletedAt).HasColumnName("deleted_at");

            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => new { u.Type, u.Status });
        });
    }

    private static void ConfigurePricingTiers(ModelBuilder mb)
    {
        mb.Entity<PricingTier>(e =>
        {
            e.ToTable("pricing_tiers");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(t => t.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
            e.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            e.Property(t => t.MaxReservationsPerMonth).HasColumnName("max_reservations_per_month");
            e.Property(t => t.MaxClients).HasColumnName("max_clients");
            e.Property(t => t.MaxServices).HasColumnName("max_services");
            e.Property(t => t.MaxStaff).HasColumnName("max_staff");
            e.Property(t => t.ChannelEmail).HasColumnName("channel_email").HasDefaultValue(true);
            e.Property(t => t.ChannelSms).HasColumnName("channel_sms").HasDefaultValue(false);
            e.Property(t => t.ChannelWhatsapp).HasColumnName("channel_whatsapp").HasDefaultValue(false);
            e.Property(t => t.HasAnalytics).HasColumnName("has_analytics").HasDefaultValue(false);
            e.Property(t => t.HasApi).HasColumnName("has_api").HasDefaultValue(false);
            e.Property(t => t.PriceMonthly).HasColumnName("price_monthly").HasColumnType("decimal(10,2)").HasDefaultValue(0m);
            e.Property(t => t.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasIndex(t => t.Code).IsUnique();
        });
    }

    private static void ConfigureBusinesses(ModelBuilder mb)
    {
        mb.Entity<Business>(e =>
        {
            e.ToTable("businesses");
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(b => b.OwnerId).HasColumnName("owner_id").IsRequired();
            e.Property(b => b.TierId).HasColumnName("tier_id").IsRequired();
            e.Property(b => b.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(b => b.SlotIntervalMinutes).HasColumnName("slot_interval_minutes");
            e.Property(b => b.Timezone).HasColumnName("timezone").HasMaxLength(64).HasDefaultValue("Europe/Madrid");
            e.Property(b => b.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("active");
            e.Property(b => b.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            e.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            e.HasOne(b => b.Owner)
                .WithMany(u => u.Businesses)
                .HasForeignKey(b => b.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(b => b.Tier)
                .WithMany(t => t.Businesses)
                .HasForeignKey(b => b.TierId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(b => b.OwnerId);
            e.HasIndex(b => b.TierId);
        });
    }

    private static void ConfigureStaff(ModelBuilder mb)
    {
        mb.Entity<Staff>(e =>
        {
            e.ToTable("staff");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.BusinessId).HasColumnName("business_id").IsRequired();
            e.Property(s => s.UserId).HasColumnName("user_id");
            e.Property(s => s.Role).HasColumnName("role").HasMaxLength(50).HasDefaultValue("employee");
            e.Property(s => s.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(s => s.Email).HasColumnName("email").HasMaxLength(255);
            e.Property(s => s.Phone).HasColumnName("phone").HasMaxLength(20);
            e.Property(s => s.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("active");
            e.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            e.HasOne(s => s.Business)
                .WithMany()
                .HasForeignKey(s => s.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(s => new { s.BusinessId, s.Status });
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => new { s.BusinessId, s.Role }); // localizar al owner-staff
        });
    }

    private static void ConfigureServices(ModelBuilder mb)
    {
        mb.Entity<Service>(e =>
        {
            e.ToTable("services");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.BusinessId).HasColumnName("business_id").IsRequired();
            e.Property(s => s.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(s => s.Description).HasColumnName("description");
            e.Property(s => s.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
            e.Property(s => s.Price).HasColumnName("price").HasColumnType("decimal(10,2)");
            e.Property(s => s.Color).HasColumnName("color").HasMaxLength(7);
            e.Property(s => s.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("active");
            e.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            e.HasOne(s => s.Business)
                .WithMany()
                .HasForeignKey(s => s.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => new { s.BusinessId, s.Status });
        });
    }

    private static void ConfigureGuests(ModelBuilder mb)
    {
        mb.Entity<Guest>(e =>
        {
            e.ToTable("guests", t => t.HasCheckConstraint(
                "ck_guests_phone_or_email", "phone_hash IS NOT NULL OR email_hash IS NOT NULL"));
            e.HasKey(g => g.Id);
            e.Property(g => g.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(g => g.BusinessId).HasColumnName("business_id").IsRequired();
            e.Property(g => g.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(g => g.PhoneEncrypted).HasColumnName("phone_encrypted").HasMaxLength(255);
            e.Property(g => g.EmailEncrypted).HasColumnName("email_encrypted").HasMaxLength(255);
            e.Property(g => g.PhoneHash).HasColumnName("phone_hash").HasMaxLength(64);
            e.Property(g => g.EmailHash).HasColumnName("email_hash").HasMaxLength(64);
            e.Property(g => g.UserId).HasColumnName("user_id");
            e.Property(g => g.TotalReservations).HasColumnName("total_reservations").HasDefaultValue(0);
            e.Property(g => g.LastReservationAt).HasColumnName("last_reservation_at");
            e.Property(g => g.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("active");
            e.Property(g => g.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            e.Property(g => g.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            e.HasOne(g => g.Business)
                .WithMany()
                .HasForeignKey(g => g.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(g => g.User)
                .WithMany()
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Dedupe/lookup por negocio: UNIQUE parciales (solo cuando el hash existe).
            e.HasIndex(g => new { g.BusinessId, g.PhoneHash })
                .IsUnique().HasFilter("phone_hash IS NOT NULL");
            e.HasIndex(g => new { g.BusinessId, g.EmailHash })
                .IsUnique().HasFilter("email_hash IS NOT NULL");
            e.HasIndex(g => g.UserId);
        });
    }

    private static void ConfigureReservations(ModelBuilder mb)
    {
        mb.Entity<Reservation>(e =>
        {
            e.ToTable("reservations", t =>
            {
                t.HasCheckConstraint("ck_reservations_user_or_guest",
                    "(user_id IS NOT NULL AND guest_id IS NULL) OR (user_id IS NULL AND guest_id IS NOT NULL)");
                t.HasCheckConstraint("ck_reservations_times", "start_time < end_time");
            });

            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(r => r.BusinessId).HasColumnName("business_id").IsRequired();
            e.Property(r => r.ServiceId).HasColumnName("service_id").IsRequired();
            e.Property(r => r.StaffId).HasColumnName("staff_id").IsRequired();
            e.Property(r => r.UserId).HasColumnName("user_id");
            e.Property(r => r.GuestId).HasColumnName("guest_id");
            e.Property(r => r.StartTime).HasColumnName("start_time").IsRequired();
            e.Property(r => r.EndTime).HasColumnName("end_time").IsRequired();
            e.Property(r => r.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("pending");
            e.Property(r => r.PaymentStatus).HasColumnName("payment_status").HasMaxLength(50).HasDefaultValue("not_required");
            e.Property(r => r.Version).HasColumnName("version").HasDefaultValue(0).IsConcurrencyToken();
            e.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            e.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
            e.Property(r => r.CancelledAt).HasColumnName("cancelled_at");

            e.HasOne(r => r.Business).WithMany().HasForeignKey(r => r.BusinessId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Service).WithMany().HasForeignKey(r => r.ServiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Staff).WithMany().HasForeignKey(r => r.StaffId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Guest>().WithMany().HasForeignKey(r => r.GuestId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(r => new { r.BusinessId, r.StartTime });
            e.HasIndex(r => new { r.StaffId, r.StartTime });
            e.HasIndex(r => new { r.GuestId, r.StartTime });
            e.HasIndex(r => new { r.Status, r.CreatedAt });
        });
    }

    private static void ConfigureBusinessHours(ModelBuilder mb)
    {
        mb.Entity<BusinessHour>(e =>
        {
            e.ToTable("business_hours", t => t.HasCheckConstraint(
                "ck_business_hours_times", "opening_time < closing_time OR is_closed = true"));
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(h => h.BusinessId).HasColumnName("business_id").IsRequired();
            e.Property(h => h.DayOfWeek).HasColumnName("day_of_week").IsRequired();
            e.Property(h => h.IsClosed).HasColumnName("is_closed").HasDefaultValue(false);
            e.Property(h => h.OpeningTime).HasColumnName("opening_time");
            e.Property(h => h.ClosingTime).HasColumnName("closing_time");

            e.HasOne(h => h.Business)
                .WithMany()
                .HasForeignKey(h => h.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(h => new { h.BusinessId, h.DayOfWeek }).IsUnique();
        });
    }

    private static void ConfigureBusinessHolidays(ModelBuilder mb)
    {
        mb.Entity<BusinessHoliday>(e =>
        {
            e.ToTable("business_holidays");
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(h => h.BusinessId).HasColumnName("business_id").IsRequired();
            e.Property(h => h.HolidayDate).HasColumnName("holiday_date").IsRequired();
            e.Property(h => h.Reason).HasColumnName("reason").HasMaxLength(255);
            e.Property(h => h.IsClosed).HasColumnName("is_closed").HasDefaultValue(true);

            e.HasOne(h => h.Business)
                .WithMany()
                .HasForeignKey(h => h.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(h => new { h.BusinessId, h.HolidayDate }).IsUnique();
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder mb)
    {
        mb.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(a => a.ReservationId).HasColumnName("reservation_id");
            e.Property(a => a.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
            e.Property(a => a.ActorId).HasColumnName("actor_id");
            e.Property(a => a.GuestId).HasColumnName("guest_id");
            e.Property(a => a.ActorType).HasColumnName("actor_type").HasMaxLength(50);
            e.Property(a => a.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
            e.Property(a => a.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
            e.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            e.Property(a => a.UserAgent).HasColumnName("user_agent");
            e.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            // La auditoría sobrevive al hard-delete de la reserva (ADR #13/#14):
            // reservation_id es nullable y ON DELETE SET NULL.
            e.HasOne<Reservation>().WithMany().HasForeignKey(a => a.ReservationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Guest>().WithMany().HasForeignKey(a => a.GuestId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(a => new { a.ReservationId, a.CreatedAt });
            e.HasIndex(a => new { a.ActorId, a.CreatedAt });
            e.HasIndex(a => new { a.Action, a.CreatedAt });
        });
    }

    private static void ConfigureRefreshTokens(ModelBuilder mb)
    {
        mb.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
            e.Property(r => r.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
            e.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").ValueGeneratedOnAdd();

            e.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(r => r.TokenHash).IsUnique();
            e.HasIndex(r => new { r.UserId, r.ExpiresAt });
        });
    }

    private static void SeedPricingTiers(ModelBuilder mb)
    {
        // Seed inicial (ADR #9 / DATA_MODEL). IDs y fecha fijos: HasData exige
        // valores estáticos para que la migración sea determinista.
        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        mb.Entity<PricingTier>().HasData(
            new PricingTier
            {
                Id = new Guid("11111111-1111-1111-1111-111111111111"),
                Code = "free",
                Name = "Free",
                MaxReservationsPerMonth = 100,
                MaxClients = 50,
                MaxServices = 5,
                MaxStaff = 1,
                ChannelEmail = true,
                ChannelSms = false,
                ChannelWhatsapp = false,
                HasAnalytics = false,
                HasApi = false,
                PriceMonthly = 0m,
                IsActive = true,
                CreatedAt = seededAt,
            },
            new PricingTier
            {
                Id = new Guid("22222222-2222-2222-2222-222222222222"),
                Code = "premium",
                Name = "Premium",
                MaxReservationsPerMonth = null, // ilimitado
                MaxClients = null,
                MaxServices = null,
                MaxStaff = null,
                ChannelEmail = true,
                ChannelSms = true,
                ChannelWhatsapp = true,
                HasAnalytics = true,
                HasApi = true,
                PriceMonthly = 9.99m,
                IsActive = true,
                CreatedAt = seededAt,
            });
    }
}
