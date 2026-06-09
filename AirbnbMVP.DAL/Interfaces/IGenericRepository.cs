using AirbnbMVP.DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.DAL.Interfaces
{
    public interface IGenericRepository<T> where T : BaseEntity
    {

        Task<IEnumerable<T>?> GetAllAsync(Specification<T> specification);


        Task<T?> GetAsync(Specification<T> specification);

        Task<int> AddAsync(T entity);

        Task<int> UpdateAsync(T entity);

        Task<int> DeleteAsync(Guid id);


    }
}
