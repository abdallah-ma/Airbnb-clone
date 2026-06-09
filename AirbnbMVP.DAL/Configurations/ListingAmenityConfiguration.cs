using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using AirbnbMVP.Models.Listings;

namespace AirbnbMVP.DAL.Configurations
{
    internal class ListingAmenityConfiguration : IEntityTypeConfiguration<ListingAmenity>
    {
        public void Configure(EntityTypeBuilder<ListingAmenity> builder)
        {
            builder.Ignore(b => b.Id);
        }
    }
}
