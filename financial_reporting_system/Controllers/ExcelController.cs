using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace financial_reporting_system.Controllers
{
    public class ExcelController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly string _connectionString;
        private readonly ILogger<ExcelController> _logger;

        public ExcelController(IWebHostEnvironment hostingEnvironment, IConfiguration configuration, ILogger<ExcelController> logger)
        {
            _hostingEnvironment = hostingEnvironment;
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
        }

        // Define the StatementInputModel inside the controller
        public class StatementInputModel
        {
            [Required(ErrorMessage = "REF_CD is required.")]
            public string REF_CD { get; set; }

            [Required(ErrorMessage = "DESCRIPTION is required.")]
            public string DESCRIPTION { get; set; }

            [Required(ErrorMessage = "CREATED_BY is required.")]
            public string CREATED_BY { get; set; }

            public string EXCEL_SHEET { get; set; }

            public string FilePath { get; set; }

            public DateTime SYS_CREATE_TS { get; set; } = DateTime.Now;
        }

        // GET: Excel/Upload
        public IActionResult Upload()
        {
            return View();
        }

        // POST: Excel/Upload
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string filePath)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Please select a valid Excel file.";
                return View();
            }

            if (string.IsNullOrEmpty(filePath))
            {
                ViewBag.Error = "Please provide a valid file path.";
                return View();
            }

            try
            {
                // Check if the workbook already exists in the database
                if (await WorkbookExistsAsync(file.FileName))
                {
                    ViewBag.Error = $"The workbook '{file.FileName}' already exists in the database.";
                    return View();
                }

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using ExcelEngine excelEngine = new ExcelEngine();
                IApplication application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Excel2016;

                IWorkbook workbook = application.Workbooks.Open(memoryStream);

                // Save the file path and workbook name to the database
                await SaveFilePathAsync(file.FileName, filePath);

                foreach (IWorksheet sheet in workbook.Worksheets)
                {
                    _logger.LogInformation($"Processing sheet: {sheet.Name}");

                    // Check if the sheet already exists in the database
                    if (await SheetExistsAsync(sheet.Name))
                    {
                        ViewBag.Error = $"The sheet '{sheet.Name}' already exists in the database, Rename the sheet and try again";
                        await DeleteFilePathAsync(file.FileName);
                        return View();
                    }

                    // Cache UsedRange bounds
                    int lastRow = sheet.UsedRange.LastRow;
                    int lastColumn = sheet.UsedRange.LastColumn;

                    _logger.LogInformation($"UsedRange: Rows={lastRow}, Columns={lastColumn}");

                    for (int row = 1; row <= lastRow; row++)
                    {
                        for (int col = 1; col <= lastColumn; col++)
                        {
                            var cell = sheet.Range[row, col];
                            if (IsCellValid(cell))
                            {
                                await InsertCellDataAsync(file.FileName, sheet.Name, cell);
                            }
                        }
                    }
                }

                ViewBag.Success = "Workbook and sheets uploaded successfully!";
            }
            catch (FileFormatException ex)
            {
                _logger.LogError($"File format error: {ex.Message}");
                ViewBag.Error = "Invalid Excel file format.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                ViewBag.Error = $"An unexpected error occurred: {ex.Message}";
            }

            return View();
        }





        // helper method to check if workbook exists 

        private async Task<bool> WorkbookExistsAsync(string workbookName)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand())
                {
                    command.Connection = connection;
                    command.CommandText = @"
                SELECT COUNT(*) 
                FROM EXCEL_WORKBOOK_TEMP_FILE_PATHS 
                WHERE FILE_IN_PATH = :FILE_IN_PATH";
                    command.Parameters.Add(new OracleParameter("FILE_IN_PATH", workbookName));

                    int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        // helper method tocheck if sheet exists 

        private async Task<bool> SheetExistsAsync(string sheetName)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand())
                {
                    command.Connection = connection;
                    command.CommandText = @"
                SELECT COUNT(*) 
                FROM ExcelSheetData 
                WHERE Workbook_sheets = :Workbook_sheets";
                    command.Parameters.Add(new OracleParameter("Workbook_sheets", sheetName));

                    int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        // method to delete file path and workbook name 

        private async Task DeleteFilePathAsync(string workbookName)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand())
                {
                    command.Connection = connection;
                    command.CommandText = @"
                DELETE FROM EXCEL_WORKBOOK_TEMP_FILE_PATHS 
                WHERE FILE_IN_PATH = :FILE_IN_PATH";
                    command.Parameters.Add(new OracleParameter("FILE_IN_PATH", workbookName));

                    await command.ExecuteNonQueryAsync();
                }
            }
        } 

        // Helper method to save file path and workbook name to EXCEL_WORKBOOK_TEMP_FILE_PATHS table
        private async Task SaveFilePathAsync(string workbookName, string filePath)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand())
                {
                    command.Connection = connection;

                    // Insert into EXCEL_WORKBOOK_TEMP_FILE_PATHS table
                    command.CommandText = @"
                        INSERT INTO EXCEL_WORKBOOK_TEMP_FILE_PATHS 
                            (FILE_PATH, FILE_IN_PATH, CREATED_AT)
                        VALUES 
                            (:FILE_PATH, :FILE_IN_PATH, SYSTIMESTAMP)";

                    command.Parameters.Add(new OracleParameter("FILE_PATH", filePath));
                    command.Parameters.Add(new OracleParameter("FILE_IN_PATH", workbookName));

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        // Helper method to check if a cell contains meaningful data
        private bool IsCellValid(IRange cell)
        {
            return !string.IsNullOrWhiteSpace(cell.Value?.ToString()) || cell.HasFormula;
        }

        // Helper method to insert cell data
        private async Task InsertCellDataAsync(string fileName, string sheetName, IRange cell)
        {
            await InsertCellAsync(
                fileName,          // Workbook name
                sheetName,         // Sheet name
                cell.Address,      // Cell address
                cell.Value?.ToString(), // Cell value
                cell.HasFormula ? cell.Formula : null // Cell formula
            );
        }

        // insertion of cell information to the table excelsheetdata
        private async Task InsertCellAsync(string workBookName, string workBookSheets, string cellAddress, string value, string formula)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand())
                {
                    command.Connection = connection;

                    // Create the SQL query for single cell insertion
                    command.CommandText = @"
                        INSERT INTO ExcelSheetData (Work_bookName, Workbook_sheets, CellAddress, Value, Formula)
                        VALUES (:Work_bookName, :Workbook_sheets, :CellAddress, :Value, :Formula)";

                    command.Parameters.Add(new OracleParameter("Work_bookName", workBookName));
                    command.Parameters.Add(new OracleParameter("Workbook_sheets", workBookSheets));
                    command.Parameters.Add(new OracleParameter("CellAddress", cellAddress));
                    command.Parameters.Add(new OracleParameter("Value", value));
                    command.Parameters.Add(new OracleParameter("Formula", formula));

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        // GET: Excel/ViewSheets
        public async Task<IActionResult> ViewSheets()
        {
            ViewBag.Workbooks = new List<string>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("SELECT DISTINCT Work_bookName FROM ExcelSheetData", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var workbooks = new List<string>();
                        while (await reader.ReadAsync())
                        {
                            workbooks.Add(reader["Work_bookName"].ToString());
                        }
                        ViewBag.Workbooks = workbooks;
                    }
                }

                if (Request.Query.ContainsKey("selectedWorkbook"))
                {
                    string selectedWorkbook = Request.Query["selectedWorkbook"];
                    ViewBag.SelectedWorkbook = selectedWorkbook;

                    using (var command = new OracleCommand("SELECT DISTINCT Workbook_sheets FROM ExcelSheetData WHERE Work_bookName = :Work_bookName", connection))
                    {
                        command.Parameters.Add(new OracleParameter("Work_bookName", selectedWorkbook));
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var sheets = new List<string>();
                            while (await reader.ReadAsync())
                            {
                                sheets.Add(reader["Workbook_sheets"].ToString());
                            }
                            ViewBag.Sheets = sheets;
                        }
                    }
                }
            }

            return View();
        }

        // GET: Excel/GetSheetDetails
        [HttpGet]
        public async Task<IActionResult> GetSheetDetails(string selectedWorkbook, string selectedSheet)
        {
            try
            {
                var sheetDetails = new SheetDetail
                {
                    SheetName = selectedSheet,
                    CellValues = new List<CellValue>()
                };

                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new OracleCommand("SELECT DISTINCT * FROM ExcelSheetData WHERE Work_bookName = :Work_bookName AND Workbook_sheets = :Workbook_sheets", connection))
                    {
                        command.Parameters.Add(new OracleParameter("Work_bookName", selectedWorkbook));
                        command.Parameters.Add(new OracleParameter("Workbook_sheets", selectedSheet));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                sheetDetails.CellValues.Add(new CellValue
                                {
                                    CellAddress = reader["CellAddress"].ToString(),
                                    Value = reader["Value"].ToString(),
                                    Formula = reader["Formula"].ToString()
                                });
                            }
                        }
                    }
                }

                ViewBag.SheetDetails = sheetDetails.CellValues;
                ViewBag.SelectedWorkbook = selectedWorkbook;
                ViewBag.SelectedSheet = selectedSheet;

                return View("ViewSheets");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                ViewBag.Error = $"An error occurred: {ex.Message}";
                return View("ViewSheets");
            }
        }

        // Updated ExcelAnalysis action
        public async Task<IActionResult> ExcelAnalysis()
        {
            // Fetch workbooks from the database
            ViewBag.Workbooks = await GetWorkbooksAsync();
            return View();
        }

        // Helper method to fetch workbooks
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

        // New action to fetch sheets for a workbook
        public async Task<IActionResult> GetSheetsForWorkbook(string workbook)
        {
            var sheets = new List<string>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("SELECT DISTINCT Workbook_sheets FROM ExcelSheetData WHERE Work_bookName = :Work_bookName", connection))
                {
                    command.Parameters.Add(new OracleParameter("Work_bookName", workbook));
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sheets.Add(reader["Workbook_sheets"].ToString());
                        }
                    }
                }
            }

            return Json(new { sheets });
        }

        // POST: Excel/SaveData
        [HttpPost]
        public IActionResult SaveData(StatementInputModel input, IFormFile fileUpload)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid model state (make sure you have uploaded your Excel sheet).";
                return View("ExcelAnalysis", input);
            }

            input.SYS_CREATE_TS = DateTime.Now;

            try
            {
                // Handle file upload
                if (fileUpload != null && fileUpload.Length > 0)
                {
                    var filePath = Path.Combine(input.FilePath, fileUpload.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        fileUpload.CopyTo(stream);
                    }

                    // Store the file path and uploaded Excel sheet in the database
                    input.EXCEL_SHEET = fileUpload.FileName; // Save the file name as the Excel sheet name
                }

                // Validate required fields
                if (string.IsNullOrEmpty(input.REF_CD) || string.IsNullOrEmpty(input.DESCRIPTION) || string.IsNullOrEmpty(input.CREATED_BY))
                {
                    TempData["ErrorMessage"] = "One or more required fields are missing or invalid.";
                    return View("ExcelAnalysis", input);
                }

                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();

                    // Insert the record
                    string insertQuery = @"
                        INSERT INTO ORG_FIN_STATEMENT_TYPE 
                        (REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, EXCEL_SHEET) 
                        VALUES (:REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY, :EXCEL_SHEET)";

                    using (var insertCommand = new OracleCommand(insertQuery, connection))
                    {
                        AddParameter(insertCommand, "REF_CD", OracleDbType.Varchar2, input.REF_CD);
                        AddParameter(insertCommand, "DESCRIPTION", OracleDbType.Varchar2, input.DESCRIPTION);
                        AddParameter(insertCommand, "SYS_CREATE_TS", OracleDbType.TimeStamp, input.SYS_CREATE_TS);
                        AddParameter(insertCommand, "CREATED_BY", OracleDbType.Varchar2, input.CREATED_BY);
                        AddParameter(insertCommand, "EXCEL_SHEET", OracleDbType.Varchar2, input.EXCEL_SHEET);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Data saved successfully.";
                return RedirectToAction("ExcelAnalysis");
            }
            catch (OracleException ex)
            {
                TempData["ErrorMessage"] = "Database error occurred while saving statement data. Please try again.";
                _logger.LogError(ex, "Database error occurred while saving statement data. Oracle Error Code: {ErrorCode}, Message: {Message}", ex.Number, ex.Message);
                return View("ExcelAnalysis", input);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while processing the file upload. Please try again.";
                _logger.LogError(ex, "An error occurred while processing the file upload.");
                return View("ExcelAnalysis", input);
            }
        }

        // Helper method to add Oracle parameters
        private void AddParameter(OracleCommand command, string parameterName, OracleDbType dbType, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.OracleDbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    public class SheetDetail
    {
        public string SheetName { get; set; }
        public List<CellValue> CellValues { get; set; }
    }

    public class CellValue
    {
        public string CellAddress { get; set; }
        public string Value { get; set; }
        public string Formula { get; set; }
    }
}