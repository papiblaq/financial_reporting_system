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
            var model = new SheetInputModel();
            PopulateStatementTypes();
            return View(model);
        }

        // POST: /Sheet/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveData(SheetInputModel input)
        {
            PopulateStatementTypes(); // Ensure the dropdown data is always available

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state detected.");
                return View("Index", input);
            }

            input.SYS_CREATE_TS = DateTime.Now;

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Check if SHEET_ID already exists
                    string checkQuery = "SELECT COUNT(*) FROM ORG_FINANCIAL_STMNT_SHEET WHERE SHEET_ID = :SHEET_ID";
                    using (var checkCommand = new OracleCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.Add(new OracleParameter("SHEET_ID", OracleDbType.Int32) { Value = input.SHEET_ID });
                        int existingCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (existingCount > 0)
                        {
                            TempData["ErrorMessage"] = "SHEET_ID already exists. Please enter a different SHEET_ID.";
                            return View("Index", input);
                        }
                    }

                    // Insert the record
                    string insertQuery = @"
                        INSERT INTO ORG_FINANCIAL_STMNT_SHEET 
                        (SHEET_ID, STMNT_ID, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY) 
                        VALUES (:SHEET_ID, :STMNT_ID, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY)";

                    using (var insertCommand = new OracleCommand(insertQuery, connection))
                    {
                        AddParameter(insertCommand, "SHEET_ID", OracleDbType.Int32, input.SHEET_ID);
                        AddParameter(insertCommand, "STMNT_ID", OracleDbType.Int32, input.STMNT_ID);
                        AddParameter(insertCommand, "REF_CD", OracleDbType.Varchar2, input.REF_CD);
                        AddParameter(insertCommand, "DESCRIPTION", OracleDbType.Varchar2, input.DESCRIPTION);
                        AddParameter(insertCommand, "SYS_CREATE_TS", OracleDbType.TimeStamp, input.SYS_CREATE_TS);
                        AddParameter(insertCommand, "CREATED_BY", OracleDbType.Varchar2, input.CREATED_BY);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Data saved successfully for SHEET_ID: {SHEET_ID}", input.SHEET_ID);
                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while saving sheet data.");
                TempData["ErrorMessage"] = "An error occurred while saving your data. Please try again.";
                return View("Index", input);
            }
        }

        private void AddParameter(OracleCommand command, string name, OracleDbType type, object value)
        {
            command.Parameters.Add(new OracleParameter(name, type) { Value = value });
        }

        private void PopulateStatementTypes()
        {
            var statementTypes = new List<SelectListItem>();
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT STMNT_ID, STMNT_NAME FROM ORG_FIN_STATEMENT_TYPE";

                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                statementTypes.Add(new SelectListItem
                                {
                                    Value = reader["STMNT_ID"].ToString(),
                                    Text = reader["STMNT_NAME"].ToString()
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

            ViewBag.StatementTypes = statementTypes;
        }

        public class SheetInputModel
        {
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
        }
    }
}