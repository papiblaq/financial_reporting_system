using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace financial_reporting_system.Controllers
{
    public class ExcelWorkbook_Statement_HeaderController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<ExcelWorkbook_Statement_HeaderController> _logger;

        public ExcelWorkbook_Statement_HeaderController(IConfiguration configuration, ILogger<ExcelWorkbook_Statement_HeaderController> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // GET: /ExcelWorkbook_Statement_Header
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

        // POST: /ExcelWorkbook_Statement_Header/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveData(SaveHeaderInputModel model)
        {
            // Declare sheetInputModel once at the beginning of the method
            var sheetInputModel = new SheetInputModel
            {
                SelectedWorkbook = await GetWorkbooksAsync(),
                AccountCategories = await GetAccountCategoriesAsync()
            };

            ViewBag.Workbooks = sheetInputModel.SelectedWorkbook;
            ViewBag.AccountCategories = sheetInputModel.AccountCategories;

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
                INSERT INTO EXCEL_WORKBOOK_STMNT_HEADER (
                    HEADER_ID, 
                    STMNT_ID, 
                    SHEET_ID, 
                    REF_CD, 
                    GL_ACCT_CAT_CD, 
                    DESCRIPTION, 
                    SYS_CREATE_TS, 
                    CREATED_BY
                ) 
                VALUES (
                    SEQ_EXCEL_WORKBOOK_STMNT_HEADER.NEXTVAL, 
                    :STMNT_ID, 
                    :SHEET_ID, 
                    :REF_CD, 
                    :GL_ACCT_CAT_CD, 
                    :DESCRIPTION, 
                    SYSTIMESTAMP, 
                    :CREATED_BY
                )";

                    using (var command = new OracleCommand(insertQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("STMNT_ID", model.STMNT_ID));
                        command.Parameters.Add(new OracleParameter("SHEET_ID", model.SHEET_ID));
                        command.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", model.GL_ACCT_CAT_CD));
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
                _logger.LogError(ex, "Database error occurred while saving header data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            // If we got this far, something failed; redisplay form
            return View("Index", sheetInputModel); // Return the updated view with error message
        }












        public async Task<IActionResult> HeaderGridView()
        {
            var headers = await GetHeadersAsync(); // Fetch all headers
            return View(headers); // Pass the list of headers to the view
        }

        private async Task<List<Header>> GetHeadersAsync()
        {
            var headers = new List<Header>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    @"SELECT 
                HEADER_ID, 
                STMNT_ID, 
                SHEET_ID, 
                REF_CD, 
                GL_ACCT_CAT_CD, 
                DESCRIPTION, 
                SYS_CREATE_TS, 
                CREATED_BY 
              FROM EXCEL_WORKBOOK_STMNT_HEADER", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            headers.Add(new Header
                            {
                                HEADER_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                SHEET_ID = reader.GetInt32(2),
                                REF_CD = reader.GetString(3),
                                GL_ACCT_CAT_CD = reader.GetString(4),
                                DESCRIPTION = reader.GetString(5),
                                SYS_CREATE_TS = reader.GetDateTime(6),
                                CREATED_BY = reader.GetString(7)
                            });
                        }
                    }
                }
            }

            return headers;
        }





        // GET: ExcelWorkbook_Statement_Header/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var header = await GetHeaderByIdAsync(id); // Fetch the header by ID
            if (header == null)
            {
                return NotFound(); // Return 404 if the header is not found
            }

            // Map the Header object to the EditHeaderInputModel
            var model = new EditHeaderInputModel
            {
                HEADER_ID = header.HEADER_ID,
                STMNT_ID = header.STMNT_ID,
                SHEET_ID = header.SHEET_ID,
                REF_CD = header.REF_CD,
                GL_ACCT_CAT_CD = header.GL_ACCT_CAT_CD,
                DESCRIPTION = header.DESCRIPTION,
                SYS_CREATE_TS = header.SYS_CREATE_TS,
                CREATED_BY = header.CREATED_BY
            };

            return View(model); // Pass the model to the Edit view
        }
        // Helper method to fetch a single header by HEADER_ID
        private async Task<Header> GetHeaderByIdAsync(int id)
        {
            Header header = null;

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    @"SELECT 
                HEADER_ID, 
                STMNT_ID, 
                SHEET_ID, 
                REF_CD, 
                GL_ACCT_CAT_CD, 
                DESCRIPTION, 
                SYS_CREATE_TS, 
                CREATED_BY 
              FROM EXCEL_WORKBOOK_STMNT_HEADER 
              WHERE HEADER_ID = :HEADER_ID", connection))
                {
                    command.Parameters.Add(new OracleParameter("HEADER_ID", id));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            header = new Header
                            {
                                HEADER_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                SHEET_ID = reader.GetInt32(2),
                                REF_CD = reader.GetString(3),
                                GL_ACCT_CAT_CD = reader.GetString(4),
                                DESCRIPTION = reader.GetString(5),
                                SYS_CREATE_TS = reader.GetDateTime(6),
                                CREATED_BY = reader.GetString(7)
                            };
                        }
                    }
                }
            }

            return header;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("HEADER_ID,STMNT_ID,SHEET_ID,REF_CD,GL_ACCT_CAT_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] EditHeaderInputModel model)
        {
            if (id != model.HEADER_ID)
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
            UPDATE EXCEL_WORKBOOK_STMNT_HEADER 
            SET REF_CD = :REF_CD, 
                GL_ACCT_CAT_CD = :GL_ACCT_CAT_CD, 
                DESCRIPTION = :DESCRIPTION 
            WHERE HEADER_ID = :HEADER_ID";

                    using (var updateCommand = new OracleCommand(updateQuery, connection))
                    {
                        updateCommand.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        updateCommand.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", model.GL_ACCT_CAT_CD));
                        updateCommand.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        updateCommand.Parameters.Add(new OracleParameter("HEADER_ID", model.HEADER_ID));

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

                return RedirectToAction("HeaderGridView");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = $"Database error: {ex.Message}";
                _logger.LogError(ex, "Database error occurred while updating header data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            // If we got this far, something failed; redisplay form
            return View(model);
        }



        // method for deleting
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
                        "DELETE FROM EXCEL_WORKBOOK_STMNT_HEADER WHERE HEADER_ID = :HEADER_ID", connection))
                    {
                        command.Parameters.Add(new OracleParameter("HEADER_ID", id));
                        await command.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Record deleted successfully.";
                return RedirectToAction("HeaderGridView");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the record: " + ex.Message;
                _logger.LogError(ex, "An error occurred while deleting the record.");
                return RedirectToAction("HeaderGridView");
            }
        }














        // View model for the form submission
        public class SaveHeaderInputModel
        {
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }

            [Required(ErrorMessage = "Excel Sheet is required.")]
            public string EXCEL_SHEET { get; set; }

            [Required(ErrorMessage = "Reference Code is required.")]
            public string REF_CD { get; set; }

            [Required(ErrorMessage = "GL Account Category is required.")]
            public string GL_ACCT_CAT_CD { get; set; }

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

        // Data model class for EXCEL_WORKBOOK_STMNT_HEADER
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

        // View model for the Edit view
        public class EditHeaderInputModel
        {
            public int HEADER_ID { get; set; }
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }

            [Required(ErrorMessage = "Reference Code is required.")]
            public string REF_CD { get; set; }

            [Required(ErrorMessage = "GL Account Category Code is required.")]
            public string GL_ACCT_CAT_CD { get; set; }

            [Required(ErrorMessage = "Description is required.")]
            public string DESCRIPTION { get; set; }

            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }
}