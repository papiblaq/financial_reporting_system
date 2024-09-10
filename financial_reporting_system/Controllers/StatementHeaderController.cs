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
    public class StatementHeaderController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<StatementHeaderController> _logger;

        public StatementHeaderController(IConfiguration configuration, ILogger<StatementHeaderController> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // GET: /StatementHeader
        public IActionResult Index()
        {
            var model = new StatementHeaderInputModel
            {
                StatementTypes = GetStatementTypes(),
                AccountCategories = GetAccountCategories(),
                SheetIds = new List<SelectListItem>() // Initialize empty list
            };

            return View(model);
        }

        // Fetch Sheet IDs based on selected Statement ID
        [HttpPost]
        public JsonResult GetSheetIdsByStatementId(int stmntId)
        {
            var sheetIds = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT SHEET_ID, REF_CD FROM ORG_FINANCIAL_STMNT_SHEET WHERE STMNT_ID = :STMNT_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Int32) { Value = stmntId });

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int sheetId = Convert.ToInt32(reader["SHEET_ID"]);
                                string refCd = reader["REF_CD"].ToString();
                                string formattedText = $"{sheetId} ({refCd})";

                                sheetIds.Add(new SelectListItem
                                {
                                    Value = sheetId.ToString(),
                                    Text = formattedText
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching sheet IDs.");
            }

            return Json(sheetIds);
        }

        // POST: /StatementHeader/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveData(StatementHeaderInputModel input)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state detected.");
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        _logger.LogWarning($"ModelState error: {error.ErrorMessage}");
                        TempData["ErrorMessage"] = error.ErrorMessage;
                    }
                }

                // Repopulate the dropdown lists after a failed validation
                input.StatementTypes = GetStatementTypes();
                input.AccountCategories = GetAccountCategories();
                input.SheetIds = new List<SelectListItem>();

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
                        INSERT INTO ORG_FINANCIAL_STMNT_HEADER 
                        (STMNT_ID, SHEET_ID, REF_CD, GL_ACCT_CAT_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY) 
                        VALUES (:STMNT_ID, :SHEET_ID, :REF_CD, :GL_ACCT_CAT_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY)";

                    using (var insertCommand = new OracleCommand(insertQuery, connection))
                    {
                        AddParameter(insertCommand, "STMNT_ID", OracleDbType.Int32, input.STMNT_ID);
                        AddParameter(insertCommand, "SHEET_ID", OracleDbType.Int32, input.SHEET_ID);
                        AddParameter(insertCommand, "REF_CD", OracleDbType.Varchar2, input.REF_CD);
                        AddParameter(insertCommand, "GL_ACCT_CAT_CD", OracleDbType.Varchar2, input.GL_ACCT_CAT_CD);
                        AddParameter(insertCommand, "DESCRIPTION", OracleDbType.Varchar2, input.DESCRIPTION);
                        AddParameter(insertCommand, "SYS_CREATE_TS", OracleDbType.TimeStamp, input.SYS_CREATE_TS);
                        AddParameter(insertCommand, "CREATED_BY", OracleDbType.Varchar2, input.CREATED_BY);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Data saved successfully for STMNT_ID: {STMNT_ID}, SHEET_ID: {SHEET_ID}", input.STMNT_ID, input.SHEET_ID);
                TempData["SuccessMessage"] = $"Created sheet for statement type {input.STMNT_ID} and sheet ID {input.SHEET_ID}";
                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while saving sheet data.");
                TempData["ErrorMessage"] = "An error occurred while saving your data. Please try again.";

                // Repopulate the dropdown lists after a failed insertion
                input.StatementTypes = GetStatementTypes();
                input.AccountCategories = GetAccountCategories();
                input.SheetIds = new List<SelectListItem>();

                return View("Index", input);
            }
        }

        private List<SelectListItem> GetStatementTypes()
        {
            var statementTypes = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT STMNT_ID, REF_CD FROM ORG_FIN_STATEMENT_TYPE";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int stmntId = Convert.ToInt32(reader["STMNT_ID"]);
                                string refCd = reader["REF_CD"].ToString();
                                string formattedText = $"{stmntId} ({refCd})";

                                statementTypes.Add(new SelectListItem
                                {
                                    Value = stmntId.ToString(),
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

        private List<SelectListItem> GetAccountCategories()
        {
            var accountCategories = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT DISTINCT GL_ACCT_CAT_CD FROM V_ORG_CHART_OF_ACCOUNT_DETAILS";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string glAcctCatCd = reader["GL_ACCT_CAT_CD"].ToString();
                                accountCategories.Add(new SelectListItem
                                {
                                    Value = glAcctCatCd,
                                    Text = glAcctCatCd
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching account categories.");
            }

            return accountCategories;
        }

        private void AddParameter(OracleCommand command, string name, OracleDbType dbType, object value)
        {
            command.Parameters.Add(new OracleParameter(name, dbType) { Value = value ?? DBNull.Value });
        }
    }

    // Input model class
    public class StatementHeaderInputModel
    {
        public StatementHeaderInputModel()
        {
            StatementTypes = new List<SelectListItem>();
            AccountCategories = new List<SelectListItem>();
            SheetIds = new List<SelectListItem>();
        }

        [Required(ErrorMessage = "STMNT_ID is required.")]
        public int STMNT_ID { get; set; }

        [Required(ErrorMessage = "REF_CD is required.")]
        [StringLength(50, ErrorMessage = "REF_CD cannot be longer than 50 characters.")]
        public string REF_CD { get; set; }

        [Required(ErrorMessage = "GL_ACCT_CAT_CD is required.")]
        public string GL_ACCT_CAT_CD { get; set; }

        [Required(ErrorMessage = "DESCRIPTION is required.")]
        [StringLength(200, ErrorMessage = "DESCRIPTION cannot be longer than 200 characters.")]
        public string DESCRIPTION { get; set; }

        public DateTime SYS_CREATE_TS { get; set; }

        [Required(ErrorMessage = "CREATED_BY is required.")]
        [StringLength(100, ErrorMessage = "CREATED_BY cannot be longer than 100 characters.")]
        public string CREATED_BY { get; set; }

        [Required(ErrorMessage = "SHEET_ID is required.")]
        public int SHEET_ID { get; set; }

        // Dropdown lists (no validation attributes)
        public List<SelectListItem> StatementTypes { get; set; }
        public List<SelectListItem> AccountCategories { get; set; }
        public List<SelectListItem> SheetIds { get; set; }
    }
}