using AirbnbMVP.DAL;
using AirbnbMVP.Models.Listings;


namespace AirbnbMVP.BLL.Specifications
{
    public class GetAmenitiesByIdSpecification : Specification<Amenity>
    {

        public GetAmenitiesByIdSpecification(List<Guid> ids) : base(a => ids.Contains(a.Id))
        {

        }

    }
}
