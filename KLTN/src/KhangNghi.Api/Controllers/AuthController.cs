using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using KhangNghi.Core.DTOs;
using KhangNghi.Core.Models;
using KhangNghi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KhangNghi.Api.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepo;
        private readonly IConfiguration _config;

        public AuthController(IUserRepository userRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new LoginResponse { Success = false, Message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!" });

            var user = await _userRepo.GetByUsernameAsync(request.Username);
            if (user == null || !user.IsActive)
                return Unauthorized(new LoginResponse { Success = false, Message = "Tài khoản không tồn tại hoặc đã bị khóa!" });

            bool verified = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!verified)
                return Unauthorized(new LoginResponse { Success = false, Message = "Mật khẩu không chính xác!" });

            // Sinh JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = _config["Jwt:Key"] ?? "KhangNghi_DWH_Super_Secret_Security_Key_2026_Graduation_Thesis!";
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.GivenName, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new LoginResponse
            {
                Success = true,
                Token = tokenString,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                Message = "Đăng nhập thành công!"
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userRepo.GetAllAsync();
            return Ok(users);
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var existing = await _userRepo.GetByUsernameAsync(request.Username);
            if (existing != null)
                return BadRequest("Tên đăng nhập đã tồn tại trong hệ thống!");

            var newUser = new SysUser
            {
                Username = request.Username,
                FullName = request.FullName,
                Email = request.Email,
                Role = request.Role,
                IsActive = true
            };

            int id = await _userRepo.CreateUserAsync(newUser, request.Password);
            newUser.UserID = id;
            return Ok(newUser);
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null) return NotFound("Không tìm thấy người dùng!");

            user.FullName = request.FullName;
            user.Email = request.Email;
            user.Role = request.Role;
            user.IsActive = request.IsActive;

            await _userRepo.UpdateUserAsync(user, request.NewPassword);
            return Ok(user);
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized(new { Success = false, Message = "Phiên đăng nhập không hợp lệ!" });

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
                return Unauthorized(new { Success = false, Message = "Tài khoản không tồn tại hoặc bị khóa!" });

            if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { Success = false, Message = "Vui lòng nhập đủ mật khẩu cũ và mới!" });

            bool verified = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash);
            if (!verified)
                return BadRequest(new { Success = false, Message = "Mật khẩu cũ không chính xác!" });

            // Update user with new password
            await _userRepo.UpdateUserAsync(user, request.NewPassword);

            return Ok(new { Success = true, Message = "Đổi mật khẩu thành công!" });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            bool success = await _userRepo.DeleteUserAsync(id);
            if (!success) return NotFound("Không thể xóa người dùng!");
            return Ok(new { Message = "Đã xóa người dùng thành công!" });
        }
    }
}
