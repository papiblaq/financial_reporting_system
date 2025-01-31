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
                    string query = "SELECT HEADER_ID, REF_CD, DESCRIPTION FROM ORG_FINANCIAL_STMNT_HEADER WHERE SHEET_ID = :SHEET_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("SHEET_ID", OracleDbType.Int32) { Value = sheetId });

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int headerId = Convert.ToInt32(reader["HEADER_ID"]); // Use HEADER_ID
                                string ref_cd = reader["REF_CD"].ToString();
                                string refCd = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{ref_cd} ({refCd})";

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


        // for the message after sucessful saving
        private string GetHeaderFormattedText(int headerId)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT REF_CD, DESCRIPTION FROM ORG_FINANCIAL_STMNT_HEADER WHERE HEADER_ID = :HEADER_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("HEADER_ID", OracleDbType.Int32) { Value = headerId });

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string ref_cd = reader["REF_CD"].ToString();
                                string description = reader["DESCRIPTION"].ToString();
                                return $"{ref_cd} ({description})";
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching header details.");
            }

            return "Unknown Header";
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
                input.SheetIds = new List<SelectListItem>();
                input.HeaderIds = new List<SelectListItem>();

                return View("Index", input);
            }

            input.SYS_CREATE_TS = DateTime.Now;

            // Ensure VERSION_NUMBER and REC_ST are set
            if (input.VERSION_NUMBER == null || input.VERSION_NUMBER <= 0)
            {
                input.VERSION_NUMBER = 1; // Default to 1 if not provided
            }

            if (string.IsNullOrEmpty(input.REC_ST))
            {
                input.REC_ST = "A"; // Default to 'A' if not provided
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Insert the record
                    string insertQuery = @"
            INSERT INTO ORG_FINANCIAL_STMNT_DETAIL 
            (STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, VERSION_NUMBER, REC_ST) 
            VALUES (:STMNT_ID, :SHEET_ID, :HEADER_ID, :GL_ACCT_CAT_CD, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY, :VERSION_NUMBER, :REC_ST)";

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
                        AddParameter(insertCommand, "VERSION_NUMBER", OracleDbType.Int32, input.VERSION_NUMBER); // Ensuring VERSION_NUMBER is set
                        AddParameter(insertCommand, "REC_ST", OracleDbType.Varchar2, input.REC_ST); // Ensuring REC_ST is set

                        insertCommand.ExecuteNonQuery();
                    }

                    // Fetch the formattedText for the selected HEADER_ID
                    string formattedText = GetHeaderFormattedText(input.HEADER_ID);

                    _logger.LogInformation("Data saved successfully for STMNT_ID: {STMNT_ID}, SHEET_ID: {SHEET_ID}, VERSION_NUMBER: {VERSION_NUMBER}, REC_ST: {REC_ST}",
                        input.STMNT_ID, input.SHEET_ID, input.VERSION_NUMBER, input.REC_ST);
                    TempData["SuccessMessage"] = $"Successfully created details for statement header: {formattedText}";
                }

                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                // Detailed Oracle error handling
                string detailedError = $"Oracle Error {ex.Number}: {ex.Message}\nStackTrace: {ex.StackTrace}";
                _logger.LogError(ex, "Database error occurred while saving statement details.");

                // Log query and parameter values for debugging
                _logger.LogError("Failed Query: {Query}", @"
            INSERT INTO ORG_FINANCIAL_STMNT_DETAIL 
            (STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, VERSION_NUMBER, REC_ST) 
            VALUES (:STMNT_ID, :SHEET_ID, :HEADER_ID, :GL_ACCT_CAT_CD, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY, :VERSION_NUMBER, :REC_ST)");

                _logger.LogError("Parameters: STMNT_ID={STMNT_ID}, SHEET_ID={SHEET_ID}, HEADER_ID={HEADER_ID}, GL_ACCT_CAT_CD={GL_ACCT_CAT_CD}, REF_CD={REF_CD}, DESCRIPTION={DESCRIPTION}, SYS_CREATE_TS={SYS_CREATE_TS}, CREATED_BY={CREATED_BY}, VERSION_NUMBER={VERSION_NUMBER}, REC_ST={REC_ST}",
                    input.STMNT_ID, input.SHEET_ID, input.HEADER_ID, input.GL_ACCT_CAT_CD, input.REF_CD, input.DESCRIPTION, input.SYS_CREATE_TS, input.CREATED_BY, input.VERSION_NUMBER, input.REC_ST);

                // Display error message in UI
                TempData["ErrorMessage"] = $"Database error occurred: {ex.Message} (Error Code: {ex.Number})";

                // Repopulate dropdown lists after a failed insertion
                input.StatementTypes = GetStatementTypes();
                input.AccountCategories = GetAccountCategories();
                input.SheetIds = new List<SelectListItem>();
                input.HeaderIds = new List<SelectListItem>();

                return View("Index", input);
            }
            catch (Exception ex)
            {
                // General error handling
                _logger.LogError(ex, "Unexpected error occurred while saving statement details.");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";

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
                    string query = "SELECT DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_DETAIL WHERE REC_ST = 'A'";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                details.Add(new Detail
                                {
                                    DETAIL_ID = Convert.ToInt32(reader["DETAIL_ID"]),
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

        // GET: /StatementDetails/Edit/{id}
        public IActionResult Edit(int id)
        {
            var detail = GetDetailById(id);
            if (detail == null)
            {
                return NotFound();
            }

            var model = new StatementDetailInputModel
            {
                DETAIL_ID = detail.DETAIL_ID,
                STMNT_ID = detail.STMNT_ID,
                SHEET_ID = detail.SHEET_ID,
                HEADER_ID = detail.HEADER_ID,
                GL_ACCT_CAT_CD = detail.GL_ACCT_CAT_CD,
                REF_CD = detail.REF_CD,
                DESCRIPTION = detail.DESCRIPTION,
                SYS_CREATE_TS = detail.SYS_CREATE_TS,
                CREATED_BY = detail.CREATED_BY
            };

            return View(model);
        }

        // POST: /StatementDetails/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("DETAIL_ID,STMNT_ID,SHEET_ID,HEADER_ID,GL_ACCT_CAT_CD,REF_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] StatementDetailInputModel model)
        {
            if (id != model.DETAIL_ID)
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

                        string updateQuery = @"
                            UPDATE ORG_FINANCIAL_STMNT_DETAIL 
                            SET REF_CD = :REF_CD, 
                                DESCRIPTION = :DESCRIPTION 
                            WHERE DETAIL_ID = :DETAIL_ID";

                        using (var updateCommand = new OracleCommand(updateQuery, connection))
                        {
                            AddParameter(updateCommand, "REF_CD", OracleDbType.Varchar2, model.REF_CD);
                            AddParameter(updateCommand, "DESCRIPTION", OracleDbType.Varchar2, model.DESCRIPTION);
                            AddParameter(updateCommand, "DETAIL_ID", OracleDbType.Int32, model.DETAIL_ID);

                            updateCommand.ExecuteNonQuery();
                        }
                    }

                    TempData["SuccessMessage"] = "Data updated successfully.";
                    return RedirectToAction("Grid");
                }
                catch (OracleException ex)
                {
                    TempData["ErrorMessage"] = "Database error occurred while updating statement detail data. Please try again.";
                    _logger.LogError(ex, "Database error occurred while updating statement detail data.");
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
            return View(model);
        }



        // POST: /StatementDetails/Delete/{id}
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
                    string deleteQuery = "UPDATE  ORG_FINANCIAL_STMNT_DETAIL SET REC_ST = 'I', VERSION_NUMBER = VERSION_NUMBER + 1 WHERE DETAIL_ID = :DETAIL_ID";

                    using (var deleteCommand = new OracleCommand(deleteQuery, connection))
                    {
                        AddParameter(deleteCommand, "DETAIL_ID", OracleDbType.Int32, id);
                        deleteCommand.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Data deleted successfully.";
                return RedirectToAction("Grid");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = "Database error occurred while deleting statement detail data. Please try again.";
                _logger.LogError(ex, "Database error occurred while deleting statement detail data.");
                return RedirectToAction("Grid");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while processing the request. Please try again.";
                _logger.LogError(ex, "An error occurred while processing the request.");
                return RedirectToAction("Grid");
            }
        }

        private Detail GetDetailById(int id)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_DETAIL WHERE DETAIL_ID = :DETAIL_ID";
                using (var command = new OracleCommand(query, connection))
                {
                    AddParameter(command, "DETAIL_ID", OracleDbType.Int32, id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Detail
                            {
                                DETAIL_ID = Convert.ToInt32(reader["DETAIL_ID"]),
                                STMNT_ID = Convert.ToInt32(reader["STMNT_ID"]),
                                SHEET_ID = Convert.ToInt32(reader["SHEET_ID"]),
                                HEADER_ID = Convert.ToInt32(reader["HEADER_ID"]),
                                GL_ACCT_CAT_CD = reader["GL_ACCT_CAT_CD"].ToString(),
                                REF_CD = reader["REF_CD"].ToString(),
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

        // Data model class for ORG_FINANCIAL_STMNT_DETAIL
        public class Detail
        {
            public int DETAIL_ID { get; set; }
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

        public int VERSION_NUMBER { get; set; } = 1; // Default value
        public string REC_ST { get; set; } = "A"; // Default value

        public List<SelectListItem> StatementTypes { get; set; }
        public List<SelectListItem> SheetIds { get; set; }
        public List<SelectListItem> HeaderIds { get; set; }
        public List<SelectListItem> AccountCategories { get; set; }
    }

    // Input model class for editing
    public class StatementDetailInputModel
    {
        public int DETAIL_ID { get; set; }
        public int STMNT_ID { get; set; }
        public int SHEET_ID { get; set; }
        public int HEADER_ID { get; set; }
        public string GL_ACCT_CAT_CD { get; set; }

        [Required(ErrorMessage = "REF_CD is required.")]
        [StringLength(50, ErrorMessage = "REF_CD cannot be longer than 50 characters.")]
        public string REF_CD { get; set; }

        [Required(ErrorMessage = "DESCRIPTION is required.")]
        [StringLength(255, ErrorMessage = "DESCRIPTION cannot be longer than 255 characters.")]
        public string DESCRIPTION { get; set; }

        public DateTime SYS_CREATE_TS { get; set; }

        [Required(ErrorMessage = "CREATED_BY is required.")]
        [StringLength(50, ErrorMessage = "CREATED_BY cannot be longer than 50 characters.")]
        public string CREATED_BY { get; set; }
    }
}