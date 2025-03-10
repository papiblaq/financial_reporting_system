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
        private readonly ILogger _logger;

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

            // Pass selected workbook and worksheet to the view
            ViewBag.SelectedWorkbook = selectedWorkbook;
            ViewBag.SelectedWorksheet = selectedWorksheet;

            // Fetch available headers for the selected worksheet
            ViewBag.AvailableHeaders = GetAvailableHeaders();

            return View();
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
                    HEADER_ID, 
                    DESCRIPTION, 
                    SYS_CREATE_TS, 
                    CREATED_BY
                    ) 
                    VALUES (
                    :STMNT_ID, 
                    :SHEET_ID, 
                    :REF_CD, 
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
            var details = await GetDetailsAsync();
            return View(details);
        }

        // Fetch all Details
        private async Task<List<Details>> GetDetailsAsync()
        {
            var details = new List<Details>();
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                    SELECT 
                    DETAIL_ID, 
                    STMNT_ID, 
                    SHEET_ID, 
                    REF_CD, 
                    DESCRIPTION, 
                    SYS_CREATE_TS, 
                    CREATED_BY 
                    FROM EXCEL_WORKBOOK_STMNT_DETAIL";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                details.Add(new Details
                                {
                                    DETAIL_ID = reader.GetInt32(0),
                                    STMNT_ID = reader.GetString(1), // Updated to string
                                    SHEET_ID = reader.GetString(2), // Updated to string
                                    REF_CD = reader.GetString(3),
                                    DESCRIPTION = reader.GetString(4),
                                    SYS_CREATE_TS = reader.GetDateTime(5),
                                    CREATED_BY = reader.GetString(6)
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching Details.");
            }
            return details;
        }

        // GET: ExcelWorkbook_Statement_Details/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var detail = await GetDetailByIdAsync(id);
            if (detail == null)
            {
                return NotFound();
            }
            var model = new EditDetailsInputModel
            {
                DETAIL_ID = detail.DETAIL_ID,
                STMNT_ID = detail.STMNT_ID,
                SHEET_ID = detail.SHEET_ID,
                REF_CD = detail.REF_CD,
                DESCRIPTION = detail.DESCRIPTION,
                SYS_CREATE_TS = detail.SYS_CREATE_TS,
                CREATED_BY = detail.CREATED_BY
            };
            return View(model);
        }

        // Fetch a single Detail by ID
        private async Task<Details> GetDetailByIdAsync(int id)
        {
            Details detail = null;
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                    SELECT 
                    DETAIL_ID, 
                    STMNT_ID, 
                    SHEET_ID, 
                    REF_CD, 
                    DESCRIPTION, 
                    SYS_CREATE_TS, 
                    CREATED_BY 
                    FROM EXCEL_WORKBOOK_STMNT_DETAIL 
                    WHERE DETAIL_ID = :DETAIL_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("DETAIL_ID", id));
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                detail = new Details
                                {
                                    DETAIL_ID = reader.GetInt32(0),
                                    STMNT_ID = reader.GetString(1), // Updated to string
                                    SHEET_ID = reader.GetString(2), // Updated to string
                                    REF_CD = reader.GetString(3),
                                    DESCRIPTION = reader.GetString(4),
                                    SYS_CREATE_TS = reader.GetDateTime(5),
                                    CREATED_BY = reader.GetString(6)
                                };
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching Detail by ID.");
            }
            return detail;
        }

        // POST: ExcelWorkbook_Statement_Details/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DETAIL_ID,STMNT_ID,SHEET_ID,REF_CD,DESCRIPTION,SYS_CREATE_TS,CREATED_BY")] EditDetailsInputModel model)
        {
            if (id != model.DETAIL_ID)
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
                    UPDATE EXCEL_WORKBOOK_STMNT_DETAIL 
                    SET 
                    REF_CD = :REF_CD, 
                    DESCRIPTION = :DESCRIPTION 
                    WHERE DETAIL_ID = :DETAIL_ID";
                    using (var command = new OracleCommand(updateQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("REF_CD", model.REF_CD));
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", model.DESCRIPTION));
                        command.Parameters.Add(new OracleParameter("DETAIL_ID", model.DETAIL_ID));
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
                _logger.LogError(ex, "Database error occurred while updating Detail data.");
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
                    string deleteQuery = "DELETE FROM EXCEL_WORKBOOK_STMNT_DETAIL WHERE DETAIL_ID = :DETAIL_ID";
                    using (var command = new OracleCommand(deleteQuery, connection))
                    {
                        command.Parameters.Add(new OracleParameter("DETAIL_ID", id));
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
            [Required(ErrorMessage = "Description is required.")]
            public string DESCRIPTION { get; set; }
            [Required(ErrorMessage = "Created By is required.")]
            public string CREATED_BY { get; set; }
            // Add a property for HEADER_ID (optional, for internal use)
            public int HEADER_ID { get; set; }
        }

        // Data model class for EXCEL_WORKBOOK_STMNT_Details
        public class Details
        {
            public int DETAIL_ID { get; set; }
            public string STMNT_ID { get; set; } // Updated to string
            public string SHEET_ID { get; set; } // Updated to string
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }

        // View model for the Edit view
        public class EditDetailsInputModel
        {
            public int DETAIL_ID { get; set; }
            public string STMNT_ID { get; set; } // Updated to string
            public string SHEET_ID { get; set; } // Updated to string
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }
    }
}