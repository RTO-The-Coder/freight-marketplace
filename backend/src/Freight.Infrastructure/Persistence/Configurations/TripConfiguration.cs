using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");

        builder.HasKey(trip => trip.Id);

        // Plain FK column, no forced navigation back to Truck - same loose-reference
        // style as Stop.ShipmentId. Trip is queried by TruckId, never reached through a
        // Truck.Trips collection.
        builder.Property(trip => trip.TruckId).IsRequired();
        builder.HasIndex(trip => trip.TruckId);

        builder.Property(trip => trip.TruckingCompanyId).IsRequired();

        builder.Property(trip => trip.StartedAt).IsRequired();

        builder.Property(trip => trip.CompletedAt);

        builder.Property(trip => trip.DistanceTravelledSoFar).IsRequired();

        builder.Property(trip => trip.TimeElapsedSoFar).IsRequired();

        // IsOpen, NextStop, IsAtOffice, CurrentLoad, TotalPlannedDistanceKm,
        // TotalPlannedTimeTick are all derived from Stops/CompletedAt, never stored.
        builder.Ignore(trip => trip.IsOpen);
        builder.Ignore(trip => trip.NextStop);
        builder.Ignore(trip => trip.IsAtOffice);
        builder.Ignore(trip => trip.CurrentLoad);
        builder.Ignore(trip => trip.TotalPlannedDistanceKm);
        builder.Ignore(trip => trip.TotalPlannedTimeTick);

        // Stops are owned by the Trip and only ever reached through it - the domain
        // model deliberately gives Stop no repository of its own. Never deleted: a Stop
        // flips Status to Reached in place rather than being removed, which is what
        // makes the owning Trip a permanent, always-queryable record.
        builder.OwnsMany(trip => trip.Stops, stop =>
        {
            stop.ToTable("TripStops");

            stop.WithOwner().HasForeignKey("TripId");

            stop.HasKey(s => s.Id);

            // Stop.Id is always client-generated (Guid.NewGuid() in Stop.ForShipment/
            // ForOffice), never left at the CLR default - without this, EF's default
            // ValueGeneratedOnAdd convention for Guid keys misreads a freshly-created
            // Stop's non-default Id as "this looks like an existing row" and emits an
            // UPDATE instead of an INSERT, which silently fails (0 rows affected) as
            // DbUpdateConcurrencyException since the row was never there to update.
            stop.Property(s => s.Id).ValueGeneratedNever();

            stop.Property(s => s.Kind)
                .HasConversion<string>()
                .IsRequired();

            stop.Property(s => s.Status)
                .HasConversion<string>()
                .IsRequired();

            stop.Property(s => s.Sequence).IsRequired();

            stop.Property(s => s.IncomingLegDistanceKm).IsRequired();

            stop.Property(s => s.IncomingLegTimeTick).IsRequired();

            stop.Property(s => s.ReachedAt);

            stop.OwnsOne(s => s.Location, location =>
            {
                location.Property(l => l.Latitude).HasColumnName("LocationLatitude");
                location.Property(l => l.Longitude).HasColumnName("LocationLongitude");
            });

            stop.Navigation(s => s.Location).IsRequired();

            stop.OwnsOne(s => s.ShipmentLoad, load =>
            {
                load.Property(l => l.WeightKg).HasColumnName("ShipmentLoadWeightKg");
                load.Property(l => l.VolumeCubicMeters).HasColumnName("ShipmentLoadVolumeCubicMeters");
            });

            stop.HasIndex("TripId");
        });

        builder.Metadata
            .FindNavigation(nameof(Trip.Stops))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
