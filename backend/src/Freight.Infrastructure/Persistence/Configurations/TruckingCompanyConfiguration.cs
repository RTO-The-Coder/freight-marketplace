using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class TruckingCompanyConfiguration : IEntityTypeConfiguration<TruckingCompany>
{
    public void Configure(EntityTypeBuilder<TruckingCompany> builder)
    {
        builder.ToTable("TruckingCompanies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.Name)
            .IsRequired();

        builder.OwnsOne(company => company.OfficeLocation, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("OfficeLatitude");
            location.Property(l => l.Longitude).HasColumnName("OfficeLongitude");
        });

        builder.Navigation(company => company.OfficeLocation).IsRequired();
    }
}
