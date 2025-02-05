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
            var selectedWorkbook = HttpContext.Session.GetString("SelectedWorkbook");
            var selectedWorksheet = HttpContext.Session.GetString("SelectedWorksheet");
            // Retrieve the GL Account Category from the session
            var selectedGLAccountCategory = HttpContext.Session.GetString("SelectedGLAccountCategory");


            // Pass selected workbook and worksheet to the view
            ViewBag.SelectedWorkbook = selectedWorkbook;
            ViewBag.SelectedWorksheet = selectedWorksheet;
            ViewBag.SelectedGL = selectedGLAccountCategory;

           
            // Fetch available headers for the selected worksheet
            ViewBag.AvailableHeaders = GetAvailableHeaders();

            var model = new SaveDetailsInputModel
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






        // Fetch Available Headers using Selected Worksheet from Session
        private List<SelectListItem> GetAvailableHeaders()
        {
            var availableHeaders = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Header --" } // Default option
            };
            try
            {
                var selectedWorksheet = HttpContext.Session.GetString("SelectedWorksheet");
                if (string.IsNullOrEmpty(selectedWorksheet))
                {
                    _logger.LogWarning("SelectedWorksheet not found in session.");
                    return availableHeaders; // Return only the default option if no worksheet is selected
                }
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = @"
                SELECT  REF_CD, DESCRIPTION 
                FROM EXCEL_WORKBOOK_STMNT_HEADER 
                WHERE SHEET_ID = :SHEET_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("SHEET_ID", selectedWorksheet));
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string refCd = reader["REF_CD"].ToString();
                                string description = reader["DESCRIPTION"].ToString();
                                string fullHeader = $"({refCd}) {description}"; // Combine REF_CD and DESCRIPTION

                                availableHeaders.Add(new SelectListItem
                                {
                                    Value = fullHeader, // Set the Value to the full format "(REF_CD) DESCRIPTION"
                                    Text = fullHeader   // Set the Text to the same format for display
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching available headers.");
            }
            return availableHeaders;
        }




        [HttpGet]
        public async Task<IActionResult> GetHeaderIdByDescriptionInView(string description)
        {
            int? headerId = await GetHeaderIdByDescription(description); // Call the private method
            if (headerId.HasValue)
            {
                return Ok(headerId.Value); // Return HEADER_ID as JSON
            }
            return NotFound(); // Return 404 if no HEADER_ID is found
        }



        // Fetch HEADER_ID by DESCRIPTION
        private async Task<int?> GetHeaderIdByDescription(string description)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                SELECT HEADER_ID 
                FROM EXCEL_WORKBOOK_STMNT_HEADER 
                WHERE DESCRIPTION = :DESCRIPTION";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", description));
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return reader.GetInt32(0); // Return HEADER_ID
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching HEADER_ID by DESCRIPTION.");
            }

            return null; // Return null if no HEADER_ID is found
        }

        // POST: /ExcelWorkbook_Statement_Details/SaveData


    

        [HttpPost]
        public async Task<IActionResult> SaveData(SaveDetailsInputModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please correct the errors below." });
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
                    REF_CD, 
                    GL_ACCT_CAT_CD, 
                    HEADER_ID, 
                    DESCRIPTION, 
                    SYS_CREATE_TS, 
                    CREATED_BY
                ) 
                VALUES (
                    :STMNT_ID, 
                    :SHEET_ID, 
                    :REF_CD, 
                    :GL_ACCT_CAT_CD, 
                    :HEADER_ID, 
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
                        command.Parameters.Add(new OracleParameter("HEADER_ID", model.HEADER_ID)); // Use HEADER_ID
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        command.Parameters.Add(new OracleParameter("CREATED_BY", model.CREATED_BY));
                        await command.ExecuteNonQueryAsync();
                    }
                }

                return Json(new { success = true, message = "Data saved successfully!", redirectUrl = Url.Action("Index") });
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while saving Details data.");
                return Json(new { success = false, message = $"Oracle Error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request.");
                return Json(new { success = false, message = $"An unexpected error occurred: {ex.Message}" });
            }
        }






        // GET: ExcelWorkbook_Statement_Details/DetailsGridView
        public async Task<IActionResult> DetailsGridView()
        {
            var Detailss = await GetDetailssAsync();
            return View(Detailss);
        }

        // Fetch all Detailss
        private async Task<List<Details>> GetDetailssAsync()
        {
            var Detailss = new List<Details>();
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT 
                            Details_ID, 
                            STMNT_ID, 
                            SHEET_ID, 
                            REF_CD, 
                            GL_ACCT_CAT_CD, 
                            DESCRIPTION, 
                            SYS_CREATE_TS, 
                            CREATED_BY 
                        FROM EXCEL_WORKBOOK_STMNT_Details";

                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Detailss.Add(new Details
                                {
                                    Details_ID = reader.GetInt32(0),
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
                _logger.LogError(ex, "Database error occurred while fetching Detailss.");
            }
            return Detailss;
        }

        // GET: ExcelWorkbook_Statement_Details/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var Details = await GetDetailsByIdAsync(id);
            if (Details == null)
            {
                return NotFound();
            }

            var model = new EditDetailsInputModel
            {
                Details_ID = Details.Details_ID,
                STMNT_ID = Details.STMNT_ID,
                SHEET_ID = Details.SHEET_ID,
                REF_CD = Details.REF_CD,
                GL_ACCT_CAT_CD = Details.GL_ACCT_CAT_CD,
                DESCRIPTION = Details.DESCRIPTION,
                SYS_CREATE_TS = Details.SYS_CREATE_TS,
                CREATED_BY = Details.CREATED_BY
            };

            return View(model);
        }

        // Fetch a single Details by ID
        private async Task<Details> GetDetailsByIdAsync(int id)
        {
            Details Details = null;
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT 
                            Details_ID, 
                            STMNT_ID, 
                            SHEET_ID, 
                            REF_CD, 
                            GL_ACCT_CAT_CD, 
                            DESCRIPTION, 
                            SYS_CREATE_TS, 
                            CREATED_BY 
                        FROM EXCEL_WORKBOOK_STMNT_Details 
                        WHERE Details_ID = :Details_ID";

                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("Details_ID", id));
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                Details = new Details
                                {
                                    Details_ID = reader.GetInt32(0),
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
                _logger.LogError(ex, "Database error occurred while fetching Details by ID.");
            }
            return Details;
        }

        // POST: ExcelWorkbook_Statement_Details/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Details_ID,STMNT_ID,SHEET_ID,REF_CD,GL_ACCT_CAT_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] EditDetailsInputModel model)
        {
            if (id != model.Details_ID)
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
                        UPDATE EXCEL_WORKBOOK_STMNT_Details 
                        SET 
                            REF_CD = :REF_CD, 
                            GL_ACCT_CAT_CD = :GL_ACCT_CAT_CD, 
                            DESCRIPTION = :DESCRIPTION 
                        WHERE Details_ID = :Details_ID";

                    using (var command = new OracleCommand(updateQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", model.GL_ACCT_CAT_CD));
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        command.Parameters.Add(new OracleParameter("Details_ID", model.Details_ID));

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

                return RedirectToAction("DetailsGridView");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = $"Database error: {ex.Message}";
                _logger.LogError(ex, "Database error occurred while updating Details data.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                _logger.LogError(ex, "An error occurred while processing the request.");
            }

            return View(model);
        }

        // POST: ExcelWorkbook_Statement_Details/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string deleteQuery = "DELETE FROM EXCEL_WORKBOOK_STMNT_Details WHERE Details_ID = :Details_ID";

                    using (var command = new OracleCommand(deleteQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("Details_ID", id));
                        await command.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = "Record deleted successfully.";
                return RedirectToAction("DetailsGridView");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while deleting the record: {ex.Message}";
                _logger.LogError(ex, "An error occurred while deleting the record.");
                return RedirectToAction("DetailsGridView");
            }
        }

        // View model for form submission
        public class SaveDetailsInputModel
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

            // Add a property for HEADER_ID (optional, for internal use)
            public int HEADER_ID { get; set; }
        }

        // Data model class for EXCEL_WORKBOOK_STMNT_Details
        public class Details
        {
            public int Details_ID { get; set; }
            public string STMNT_ID { get; set; } // Updated to string
            public string SHEET_ID { get; set; } // Updated to string
            public string REF_CD { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }

        // View model for the Edit view
        public class EditDetailsInputModel
        {
            public int Details_ID { get; set; }
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