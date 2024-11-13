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
                    string query = "SELECT SHEET_ID, STMNT_ID, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_SHEET";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sheets.Add(new Sheet
                                {
                                    SHEET_ID = Convert.ToInt32(reader["SHEET_ID"]),
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

        // GET: /Sheet/Edit/{id}
        public IActionResult Edit(int id)
        {
            var sheet = GetSheetById(id);
            if (sheet == null)
            {
                return NotFound();
            }

            var model = new SheetInputModel
            {
                SHEET_ID = sheet.SHEET_ID,
                STMNT_ID = sheet.STMNT_ID,
                REF_CD = sheet.REF_CD,
                DESCRIPTION = sheet.DESCRIPTION,
                SYS_CREATE_TS = sheet.SYS_CREATE_TS,
                CREATED_BY = sheet.CREATED_BY,
                StatementTypes = GetStatementTypes()
            };

            return View(model);
        }

        // POST: /Sheet/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("SHEET_ID,REF_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] SheetInputModel model)
        {
            if (id != model.SHEET_ID)
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
                    UPDATE ORG_FINANCIAL_STMNT_SHEET 
                    SET REF_CD = :REF_CD, 
                        DESCRIPTION = :DESCRIPTION, 
                        SYS_CREATE_TS = :SYS_CREATE_TS, 
                        CREATED_BY = :CREATED_BY 
                    WHERE SHEET_ID = :SHEET_ID";

                        using (var updateCommand = new OracleCommand(updateQuery, connection))
                        {
                            AddParameter(updateCommand, "REF_CD", OracleDbType.Varchar2, model.REF_CD);
                            AddParameter(updateCommand, "DESCRIPTION", OracleDbType.Varchar2, model.DESCRIPTION);
                            AddParameter(updateCommand, "SYS_CREATE_TS", OracleDbType.Date, model.SYS_CREATE_TS);
                            AddParameter(updateCommand, "CREATED_BY", OracleDbType.Varchar2, model.CREATED_BY);
                            AddParameter(updateCommand, "SHEET_ID", OracleDbType.Int32, model.SHEET_ID);

                            updateCommand.ExecuteNonQuery();
                        }
                    }

                    _logger.LogInformation("Data updated successfully for SHEET_ID: {SHEET_ID}", model.SHEET_ID);

                    return RedirectToAction("Grid");
                }
                catch (OracleException ex)
                {
                    _logger.LogError(ex, "Database error occurred while updating sheet data.");
                    ModelState.AddModelError(string.Empty, "An error occurred while updating your data. Please try again.");
                }
            }

            // If we got this far, something failed; redisplay form
            model.StatementTypes = GetStatementTypes();
            return View(model);
        }

        // POST: /Sheet/Delete/{id}
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
                    string deleteQuery = "DELETE FROM ORG_FINANCIAL_STMNT_SHEET WHERE SHEET_ID = :SHEET_ID";

                    using (var deleteCommand = new OracleCommand(deleteQuery, connection))
                    {
                        AddParameter(deleteCommand, "SHEET_ID", OracleDbType.Int32, id);
                        deleteCommand.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Data deleted successfully for SHEET_ID: {SHEET_ID}", id);
                TempData["SuccessMessage"] = $"Deleted sheet with SHEET_ID {id}";
                return RedirectToAction("Grid");
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while deleting sheet data.");
                ModelState.AddModelError(string.Empty, "An error occurred while deleting your data. Please try again.");
                return RedirectToAction("Grid");
            }
        }

        private Sheet GetSheetById(int id)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT SHEET_ID, STMNT_ID, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_SHEET WHERE SHEET_ID = :SHEET_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        AddParameter(command, "SHEET_ID", OracleDbType.Int32, id);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Sheet
                                {
                                    SHEET_ID = Convert.ToInt32(reader["SHEET_ID"]),
                                    STMNT_ID = Convert.ToInt32(reader["STMNT_ID"]),
                                    REF_CD = reader["REF_CD"].ToString(),
                                    DESCRIPTION = reader["DESCRIPTION"].ToString(),
                                    SYS_CREATE_TS = Convert.ToDateTime(reader["SYS_CREATE_TS"]),
                                    CREATED_BY = reader["CREATED_BY"].ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching sheet data by ID.");
            }

            return null;
        }

        // POST: /Sheet/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveData(SheetInputModel model)
        {
            if (!ModelState.IsValid)
            {
                // If the model state is not valid, return the view with the current model to show validation errors
                model.StatementTypes = GetStatementTypes();
                return View("Index", model);
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    string insertQuery = @"
                INSERT INTO ORG_FINANCIAL_STMNT_SHEET (STMNT_ID, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY)
                VALUES (:STMNT_ID, :REF_CD, :DESCRIPTION, SYSTIMESTAMP, :CREATED_BY)";

                    using (var command = new OracleCommand(insertQuery, connection))
                    {
                        AddParameter(command, "STMNT_ID", OracleDbType.Int32, model.STMNT_ID);
                        AddParameter(command, "REF_CD", OracleDbType.Varchar2, model.REF_CD);
                        AddParameter(command, "DESCRIPTION", OracleDbType.Varchar2, model.DESCRIPTION);
                        AddParameter(command, "CREATED_BY", OracleDbType.Varchar2, model.CREATED_BY);

                        command.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Data saved successfully.");
                TempData["SuccessMessage"] = "Data saved successfully!";

                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while saving sheet data.");
                TempData["ErrorMessage"] = "An error occurred while saving your data. Please try again.";
                model.StatementTypes = GetStatementTypes();
                return View("Index", model);
            }
        }

        // Input model class
        public class SheetInputModel
        {
            public SheetInputModel()
            {
                StatementTypes = new List<SelectListItem>();
            }

            [Required(ErrorMessage = "SHEET_ID is required.")]
            public int SHEET_ID { get; set; }

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
            public int SHEET_ID { get; set; }
            public int STMNT_ID { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }
}