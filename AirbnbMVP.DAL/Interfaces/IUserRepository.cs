using AirbnbMVP.Models.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace AirbnbMVP.DAL.Interfaces
{
    public interface IUserRepository
    {

        Task<User?> GetUserByIdAsync(Guid id); 

    }
}
