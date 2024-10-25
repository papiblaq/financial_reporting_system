using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.XlsIO;
using System;
using System.Data;
using System.IO;

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
            return View();
        }

        [HttpPost]
        public IActionResult ExportFinancialDataToExcel(string ref_cd)
        {
            try
            {
                // Execute the SQL query to fetch the specific value
                double specificValue = GetSpecificValueFromDatabase(ref_cd);

                // Load the predefined Excel template
                string templatePath = Path.Combine(_hostingEnvironment.WebRootPath, "Templates", "FinancialTemplate.xls");

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

                        // Insert the specific value into cell C10
                        worksheet.Range["C11"].Value = specificValue.ToString();

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

        private double GetSpecificValueFromDatabase(string ref_cd)
        {
            double specificValue = 0;

            // Example SQL query with reference code filter
            string sqlQuery = @"
                SELECT SUM(gas.ledger_bal) 
                FROM ORG_FINANCIAL_MAPPING a, gl_account_summary gas 
                WHERE a.gl_acct_id = gas.gl_acct_id 
                
                AND a.REF_CD = :ref_cd";

            // Debugging: Print the SQL query and parameter values
            Console.WriteLine($"SQL Query: {sqlQuery}");
            Console.WriteLine($"Reference Code Parameter: {ref_cd}");

            // Execute the query and fetch the result
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand(sqlQuery, connection))
                {
                    // Add the reference code parameter to the command
                    command.Parameters.Add(new OracleParameter("ref_cd", ref_cd));

                    // Debugging: Print the parameter value
                    Console.WriteLine($"Parameter Value: {ref_cd}");

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
    }
}