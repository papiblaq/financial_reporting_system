using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using static financial_reporting_system.Controllers.StatementDetailsController;

namespace financial_reporting_system.Controllers
{
    public class ExcelWorkbook_Statement_DetailsController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<ExcelWorkbook_Statement_DetailsController> _logger;

        public ExcelWorkbook_Statement_DetailsController(IConfiguration configuration, ILogger<ExcelWorkbook_Statement_DetailsController> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // GET: /ExcelWorkbook_Statement_Details
        public async Task<IActionResult> Index()
        {
            var model = new SheetInputModel
            {
                SelectedWorkbook = await GetWorkbooksAsync(), // Populate workbook dropdown
                AccountCategories = await GetAccountCategoriesAsync() // Populate GL account categories dropdown
            };

            // Pass Workbooks and AccountCategories to the view via ViewBag
            ViewBag.Workbooks = model.SelectedWorkbook;
            ViewBag.AccountCategories = model.AccountCategories;
            ViewBag.Headers = new List<HeaderData>(); // Initialize empty headers list

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

        // Action to fetch sheets for a selected workbook
        [HttpGet]
        public async Task<IActionResult> GetSheetsForWorkbook(string workbook)
        {
            var sheets = await GetExcelSheetsAsync(workbook);
            return Json(new { sheets });
        }

        // Action to fetch STMNT_ID for a selected sheet
        [HttpGet]
        public async Task<IActionResult> GetStmntIdByExcelSheet(string excelSheet)
        {
            int stmntId = 0;

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("SELECT DISTINCT STMNT_ID FROM EXCEL_WORKBOOK_STMNT_SHEET WHERE EXCEL_SHEET = :EXCEL_SHEET", connection))
                {
                    command.Parameters.Add(new OracleParameter("EXCEL_SHEET", excelSheet));

                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        stmntId = Convert.ToInt32(result);
                    }
                }
            }

            return Json(new { excelStmntId = stmntId }); // Return as excelStmntId
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

        // Fetch Sheet IDs based on selected Statement ID
        [HttpPost]
        public async Task<List<SelectListItem>> GetSheetIdsByStatementId(int stmntId)
        {
            var sheetIds = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT EXCEL_SHEET_ID, REF_CD, DESCRIPTION FROM EXCEL_WORKBOOK_STMNT_SHEET WHERE STMNT_ID = :STMNT_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Int32) { Value = stmntId });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int sheetId = Convert.ToInt32(reader["EXCEL_SHEET_ID"]);
                                string refCd = reader["REF_CD"].ToString();
                                string description = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{refCd} ({description})";

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

        // Fetch Headers based on selected Sheet ID
        [HttpPost]
        public async Task<List<SelectListItem>> GetHeadersBySheetId(int sheetId)
        {
            var headers = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT HEADER_ID, REF_CD, DESCRIPTION FROM EXCEL_WORKBOOK_STMNT_HEADER WHERE SHEET_ID = :SHEET_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("SHEET_ID", OracleDbType.Int32) { Value = sheetId });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int headerId = Convert.ToInt32(reader["HEADER_ID"]);
                                string refCd = reader["REF_CD"].ToString();
                                string description = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{refCd} ({description})";

                                headers.Add(new SelectListItem
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
                _logger.LogError(ex, "Database error occurred while fetching headers.");
            }

            return headers;
        }

        // Fetch GL Account Categories
        private async Task<List<SelectListItem>> GetAccountCategoriesAsync()
        {
            var accountCategories = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT DISTINCT GL_ACCT_CAT_CD FROM V_ORG_CHART_OF_ACCOUNT_DETAILS";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
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

        // POST: /ExcelWorkbook_Statement_Details/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveData(SaveDetailInputModel model)
        {
            var sheetInputModel = new SheetInputModel
            {
                SelectedWorkbook = await GetWorkbooksAsync(),
                AccountCategories = await GetAccountCategoriesAsync()
            };

            ViewBag.Workbooks = sheetInputModel.SelectedWorkbook;
            ViewBag.AccountCategories = sheetInputModel.AccountCategories;
            ViewBag.Headers = new List<HeaderData>(); // Initialize empty headers list

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors below.";
                return View("Index", sheetInputModel); // Return the updated view with validation errors
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string insertQuery = @"
                INSERT INTO EXCEL_WORKBOOK_STMNT_DETAIL (
                   
                    STMNT_ID, 
                    SHEET_ID, 
                    HEADER_ID, 
                    GL_ACCT_CAT_CD, 
                    REF_CD, 
                    DESCRIPTION, 
                    SYS_CREATE_TS, 
                    CREATED_BY
                ) 
                VALUES (
                    
                    :STMNT_ID, 
                    :SHEET_ID, 
                    :HEADER_ID, 
                    :GL_ACCT_CAT_CD, 
                    :REF_CD, 
                    :DESCRIPTION, 
                    SYSTIMESTAMP, 
                    :CREATED_BY
                )";

                    using (var command = new OracleCommand(insertQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("STMNT_ID", model.STMNT_ID));
                        command.Parameters.Add(new OracleParameter("SHEET_ID", model.SHEET_ID));
                        command.Parameters.Add(new OracleParameter("HEADER_ID", model.HEADER_ID));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", model.GL_ACCT_CAT_CD));
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
                _logger.LogError(ex, "Database error occurred while saving detail data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            // If we got this far, something failed; redisplay form
            return View("Index", sheetInputModel); // Return the updated view with error message
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditDetailInputModel model)
        {
            if (id != model.DETAIL_ID)
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
                UPDATE EXCEL_WORKBOOK_STMNT_DETAIL 
                SET GL_ACCT_CAT_CD = :GL_ACCT_CAT_CD, 
                    REF_CD = :REF_CD, 
                    DESCRIPTION = :DESCRIPTION 
                WHERE DETAIL_ID = :DETAIL_ID";

                    using (var updateCommand = new OracleCommand(updateQuery, connection))
                    {
                        updateCommand.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", model.GL_ACCT_CAT_CD));
                        updateCommand.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        updateCommand.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        updateCommand.Parameters.Add(new OracleParameter("DETAIL_ID", model.DETAIL_ID));

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

                return RedirectToAction("DetailGridView");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = $"Database error: {ex.Message}";
                _logger.LogError(ex, "Database error occurred while updating detail data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            // If we got this far, something failed; redisplay form
            return View(model);
        }











        public async Task<IActionResult> DetailGridView()
        {
            var details = await GetDetailsAsync(); // Fetch all details
            return View(details); // Pass the list of details to the view
        }

        private async Task<List<Detail>> GetDetailsAsync()
        {
            var details = new List<Detail>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    @"SELECT 
                DETAIL_ID, 
                STMNT_ID, 
                SHEET_ID, 
                HEADER_ID, 
                GL_ACCT_CAT_CD, 
                REF_CD, 
                DESCRIPTION, 
                SYS_CREATE_TS, 
                CREATED_BY 
              FROM EXCEL_WORKBOOK_STMNT_DETAIL", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            details.Add(new Detail
                            {
                                DETAIL_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                SHEET_ID = reader.GetInt32(2),
                                HEADER_ID = reader.GetInt32(3),
                                GL_ACCT_CAT_CD = reader.GetString(4),
                                REF_CD = reader.GetString(5),
                                DESCRIPTION = reader.GetString(6),
                                SYS_CREATE_TS = reader.GetDateTime(7),
                                CREATED_BY = reader.GetString(8)
                            });
                        }
                    }
                }
            }

            return details;
        }

        public async Task<IActionResult> Edit(int id)
        {
            var detail = await GetDetailByIdAsync(id); // Fetch the detail by ID
            if (detail == null)
            {
                return NotFound(); // Return 404 if the detail is not found
            }

            // Map the Detail object to the EditDetailInputModel
            var model = new EditDetailInputModel
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

            return View(model); // Pass the model to the Edit view
        }

        private async Task<Detail> GetDetailByIdAsync(int id)
        {
            Detail detail = null;

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    @"SELECT 
                DETAIL_ID, 
                STMNT_ID, 
                SHEET_ID, 
                HEADER_ID, 
                GL_ACCT_CAT_CD, 
                REF_CD, 
                DESCRIPTION, 
                SYS_CREATE_TS, 
                CREATED_BY 
              FROM EXCEL_WORKBOOK_STMNT_DETAIL 
              WHERE DETAIL_ID = :DETAIL_ID", connection))
                {
                    command.Parameters.Add(new OracleParameter("DETAIL_ID", id));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            detail = new Detail
                            {
                                DETAIL_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                SHEET_ID = reader.GetInt32(2),
                                HEADER_ID = reader.GetInt32(3),
                                GL_ACCT_CAT_CD = reader.GetString(4),
                                REF_CD = reader.GetString(5),
                                DESCRIPTION = reader.GetString(6),
                                SYS_CREATE_TS = reader.GetDateTime(7),
                                CREATED_BY = reader.GetString(8)
                            };
                        }
                    }
                }
            }

            return detail;
        }














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
                        "DELETE FROM EXCEL_WORKBOOK_STMNT_DETAIL WHERE DETAIL_ID = :DETAIL_ID", connection))
                    {
                        command.Parameters.Add(new OracleParameter("DETAIL_ID", id));
                        await command.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Record deleted successfully.";
                return RedirectToAction("DetailGridView");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the record: " + ex.Message;
                _logger.LogError(ex, "An error occurred while deleting the record.");
                return RedirectToAction("DetailsGridView");
            }
        }









        // View model for the form submission
        public class SaveDetailInputModel
        {
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public int HEADER_ID { get; set; }

            [Required(ErrorMessage = "GL Account Category is required.")]
            public string GL_ACCT_CAT_CD { get; set; }

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
            public List<SelectListItem> SelectedWorkbook { get; set; }
            public List<SelectListItem> AccountCategories { get; set; }
        }

        // Class to represent header data
        public class HeaderData
        {
            public int HeaderId { get; set; }
            public string DisplayText { get; set; } // REF_CD (DESCRIPTION)
        }






        // Data model class for EXCEL_WORKBOOK_STMNT_DETAIL
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

        // View model for the Edit view
        public class EditDetailInputModel
        {
            public int DETAIL_ID { get; set; }
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public int HEADER_ID { get; set; }

            [Required(ErrorMessage = "GL Account Category Code is required.")]
            public string GL_ACCT_CAT_CD { get; set; }

            [Required(ErrorMessage = "Reference Code is required.")]
            public string REF_CD { get; set; }

            [Required(ErrorMessage = "Description is required.")]
            public string DESCRIPTION { get; set; }

            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }
}