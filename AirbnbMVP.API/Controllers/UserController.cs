using AirbnbMVP.BLL.DTOs.Requests.User;
using AirbnbMVP.Models.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AirbnbMVP.API.Controllers
{
    [ApiController]
    [Route("/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UserController : ControllerBase
    {
        private readonly IConfiguration Configuration;

        private readonly SignInManager<User> SignInManager;

        private readonly UserManager<User> UserManager;

        public UserController(IConfiguration configuration, SignInManager<User> signInManager, UserManager<User> userManager)
        {
            Configuration = configuration;
            SignInManager = signInManager;
            UserManager = userManager;
        }

        [HttpPost("register")]

        public async Task<ActionResult> Register(RegisterRequestDto request)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                UserName = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsHost = false,
                CreatedAt = DateTime.UtcNow
            };

            var result = await UserManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest();
            }

            return Ok();
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(LoginRequestDto request)
            {
            var user = await UserManager.FindByEmailAsync(request.Email);

            if (user == null)
            {
                return BadRequest("Invalid email or password.");
            }

            var result = await SignInManager.CheckPasswordSignInAsync(user , request.Password , false);

            if (!result.Succeeded){
                return BadRequest("Invalid email or password.");
            }

            SetTokenCookie(user);

            return Ok();
        }

        [NonAction]
        private void SetTokenCookie(User user)
        {
            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.GivenName, user.FirstName),
            new("isHost", user.IsHost.ToString())
        };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]!));

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            Response.Cookies.Append("token", tokenString, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7)
            });
        }


    }
}
