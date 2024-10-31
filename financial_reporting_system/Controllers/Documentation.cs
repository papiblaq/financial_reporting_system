using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
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

        public IActionResult Index(string selectedTemplate)
        {
            var templates = GetAvailableTemplates();
            var refCodes = GetRefCodes();
            ViewBag.Templates = templates;
            ViewBag.RefCodes = refCodes;

            // Retrieve saved values from the temporary table
            var savedValues = GetSavedValues(selectedTemplate);
            ViewBag.SavedValues = savedValues;

            // Check if there are no saved values for the selected template
            if (savedValues == null || savedValues.Count == 0)
            {
                ViewBag.NoSavedValues = true;
            }
            else
            {
                ViewBag.NoSavedValues = false;
            }

            // Pass the selected template to the view
            ViewBag.SelectedTemplate = selectedTemplate;

            return View();
        }

        [HttpGet]
        public IActionResult InsertValues(string selectedTemplate)
        {
            var templates = GetAvailableTemplates();
            var refCodes = GetRefCodes();
            ViewBag.Templates = templates;
            ViewBag.RefCodes = refCodes;
            ViewBag.SelectedTemplate = selectedTemplate;

            return View("InsertValues");
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
                    Console.WriteLine($"RefCd: {item.RefCd}, Value for Cells: {item.ValueForCells}");
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
                            // Construct the SQL query using the ref_cd
                            string sqlQuery = $"SELECT SUM(gas.ledger_bal) FROM ORG_FINANCIAL_MAPPING a, gl_account_summary gas WHERE a.gl_acct_id = gas.gl_acct_id AND ref_cd = '{cellValue.RefCd}'";

                            // Execute the SQL query to fetch the specific value
                            double specificValue = GetSpecificValueFromDatabase(sqlQuery);

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

        [HttpPost]
        public IActionResult SaveExportingData([FromBody] SaveExportingDataModel model)
        {
            try
            {
                // Debugging: Print the received model
                Console.WriteLine("Received Model:");
                Console.WriteLine($"Selected Template: {model.SelectedTemplate}");
                foreach (var item in model.ExellCellsMappingInfo)
                {
                    Console.WriteLine($"RefCd: {item.RefCd}, Value for Cells: {item.ValueForCells}");
                }

                // Check for null values
                if (model == null || model.ExellCellsMappingInfo == null || model.SelectedTemplate == null)
                {
                    return Json(new { success = false, message = "Invalid data received." });
                }

                // Save the values to the temporary table
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    foreach (var item in model.ExellCellsMappingInfo)
                    {
                        // Check for duplicate values
                        using (var checkCommand = new OracleCommand("SELECT COUNT(*) FROM TEMP_EXPORT_DATA WHERE TEMPLATE_NAME = :templateName AND REF_CD = :refCd AND VALUE_FOR_CELLS = :valueForCells", connection))
                        {
                            checkCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                            checkCommand.Parameters.Add(new OracleParameter("refCd", item.RefCd));
                            checkCommand.Parameters.Add(new OracleParameter("valueForCells", item.ValueForCells));

                            int duplicateCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                            if (duplicateCount > 0)
                            {
                                Console.WriteLine($"Duplicate found for Template: {model.SelectedTemplate}, RefCd: {item.RefCd}, Value for Cells: {item.ValueForCells}. Skipping insertion.");
                                continue; // Skip insertion if duplicate is found
                            }
                        }

                        // Insert the value if no duplicate is found
                        using (var insertCommand = new OracleCommand("INSERT INTO TEMP_EXPORT_DATA (TEMPLATE_NAME, REF_CD, VALUE_FOR_CELLS) VALUES (:templateName, :refCd, :valueForCells)", connection))
                        {
                            insertCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                            insertCommand.Parameters.Add(new OracleParameter("refCd", item.RefCd));
                            insertCommand.Parameters.Add(new OracleParameter("valueForCells", item.ValueForCells));
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }

                return Json(new { success = true, message = "Exporting data saved successfully." });
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while saving exporting data.", details = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult EditExportingData([FromBody] EditExportingDataModel model)
        {
            try
            {
                // Debugging: Print the received model
                Console.WriteLine("Received Model for Edit:");
                Console.WriteLine($"Selected Template: {model.SelectedTemplate}");
                Console.WriteLine($"RefCd: {model.RefCd}, Value for Cells: {model.ValueForCells}");

                // Check for null values
                if (model == null || model.SelectedTemplate == null || model.RefCd == null || model.ValueForCells == null)
                {
                    return Json(new { success = false, message = "Invalid data received." });
                }

                // Update the values in the temporary table
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    using (var updateCommand = new OracleCommand("UPDATE TEMP_EXPORT_DATA SET VALUE_FOR_CELLS = :valueForCells WHERE TEMPLATE_NAME = :templateName AND REF_CD = :refCd", connection))
                    {
                        updateCommand.Parameters.Add(new OracleParameter("valueForCells", model.ValueForCells));
                        updateCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                        updateCommand.Parameters.Add(new OracleParameter("refCd", model.RefCd));
                        updateCommand.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Exporting data updated successfully." });
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while updating exporting data.", details = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeleteExportingData([FromBody] DeleteExportingDataModel model)
        {
            try
            {
                // Debugging: Print the received model
                Console.WriteLine("Received Model for Delete:");
                Console.WriteLine($"Selected Template: {model.SelectedTemplate}");
                Console.WriteLine($"RefCd: {model.RefCd}, Value for Cells: {model.ValueForCells}");

                // Check for null values
                if (model == null || model.SelectedTemplate == null || model.RefCd == null || model.ValueForCells == null)
                {
                    return Json(new { success = false, message = "Invalid data received." });
                }

                // Delete the values from the temporary table
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    using (var deleteCommand = new OracleCommand("DELETE FROM TEMP_EXPORT_DATA WHERE TEMPLATE_NAME = :templateName AND REF_CD = :refCd AND VALUE_FOR_CELLS = :valueForCells", connection))
                    {
                        deleteCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                        deleteCommand.Parameters.Add(new OracleParameter("refCd", model.RefCd));
                        deleteCommand.Parameters.Add(new OracleParameter("valueForCells", model.ValueForCells));
                        deleteCommand.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Exporting data deleted successfully." });
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while deleting exporting data.", details = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult CheckSavedValues(string selectedTemplate)
        {
            var savedValues = GetSavedValues(selectedTemplate);
            return Json(new { noSavedValues = savedValues == null || savedValues.Count == 0 });
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

        private List<RefCode> GetRefCodes()
        {
            var refCodes = new List<RefCode>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT DISTINCT ref_cd, description FROM ORG_FINANCIAL_MAPPING", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            refCodes.Add(new RefCode
                            {
                                RefCd = reader["ref_cd"].ToString(),
                                Description = reader["description"].ToString()
                            });
                        }
                    }
                }
            }
            return refCodes;
        }

        private List<UserDefinedCellValues> GetSavedValues(string selectedTemplate)
        {
            var savedValues = new List<UserDefinedCellValues>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT REF_CD, VALUE_FOR_CELLS FROM TEMP_EXPORT_DATA WHERE TEMPLATE_NAME = :templateName", connection))
                {
                    command.Parameters.Add(new OracleParameter("templateName", selectedTemplate));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            savedValues.Add(new UserDefinedCellValues
                            {
                                RefCd = reader["REF_CD"].ToString(),
                                ValueForCells = reader["VALUE_FOR_CELLS"].ToString()
                            });
                        }
                    }
                }
            }
            return savedValues;
        }

        public class RefCode
        {
            public string RefCd { get; set; }
            public string Description { get; set; }
        }

        public class UserDefinedCellValues
        {
            public string RefCd { get; set; }
            public string ValueForCells { get; set; }
        }

        public class SaveExportingDataModel
        {
            public List<UserDefinedCellValues> ExellCellsMappingInfo { get; set; }
            public string SelectedTemplate { get; set; }
        }

        public class EditExportingDataModel
        {
            public string SelectedTemplate { get; set; }
            public string RefCd { get; set; }
            public string ValueForCells { get; set; }
        }

        public class DeleteExportingDataModel
        {
            public string SelectedTemplate { get; set; }
            public string RefCd { get; set; }
            public string ValueForCells { get; set; }
        }
    }
}