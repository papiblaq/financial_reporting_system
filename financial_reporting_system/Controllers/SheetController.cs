using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace financial_reporting_system.Controllers
{
    public class SheetController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<SheetController> _logger;

        public SheetController(IConfiguration configuration, ILogger<SheetController> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // GET: /Sheet
        public IActionResult Index()
        {
            var model = new SheetInputModel
            {
                StatementTypes = GetStatementTypes() // Populate dropdown options
            };

            return View(model);
        }

        // POST: /Sheet/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveData(SheetInputModel input)
        {
            // Check if STMNT_ID is valid and present in the dropdown options
            if (!IsValidStatementType(input.STMNT_ID))
            {
                ModelState.AddModelError("STMNT_ID", "Invalid STMNT_ID selected.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state detected.");
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        _logger.LogWarning($"ModelState error: {error.ErrorMessage}");
                    }
                }
                input.StatementTypes = GetStatementTypes(); // Re-populate dropdown data
                return View("Index", input);
            }

            input.SYS_CREATE_TS = DateTime.Now;

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Insert the record
                    string insertQuery = @"
                        INSERT INTO ORG_FINANCIAL_STMNT_SHEET 
                        (STMNT_ID, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY) 
                        VALUES (:STMNT_ID, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY)";

                    using (var insertCommand = new OracleCommand(insertQuery, connection))
                    {
                        // Use the values from the input model
                        AddParameter(insertCommand, "STMNT_ID", OracleDbType.Int32, input.STMNT_ID);
                        AddParameter(insertCommand, "REF_CD", OracleDbType.Varchar2, input.REF_CD);
                        AddParameter(insertCommand, "DESCRIPTION", OracleDbType.Varchar2, input.DESCRIPTION);
                        AddParameter(insertCommand, "SYS_CREATE_TS", OracleDbType.TimeStamp, input.SYS_CREATE_TS);
                        AddParameter(insertCommand, "CREATED_BY", OracleDbType.Varchar2, input.CREATED_BY);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Data saved successfully for STMNT_ID: {STMNT_ID}", input.STMNT_ID);
                TempData["SuccessMessage"] = $"Created sheet for statement type {input.STMNT_ID}";
                return RedirectToAction("Index"); // Redirect to clear input fields
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while saving sheet data.");
                ModelState.AddModelError(string.Empty, "An error occurred while saving your data. Please try again.");
                input.StatementTypes = GetStatementTypes(); // Re-populate dropdown data
                return View("Index", input);
            }
        }

        // Helper method to validate the selected STMNT_ID
        private bool IsValidStatementType(int stmntId)
        {
            var statementTypes = GetStatementTypes();
            return statementTypes.Exists(item => item.Value == stmntId.ToString());
        }

        private List<SelectListItem> GetStatementTypes()
        {
            var statementTypes = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT STMNT_ID, DESCRIPTION FROM ORG_FIN_STATEMENT_TYPE";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int stmntId = Convert.ToInt32(reader["STMNT_ID"]);
                                string description = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{stmntId} ({description})";

                                statementTypes.Add(new SelectListItem
                                {
                                    Value = stmntId.ToString(), // Ensure Value is set to STMNT_ID
                                    Text = formattedText
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching statement types.");
            }

            return statementTypes;
        }

        private void AddParameter(OracleCommand command, string name, OracleDbType type, object value)
        {
            command.Parameters.Add(new OracleParameter(name, type) { Value = value });
        }

        // New action method to fetch data from ORG_FINANCIAL_STMNT_SHEET and display it in a grid
        public IActionResult Grid()
        {
            var sheets = GetSheets();
            return View(sheets);
        }

        private List<Sheet> GetSheets()
        {
            var sheets = new List<Sheet>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT STMNT_ID, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_SHEET";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sheets.Add(new Sheet
                                {
                                    STMNT_ID = Convert.ToInt32(reader["STMNT_ID"]),
                                    REF_CD = reader["REF_CD"].ToString(),
                                    DESCRIPTION = reader["DESCRIPTION"].ToString(),
                                    SYS_CREATE_TS = Convert.ToDateTime(reader["SYS_CREATE_TS"]),
                                    CREATED_BY = reader["CREATED_BY"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching sheet data.");
            }

            return sheets;
        }

        // Input model class
        public class SheetInputModel
        {
            public SheetInputModel()
            {
                StatementTypes = new List<SelectListItem>();
            }

            [Required(ErrorMessage = "STMNT_ID is required.")]
            public int STMNT_ID { get; set; }

            [Required(ErrorMessage = "REF_CD is required.")]
            [StringLength(50, ErrorMessage = "REF_CD cannot be longer than 50 characters.")]
            public string REF_CD { get; set; }

            [Required(ErrorMessage = "DESCRIPTION is required.")]
            [StringLength(200, ErrorMessage = "DESCRIPTION cannot be longer than 200 characters.")]
            public string DESCRIPTION { get; set; }

            public DateTime SYS_CREATE_TS { get; set; }

            [Required(ErrorMessage = "CREATED_BY is required.")]
            [StringLength(100, ErrorMessage = "CREATED_BY cannot be longer than 100 characters.")]
            public string CREATED_BY { get; set; }

            public List<SelectListItem> StatementTypes { get; set; }
        }

        // Data model class for ORG_FINANCIAL_STMNT_SHEET
        public class Sheet
        {
            public int STMNT_ID { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }
}