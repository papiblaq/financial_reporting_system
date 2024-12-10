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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveData(StatementInputModel input, IFormFile fileUpload)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid model state(make sure you have uploaded your Excel sheet).";
                return View("Index", input);
            }

            input.SYS_CREATE_TS = DateTime.Now;

            try
            {
                // Handle file upload
                if (fileUpload != null && fileUpload.Length > 0)
                {
                    var filePath = Path.Combine(input.FilePath, fileUpload.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        fileUpload.CopyTo(stream);
                    }

                    // Store the file path and uploaded Excel sheet in the database
                    StoreFilePathAndSheet(input.FilePath, fileUpload.FileName);
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

        private void StoreFilePathAndSheet(string filePath, string fileInPath)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                string insertQuery = @"
            INSERT INTO TEMP_FILE_PATHS (FILE_PATH, FILE_IN_PATH) 
            VALUES (:FILE_PATH, :FILE_IN_PATH)";

                using (var insertCommand = new OracleCommand(insertQuery, connection))
                {
                    AddParameter(insertCommand, "FILE_PATH", OracleDbType.Varchar2, filePath);
                    AddParameter(insertCommand, "FILE_IN_PATH", OracleDbType.Varchar2, fileInPath);
                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        private void StoreFilePath(string filePath)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                string insertQuery = @"
            INSERT INTO TEMP_FILE_PATHS (FILE_PATH) 
            VALUES (:FILE_PATH)";

                using (var insertCommand = new OracleCommand(insertQuery, connection))
                {
                    AddParameter(insertCommand, "FILE_PATH", OracleDbType.Varchar2, filePath);
                    insertCommand.ExecuteNonQuery();
                }
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
            public StatementInputModel()
            {
                FilePath = "your file path to excel sheets"; // Set default value for FilePath
            }

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

            // New property for the file path
            [Required(ErrorMessage = "File path is required.")]
            public string FilePath { get; set; }

            // New properties for SHEET_ID and STMNT_ID
            public int STMNT_ID { get; set; }
        }

        public class StatementType
        {
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
            public int STMNT_ID { get; set; }
            public string FilePath { get; set; } // Add FilePath property
        }

        private List<StatementType> GetStatementTypes()
        {
            var statementTypes = new List<StatementType>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT 
                            t.STMNT_ID, 
                            t.REF_CD, 
                            t.DESCRIPTION, 
                            t.SYS_CREATE_TS, 
                            t.CREATED_BY 
                        FROM 
                            ORG_FIN_STATEMENT_TYPE t";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            statementTypes.Add(new StatementType
                            {
                                STMNT_ID = reader.GetInt32(0),
                                REF_CD = reader.GetString(1),
                                DESCRIPTION = reader.GetString(2),
                                SYS_CREATE_TS = reader.GetDateTime(3),
                                CREATED_BY = reader.GetString(4)
                            });
                        }
                    }
                }
            }

            return statementTypes;
        }

        // GET: /Statement_types/Edit/{id}
        public IActionResult Edit(int id)
        {
            var statementType = GetStatementTypeByStmntId(id);
            if (statementType == null)
            {
                return NotFound();
            }

            var model = new StatementInputModel
            {
                STMNT_ID = statementType.STMNT_ID,
                REF_CD = statementType.REF_CD,
                DESCRIPTION = statementType.DESCRIPTION,
                SYS_CREATE_TS = statementType.SYS_CREATE_TS,
                CREATED_BY = statementType.CREATED_BY,
                FilePath = statementType.FilePath // Ensure FilePath is set
            };

            return View(model);
        }

        // POST: /Statement_types/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("STMNT_ID,REF_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] StatementInputModel model)
        {
            if (id != model.STMNT_ID)
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
                            UPDATE ORG_FIN_STATEMENT_TYPE 
                            SET REF_CD = :REF_CD, 
                                DESCRIPTION = :DESCRIPTION 
                            WHERE STMNT_ID = :STMNT_ID";

                        using (var updateCommand = new OracleCommand(updateQuery, connection))
                        {
                            AddParameter(updateCommand, "REF_CD", OracleDbType.Varchar2, model.REF_CD);
                            AddParameter(updateCommand, "DESCRIPTION", OracleDbType.Varchar2, model.DESCRIPTION);
                            AddParameter(updateCommand, "STMNT_ID", OracleDbType.Int32, model.STMNT_ID);

                            int rowsAffected = updateCommand.ExecuteNonQuery();
                            _logger.LogInformation($"Rows affected: {rowsAffected}");
                        }
                    }

                    TempData["SuccessMessage"] = "Data updated successfully.";
                    return RedirectToAction("Grid");
                }
                catch (OracleException ex)
                {
                    TempData["ErrorMessage"] = "Database error occurred while updating statement data. Please try again.";
                    _logger.LogError(ex, "Database error occurred while updating statement data.");
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

        private StatementType GetStatementTypeByStmntId(int stmntId)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT 
                            t.STMNT_ID, 
                            t.REF_CD, 
                            t.DESCRIPTION, 
                            t.SYS_CREATE_TS, 
                            t.CREATED_BY 
                        FROM 
                            ORG_FIN_STATEMENT_TYPE t
                        WHERE 
                            t.STMNT_ID = :STMNT_ID";

                    AddParameter(command, "STMNT_ID", OracleDbType.Int32, stmntId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new StatementType
                            {
                                STMNT_ID = reader.GetInt32(0),
                                REF_CD = reader.GetString(1),
                                DESCRIPTION = reader.GetString(2),
                                SYS_CREATE_TS = reader.GetDateTime(3),
                                CREATED_BY = reader.GetString(4)
                            };
                        }
                    }
                }
            }
            return null;
        }

        // POST: /Statement_types/Delete/{id}
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
                    string deleteQuery = "DELETE FROM ORG_FIN_STATEMENT_TYPE WHERE STMNT_ID = :STMNT_ID";

                    using (var deleteCommand = new OracleCommand(deleteQuery, connection))
                    {
                        AddParameter(deleteCommand, "STMNT_ID", OracleDbType.Int32, id);
                        deleteCommand.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("Grid");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = "Database error occurred while deleting statement data. Please try again.";
                _logger.LogError(ex, "Database error occurred while deleting statement data.");
                return RedirectToAction("Grid");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while processing the request. Please try again.";
                _logger.LogError(ex, "An error occurred while processing the request.");
                return RedirectToAction("Grid");
            }
        }
    }
}