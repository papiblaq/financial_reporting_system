using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace financial_reporting_system.Controllers
{
    public class StatementDetailsController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<StatementDetailsController> _logger;

        public StatementDetailsController(IConfiguration configuration, ILogger<StatementDetailsController> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // GET: /StatementDetails
        public IActionResult Index()
        {
            var model = new StatementDetailsInputModel
            {
                StatementTypes = GetStatementTypes(),
                SheetIds = new List<SelectListItem>(), // Initialize empty list
                HeaderIds = new List<SelectListItem>(), // Initialize empty list
                AccountCategories = GetAccountCategories()
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

        // Fetch Header IDs based on selected Sheet ID
        [HttpPost]
        public JsonResult GetHeaderIdsBySheetId(int sheetId)
        {
            var headerIds = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT HEADER_ID, REF_CD FROM ORG_FINANCIAL_STMNT_HEADER WHERE SHEET_ID = :SHEET_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("SHEET_ID", OracleDbType.Int32) { Value = sheetId });

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int headerId = Convert.ToInt32(reader["HEADER_ID"]);
                                string refCd = reader["REF_CD"].ToString();
                                string formattedText = $"{headerId} ({refCd})";

                                headerIds.Add(new SelectListItem
                                {
                                    Value = headerId.ToString(),
                                    Text = formattedText
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching header IDs.");
            }

            return Json(headerIds);
        }

        // POST: /StatementDetails/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveData(StatementDetailsInputModel input)
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

                // Repopulate dropdown lists after a failed validation
                input.StatementTypes = GetStatementTypes();
                input.AccountCategories = GetAccountCategories();
                // Reinitialize empty lists for dropdowns
                input.SheetIds = new List<SelectListItem>();
                input.HeaderIds = new List<SelectListItem>();

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
                        INSERT INTO ORG_FINANCIAL_STMNT_DETAIL 
                        (STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY) 
                        VALUES (:STMNT_ID, :SHEET_ID, :HEADER_ID, :GL_ACCT_CAT_CD, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY)";

                    using (var insertCommand = new OracleCommand(insertQuery, connection))
                    {
                        AddParameter(insertCommand, "STMNT_ID", OracleDbType.Int32, input.STMNT_ID);
                        AddParameter(insertCommand, "SHEET_ID", OracleDbType.Int32, input.SHEET_ID);
                        AddParameter(insertCommand, "HEADER_ID", OracleDbType.Int32, input.HEADER_ID);
                        AddParameter(insertCommand, "GL_ACCT_CAT_CD", OracleDbType.Varchar2, input.GL_ACCT_CAT_CD);
                        AddParameter(insertCommand, "REF_CD", OracleDbType.Varchar2, input.REF_CD);
                        AddParameter(insertCommand, "DESCRIPTION", OracleDbType.Varchar2, input.DESCRIPTION);
                        AddParameter(insertCommand, "SYS_CREATE_TS", OracleDbType.TimeStamp, input.SYS_CREATE_TS);
                        AddParameter(insertCommand, "CREATED_BY", OracleDbType.Varchar2, input.CREATED_BY);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Data saved successfully for STMNT_ID: {STMNT_ID}, SHEET_ID: {SHEET_ID}", input.STMNT_ID, input.SHEET_ID);
                TempData["SuccessMessage"] = $"Created entry for statement ID {input.STMNT_ID} and sheet ID {input.SHEET_ID}";
                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while saving statement details.");
                TempData["ErrorMessage"] = "An error occurred while saving your data. Please try again.";

                // Repopulate dropdown lists after a failed insertion
                input.StatementTypes = GetStatementTypes();
                input.AccountCategories = GetAccountCategories();
                // Reinitialize empty lists for dropdowns
                input.SheetIds = new List<SelectListItem>();
                input.HeaderIds = new List<SelectListItem>();

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

        private void AddParameter(OracleCommand command, string name, OracleDbType type, object value)
        {
            command.Parameters.Add(new OracleParameter(name, type) { Value = value });
        }

        // New action method to fetch data from ORG_FINANCIAL_STMNT_DETAIL and display it in a grid
        public IActionResult Grid()
        {
            var details = GetDetails();
            return View(details);
        }

        private List<Detail> GetDetails()
        {
            var details = new List<Detail>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_DETAIL";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                details.Add(new Detail
                                {
                                    STMNT_ID = Convert.ToInt32(reader["STMNT_ID"]),
                                    SHEET_ID = Convert.ToInt32(reader["SHEET_ID"]),
                                    HEADER_ID = Convert.ToInt32(reader["HEADER_ID"]),
                                    GL_ACCT_CAT_CD = reader["GL_ACCT_CAT_CD"].ToString(),
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
                _logger.LogError(ex, "Database error occurred while fetching detail data.");
            }

            return details;
        }

        // Data model class for ORG_FINANCIAL_STMNT_DETAIL
        public class Detail
        {
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public int HEADER_ID { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }

    // Input model class
    public class StatementDetailsInputModel
    {
        public StatementDetailsInputModel()
        {
            StatementTypes = new List<SelectListItem>();
            SheetIds = new List<SelectListItem>();
            HeaderIds = new List<SelectListItem>();
            AccountCategories = new List<SelectListItem>();
        }

        [Required(ErrorMessage = "STMNT_ID is required.")]
        public int STMNT_ID { get; set; }

        [Required(ErrorMessage = "SHEET_ID is required.")]
        public int SHEET_ID { get; set; }

        [Required(ErrorMessage = "HEADER_ID is required.")]
        public int HEADER_ID { get; set; }

        [Required(ErrorMessage = "GL_ACCT_CAT_CD is required.")]
        public string GL_ACCT_CAT_CD { get; set; }

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
        public List<SelectListItem> SheetIds { get; set; }
        public List<SelectListItem> HeaderIds { get; set; }
        public List<SelectListItem> AccountCategories { get; set; }
    }
}