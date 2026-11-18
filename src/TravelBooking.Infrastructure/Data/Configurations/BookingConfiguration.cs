using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelBooking.Domain.Models;

namespace TravelBooking.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BookingReference).HasMaxLength(16).IsRequired();
        builder.Property(b => b.SeatNumber).HasMaxLength(8).IsRequired();
        builder.Property(b => b.UserId).HasMaxLength(128);
        builder.Property(b => b.Status).HasConversion<int>();

        builder.HasIndex(b => b.BookingReference).IsUnique();
        builder.HasIndex(b => new { b.FlightId, b.SeatNumber })
            .IsUnique()
            .HasFilter($"\"{nameof(Booking.Status)}\" = 0");
        builder.HasIndex(b => b.UserId);

        builder.HasOne(b => b.Flight)
            .WithMany()
            .HasForeignKey(b => b.FlightId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
