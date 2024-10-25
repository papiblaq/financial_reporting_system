using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;

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
        public IActionResult SaveData(StatementInputModel input, IFormFile fileUpload)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid model state(make shure you have uploaded your exell sheet).";
                return View("Index", input);
            }

            input.SYS_CREATE_TS = DateTime.Now;

            try
            {
                // Handle file upload
                if (fileUpload != null && fileUpload.Length > 0)
                {
                    var filePath = Path.Combine("C:\\Users\\hp\\source\\repos\\financial_reporting_system\\financial_reporting_system\\wwwroot\\Templates\\", fileUpload.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        fileUpload.CopyTo(stream);
                    }
                }

                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Insert the record
                    string insertQuery = @"
                        INSERT INTO ORG_FIN_STATEMENT_TYPE 
                        (REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY) 
                        VALUES (:REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY)";

                    using (var insertCommand = new OracleCommand(insertQuery, connection))
                    {
                        AddParameter(insertCommand, "REF_CD", OracleDbType.Varchar2, input.REF_CD);
                        AddParameter(insertCommand, "DESCRIPTION", OracleDbType.Varchar2, input.DESCRIPTION);
                        AddParameter(insertCommand, "SYS_CREATE_TS", OracleDbType.TimeStamp, input.SYS_CREATE_TS);
                        AddParameter(insertCommand, "CREATED_BY", OracleDbType.Varchar2, input.CREATED_BY);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Data saved successfully.";
                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = "Database error occurred while saving statement data. Please try again.";
                _logger.LogError(ex, "Database error occurred while saving statement data.");
                return View("Index", input);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while processing the file upload. Please try again.";
                _logger.LogError(ex, "An error occurred while processing the file upload.");
                return View("Index", input);
            }
        }

        private void AddParameter(OracleCommand command, string name, OracleDbType type, object value)
        {
            command.Parameters.Add(new OracleParameter(name, type) { Value = value });
        }

        // GET: /Statement_types/Grid
        public IActionResult Grid()
        {
            var statementTypes = GetStatementTypes();
            return View(statementTypes);
        }

        public class StatementInputModel
        {
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

        public class StatementType
        {
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }

        private List<StatementType> GetStatementTypes()
        {
            var statementTypes = new List<StatementType>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FIN_STATEMENT_TYPE";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            statementTypes.Add(new StatementType
                            {
                                REF_CD = reader.GetString(0),
                                DESCRIPTION = reader.GetString(1),
                                SYS_CREATE_TS = reader.GetDateTime(2),
                                CREATED_BY = reader.GetString(3)
                            });
                        }
                    }
                }
            }

            return statementTypes;
        }
    }
}