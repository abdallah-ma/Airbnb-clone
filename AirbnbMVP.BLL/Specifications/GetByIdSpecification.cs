using AirbnbMVP.DAL;
using AirbnbMVP.DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.BLL.Specifications
{
    public class GetByIdSpecification<T> : Specification<T> where T : BaseEntity
    {

        public GetByIdSpecification(Guid id) : base(e => e.Id == id)
        {
        }

    }
}
