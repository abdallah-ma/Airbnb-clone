using AirbnbMVP.BLL.DTOs.Requests.User;
using AirbnbMVP.BLL.Exceptions;
using AirbnbMVP.BLL.Interfaces;
using AirbnbMVP.DAL.Interfaces;
using AirbnbMVP.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AirbnbMVP.BLL.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<User> UserManager;

        public UserService(
            UserManager<User> userManager,
            IConfiguration configuration)
        {
            UserManager = userManager;
        }


        public async Task BecomeHostAsync(Guid userId)
        {
            var user = await UserManager.FindByIdAsync(userId.ToString())
                ?? throw new NotFoundException("User not found.");

            if (user.IsHost)
                throw new BadRequestException("User is already a host.");

            user.IsHost = true;

            var result = await UserManager.UpdateAsync(user);

            if (!result.Succeeded)
                throw new BadRequestException(result.Errors.First().Description);
        }
        


    }
}
