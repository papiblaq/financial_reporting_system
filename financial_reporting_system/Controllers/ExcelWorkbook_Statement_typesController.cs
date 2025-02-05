using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
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

        // GET: ExcelWorkbook/Index
        public async Task<IActionResult> Index()
        {
            ViewBag.Workbooks = await GetWorkbooksAsync();
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

        public IActionResult GetSelectedWorkbook()
        {
            var workbook = HttpContext.Session.GetString("SelectedWorkbook") ?? "No workbook selected";
            return Content($"Selected Workbook: {workbook}");
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
    }
}
