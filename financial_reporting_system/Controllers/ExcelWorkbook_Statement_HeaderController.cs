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
            var selectedWorkbook = HttpContext.Session.GetString("SelectedWorkbook");
            var selectedWorksheet = HttpContext.Session.GetString("SelectedWorksheet");

            // Pass selected workbook and worksheet to the view
            ViewBag.SelectedWorkbook = selectedWorkbook;
            ViewBag.SelectedWorksheet = selectedWorksheet;

            var model = new SaveHeaderInputModel
            {
                AccountCategories = await GetAccountCategoriesAsync(),

            };

            return View(model);
        }

        // Fetch GL Account Categories
        private async Task<List<SelectListItem>> GetAccountCategoriesAsync()
        {
            var accountCategories = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "-- Select Account Category --" } // Default option
                };

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

        // Fetch available workbooks
        private async Task<List<SelectListItem>> GetWorkbooksAsync()
        {
            var workbooks = new List<SelectListItem>();
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT WORKBOOK_ID, WORKBOOK_NAME FROM WORKBOOKS";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                workbooks.Add(new SelectListItem
                                {
                                    Value = reader["WORKBOOK_ID"].ToString(),
                                    Text = reader["WORKBOOK_NAME"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching workbooks.");
            }
            return workbooks;
        }

        // Fetch available sheets
        private async Task<List<SelectListItem>> GetSheetsAsync()
        {
            var sheets = new List<SelectListItem>();
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT SHEET_ID, SHEET_NAME FROM SHEETS";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                sheets.Add(new SelectListItem
                                {
                                    Value = reader["SHEET_ID"].ToString(),
                                    Text = reader["SHEET_NAME"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching sheets.");
            }
            return sheets;
        }

        // POST: /ExcelWorkbook_Statement_Header/SaveData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveData(SaveHeaderInputModel model)
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

                // Store the selected GL Account Category in the session
                HttpContext.Session.SetString("SelectedGLAccountCategory", model.GL_ACCT_CAT_CD);

                TempData["SuccessMessage"] = "Data saved successfully!";
                return RedirectToAction("Index");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = $"Oracle Error: {ex.Message}";
                _logger.LogError(ex, "Database error occurred while saving header data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            return View("Index", model);
        }

        // GET: ExcelWorkbook_Statement_Header/HeaderGridView
        public async Task<IActionResult> HeaderGridView()
        {
            var headers = await GetHeadersAsync();
            return View(headers);
        }

        // Fetch all headers
        private async Task<List<Header>> GetHeadersAsync()
        {
            var headers = new List<Header>();
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT 
                            HEADER_ID, 
                            STMNT_ID, 
                            SHEET_ID, 
                            REF_CD, 
                            GL_ACCT_CAT_CD, 
                            DESCRIPTION, 
                            SYS_CREATE_TS, 
                            CREATED_BY 
                        FROM EXCEL_WORKBOOK_STMNT_HEADER";

                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                headers.Add(new Header
                                {
                                    HEADER_ID = reader.GetInt32(0),
                                    STMNT_ID = reader.GetString(1), // Updated to string
                                    SHEET_ID = reader.GetString(2), // Updated to string
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
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching headers.");
            }
            return headers;
        }

        // GET: ExcelWorkbook_Statement_Header/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var header = await GetHeaderByIdAsync(id);
            if (header == null)
            {
                return NotFound();
            }

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

            return View(model);
        }

        // Fetch a single header by ID
        private async Task<Header> GetHeaderByIdAsync(int id)
        {
            Header header = null;
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT 
                            HEADER_ID, 
                            STMNT_ID, 
                            SHEET_ID, 
                            REF_CD, 
                            GL_ACCT_CAT_CD, 
                            DESCRIPTION, 
                            SYS_CREATE_TS, 
                            CREATED_BY 
                        FROM EXCEL_WORKBOOK_STMNT_HEADER 
                        WHERE HEADER_ID = :HEADER_ID";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("HEADER_ID", id));
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                header = new Header
                                {
                                    HEADER_ID = reader.GetInt32(0),
                                    STMNT_ID = reader.GetString(1), // Updated to string
                                    SHEET_ID = reader.GetString(2), // Updated to string
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
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching header by ID.");
            }
            return header;
        }

        // POST: ExcelWorkbook_Statement_Header/Edit/{id}
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
                return View(model);
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string updateQuery = @"
                        UPDATE EXCEL_WORKBOOK_STMNT_HEADER 
                        SET 
                            REF_CD = :REF_CD, 
                            GL_ACCT_CAT_CD = :GL_ACCT_CAT_CD, 
                            DESCRIPTION = :DESCRIPTION 
                        WHERE HEADER_ID = :HEADER_ID";

                    using (var command = new OracleCommand(updateQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", model.GL_ACCT_CAT_CD));
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        command.Parameters.Add(new OracleParameter("HEADER_ID", model.HEADER_ID));

                        int rowsAffected = await command.ExecuteNonQueryAsync();
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

            return View(model);
        }

        // POST: ExcelWorkbook_Statement_Header/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string deleteQuery = "DELETE FROM EXCEL_WORKBOOK_STMNT_HEADER WHERE HEADER_ID = :HEADER_ID";

                    using (var command = new OracleCommand(deleteQuery, connection))
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
                TempData["ErrorMessage"] = $"An error occurred while deleting the record: {ex.Message}";
                _logger.LogError(ex, "An error occurred while deleting the record.");
                return RedirectToAction("HeaderGridView");
            }
        }

        // View model for form submission
        public class SaveHeaderInputModel
        {
            public string STMNT_ID { get; set; } // Updated to string

            public string SHEET_ID { get; set; } // Updated to string

            [Required(ErrorMessage = "Reference Code is required.")]
            public string REF_CD { get; set; }

            [Required(ErrorMessage = "GL Account Category is required.")]
            public string GL_ACCT_CAT_CD { get; set; }

            [Required(ErrorMessage = "Description is required.")]
            public string DESCRIPTION { get; set; }

            [Required(ErrorMessage = "Created By is required.")]
            public string CREATED_BY { get; set; }

            // Add the AccountCategories property with a default value
            public List<SelectListItem> AccountCategories { get; set; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = "-- Select Account Category --" }
    };
        }

        // Data model class for EXCEL_WORKBOOK_STMNT_HEADER
        public class Header
        {
            public int HEADER_ID { get; set; }
            public string STMNT_ID { get; set; } // Updated to string
            public string SHEET_ID { get; set; } // Updated to string
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
            public string STMNT_ID { get; set; } // Updated to string
            public string SHEET_ID { get; set; } // Updated to string
            public string REF_CD { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }
}