using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace financial_reporting_system.Controllers
{
    public class ExcelWorkbook_Statement_SheetController : Controller
    {
        private readonly string _connectionString;

        public ExcelWorkbook_Statement_SheetController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
        }

        // GET: ExcelWorkbook/Index
        public async Task<IActionResult> Index()
        {
            var selectedWorkbook = HttpContext.Session.GetString("SelectedWorkbook");

            if (!string.IsNullOrEmpty(selectedWorkbook))
            {
                ViewBag.Workbooks = new List<string> { selectedWorkbook }; // Only the selected workbook
                ViewBag.SelectedWorkbook = selectedWorkbook;  // Pass the selected workbook to the view
            }
            else
            {
                ViewBag.Workbooks = await GetWorkbooksAsync(); // Fallback to fetching all workbooks
                ViewBag.SelectedWorkbook = string.Empty; // No workbook selected by default
            }

            // Ensure that ViewBag.Workbooks is not null
            if (ViewBag.Workbooks == null)
            {
                ViewBag.Workbooks = new List<string>(); // Initialize an empty list if null
            }

            return View();
        }



        [HttpPost]
        public JsonResult SaveWorkbook(string workbook)
        {
            if (!string.IsNullOrEmpty(workbook))
            {
                HttpContext.Session.SetString("SelectedWorkbook", workbook);
                return Json(new { success = true, message = "Workbook saved successfully!" });
            }
            return Json(new { success = false, message = "Invalid workbook selection." });
        }
        [HttpPost]
        public JsonResult SaveWorksheet(string worksheet)
        {
            if (!string.IsNullOrEmpty(worksheet))
            {
                HttpContext.Session.SetString("SelectedWorksheet", worksheet); // Save the worksheet to session
            }

            return Json(new { success = true, message = "Worksheet saved successfully!" });
        }


        // Fetch worksheets based on selected workbook
        [HttpGet]
        public async Task<JsonResult> GetWorksheets(string workBookName)
        {
            if (string.IsNullOrEmpty(workBookName))
            {
                return Json(new { success = false, message = "No workbook selected." });
            }

            var worksheets = await GetWorksheetsAsync(workBookName);
            return Json(new { success = true, worksheets = worksheets });
        }

        // Fetch list of workbooks from the database
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

        // Fetch worksheets based on selected workbook
        private async Task<List<string>> GetWorksheetsAsync(string workBookName)
        {
            var worksheets = new List<string>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT DISTINCT Workbook_sheets FROM ExcelSheetData WHERE Work_bookName = :Work_bookName";
                using (var command = new OracleCommand(query, connection))
                {
                    command.Parameters.Add(new OracleParameter("Work_bookName", OracleDbType.Varchar2) { Value = workBookName });

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            worksheets.Add(reader["Workbook_sheets"].ToString());
                        }
                    }
                }
            }

            return worksheets;
        }
    }

}
