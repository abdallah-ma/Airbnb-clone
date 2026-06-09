using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.Data;
using AirbnbMVP.Models.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.DAL.Repositories
{
    public class UserRepository : IUserRepository
    {

        private readonly AirbnbDbContext Context;

        public UserRepository(AirbnbDbContext context)
        {
            Context = context;
        }
        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await Context.Users.FindAsync(id);
        }
    }
}
