using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace financial_reporting_system
{
    public class Statement_typesController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<Statement_typesController> _logger;

        public Statement_typesController(IConfiguration configuration, ILogger<Statement_typesController> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // GET: /Statement_types
        public IActionResult Index()
        {
            return View(new StatementInputModel());
        }

        // POST: /Statement_types/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveData(StatementInputModel input)
        {
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

                    // Check if STMNT_ID already exists
                    string checkQuery = "SELECT COUNT(*) FROM ORG_FIN_STATEMENT_TYPE WHERE STMNT_ID = :STMNT_ID";
                    using (var checkCommand = new OracleCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Int32) { Value = input.STMNT_ID });
                        int existingCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (existingCount > 0)
                        {
                            TempData["ErrorMessage"] = "STMNT_ID already exists. Please enter a different STMNT_ID.";
                            return View("Index", input);
                        }
                    }

                    // Insert the record
                    string insertQuery = @"
                        INSERT INTO ORG_FIN_STATEMENT_TYPE 
                        (STMNT_ID, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY) 
                        VALUES (:STMNT_ID, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY)";

                    using (var insertCommand = new OracleCommand(insertQuery, connection))
                    {
                        AddParameter(insertCommand, "STMNT_ID", OracleDbType.Int32, input.STMNT_ID);
                        AddParameter(insertCommand, "REF_CD", OracleDbType.Varchar2, input.REF_CD);
                        AddParameter(insertCommand, "DESCRIPTION", OracleDbType.Varchar2, input.DESCRIPTION);
                        AddParameter(insertCommand, "SYS_CREATE_TS", OracleDbType.TimeStamp, input.SYS_CREATE_TS);
                        AddParameter(insertCommand, "CREATED_BY", OracleDbType.Varchar2, input.CREATED_BY);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Data saved successfully for STMNT_ID: {STMNT_ID}", input.STMNT_ID);
                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while saving statement data.");
                ModelState.AddModelError(string.Empty, "An error occurred while saving your data. Please try again.");
                return View("Index", input);
            }
        }

        private void AddParameter(OracleCommand command, string name, OracleDbType type, object value)
        {
            command.Parameters.Add(new OracleParameter(name, type) { Value = value });
        }

        public class StatementInputModel
        {
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