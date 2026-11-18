using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelBooking.Domain.Models;

namespace TravelBooking.Infrastructure.Data.Configurations;

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.ToTable("flights");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FlightCode).HasMaxLength(32).IsRequired();
        builder.Property(f => f.Airline).HasMaxLength(100).IsRequired();
        builder.Property(f => f.FlightNumber).HasMaxLength(10).IsRequired();
        builder.Property(f => f.PriceUsd).HasColumnType("numeric(10,2)");

        builder.HasIndex(f => f.FlightCode).IsUnique();
        builder.HasIndex(f => new { f.DepartureAirportId, f.ArrivalAirportId, f.DepartureTimeUtc });

        builder.HasOne(f => f.DepartureAirport)
            .WithMany()
            .HasForeignKey(f => f.DepartureAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.ArrivalAirport)
            .WithMany()
            .HasForeignKey(f => f.ArrivalAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(f => f.SeatMap, b => b.ToJson());
    }
}
