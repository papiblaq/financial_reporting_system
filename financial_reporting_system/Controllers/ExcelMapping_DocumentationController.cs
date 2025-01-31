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
    public class ExcelMapping_Documentation : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly string _connectionString;

        public ExcelMapping_Documentation(IWebHostEnvironment hostingEnvironment, IConfiguration configuration)
        {
            _hostingEnvironment = hostingEnvironment;
            _connectionString = configuration.GetConnectionString("OracleConnection");
        }

        // Main Index Action
        public IActionResult Index(string selectedDirectory, string selectedWorkbook, string selectedTemplate)
        {
            // Fetch directories, workbooks, and sheets
            ViewBag.Directories = FetchingExcelDirectories();
            ViewBag.Workbooks = GetAvailableWorkbooks(selectedDirectory);
            ViewBag.Templates = GetSheetsForWorkbook(selectedWorkbook);
            ViewBag.RefCodes = GetRefCodes();

            // Fetch saved values if a workbook is selected
            if (!string.IsNullOrEmpty(selectedWorkbook))
            {
                ViewBag.SavedValues = ExcelWorkbookMappingData(selectedWorkbook);
            }
            else
            {
                ViewBag.SavedValues = new Dictionary<string, List<UserDefinedCellValues>>();
            }

            // Pass selected values to the view
            ViewBag.SelectedDirectory = selectedDirectory;
            ViewBag.SelectedWorkbook = selectedWorkbook;
            ViewBag.SelectedTemplate = selectedTemplate;

            return View();
        }

        // Fetch Workbooks by Directory (AJAX)
        [HttpGet]
        public IActionResult GetWorkbooksByDirectory(string selectedDirectory)
        {
            try
            {
                var workbooks = GetAvailableWorkbooks(selectedDirectory);
                return Json(workbooks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetWorkbooksByDirectory: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "An error occurred while fetching workbooks." });
            }
        }

        // Fetch Sheets by Workbook (AJAX)
        [HttpGet]
        public IActionResult GetSheetsByWorkbook(string selectedWorkbook)
        {
            try
            {
                var sheets = GetSheetsForWorkbook(selectedWorkbook);
                return Json(sheets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSheetsByWorkbook: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "An error occurred while fetching sheets." });
            }
        }

        // Check Saved Values (AJAX)
        [HttpGet]
        public IActionResult CheckSavedValues(string selectedTemplate)
        {
            try
            {
                var savedValues = GetSavedValues(selectedTemplate);
                return Json(new { noSavedValues = savedValues == null || savedValues.Count == 0 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckSavedValues: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "An error occurred while checking saved values." });
            }
        }

        // Helper method for ExcelWorkbookUpload
        public IActionResult ExcelWorkbookUpload()
        {
            // Fetch directories and pass them to the view
            ViewBag.Directories = FetchingExcelDirectories();
            ViewBag.Workbooks = new List<string>(); // Initially empty, populated via AJAX
            return View();
        }

        // Export Financial Data to Excel
        // Controller action to export financial data to Excel
        [HttpPost]
        public IActionResult ExportFinancialDataToExcel([FromBody] ExportExcelRequestModel model)
        {
            // Validate the model
            if (model == null || string.IsNullOrEmpty(model.SelectedDirectory) || string.IsNullOrEmpty(model.SelectedWorkbook) || string.IsNullOrEmpty(model.StartDate) || string.IsNullOrEmpty(model.EndDate))
            {
                return Json(new { error = "Invalid input. Please provide the directory, workbook name, start date, and end date." });
            }

            try
            {
                // Fetch the saved values from the database
                var savedValues = ExcelWorkbookMappingData(model.SelectedWorkbook);

                // Construct the workbook path
                string workbookPath = Path.Combine(model.SelectedDirectory, model.SelectedWorkbook);

                // Ensure the file exists
                if (!System.IO.File.Exists(workbookPath))
                {
                    throw new FileNotFoundException("The specified workbook does not exist.", workbookPath);
                }

                // Open the workbook
                using (FileStream fileStream = new FileStream(workbookPath, FileMode.Open, FileAccess.Read))
                using (ExcelEngine excelEngine = new ExcelEngine())
                {
                    IApplication application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Excel97to2003;

                    // Load the workbook
                    IWorkbook workbook = application.Workbooks.Open(fileStream);

                    // Loop through each worksheet in the savedValues dictionary
                    foreach (var worksheetMapping in savedValues)
                    {
                        string worksheetName = worksheetMapping.Key;
                        List<UserDefinedCellValues> cellValues = worksheetMapping.Value;

                        // Get the worksheet by name
                        IWorksheet worksheet = workbook.Worksheets[worksheetName];
                        if (worksheet == null)
                        {
                            throw new Exception($"Worksheet '{worksheetName}' not found in the workbook.");
                        }

                        // Process each cell value for the current worksheet
                        foreach (var cellValue in cellValues)
                        {
                            if (cellValue == null)
                            {
                                throw new Exception("A null value was found in the cell values list.");
                            }

                            if (string.IsNullOrEmpty(cellValue.ValueForCells))
                            {
                                throw new Exception("Cell address is null or empty.");
                            }

                            // Construct the SQL query with start and end dates
                            string sqlQuery = $@"
                            SELECT SUM(gas.ledger_bal) 
                            FROM ORG_MAPPED_DESCRIPTION_WITH_LEDGRRS a, gl_account_summary gas 
                            WHERE a.gl_acct_id = gas.gl_acct_id 
                            AND ref_cd = '1' 
                            AND VALUE_DATE BETWEEN TO_DATE('31-DEC-22', 'DD-MON-YYY') AND TO_DATE('31-AUG-24', 'DD-MON-YYY')";

                            // Execute the SQL query to fetch the specific value
                            double specificValue = GetSpecificValueFromDatabase(sqlQuery);
                            if (specificValue == null)
                            {
                                throw new Exception("No value was returned from the database.");
                            }

                            // Insert the specific value into the specified cell
                            worksheet.Range[cellValue.ValueForCells].Value = specificValue.ToString();
                        }
                    }

                    // Create a memory stream for the modified workbook
                    MemoryStream workbookStream = new MemoryStream();
                    workbook.SaveAs(workbookStream);
                    workbookStream.Position = 0;

                    // Return the workbook as a downloadable file
                    return File(workbookStream, "application/vnd.ms-excel", $"{Path.GetFileNameWithoutExtension(workbookPath)}_Processed.xls");
                }
            }
            catch (Exception ex)
            {
                // Log the exception (if logging is set up)
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Return error details
                return Json(new { error = "An error occurred while exporting to Excel.", details = ex.Message });
            }
        }



        // Save Exporting Data
        [HttpPost]
        public IActionResult SaveExportingData([FromBody] SaveExportingDataModel model)
        {
            try
            {
                // Validate the model
                if (model == null || model.SavedValues == null || model.SelectedTemplate == null)
                {
                    return Json(new { success = false, message = "Invalid data received." });
                }

                // Save the values to the temporary table
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Iterate through each worksheet and its mappings
                    foreach (var worksheetEntry in model.SavedValues)
                    {
                        string worksheetName = worksheetEntry.Key; // This will be the selected workbook value
                        var mappings = worksheetEntry.Value;

                        foreach (var item in mappings)
                        {
                            // Check for duplicate values
                            using (var checkCommand = new OracleCommand(
                                "SELECT COUNT(*) FROM EXCEL_WORKBOOK_TEMP_EXPORT_DATA WHERE TEMPLATE_NAME = :templateName AND WORKSHEET_NAME = :worksheetName AND REF_CD = :refCd AND VALUE_FOR_CELLS = :valueForCells",
                                connection))
                            {
                                checkCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                                checkCommand.Parameters.Add(new OracleParameter("worksheetName", worksheetName));
                                checkCommand.Parameters.Add(new OracleParameter("refCd", item.RefCd));
                                checkCommand.Parameters.Add(new OracleParameter("valueForCells", item.ValueForCells));

                                int duplicateCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                                if (duplicateCount > 0)
                                {
                                    Console.WriteLine($"Duplicate found for Template: {model.SelectedTemplate}, Worksheet: {worksheetName}, RefCd: {item.RefCd}, Value for Cells: {item.ValueForCells}. Skipping insertion.");
                                    continue; // Skip insertion if duplicate is found
                                }
                            }

                            // Insert the value if no duplicate is found
                            using (var insertCommand = new OracleCommand(
                                "INSERT INTO EXCEL_WORKBOOK_TEMP_EXPORT_DATA (TEMPLATE_NAME, WORKSHEET_NAME, REF_CD, VALUE_FOR_CELLS) VALUES (:templateName, :worksheetName, :refCd, :valueForCells)",
                                connection))
                            {
                                insertCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                                insertCommand.Parameters.Add(new OracleParameter("worksheetName", worksheetName));
                                insertCommand.Parameters.Add(new OracleParameter("refCd", item.RefCd));
                                insertCommand.Parameters.Add(new OracleParameter("valueForCells", item.ValueForCells));
                                insertCommand.ExecuteNonQuery();
                            }
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

        // Fetch Specific Value from Database
        private double GetSpecificValueFromDatabase(string sqlQuery)
        {
            double specificValue = 0;

            // Debugging: Print the SQL query
            Console.WriteLine($"SQL Query: {sqlQuery}");

            // Execute the query and fetch the result
            using (var connection = new OracleConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    Console.WriteLine("Database connection established successfully.");

                    using (var command = new OracleCommand(sqlQuery, connection))
                    {
                        try
                        {
                            object result = command.ExecuteScalar();

                            // Log the raw result from the database
                            Console.WriteLine($"Raw Query Result: {result}");

                            if (result != null && result != DBNull.Value)
                            {
                                if (double.TryParse(result.ToString(), out specificValue))
                                {
                                    Console.WriteLine($"Parsed Result as Double: {specificValue}");
                                }
                                else
                                {
                                    Console.WriteLine("Result is not a valid double. Defaulting to 0.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Query returned NULL or no result.");
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
                catch (Exception ex)
                {
                    Console.WriteLine($"General Exception: {ex.Message}");
                    throw;
                }
            }

            // Log the final value being returned
            Console.WriteLine($"Final Returned Value: {specificValue}");

            return specificValue;
        }


        // Fetch Directories
        private List<string> FetchingExcelDirectories()
        {
            var directories = new List<string>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT DISTINCT FILE_PATH FROM EXCEL_WORKBOOK_TEMP_FILE_PATHS", connection))
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

        // Fetch Workbooks
        private List<string> GetAvailableWorkbooks(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Console.WriteLine($"Directory not found: {directoryPath}");
                    return new List<string>();
                }
                return Directory.GetFiles(directoryPath).Select(Path.GetFileName).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing directory: {ex.Message}");
                return new List<string>();
            }
        }

        // Fetch Sheets for Workbook
        private List<string> GetSheetsForWorkbook(string workbookName)
        {
            var sheets = new List<string>();

            if (string.IsNullOrEmpty(workbookName))
            {
                Console.WriteLine("Workbook name is null or empty.");
                return sheets;
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new OracleCommand("SELECT DISTINCT Workbook_sheets FROM ExcelSheetData WHERE Work_bookName = :Work_bookName", connection))
                    {
                        command.Parameters.Add(new OracleParameter("Work_bookName", workbookName));
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sheets.Add(reader["Workbook_sheets"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetSheetsForWorkbook: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            return sheets;
        }

        // Fetch Reference Codes
        private List<RefCode> GetRefCodes()
        {
            var refCodes = new List<RefCode>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT DISTINCT ref_cd, description FROM EXCEL_WORKBOOK_FINANCIAL_MAPPING", connection))
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

        // Fetch Saved Values
        private Dictionary<string, List<UserDefinedCellValues>> GetSavedValues(string selectedTemplate)
        {
            var savedValues = new Dictionary<string, List<UserDefinedCellValues>>();

            // Validate the selected template
            if (string.IsNullOrEmpty(selectedTemplate))
            {
                Console.WriteLine("Selected template is null or empty.");
                return savedValues;
            }

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Query to fetch saved values for the selected template
                    using (var command = new OracleCommand(
                        "SELECT WORKSHEET_NAME, REF_CD, VALUE_FOR_CELLS " +
                        "FROM EXCEL_WORKBOOK_TEMP_EXPORT_DATA " +
                        "WHERE TEMPLATE_NAME = :templateName", connection))
                    {
                        command.Parameters.Add(new OracleParameter("templateName", selectedTemplate));

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Read values from the database
                                string worksheetName = reader["WORKSHEET_NAME"].ToString();
                                string refCd = reader["REF_CD"].ToString();
                                string valueForCells = reader["VALUE_FOR_CELLS"].ToString();

                                // Log the fetched data for debugging
                                Console.WriteLine($"Worksheet: {worksheetName}, RefCd: {refCd}, ValueForCells: {valueForCells}");

                                // Ensure the worksheet name exists in the dictionary
                                if (!savedValues.ContainsKey(worksheetName))
                                {
                                    savedValues[worksheetName] = new List<UserDefinedCellValues>();
                                }

                                // Add the cell value to the worksheet's list
                                savedValues[worksheetName].Add(new UserDefinedCellValues
                                {
                                    RefCd = refCd,
                                    ValueForCells = valueForCells
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                Console.WriteLine($"Error in GetSavedValues: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            return savedValues;
        }


        // this method is used to fetch mapping data of all excel sheets from a selected workbook 
        private Dictionary<string, List<UserDefinedCellValues>> ExcelWorkbookMappingData(string selectedWorkbook)
        {
            var savedValues = new Dictionary<string, List<UserDefinedCellValues>>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand(
                    "SELECT TEMPLATE_NAME, REF_CD, VALUE_FOR_CELLS " +
                    "FROM EXCEL_WORKBOOK_TEMP_EXPORT_DATA " +
                    "WHERE WORKSHEET_NAME = :worksheetName", connection))
                {
                    command.Parameters.Add(new OracleParameter("worksheetName", selectedWorkbook));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string templateName = reader["TEMPLATE_NAME"].ToString();
                            string refCd = reader["REF_CD"].ToString();
                            string valueForCells = reader["VALUE_FOR_CELLS"].ToString();

                            // Check if the template already exists in the dictionary
                            if (!savedValues.ContainsKey(templateName))
                            {
                                savedValues[templateName] = new List<UserDefinedCellValues>();
                            }

                            // Add the cell values to the corresponding template
                            savedValues[templateName].Add(new UserDefinedCellValues
                            {
                                RefCd = refCd,
                                ValueForCells = valueForCells
                            });
                        }
                    }
                }
            }
            return savedValues;
        }



        // table to display the descriptions from the selected workbook 
        [HttpGet]
        public IActionResult GetDescriptionsForWorkbook(string selectedWorkbook)
        {
            try
            {
                var sheetDescriptions = new Dictionary<string, List<string>>();

                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new OracleCommand(
                        "SELECT EWST.EXCEL_SHEET, EWSD.DESCRIPTION " +
                        "FROM EXCEL_WORKBOOK_STATEMENT_TYPE EWST " +
                        "JOIN EXCEL_WORKBOOK_STMNT_DETAIL EWSD " +
                        "ON EWST.STMNT_ID = EWSD.STMNT_ID " +
                        "WHERE EWST.EXCEL_WORKBOOK = :selectedWorkbook " +
                        "ORDER BY EWST.EXCEL_SHEET", connection))
                    {
                        command.Parameters.Add(new OracleParameter("selectedWorkbook", selectedWorkbook));
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var sheet = reader["EXCEL_SHEET"].ToString();
                                var description = reader["DESCRIPTION"].ToString();

                                if (!sheetDescriptions.ContainsKey(sheet))
                                {
                                    sheetDescriptions[sheet] = new List<string>();
                                }

                                sheetDescriptions[sheet].Add(description);
                            }
                        }
                    }
                }

                return Json(sheetDescriptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDescriptionsForWorkbook: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "An error occurred while fetching descriptions." });
            }
        }

        // editing existing rows

        [HttpPost]
        public IActionResult EditExportingData([FromBody] EditExportingDataModel model)
        {
            try
            {
                // Validate the model
                if (model == null || string.IsNullOrEmpty(model.SelectedTemplate) || string.IsNullOrEmpty(model.RefCd) || string.IsNullOrEmpty(model.ValueForCells))
                {
                    return Json(new { success = false, message = "Invalid data received." });
                }

                // Update the values in the temporary table
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    using (var updateCommand = new OracleCommand(
                        "UPDATE EXCEL_WORKBOOK_TEMP_EXPORT_DATA SET VALUE_FOR_CELLS = :valueForCells WHERE TEMPLATE_NAME = :templateName AND WORKSHEET_NAME = :worksheetName AND REF_CD = :refCd",
                        connection))
                    {
                        updateCommand.Parameters.Add(new OracleParameter("valueForCells", model.ValueForCells));
                        updateCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                        updateCommand.Parameters.Add(new OracleParameter("worksheetName", model.SelectedWorkbook));
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

        // Model for EditExportingData
        public class EditExportingDataModel
        {
            public string SelectedTemplate { get; set; }
            public string SelectedWorkbook { get; set; }
            public string RefCd { get; set; }
            public string ValueForCells { get; set; }
        }


        // deleting the saved cells
        [HttpPost]
        public IActionResult DeleteExportingData([FromBody] DeleteExportingDataModel model)
        {
            try
            {
                // Validate the model
                if (model == null || string.IsNullOrEmpty(model.SelectedTemplate) ||  string.IsNullOrEmpty(model.ValueForCells))
                {
                    return Json(new { success = false, message = "Invalid data received." });
                }

                // Delete the values from the temporary table
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    using (var deleteCommand = new OracleCommand(
                        "DELETE FROM EXCEL_WORKBOOK_TEMP_EXPORT_DATA WHERE TEMPLATE_NAME = :templateName AND WORKSHEET_NAME = :worksheetName  AND VALUE_FOR_CELLS = :valueForCells",
                        connection))
                    {
                        deleteCommand.Parameters.Add(new OracleParameter("templateName", model.SelectedTemplate));
                        deleteCommand.Parameters.Add(new OracleParameter("worksheetName", model.SelectedWorkbook));
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

        // Model for DeleteExportingData
        public class DeleteExportingDataModel
        {
            public string SelectedTemplate { get; set; }
            public string SelectedWorkbook { get; set; }
            public string RefCd { get; set; }
            public string ValueForCells { get; set; }
        }



        // Model Classes
        public class RefCode
        {
            public string RefCd { get; set; }
            public string Description { get; set; }
        }

        public class SaveExportingDataModel
        {
            public Dictionary<string, List<UserDefinedCellValues>> SavedValues { get; set; }
            public string SelectedTemplate { get; set; }
        }

        public class UserDefinedCellValues
        {
            public string RefCd { get; set; }
            public string ValueForCells { get; set; }
        }




        // Strongly-typed request model for the export action
        public class ExportExcelRequestModel
        {
            public string SelectedDirectory { get; set; }
            public string SelectedWorkbook { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
        }
    }


}