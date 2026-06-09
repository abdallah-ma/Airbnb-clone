using AirbnbMVP.DAL;
using AirbnbMVP.Models.Listings;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Specifications.Listings
{


    public class ListingFilterParams
    {
        public string? City {  get; set; }

        public string? Country { get; set; }

        public int? MaxPrice {  get; set; }

        public int? MinGuests { get; set; }
         
        public int Page {  get; set; }

        public int PageSize { get; set; }
    }
    public class ListingFilterSpecification : Specification<Listing>
    {

        public ListingFilterSpecification(ListingFilterParams param) : 
                      base(l => (param.City.IsNullOrEmpty() || l.City == param.City) && 
                      (param.Country.IsNullOrEmpty() || l.Country == param.Country) && 
                      (!param.MaxPrice.HasValue || l.PricePerNight <= param.MaxPrice) &&
                      (!param.MinGuests.HasValue || l.MaxGuests >= param.MinGuests) 
                      ){

            AddInclude(l => l.Photos);
            AddInclude(l => l.ListingAmenities);

            if (param.PageSize == 0) param.PageSize = 10;

            ApplyOrderBy(l => l.Id);

            ApplyPaging(Math.Max(param.Page - 1 , 0) * param.PageSize, param.PageSize );
        }

    }
}
