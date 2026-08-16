using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

/// <summary>
/// DriverAssignment has no CLR identity by domain design - it only makes sense in the
/// context of one Truck (Single/Team factories only, no public constructor). It is
/// promoted to a first-class table here purely as a persistence-layer accommodation:
/// EF Core cannot bind DriverAssignment's private constructor because two of its three
/// parameters are references to the independent Driver entity, and EF's constructor
/// injection only binds scalar/owned properties, never reference navigations
/// (confirmed empirically - see Slice 5 spike notes). A shadow Guid key here has no
/// domain meaning; it exists solely so EF has a row identity to hang the FK
/// relationships to Driver off of.
/// </summary>
public sealed class DriverAssignmentConfiguration : IEntityTypeConfiguration<DriverAssignment>
{
    public void Configure(EntityTypeBuilder<DriverAssignment> builder)
    {
        builder.Property<Guid>("Id");
        builder.HasKey("Id");

        builder.Property(d => d.ConfigurationType);

        builder.HasOne(d => d.PrimaryDriver)
            .WithMany()
            .HasForeignKey("PrimaryDriverId")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(d => d.SecondaryDriver)
            .WithMany()
            .HasForeignKey("SecondaryDriverId")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
