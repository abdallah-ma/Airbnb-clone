using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.DAL.Models;
using AirbnbMVP.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AirbnbMVP.DAL.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {


        protected readonly AirbnbDbContext Context;



        public GenericRepository(AirbnbDbContext context)
        {
            Context = context;
        }
        public async virtual Task<int> AddAsync(T entity, bool saveChanges = true)
        {
            await Context.Set<T>().AddAsync(entity);

            if (saveChanges)
            {
                return await Context.SaveChangesAsync();
            }

            return 0;
        }

        public async virtual Task<int> DeleteAsync(Guid id)
        {
            var entity = await Context.Set<T>().FindAsync(id);
            Context.Set<T>().Remove(entity);

            return await Context.SaveChangesAsync();
        }

        public async virtual Task<int> UpdateAsync(T entity)
        {
            Context.Set<T>().Update(entity);
            return await Context.SaveChangesAsync();
        }

        public async Task<T?> GetAsync(Specification<T> specification)
        {
            return await SpecificationEvaluator.GetQuery(Context.Set<T>(), specification).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>?> GetAllAsync(Specification<T> specification)
        {
            return await SpecificationEvaluator.GetQuery(Context.Set<T>(), specification).ToListAsync();

        }

        public async virtual Task<int> SaveAsync()
        {
            return await Context.SaveChangesAsync();
        }

        public async virtual Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel)
        {
            return await Context.Database.BeginTransactionAsync(isolationLevel);
        }


    }
}
