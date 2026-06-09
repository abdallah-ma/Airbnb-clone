using AirbnbMVP.DAL;
using AirbnbMVP.Models.Listings;

namespace AirbnbMVP.BLL.Specifications.Listings
{
    public class ListingDetailsSpecification : Specification<Listing>
    {

        public ListingDetailsSpecification(Guid listingId) : base(l => l.Id == listingId){
            AddInclude(l => l.Photos);
            AddInclude(l => l.AvailabilityBlocks);
            AddInclude(l => l.Reviews);
        }

    }
}
