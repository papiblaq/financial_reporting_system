using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace financial_reporting_system.Controllers
{
    public class Documentation : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly string _connectionString;

        public Documentation(IWebHostEnvironment hostingEnvironment, IConfiguration configuration)
        {
            _hostingEnvironment = hostingEnvironment;
            _connectionString = configuration.GetConnectionString("OracleConnection"); // Replace with your actual connection string
        }

        public IActionResult Index()
        {
            var templates = GetAvailableTemplates();
            ViewBag.Templates = templates;
            return View();
        }

        [HttpPost]
        public IActionResult ExportFinancialDataToExcel(List<UserDefinedCellValues> exellCellsMappingInfo, string selectedTemplate)
        {
            try
            {
                // Debugging: Print the received list
                Console.WriteLine("Received List:");
                foreach (var item in exellCellsMappingInfo)
                {
                    Console.WriteLine($"SQL Query: {item.SqlQuery}, Value for Cells: {item.ValueForCells}");
                }

                // Load the predefined Excel template
                string templatePath = Path.Combine(_hostingEnvironment.WebRootPath, "Templates", selectedTemplate);

                // Debugging: Print the template path
                Console.WriteLine($"Template Path: {templatePath}");

                // Check if the file exists
                if (!System.IO.File.Exists(templatePath))
                {
                    throw new FileNotFoundException("Excel template file not found.", templatePath);
                }

                using (FileStream fileStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
                {
                    using (ExcelEngine excelEngine = new ExcelEngine())
                    {
                        IApplication application = excelEngine.Excel;
                        application.DefaultVersion = ExcelVersion.Excel97to2003; // Set the default version to handle .xls format

                        // Load the template workbook
                        IWorkbook workbook = application.Workbooks.Open(fileStream);
                        IWorksheet worksheet = workbook.Worksheets[0];

                        // Loop through the list of UserDefinedCellValues
                        foreach (var cellValue in exellCellsMappingInfo)
                        {
                            // Execute the SQL query to fetch the specific value
                            double specificValue = GetSpecificValueFromDatabase(cellValue.SqlQuery);

                            // Insert the specific value into the specified cell
                            worksheet.Range[cellValue.ValueForCells].Value = specificValue.ToString();
                        }

                        // Save the modified workbook to a memory stream
                        MemoryStream stream = new MemoryStream();
                        workbook.SaveAs(stream);

                        // Return the file as a download
                        stream.Position = 0;
                        return File(stream, "application/vnd.ms-excel", "FinancialData.xls");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { error = "An error occurred while exporting to Excel.", details = ex.Message });
            }
        }

        private double GetSpecificValueFromDatabase(string sqlQuery)
        {
            double specificValue = 0;

            // Debugging: Print the SQL query
            Console.WriteLine($"SQL Query: {sqlQuery}");

            // Execute the query and fetch the result
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand(sqlQuery, connection))
                {
                    try
                    {
                        object result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            specificValue = Convert.ToDouble(result);
                        }
                    }
                    catch (OracleException ex)
                    {
                        // Log the Oracle exception details
                        Console.WriteLine($"Oracle Exception: {ex.Message}");
                        Console.WriteLine($"Oracle Error Code: {ex.ErrorCode}");
                        Console.WriteLine($"Oracle Error State: {ex.Source}");
                        throw;
                    }
                }
            }

            return specificValue;
        }

        private List<string> GetAvailableTemplates()
        {
            string templatesPath = Path.Combine(_hostingEnvironment.WebRootPath, "Templates");
            return Directory.GetFiles(templatesPath, "*.xls").Select(Path.GetFileName).ToList();
        }
    }

    public class UserDefinedCellValues
    {
        public string SqlQuery { get; set; }
        public string ValueForCells { get; set; }
    }
}