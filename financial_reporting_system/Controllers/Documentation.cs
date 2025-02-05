using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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

        public IActionResult Index(string selectedDirectory, string selectedTemplate)
        {
            // Fetch distinct directories from the database
            var directories = FetchingExcelDirectories();
            ViewBag.Directories = directories;

            // Set the selected directory (if any)
            ViewBag.SelectedDirectory = selectedDirectory;

            // Fetch templates based on the selected directory
            var templates = GetAvailableTemplates(selectedDirectory);
            var refCodes = GetRefCodes();
            ViewBag.Templates = templates;
            ViewBag.RefCodes = refCodes;

            // Retrieve saved values from the temporary table
            var savedValues = GetSavedValues(selectedTemplate);
            ViewBag.SavedValues = savedValues;

            // Pass the selected template to the view
            ViewBag.SelectedTemplate = selectedTemplate;

            return View();
        }

        [HttpGet]
        public IActionResult InsertValues(string selectedDirectory, string selectedTemplate)
        {
            // Fetch distinct directories from the database
            var directories = FetchingExcelDirectories();
            ViewBag.Directories = directories;

            // Set the selected directory (if any)
            ViewBag.SelectedDirectory = selectedDirectory;

            // Fetch templates based on the selected directory
            var templates = GetAvailableTemplates(selectedDirectory);
            var refCodes = GetRefCodes();
            ViewBag.Templates = templates;
            ViewBag.RefCodes = refCodes;
            ViewBag.SelectedTemplate = selectedTemplate;

            return View("InsertValues");
        }


        // method to export financial data to excel

        [HttpPost]
        public IActionResult ExportFinancialDataToExcel(
            List<UserDefinedCellValues> exellCellsMappingInfo,
            string selectedDirectory,
            string selectedTemplate,
            string startDate,  // Start Date from the form
            string endDate)    // End Date from the form
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
                string templatePath = Path.Combine(selectedDirectory, selectedTemplate);

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
                            // Format the dates for the SQL query
                            var formattedStartDate = DateTime.Parse(startDate).ToString("dd-MMM-yyyy").ToUpper();
                            var formattedEndDate = DateTime.Parse(endDate).ToString("dd-MMM-yyyy").ToUpper();

                            // Construct the SQL query using the ref_cd and date range
                            string sqlQuery = $@"
                            SELECT SUM(gas.ledger_bal) 
                            FROM SINGLE_SHEET_MAPPED_DESCRIPTION_WITH_LEDGRRS a, gl_account_summary gas 
                            WHERE a.gl_acct_id = gas.gl_acct_id 
                            AND ref_cd = '{cellValue.RefCd}' 
                            AND VALUE_DATE BETWEEN TO_DATE('{formattedStartDate}', 'DD-MON-YYYY') AND TO_DATE('{formattedEndDate}', 'DD-MON-YYYY')";

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


        // helper method for the  method to export financial data to excel communicates wuth database
        private double GetSpecificValueFromDatabase(string sqlQuery, string refCd, string formattedStartDate, string formattedEndDate)
        {
            double result = 0.0;

            // Replace with your actual database connection string
            string connectionString = "OracleConnection";

            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand(sqlQuery, connection);
                command.Parameters.AddWithValue("@RefCd", refCd);
                command.Parameters.AddWithValue("@StartDate", formattedStartDate);
                command.Parameters.AddWithValue("@EndDate", formattedEndDate);

                connection.Open();
                var dbResult = command.ExecuteScalar();
                result = dbResult != DBNull.Value ? Convert.ToDouble(dbResult) : 0.0;
            }

            return result;
        }

        // inserting of new cell(ref_cd and cellValue) when a user clicks 'add new cell button' in the index view
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



        // method to edit  cell(ref_cd and cellValue) when a user clicks 'edit' in the index view 

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


        // method to delete  cell(ref_cd and cellValue) when a user clicks 'delete' in the index view 

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





        // this is a method to fetch paths for the dropdown

        private List<string> FetchingExcelDirectories()
        {
            var directories = new List<string>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT DISTINCT FILE_PATH FROM TEMP_FILE_PATHS", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            directories.Add(reader["FILE_PATH"].ToString());
                        }
                    }
                }
            }
            return directories;
        }
        // helper method to check if the path is valid, and returns the templates from there 
        private List<string> GetAvailableTemplates(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    // Log the error and return an empty list
                    Console.WriteLine($"Directory not found: {directoryPath}");
                    return new List<string>();
                }
                return Directory.GetFiles(directoryPath, "*.xls").Select(Path.GetFileName).ToList();
            }
            catch (Exception ex)
            {
                // Log the exception and return an empty list
                Console.WriteLine($"Error accessing directory: {ex.Message}");
                return new List<string>();
            }
        }


        //method to get the description and ref_cd of the mapped descriptions
        private List<RefCode> GetRefCodes()
        {
            var refCodes = new List<RefCode>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT DISTINCT REF_CD, DESCRIPTION FROM SINGLE_SHEET_MAPPED_DESCRIPTION", connection))
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



        // helper method to check saved values 

        [HttpGet]
        public IActionResult CheckSavedValues(string selectedTemplate)
        {
            var savedValues = GetSavedValues(selectedTemplate);
            return Json(new { noSavedValues = savedValues == null || savedValues.Count == 0 });
        }

        // method to fetch saved cell values 

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

        // New Action: GetTemplatesByDirectory


        [HttpGet]
        public IActionResult GetTemplatesByDirectory(string selectedDirectory)
        {
            try
            {
                // Debugging: Log the selected directory
                Console.WriteLine($"Fetching templates for directory: {selectedDirectory}");

                // Fetch templates for the selected directory
                var templates = GetAvailableTemplates(selectedDirectory);

                // Debugging: Log the templates
                Console.WriteLine($"Templates found: {string.Join(", ", templates)}");

                // Return the templates as JSON
                return Json(templates);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error in GetTemplatesByDirectory: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Return an error response
                return StatusCode(500, new { error = "An error occurred while fetching templates." });
            }
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