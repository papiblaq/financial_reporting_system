using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace financial_reporting_system.Controllers
{
    public class ExcelWorkbook_Statement_SheetController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<ExcelWorkbook_Statement_SheetController> _logger;

        public ExcelWorkbook_Statement_SheetController(IConfiguration configuration, ILogger<ExcelWorkbook_Statement_SheetController> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // GET: /ExcelWorkbook_Statement_Sheet
        public async Task<IActionResult> Index()
        {
            var model = new SheetInputModel
            {
                Workbooks = await GetWorkbooksAsync() // Populate workbook dropdown
            };

            // Pass Workbooks to the view via ViewBag
            ViewBag.Workbooks = model.Workbooks;

            return View();
        }
        // Helper method to fetch workbooks from the database
        private async Task<List<SelectListItem>> GetWorkbooksAsync()
        {
            var workbooks = new List<SelectListItem>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("SELECT DISTINCT EXCEL_WORKBOOK FROM EXCEL_WORKBOOK_STATEMENT_TYPE", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            workbooks.Add(new SelectListItem
                            {
                                Text = reader["EXCEL_WORKBOOK"].ToString(),
                                Value = reader["EXCEL_WORKBOOK"].ToString()
                            });
                        }
                    }
                }
            }

            return workbooks;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveData(SaveSheetInputModel model)
        {
            if (!ModelState.IsValid)
            {
                // Repopulate dropdowns for the view
                ViewBag.Workbooks = await GetWorkbooksAsync();
                TempData["ErrorMessage"] = "Please correct the errors below.";
                return View("Index", model); // Return the updated view with validation errors
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string insertQuery = @"
                    INSERT INTO EXCEL_WORKBOOK_STMNT_SHEET (
                        EXCEL_SHEET_ID, 
                        STMNT_ID, 
                        EXCEL_SHEET, 
                        REF_CD, 
                        DESCRIPTION, 
                        SYS_CREATE_TS, 
                        CREATED_BY
                    ) 
                    VALUES (
                        EXCEL_WORKBOOK_STMNT_SHEET_SEQ.NEXTVAL, 
                        :STMNT_ID, 
                        :EXCEL_SHEET, 
                        :REF_CD, 
                        :DESCRIPTION, 
                        SYSTIMESTAMP, 
                        :CREATED_BY
                    )";

                    using (var command = new OracleCommand(insertQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("STMNT_ID", model.STMNT_ID));
                        command.Parameters.Add(new OracleParameter("EXCEL_SHEET", model.EXCEL_SHEET));
                        command.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        command.Parameters.Add(new OracleParameter("CREATED_BY", model.CREATED_BY));

                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Set success message
                TempData["SuccessMessage"] = "Data saved successfully!";
                return RedirectToAction("Index"); // Redirect to Index after successful save
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = $"Oracle Error: {ex.Message}";
                _logger.LogError(ex, "Database error occurred while saving sheet data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            // If we got this far, something failed; redisplay form
            ViewBag.Workbooks = await GetWorkbooksAsync();
            return View("Index", model); // Return the updated view with error message
        }























        // GET: ExcelWorkbook_Statement_Sheet/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var statementSheet = await GetStatementSheetByIdAsync(id);
            if (statementSheet == null)
            {
                return NotFound();
            }

            // Map the Sheet object to EditSheetInputModel
            var model = new EditSheetInputModel
            {
                EXCEL_SHEET_ID = statementSheet.EXCEL_SHEET_ID,
                STMNT_ID = statementSheet.STMNT_ID,
                EXCEL_SHEET = statementSheet.EXCEL_SHEET,
                REF_CD = statementSheet.REF_CD,
                DESCRIPTION = statementSheet.DESCRIPTION,
                SYS_CREATE_TS = statementSheet.SYS_CREATE_TS,
                CREATED_BY = statementSheet.CREATED_BY
            };

            return View(model);
        }
        // Helper method to fetch a single statement sheet by EXCEL_SHEET_ID
        private async Task<Sheet> GetStatementSheetByIdAsync(int id)
        {
            Sheet statementSheet = null;

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    @"SELECT 
                        EXCEL_SHEET_ID, 
                        STMNT_ID, 
                        EXCEL_SHEET, 
                        REF_CD, 
                        DESCRIPTION, 
                        SYS_CREATE_TS, 
                        CREATED_BY 
                      FROM EXCEL_WORKBOOK_STMNT_SHEET 
                      WHERE EXCEL_SHEET_ID = :EXCEL_SHEET_ID", connection))
                {
                    command.Parameters.Add(new OracleParameter("EXCEL_SHEET_ID", id));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            statementSheet = new Sheet
                            {
                                EXCEL_SHEET_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                EXCEL_SHEET = reader.GetString(2),
                                REF_CD = reader.GetString(3),
                                DESCRIPTION = reader.GetString(4),
                                SYS_CREATE_TS = reader.GetDateTime(5),
                                CREATED_BY = reader.GetString(6)
                            };
                        }
                    }
                }
            }

            return statementSheet;
        }

        // POST: ExcelWorkbook_Statement_Sheet/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EXCEL_SHEET_ID,STMNT_ID,EXCEL_SHEET,REF_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] EditSheetInputModel model)
        {
            if (id != model.EXCEL_SHEET_ID)
            {
                TempData["ErrorMessage"] = "Invalid record ID.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors below.";
                return View(model); // Return the view with validation errors
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string updateQuery = @"
                    UPDATE EXCEL_WORKBOOK_STMNT_SHEET 
                    SET REF_CD = :REF_CD, 
                        DESCRIPTION = :DESCRIPTION 
                    WHERE EXCEL_SHEET_ID = :EXCEL_SHEET_ID";

                    using (var updateCommand = new OracleCommand(updateQuery, connection))
                    {
                        updateCommand.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        updateCommand.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        updateCommand.Parameters.Add(new OracleParameter("EXCEL_SHEET_ID", model.EXCEL_SHEET_ID));

                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Data updated successfully!";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "No records were updated.";
                        }
                    }
                }

                return RedirectToAction("WorkbookGridView");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = $"Database error: {ex.Message}";
                _logger.LogError(ex, "Database error occurred while updating sheet data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            // If we got this far, something failed; redisplay form
            return View(model);
        }























        // POST: ExcelWorkbook_Statement_Sheet/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand(
                        "DELETE FROM EXCEL_WORKBOOK_STMNT_SHEET WHERE EXCEL_SHEET_ID = :EXCEL_SHEET_ID", connection))
                    {
                        command.Parameters.Add(new OracleParameter("EXCEL_SHEET_ID", id));
                        await command.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Record deleted successfully.";
                return RedirectToAction("WorkbookGridView");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the record: " + ex.Message;
                _logger.LogError(ex, "An error occurred while deleting the record.");
                return RedirectToAction("WorkbookGridView");
            }
        }











        // GET: ExcelWorkbook_Statement_Sheet/WorkbookGridView
        public async Task<IActionResult> WorkbookGridView()
        {
            var statementSheets = await GetStatementSheetsAsync(); // Fetch all statement sheets
            return View(statementSheets); // Pass the list of sheets to the view
        }

        // Helper method to fetch all statement sheets from the database
        private async Task<List<Sheet>> GetStatementSheetsAsync()
        {
            var statementSheets = new List<Sheet>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    @"SELECT 
                        EXCEL_SHEET_ID, 
                        STMNT_ID, 
                        EXCEL_SHEET, 
                        REF_CD, 
                        DESCRIPTION, 
                        SYS_CREATE_TS, 
                        CREATED_BY 
                      FROM EXCEL_WORKBOOK_STMNT_SHEET", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            statementSheets.Add(new Sheet
                            {
                                EXCEL_SHEET_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                EXCEL_SHEET = reader.GetString(2),
                                REF_CD = reader.GetString(3),
                                DESCRIPTION = reader.GetString(4),
                                SYS_CREATE_TS = reader.GetDateTime(5),
                                CREATED_BY = reader.GetString(6)
                            });
                        }
                    }
                }
            }

            return statementSheets;
        }















        // Action to fetch sheets for a selected workbook
        public async Task<IActionResult> GetSheetsForWorkbook(string workbook)
        {
            var sheets = await GetExcelSheetsAsync(workbook);
            return Json(new { sheets });
        }

        // Action to fetch STMNT_ID for a selected sheet
        public async Task<IActionResult> GetStmntIdByExcelSheet(string excelSheet)
        {
            int stmntId = 0;

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("SELECT STMNT_ID FROM EXCEL_WORKBOOK_STATEMENT_TYPE WHERE EXCEL_SHEET = :EXCEL_SHEET", connection))
                {
                    command.Parameters.Add(new OracleParameter("EXCEL_SHEET", excelSheet));

                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        stmntId = Convert.ToInt32(result);
                    }
                }
            }

            return Json(new { stmntId });
        }
        // Helper method to fetch ExcelSheets for a specific workbook
        private async Task<List<SelectListItem>> GetExcelSheetsAsync(string workbook)
        {
            var excelSheets = new List<SelectListItem>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    "SELECT DISTINCT EXCEL_SHEET FROM EXCEL_WORKBOOK_STATEMENT_TYPE WHERE EXCEL_WORKBOOK = :EXCEL_WORKBOOK", connection))
                {
                    command.Parameters.Add(new OracleParameter("EXCEL_WORKBOOK", workbook));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            excelSheets.Add(new SelectListItem
                            {
                                Value = reader["EXCEL_SHEET"].ToString(),
                                Text = reader["EXCEL_SHEET"].ToString()
                            });
                        }
                    }
                }
            }

            return excelSheets;
        }



















        // View model for the form submission
        public class SaveSheetInputModel
        {
            public int STMNT_ID { get; set; }

            [Required(ErrorMessage = "Excel Sheet is required.")]
            public string EXCEL_SHEET { get; set; }

            [Required(ErrorMessage = "Reference Code is required.")]
            public string REF_CD { get; set; }

            [Required(ErrorMessage = "Description is required.")]
            public string DESCRIPTION { get; set; }

            [Required(ErrorMessage = "Created By is required.")]
            public string CREATED_BY { get; set; }
        }

        // View model for the Index view
        public class SheetInputModel
        {
            public List<SelectListItem> Workbooks { get; set; }
        }

        // Data model class for EXCEL_WORKBOOK_STMNT_SHEET
        public class Sheet
        {
            public int EXCEL_SHEET_ID { get; set; }
            public int STMNT_ID { get; set; }
            public string EXCEL_SHEET { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }

        // View model for the Edit view
        public class EditSheetInputModel
        {
            public int EXCEL_SHEET_ID { get; set; }
            public int STMNT_ID { get; set; }
            public string EXCEL_SHEET { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }
}