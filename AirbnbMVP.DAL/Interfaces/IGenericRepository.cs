using AirbnbMVP.DAL.Models;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AirbnbMVP.DAL.Interfaces
{
    public interface IGenericRepository<T> where T : BaseEntity
    {

        Task<IEnumerable<T>?> GetAllAsync(Specification<T> specification);


        Task<T?> GetAsync(Specification<T> specification);

        Task<int> AddAsync(T entity, bool saveChanges = true);

        Task<int> UpdateAsync(T entity);

        Task<int> DeleteAsync(Guid id);

        Task<int> SaveAsync();

        Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel);


    }
}
