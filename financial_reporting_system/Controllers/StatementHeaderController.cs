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
        public List<SelectListItem> GetSheetIdsByStatementId(int stmntId)
        {
            var sheetIds = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT SHEET_ID, REF_CD, DESCRIPTION FROM ORG_FINANCIAL_STMNT_SHEET WHERE STMNT_ID = :STMNT_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Int32) { Value = stmntId });

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int sheetId = Convert.ToInt32(reader["SHEET_ID"]);
                                string ref_cd = reader["REF_CD"].ToString();
                                string refCd = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{ref_cd} ({refCd})";

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

            return sheetIds;
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
                TempData["SuccessMessage"] = $"Sucessfuly created header for sheet ID :{input.SHEET_ID}";
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
                    string query = "SELECT STMNT_ID, REF_CD, DESCRIPTION FROM ORG_FIN_STATEMENT_TYPE";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int stmntId = Convert.ToInt32(reader["STMNT_ID"]); // Use STMNT_ID
                                string ref_cd = reader["REF_CD"].ToString();
                                string refCd = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{ref_cd} ({refCd})";

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

        // New action method to fetch data from ORG_FINANCIAL_STMNT_HEADER and display it in a grid
        public IActionResult Grid()
        {
            var headers = GetHeaders();
            return View(headers);
        }

        private List<Header> GetHeaders()
        {
            var headers = new List<Header>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT HEADER_ID, STMNT_ID, SHEET_ID, REF_CD, GL_ACCT_CAT_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_HEADER";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                headers.Add(new Header
                                {
                                    HEADER_ID = Convert.ToInt32(reader["HEADER_ID"]),
                                    STMNT_ID = Convert.ToInt32(reader["STMNT_ID"]),
                                    SHEET_ID = Convert.ToInt32(reader["SHEET_ID"]),
                                    REF_CD = reader["REF_CD"].ToString(),
                                    GL_ACCT_CAT_CD = reader["GL_ACCT_CAT_CD"].ToString(),
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
                _logger.LogError(ex, "Database error occurred while fetching header data.");
            }

            return headers;
        }

        // GET: /StatementHeader/Edit/{id}
        public IActionResult Edit(int id)
        {
            var header = GetHeaderById(id);
            if (header == null)
            {
                return NotFound();
            }

            var model = new StatementHeaderInputModel
            {
                HEADER_ID = header.HEADER_ID,
                STMNT_ID = header.STMNT_ID,
                SHEET_ID = header.SHEET_ID,
                REF_CD = header.REF_CD,
                GL_ACCT_CAT_CD = header.GL_ACCT_CAT_CD,
                DESCRIPTION = header.DESCRIPTION,
                SYS_CREATE_TS = header.SYS_CREATE_TS,
                CREATED_BY = header.CREATED_BY,
                StatementTypes = GetStatementTypes(),
                AccountCategories = GetAccountCategories(),
                SheetIds = GetSheetIdsByStatementId(header.STMNT_ID)
            };

            return View(model);
        }

        // POST: /StatementHeader/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("HEADER_ID,STMNT_ID,SHEET_ID,REF_CD,GL_ACCT_CAT_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] StatementHeaderInputModel model)
        {
            if (id != model.HEADER_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    using (var connection = new OracleConnection(_connectionString))
                    {
                        connection.Open();

                        // Include GL_ACCT_CAT_CD in the UPDATE query
                        string updateQuery = @"
                    UPDATE ORG_FINANCIAL_STMNT_HEADER 
                    SET REF_CD = :REF_CD, 
                        DESCRIPTION = :DESCRIPTION, 
                        GL_ACCT_CAT_CD = :GL_ACCT_CAT_CD 
                    WHERE HEADER_ID = :HEADER_ID";

                        using (var updateCommand = new OracleCommand(updateQuery, connection))
                        {
                            AddParameter(updateCommand, "REF_CD", OracleDbType.Varchar2, model.REF_CD);
                            AddParameter(updateCommand, "DESCRIPTION", OracleDbType.Varchar2, model.DESCRIPTION);
                            AddParameter(updateCommand, "GL_ACCT_CAT_CD", OracleDbType.Varchar2, model.GL_ACCT_CAT_CD); // Add GL_ACCT_CAT_CD parameter
                            AddParameter(updateCommand, "HEADER_ID", OracleDbType.Int32, model.HEADER_ID);

                            updateCommand.ExecuteNonQuery();
                        }
                    }

                    TempData["SuccessMessage"] = "Data updated successfully.";
                    return RedirectToAction("Grid");
                }
                catch (OracleException ex)
                {
                    TempData["ErrorMessage"] = "Database error occurred while updating statement header data. Please try again.";
                    _logger.LogError(ex, "Database error occurred while updating statement header data.");
                    return View(model);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while processing the request. Please try again.";
                    _logger.LogError(ex, "An error occurred while processing the request.");
                    return View(model);
                }
            }

            // If we got this far, something failed; redisplay form
            model.StatementTypes = GetStatementTypes();
            model.AccountCategories = GetAccountCategories();
            model.SheetIds = GetSheetIdsByStatementId(model.STMNT_ID);
            return View(model);
        }

        private Header GetHeaderById(int id)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT HEADER_ID, STMNT_ID, SHEET_ID, REF_CD, GL_ACCT_CAT_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_HEADER WHERE HEADER_ID = :HEADER_ID";
                using (var command = new OracleCommand(query, connection))
                {
                    AddParameter(command, "HEADER_ID", OracleDbType.Int32, id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Header
                            {
                                HEADER_ID = Convert.ToInt32(reader["HEADER_ID"]),
                                STMNT_ID = Convert.ToInt32(reader["STMNT_ID"]),
                                SHEET_ID = Convert.ToInt32(reader["SHEET_ID"]),
                                REF_CD = reader["REF_CD"].ToString(),
                                GL_ACCT_CAT_CD = reader["GL_ACCT_CAT_CD"].ToString(),
                                DESCRIPTION = reader["DESCRIPTION"].ToString(),
                                SYS_CREATE_TS = Convert.ToDateTime(reader["SYS_CREATE_TS"]),
                                CREATED_BY = reader["CREATED_BY"].ToString()
                            };
                        }
                    }
                }
            }
            return null;
        }

        // POST: /StatementHeader/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Delete the record
                    string deleteQuery = "DELETE FROM ORG_FINANCIAL_STMNT_HEADER WHERE HEADER_ID = :HEADER_ID";

                    using (var deleteCommand = new OracleCommand(deleteQuery, connection))
                    {
                        AddParameter(deleteCommand, "HEADER_ID", OracleDbType.Int32, id);
                        deleteCommand.ExecuteNonQuery();
                    }
                }


                return RedirectToAction("Grid");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = "Database error occurred while deleting statement header data. Please try again.";
                _logger.LogError(ex, "Database error occurred while deleting statement header data.");
                return RedirectToAction("Grid");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while processing the request. Please try again.";
                _logger.LogError(ex, "An error occurred while processing the request.");
                return RedirectToAction("Grid");
            }
        }

        // Data model class for ORG_FINANCIAL_STMNT_HEADER
        public class Header
        {
            public int HEADER_ID { get; set; }
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public string REF_CD { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
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

        public int HEADER_ID { get; set; }

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