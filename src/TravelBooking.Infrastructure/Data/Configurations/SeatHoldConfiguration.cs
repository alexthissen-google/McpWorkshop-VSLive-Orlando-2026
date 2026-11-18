using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelBooking.Domain.Models;

namespace TravelBooking.Infrastructure.Data.Configurations;

public class SeatHoldConfiguration : IEntityTypeConfiguration<SeatHold>
{
    public void Configure(EntityTypeBuilder<SeatHold> builder)
    {
        builder.ToTable("seat_holds");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.SeatNumber).HasMaxLength(8).IsRequired();
        builder.Property(h => h.UserId).HasMaxLength(128);
        builder.Property(h => h.Status).HasConversion<int>();

        builder.HasIndex(h => new { h.FlightId, h.SeatNumber })
            .IsUnique()
            .HasFilter($"\"{nameof(SeatHold.Status)}\" = 0");
        builder.HasIndex(h => h.ExpiresAtUtc);

        builder.HasOne(h => h.Flight)
            .WithMany()
            .HasForeignKey(h => h.FlightId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ConfirmedAsBooking)
            .WithMany()
            .HasForeignKey(h => h.ConfirmedAsBookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
