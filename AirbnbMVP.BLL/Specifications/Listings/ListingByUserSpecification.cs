

using AirbnbMVP.DAL;
using AirbnbMVP.Models.Listings;

namespace AirbnbMVP.BLL.Specifications.Listings
{
    public class ListingByUserSpecification : Specification<Listing>
    {

        public ListingByUserSpecification(Guid userId) : base(l => l.HostId == userId)
        {

        }

    }
}
