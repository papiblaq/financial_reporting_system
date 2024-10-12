using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

public class AccountController : Controller
{
    private readonly string _connectionString;

    public AccountController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleConnection");
    }

    // GET: /Account/Register
    public async Task<IActionResult> Register()
    {
        // Fetch RoleId and RoleName values from the ROLES table
        var roles = new List<SelectListItem>();
        using (var connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new OracleCommand("SELECT ROLE_ID, ROLE_NAME FROM ROLES", connection);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (reader.Read())
                {
                    var roleId = reader.GetInt32(reader.GetOrdinal("ROLE_ID"));
                    var roleName = reader.GetString(reader.GetOrdinal("ROLE_NAME"));
                    roles.Add(new SelectListItem { Value = roleId.ToString(), Text = roleName });
                }
            }
        }

        ViewBag.Roles = roles;
        return View();
    }

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Check if the username already exists
            if (await IsUsernameExistsAsync(model.Username))
            {
                TempData["ErrorMessage"] = "Username already exists.";
                return View(model);
            }

            // Check if the email already exists
            if (await IsEmailExistsAsync(model.Email))
            {
                TempData["ErrorMessage"] = "Email already exists.";
                return View(model);
            }

            var salt = PasswordHelper.GenerateSalt();
            var passwordHash = PasswordHelper.HashPassword(model.Password, salt);

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new OracleCommand("INSERT INTO LOGIN_DATA (USERNAME, EMAIL, PASSWORD_HASH, SALT, FIRST_NAME, LAST_NAME, IS_ACTIVE, CREATED_DATE, ROLE_ID) VALUES (:Username, :Email, :PasswordHash, :Salt, :FirstName, :LastName, :IsActive, :CreatedDate, :RoleId)", connection);
                command.Parameters.Add(new OracleParameter("Username", model.Username));
                command.Parameters.Add(new OracleParameter("Email", model.Email));
                command.Parameters.Add(new OracleParameter("PasswordHash", passwordHash));
                command.Parameters.Add(new OracleParameter("Salt", salt));
                command.Parameters.Add(new OracleParameter("FirstName", model.FirstName));
                command.Parameters.Add(new OracleParameter("LastName", model.LastName));
                command.Parameters.Add(new OracleParameter("IsActive", 1)); // Use 1 for true, 0 for false
                command.Parameters.Add(new OracleParameter("CreatedDate", DateTime.UtcNow));
                command.Parameters.Add(new OracleParameter("RoleId", model.RoleId));

                await command.ExecuteNonQueryAsync();
            }

            return RedirectToAction("Login");
        }

        // If we got this far, something failed; redisplay form
        var roles = new List<SelectListItem>();
        using (var connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new OracleCommand("SELECT ROLE_ID, ROLE_NAME FROM ROLES", connection);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (reader.Read())
                {
                    var roleId = reader.GetInt32(reader.GetOrdinal("ROLE_ID"));
                    var roleName = reader.GetString(reader.GetOrdinal("ROLE_NAME"));
                    roles.Add(new SelectListItem { Value = roleId.ToString(), Text = roleName });
                }
            }
        }

        ViewBag.Roles = roles;
        return View(model);
    }

    private async Task<bool> IsUsernameExistsAsync(string username)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new OracleCommand("SELECT COUNT(*) FROM LOGIN_DATA WHERE USERNAME = :Username", connection);
            command.Parameters.Add(new OracleParameter("Username", username));

            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }
    }

    private async Task<bool> IsEmailExistsAsync(string email)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync();
            var command = new OracleCommand("SELECT COUNT(*) FROM LOGIN_DATA WHERE EMAIL = :Email", connection);
            command.Parameters.Add(new OracleParameter("Email", email));

            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }
    }

    // GET: /Account/Login
    public IActionResult Login()
    {
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new OracleCommand("SELECT * FROM LOGIN_DATA WHERE USERNAME = :Username", connection);
                command.Parameters.Add(new OracleParameter("Username", model.Username));

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        var loginData = new LoginData
                        {
                            UserId = reader.GetInt32(reader.GetOrdinal("USER_ID")),
                            Username = reader.GetString(reader.GetOrdinal("USERNAME")),
                            Email = reader.GetString(reader.GetOrdinal("EMAIL")),
                            PasswordHash = reader.GetString(reader.GetOrdinal("PASSWORD_HASH")),
                            Salt = reader.GetString(reader.GetOrdinal("SALT")),
                            FirstName = reader.GetString(reader.GetOrdinal("FIRST_NAME")),
                            LastName = reader.GetString(reader.GetOrdinal("LAST_NAME")),
                            IsActive = reader.GetInt32(reader.GetOrdinal("IS_ACTIVE")), // Changed from bool to int
                            IsLocked = reader.GetInt32(reader.GetOrdinal("IS_LOCKED")), // Changed from bool to int
                            FailedLoginAttempts = reader.GetInt32(reader.GetOrdinal("FAILED_LOGIN_ATTEMPTS")),
                            LastLoginDate = reader.IsDBNull(reader.GetOrdinal("LAST_LOGIN_DATE")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("LAST_LOGIN_DATE")),
                            CreatedDate = reader.GetDateTime(reader.GetOrdinal("CREATED_DATE")),
                            UpdatedDate = reader.IsDBNull(reader.GetOrdinal("UPDATED_DATE")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("UPDATED_DATE")),
                            RoleId = reader.GetInt32(reader.GetOrdinal("ROLE_ID")),
                            ResetToken = reader.IsDBNull(reader.GetOrdinal("RESET_TOKEN")) ? null : reader.GetString(reader.GetOrdinal("RESET_TOKEN")),
                            ResetTokenExpiry = reader.IsDBNull(reader.GetOrdinal("RESET_TOKEN_EXPIRY")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("RESET_TOKEN_EXPIRY"))
                        };

                        if (PasswordHelper.VerifyPassword(model.Password, loginData.PasswordHash, loginData.Salt))
                        {
                            if (loginData.IsActive == 1 && loginData.IsLocked == 0) // Changed from bool to int
                            {
                                loginData.LastLoginDate = DateTime.UtcNow;
                                loginData.FailedLoginAttempts = 0;

                                var updateCommand = new OracleCommand("UPDATE LOGIN_DATA SET LAST_LOGIN_DATE = :LastLoginDate, FAILED_LOGIN_ATTEMPTS = :FailedLoginAttempts WHERE USER_ID = :UserId", connection);
                                updateCommand.Parameters.Add(new OracleParameter("LastLoginDate", loginData.LastLoginDate));
                                updateCommand.Parameters.Add(new OracleParameter("FailedLoginAttempts", loginData.FailedLoginAttempts));
                                updateCommand.Parameters.Add(new OracleParameter("UserId", loginData.UserId));

                                await updateCommand.ExecuteNonQueryAsync();

                                // Sign in the user
                                var roleId = loginData.RoleId ?? 0; // Provide a default value if RoleId is null
                                var roleName = GetRoleName(roleId);
                                var claims = new List<Claim>
                                {
                                    new Claim(ClaimTypes.Name, loginData.Username),
                                    new Claim(ClaimTypes.Email, loginData.Email),
                                    new Claim(ClaimTypes.Role, roleName)
                                };

                                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                                var authProperties = new AuthenticationProperties
                                {
                                    IsPersistent = true,
                                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20)
                                };

                                await HttpContext.SignInAsync(
                                    CookieAuthenticationDefaults.AuthenticationScheme,
                                    new ClaimsPrincipal(claimsIdentity),
                                    authProperties);

                                return RedirectToAction("Index", "Statement_types");
                            }
                            else
                            {
                                ModelState.AddModelError("", "Account is inactive or locked.");
                            }
                        }
                        else
                        {
                            loginData.FailedLoginAttempts++;
                            if (loginData.FailedLoginAttempts >= 5) // Example: Lock account after 5 failed attempts
                            {
                                loginData.IsLocked = 1; // Changed from bool to int
                            }

                            var updateCommand = new OracleCommand("UPDATE LOGIN_DATA SET FAILED_LOGIN_ATTEMPTS = :FailedLoginAttempts, IS_LOCKED = :IsLocked WHERE USER_ID = :UserId", connection);
                            updateCommand.Parameters.Add(new OracleParameter("FailedLoginAttempts", loginData.FailedLoginAttempts));
                            updateCommand.Parameters.Add(new OracleParameter("IsLocked", loginData.IsLocked)); // Changed from bool to int
                            updateCommand.Parameters.Add(new OracleParameter("UserId", loginData.UserId));

                            await updateCommand.ExecuteNonQueryAsync();

                            ModelState.AddModelError("", "Invalid username or password.");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid username or password.");
                    }
                }
            }
        }

        return View(model);
    }

    private string GetRoleName(int roleId)
    {
        switch (roleId)
        {
            case 1:
                return "Admin";
            case 2:
                return "System User";
            case 3:
                return "VIP";
            default:
                return "User"; // Default role if roleId is not recognized
        }
    }

    // GET: /Account/Logout
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // GET: /Account/AccessDenied
    public IActionResult AccessDenied()
    {
        return View();
    }

    // Models
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
    }

    public class LoginData
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int IsActive { get; set; } // Changed from bool to int
        public int IsLocked { get; set; } // Changed from bool to int
        public int FailedLoginAttempts { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? RoleId { get; set; }
        public string ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // Navigation property
        public Role Role { get; set; }
    }

    // View Models
    public class RegisterViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }

        [Required]
        public int RoleId { get; set; }
    }

    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    // Password Hashing and Verification Logic
    public static class PasswordHelper
    {
        public static string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        public static string HashPassword(string password, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000))
            {
                byte[] hash = pbkdf2.GetBytes(20);
                byte[] hashBytes = new byte[36];
                Array.Copy(saltBytes, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 20);
                return Convert.ToBase64String(hashBytes);
            }
        }

        public static bool VerifyPassword(string password, string hashedPassword, string salt)
        {
            string newHashedPassword = HashPassword(password, salt);
            return newHashedPassword == hashedPassword;
        }
    }
}