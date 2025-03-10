using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using PHilae.Cipher;
using System.Data;

namespace syncfusion_grid.Controllers
{
    public class LoginController : Controller
    {
        private readonly string _connectionString;
        private readonly AECrypt _crypt;

        public LoginController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _crypt = new AECrypt(); // Initialize AECrypt instance
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string loginId, string password)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                var query = "SELECT SYSUSER_ID, FIRST_NM FROM sysuser WHERE LOGIN_ID = :LoginId";
                using (var command = new OracleCommand(query, connection))
                {
                    command.Parameters.Add(new OracleParameter("LoginId", loginId));
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int sysuserId = reader.GetInt32(0);
                            string firstName = reader.GetString(1);

                            // Store the SYSUSER_ID and FIRST_NM in TempData for further use
                            TempData["SYSUSER_ID"] = sysuserId;
                            TempData["FIRST_NM"] = firstName;

                            // Redirect to the password entry view
                            return View("PasswordEntry");
                        }
                        else
                        {
                            // User not found, return an error message
                            ViewBag.ErrorMessage = "Invalid username";
                            return View("Index");
                        }
                    }
                }
            }
        }

        [HttpPost]
        public IActionResult VerifyPassword(string password)
        {
            int? sysuserId = TempData["SYSUSER_ID"] as int?;
            if (sysuserId.HasValue)
            {
                // Implement password verification logic here
                // You can retrieve the SYSUSER_ID from TempData and query the database to verify the password
                // If the password is correct, redirect to the dashboard or another page
                // If the password is incorrect, return an error message

                // Example: Assume password verification logic
                bool isPasswordCorrect = VerifyPasswordFromDatabase(sysuserId.Value, password);

                if (isPasswordCorrect)
                {
                    return RedirectToAction( "Index", "Statement_types"); // Redirect to the Statement_types
                }
                else
                {
                    ViewBag.ErrorMessage = "Invalid password";
                    return View("Index");
                }
            }
            else
            {
                ViewBag.ErrorMessage = "Session expired. Please login again.";
                return View("Index");
            }
        }

        private bool VerifyPasswordFromDatabase(int sysuserId, string password)
        {
            // Implement your password verification logic here
            // Return true if the password is correct, otherwise false

            // Example: Assume the password is stored encrypted in the database
            string encryptedPasswordFromDb = GetEncryptedPasswordFromDatabase(sysuserId);
            if (encryptedPasswordFromDb == null)
            {
                return false;
            }

            string decryptedPasswordFromDb = _crypt.Decrypt(encryptedPasswordFromDb);

            return password == decryptedPasswordFromDb;
        }

        private string GetEncryptedPasswordFromDatabase(int sysuserId)
        {
            // Implement your logic to retrieve the encrypted password from the database
            // Return the encrypted password as a string

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                var query = "SELECT passwd FROM syspwd_hist WHERE sysuser_id = :sysuserId AND rec_st = 'A'";
                using (var command = new OracleCommand(query, connection))
                {
                    command.Parameters.Add(new OracleParameter("sysuserId", sysuserId));
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetString(0);
                        }
                    }
                }
            }

            return null; // Return null if no password is found
        }
    }
}