using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace financial_reporting_system.Controllers
{
    public class ExcelWorkbook_Statement_typesController : Controller
    {
        private readonly string _connectionString;

        public ExcelWorkbook_Statement_typesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
        }

        // GET: ExcelWorkbook_Statement_types/Index
        public async Task<IActionResult> Index()
        {
            // Populate the dropdown list of workbooks
            ViewBag.Workbooks = await GetWorkbooksAsync();
            return View();
        }

        // GET: ExcelWorkbook_Statement_types/WorkbookGridView
        public async Task<IActionResult> WorkbookGridView()
        {
            var statementTypes = await GetStatementTypesAsync();
            return View(statementTypes);
        }

        // Helper method to fetch workbooks from the database
        private async Task<List<string>> GetWorkbooksAsync()
        {
            var workbooks = new List<string>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("SELECT DISTINCT Work_bookName FROM ExcelSheetData", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            workbooks.Add(reader["Work_bookName"].ToString());
                        }
                    }
                }
            }

            return workbooks;
        }

        // Helper method to fetch sheets for a specific workbook
        [HttpGet]
        public async Task<IActionResult> GetSheetsForWorkbook(string workbook)
        {
            var sheets = new List<string>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("SELECT DISTINCT Workbook_sheets FROM ExcelSheetData WHERE Work_bookName = :Work_bookName", connection))
                {
                    command.Parameters.Add(new OracleParameter("Work_bookName", workbook));
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sheets.Add(reader["Workbook_sheets"].ToString());
                        }
                    }
                }
            }

            return Json(new { sheets });
        }

        // Helper method to fetch all statement types from the database
        private async Task<List<StatementType>> GetStatementTypesAsync()
        {
            var statementTypes = new List<StatementType>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    @"SELECT 
                        STMNT_ID, 
                        REF_CD, 
                        DESCRIPTION, 
                        SYS_CREATE_TS, 
                        CREATED_BY, 
                        EXCEL_SHEET, 
                        EXCEL_WORKBOOK,
                        FILEPATH 
                      FROM EXCEL_WORKBOOK_STATEMENT_TYPE", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            statementTypes.Add(new StatementType
                            {
                                STMNT_ID = reader.GetInt32(0),
                                REF_CD = reader.GetString(1),
                                DESCRIPTION = reader.GetString(2),
                                SYS_CREATE_TS = reader.GetDateTime(3),
                                CREATED_BY = reader.GetString(4),
                                EXCEL_SHEET = reader.GetString(5),
                                EXCEL_WORKBOOK = reader.GetString(6),
                                FILEPATH = reader.GetString(7)
                            });
                        }
                    }
                }
            }

            return statementTypes;
        }

        // Model class for StatementType
        public class StatementType
        {
            public int STMNT_ID { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
            public string EXCEL_SHEET { get; set; }
            public string EXCEL_WORKBOOK { get; set; }
            public string FILEPATH { get; set; } // Add this property
        }

        // Model class for the form input
        public class StatementInputModel
        {
            public int STMNT_ID { get; set; } // Added
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; } // Added
            public string CREATED_BY { get; set; }
            public string EXCEL_SHEET { get; set; } // Added
            public string EXCEL_WORKBOOK { get; set; } // Added
            public string FilePath { get; set; } // Added
        }

        [HttpPost]
        public async Task<IActionResult> SaveData(StatementInputModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors below.";
                return View("Index", model);
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        INSERT INTO EXCEL_WORKBOOK_STATEMENT_TYPE (
                            STMNT_ID, 
                            REF_CD, 
                            DESCRIPTION, 
                            SYS_CREATE_TS, 
                            CREATED_BY, 
                            EXCEL_SHEET,
                            EXCEL_WORKBOOK,
                            FilePath
                        ) 
                        VALUES (
                            EXCEL_WORKBOOK_STMT_SEQ.NEXTVAL, 
                            :REF_CD, 
                            :DESCRIPTION, 
                            :SYS_CREATE_TS, 
                            :CREATED_BY, 
                            :EXCEL_SHEET,
                            :EXCEL_WORKBOOK,
                            :FilePath
                        )";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        command.Parameters.Add(new OracleParameter("SYS_CREATE_TS", DateTime.Now));
                        command.Parameters.Add(new OracleParameter("CREATED_BY", model.CREATED_BY));
                        command.Parameters.Add(new OracleParameter("EXCEL_SHEET", model.EXCEL_SHEET));
                        command.Parameters.Add(new OracleParameter("EXCEL_WORKBOOK", model.EXCEL_WORKBOOK));
                        command.Parameters.Add(new OracleParameter("FilePath", model.FilePath));

                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Set success flag
                TempData["IsSuccess"] = true;
                TempData["SuccessMessage"] = "Data saved successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while saving the data: " + ex.Message;
                return View("Index", model);
            }
        }

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
                    string deleteQuery = "DELETE FROM EXCEL_WORKBOOK_STATEMENT_TYPE WHERE STMNT_ID = :STMNT_ID";

                    using (var deleteCommand = new OracleCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.Add(new OracleParameter("STMNT_ID", id));
                        deleteCommand.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("WorkbookGridView");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = "Database error occurred while deleting statement data. Please try again.";
                // Log the exception if you have a logger
                // _logger.LogError(ex, "Database error occurred while deleting statement data.");
                return RedirectToAction("WorkbookGridView");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while processing the request. Please try again.";
                // Log the exception if you have a logger
                // _logger.LogError(ex, "An error occurred while processing the request.");
                return RedirectToAction("WorkbookGridView");
            }
        }

       
        // GET: /Statement_types/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var statementType = await GetStatementTypeByStmntIdAsync(id);
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
                EXCEL_WORKBOOK = statementType.EXCEL_WORKBOOK,
                EXCEL_SHEET = statementType.EXCEL_SHEET,
                FilePath = statementType.FILEPATH // Map FILEPATH
            };

            return View(model);
        }

        // POST: /Statement_types/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("STMNT_ID,REF_CD,DESCRIPTION,SYS_CREATE_TS,EXCEL_WORKBOOK,EXCEL_SHEET,CREATED_BY,FilePath")] StatementInputModel model)
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
                            UPDATE EXCEL_WORKBOOK_STATEMENT_TYPE 
                            SET REF_CD = :REF_CD, 
                                DESCRIPTION = :DESCRIPTION 
                            WHERE STMNT_ID = :STMNT_ID";

                        using (var updateCommand = new OracleCommand(updateQuery, connection))
                        {
                            updateCommand.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                            updateCommand.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                            updateCommand.Parameters.Add(new OracleParameter("STMNT_ID", model.STMNT_ID));

                            int rowsAffected = updateCommand.ExecuteNonQuery();
                            Console.WriteLine($"Rows affected: {rowsAffected}");
                        }
                    }

                    TempData["SuccessMessage"] = "Data updated successfully.";
                    return RedirectToAction("WorkbookGridView");
                }
                catch (OracleException ex)
                {
                    TempData["ErrorMessage"] = "Database error occurred while updating statement data. Please try again.";
                    return View(model);
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "An error occurred while processing the request. Please try again.";
                    return View(model);
                }
            }

            // If we got this far, something failed; redisplay form
            return View(model);
        }

        // Helper method to fetch a single statement type by STMNT_ID
        private async Task<StatementType> GetStatementTypeByStmntIdAsync(int stmntId)
        {
            StatementType statementType = null;

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT 
                STMNT_ID, 
                REF_CD, 
                DESCRIPTION, 
                SYS_CREATE_TS, 
                CREATED_BY, 
                EXCEL_SHEET, 
                EXCEL_WORKBOOK,
                FILEPATH 
            FROM EXCEL_WORKBOOK_STATEMENT_TYPE 
            WHERE STMNT_ID = :STMNT_ID";

                using (var command = new OracleCommand(query, connection))
                {
                    command.Parameters.Add(new OracleParameter("STMNT_ID", stmntId));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            statementType = new StatementType
                            {
                                STMNT_ID = reader.GetInt32(0),
                                REF_CD = reader.GetString(1),
                                DESCRIPTION = reader.GetString(2),
                                SYS_CREATE_TS = reader.GetDateTime(3),
                                CREATED_BY = reader.GetString(4),
                                EXCEL_SHEET = reader.GetString(5),
                                EXCEL_WORKBOOK = reader.GetString(6),
                                FILEPATH = reader.GetString(7) // Map FILEPATH
                            };
                        }
                    }
                }
            }

            return statementType;
        }

        // to check if excel sheet is available 
        [HttpGet]
        public async Task<IActionResult> CheckSheetExists(string sheet)
        {
            bool exists = false;

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    "SELECT COUNT(*) FROM EXCEL_WORKBOOK_STATEMENT_TYPE WHERE EXCEL_SHEET = :EXCEL_SHEET", connection))
                {
                    command.Parameters.Add(new OracleParameter("EXCEL_SHEET", sheet));
                    int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    exists = count > 0;
                }
            }

            return Json(new { exists });
        }
    }
}