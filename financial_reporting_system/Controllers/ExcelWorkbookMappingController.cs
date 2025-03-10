using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.EJ2.Spreadsheet;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace financial_reporting_system.Controllers
{
    public class ExcelWorkbookMappingController : Controller
    {
        private readonly string _connectionString;

        public ExcelWorkbookMappingController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
        }

        public IActionResult Index(string stmntId = "0", int detailId = 0)
        {
            // Fetch workbooks for the dropdown
            var workbooks = GetWorkbooks();
            ViewBag.Workbooks = workbooks;

            // Fetch filtered financial statement details based on the STMNT_ID
            var financialStatementDetails = GetFinancialStatementDetails(stmntId);

            // Fetch account details using the new query
            var accountDetails = GetAccountDetails(detailId);

            // Fetch all statement types (only STMNT_ID)
            var statementTypes = GetExcelWorkbookStatementTypes(); // Updated method call

            // Find the selected statement type excel sheets
            var selectedDescription = statementTypes.FirstOrDefault(st => st == stmntId) ?? "All statement types";

            // Pass data to the view
            ViewBag.AccountDetails = accountDetails;
            ViewBag.StatementTypes = statementTypes; // Pass the list of STMNT_IDs
            ViewBag.SelectedDescription = selectedDescription;

            return View(financialStatementDetails); // Pass the filtered data to the view
        }

        [HttpGet]
        public async Task<IActionResult> GetStatementTypesByWorkbook(string workbook)
        {
            var sheetNames = await GetSheetNamesForWorkbookAsync(workbook);
            return Json(sheetNames); // Return the list of sheet names as JSON
        }

        private async Task<List<string>> GetSheetNamesForWorkbookAsync(string workbook)
        {
            var sheetNames = new List<string>();
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    "SELECT DISTINCT SHEET_ID FROM EXCEL_WORKBOOK_STMNT_DETAIL WHERE STMNT_ID = :EXCEL_WORKBOOK", connection))
                {
                    command.Parameters.Add(new OracleParameter("EXCEL_WORKBOOK", workbook));
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sheetNames.Add(reader.IsDBNull(0) ? null : reader.GetString(0)); // Add SHEET_ID to the list
                        }
                    }
                }
            }
            return sheetNames;
        }

        private List<SelectListItem> GetWorkbooks()
        {
            var workbooks = new List<SelectListItem>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT DISTINCT STMNT_ID FROM EXCEL_WORKBOOK_STMNT_DETAIL", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            workbooks.Add(new SelectListItem
                            {
                                Text = reader["STMNT_ID"].ToString(),
                                Value = reader["STMNT_ID"].ToString()
                            });
                        }
                    }
                }
            }
            return workbooks;
        }

        public IActionResult Grid()
        {
            var mappings = GetMappings(); // Fetch data from the database
            return View(mappings); // Return the Grid view with data
        }

        [HttpPost]
        public IActionResult SaveCombinedRows([FromBody] List<CombinedRow> combinedRows)
        {
            try
            {
                InsertCombinedRows(combinedRows);
                return Json(new { success = true, message = "Data inserted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SaveCombinedRows Failed: {ex.Message}");
                return Json(new { error = "An error occurred while saving the data." });
            }
        }

        [HttpPost]
        public IActionResult DeleteMappings([FromBody] List<int> mappingIds)
        {
            try
            {
                DeleteMappingRows(mappingIds);
                return Json(new { message = "Selected rows deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        public IActionResult ExportToExcel()
        {
            try
            {
                var mappings = GetMappings();
                using (ExcelEngine excelEngine = new ExcelEngine())
                {
                    IApplication application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Excel2016;
                    IWorkbook workbook = application.Workbooks.Create(1);
                    IWorksheet worksheet = workbook.Worksheets[0];

                    // Set header
                    worksheet.Range["A1"].Text = "Reference Code";
                    worksheet.Range["B1"].Text = "Description";
                    worksheet.Range["C1"].Text = "System Create Timestamp";
                    worksheet.Range["D1"].Text = "Created By";
                    worksheet.Range["E1"].Text = "Ledger No";
                    worksheet.Range["F1"].Text = "Account Description";
                    worksheet.Range["G1"].Text = "Statement ID"; // Added column for STMNT_ID
                    worksheet.Range["H1"].Text = "Sheet ID";     // Added column for SHEET_ID

                    // Set data
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        worksheet.Range["A" + (i + 2)].Text = mappings[i].REF_CD;
                        worksheet.Range["B" + (i + 2)].Text = mappings[i].DESCRIPTION;
                        worksheet.Range["C" + (i + 2)].Text = mappings[i].SYS_CREATE_TS.ToString("yyyy-MM-dd");
                        worksheet.Range["D" + (i + 2)].Text = mappings[i].CREATED_BY;
                        worksheet.Range["E" + (i + 2)].Text = mappings[i].LEDGER_NO;
                        worksheet.Range["F" + (i + 2)].Text = mappings[i].ACCT_DESC;
                        worksheet.Range["G" + (i + 2)].Text = mappings[i].STMNT_ID; // Write STMNT_ID as string
                        worksheet.Range["H" + (i + 2)].Text = mappings[i].SHEET_ID; // Write SHEET_ID as string
                    }

                    // Save the workbook to a memory stream
                    MemoryStream stream = new MemoryStream();
                    workbook.SaveAs(stream);

                    // Return the file as a download
                    stream.Position = 0;
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mappings.xlsx");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Json(new { error = "An error occurred while exporting to Excel." });
            }
        }

        private List<FinancialStatementDetail> GetFinancialStatementDetails(string stmntId)
        {
            var financialStatementDetails = new List<FinancialStatementDetail>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                    SELECT D.DETAIL_ID, D.STMNT_ID, D.SHEET_ID, D.HEADER_ID, 
                           D.REF_CD, D.DESCRIPTION, D.SYS_CREATE_TS, D.CREATED_BY
                    FROM EXCEL_WORKBOOK_STMNT_DETAIL D";
                    if (!string.IsNullOrEmpty(stmntId) && stmntId != "0")
                    {
                        command.CommandText += " WHERE D.SHEET_ID = :stmntId";
                        command.Parameters.Add(new OracleParameter("stmntId", OracleDbType.Varchar2) { Value = stmntId });
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            financialStatementDetails.Add(new FinancialStatementDetail
                            {
                                DETAIL_ID = reader.GetInt32(0),
                                STMNT_ID = reader.IsDBNull(1) ? null : reader.GetString(1),
                                SHEET_ID = reader.IsDBNull(2) ? null : reader.GetString(2),
                                HEADER_ID = reader.GetInt32(3),
                                REF_CD = reader.IsDBNull(4) ? null : reader.GetString(4),
                                DESCRIPTION = reader.IsDBNull(5) ? null : reader.GetString(5),
                                SYS_CREATE_TS = reader.GetDateTime(6),
                                CREATED_BY = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }
            }
            return financialStatementDetails;
        }

        // Method to fetch ledgers that are unmapped
        private List<AccountDetail> GetAccountDetails(int detailId = 0)
        {
            var accountDetails = new List<AccountDetail>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                    SELECT DISTINCT v.LEDGER_NO, v.ACCT_DESC, v.GL_ACCT_NO
                    FROM V_ORG_CHART_OF_ACCOUNT_DETAILS_WITHVALUE_DATE v
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM ORG_MAPPED_DESCRIPTION_WITH_LEDGRRS l
                        WHERE l.DETAIL_ID = :selectedDetailId
                          AND l.LEDGER_NO = v.LEDGER_NO
                    )";

                    // Add the parameter for DETAIL_ID (selectedDetailId)
                    if (detailId > 0)  // Only pass DETAIL_ID if it's greater than 0
                    {
                        command.Parameters.Add(new OracleParameter("selectedDetailId", detailId));
                    }
                    else
                    {
                        command.Parameters.Add(new OracleParameter("selectedDetailId", DBNull.Value));
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            accountDetails.Add(new AccountDetail
                            {
                                LEDGER_NO = reader.IsDBNull(0) ? null : reader.GetString(0),
                                ACCT_DESC = reader.IsDBNull(1) ? null : reader.GetString(1),
                                GL_ACCT_NO = reader.IsDBNull(2) ? null : reader.GetString(2)
                            });
                        }
                    }
                }
            }
            return accountDetails;
        }

        private void InsertCombinedRows(List<CombinedRow> combinedRows)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                try
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        // SQL Insert Command with Default Values
                        command.CommandText = @"
                    INSERT INTO ORG_MAPPED_DESCRIPTION 
                    (DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, REF_CD, DESCRIPTION, 
                     SYS_CREATE_TS, CREATED_BY, REC_ST, VERSION_NUMBER, LEDGER_NO, ACCT_DESC) 
                    VALUES 
                    (:DETAIL_ID, :STMNT_ID, :SHEET_ID, :HEADER_ID, :REF_CD, :DESCRIPTION, 
                     :SYS_CREATE_TS, :CREATED_BY, 'A', 1, :LEDGER_NO, :ACCT_DESC)";

                        foreach (var row in combinedRows)
                        {
                            try
                            {
                                // Clear parameters for each row
                                command.Parameters.Clear();

                                // Add parameters for the INSERT command
                                command.Parameters.Add(new OracleParameter("DETAIL_ID", row.DETAIL_ID));
                                command.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Varchar2)
                                { Value = row.STMNT_ID ?? (object)DBNull.Value });
                                command.Parameters.Add(new OracleParameter("SHEET_ID", OracleDbType.Varchar2)
                                { Value = row.SHEET_ID ?? (object)DBNull.Value });
                                command.Parameters.Add(new OracleParameter("HEADER_ID", row.HEADER_ID));
                                command.Parameters.Add(new OracleParameter("REF_CD", OracleDbType.Varchar2)
                                { Value = row.REF_CD ?? (object)DBNull.Value });
                                command.Parameters.Add(new OracleParameter("DESCRIPTION", OracleDbType.Varchar2)
                                { Value = row.DESCRIPTION ?? (object)DBNull.Value });
                                command.Parameters.Add(new OracleParameter("SYS_CREATE_TS", OracleDbType.TimeStamp)
                                { Value = row.SYS_CREATE_TS });
                                command.Parameters.Add(new OracleParameter("CREATED_BY", OracleDbType.Varchar2)
                                { Value = row.CREATED_BY ?? (object)DBNull.Value });
                                command.Parameters.Add(new OracleParameter("LEDGER_NO", OracleDbType.Varchar2)
                                { Value = row.LEDGER_NO ?? (object)DBNull.Value });
                                command.Parameters.Add(new OracleParameter("ACCT_DESC", OracleDbType.Varchar2)
                                { Value = row.ACCT_DESC ?? (object)DBNull.Value });

                                // Execute the INSERT command
                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    Console.WriteLine($"[SUCCESS] Row inserted successfully: DETAIL_ID = {row.DETAIL_ID}");
                                }
                                else
                                {
                                    Console.WriteLine($"[WARNING] No rows affected for DETAIL_ID: {row.DETAIL_ID}");
                                }

                                // Execute trigger logic if LEDGER_NO is provided
                                if (!string.IsNullOrEmpty(row.LEDGER_NO))
                                {
                                    try
                                    {
                                        command.CommandText = "CALL CALL_TRIGGER_LOGIC(:LEDGER_NO)";
                                        command.Parameters.Clear();
                                        command.Parameters.Add(new OracleParameter("LEDGER_NO", row.LEDGER_NO));

                                        int triggerResult = command.ExecuteNonQuery();

                                        if (triggerResult >= 0)
                                        {
                                            Console.WriteLine($"[SUCCESS] Trigger executed successfully for LEDGER_NO: {row.LEDGER_NO}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[WARNING] Trigger did not execute as expected for LEDGER_NO: {row.LEDGER_NO}");
                                        }
                                    }
                                    catch (OracleException ex)
                                    {
                                        Console.WriteLine($"[ERROR] Trigger Execution Failed: {ex.Message}");
                                        Console.WriteLine($"Oracle Error Code: {ex.Number}");
                                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                                    }
                                }
                            }
                            catch (OracleException ex)
                            {
                                Console.WriteLine($"[ERROR] Oracle Insertion Failed for DETAIL_ID: {row.DETAIL_ID}");
                                Console.WriteLine($"Oracle Error Code: {ex.Number}");
                                Console.WriteLine($"Message: {ex.Message}");
                                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] General Exception for DETAIL_ID: {row.DETAIL_ID}");
                                Console.WriteLine($"Message: {ex.Message}");
                                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                            }
                        }
                    }
                }
                catch (OracleException ex)
                {
                    Console.WriteLine($"[ERROR] Database Connection Issue: {ex.Message}");
                    Console.WriteLine($"Oracle Error Code: {ex.Number}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] General Exception: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        }


        private void DeleteMappingRows(List<int> mappingIds)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ORG_MAPPED_DESCRIPTION WHERE MAPPED_DESC_ID IN (" + string.Join(",", mappingIds) + ")";
                    command.ExecuteNonQuery();
                }
            }
        }

        private List<Mapping> GetMappings()
        {
            var mappings = new List<Mapping>();
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                    SELECT MAPPED_DESC_ID, DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, 
                           REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, LEDGER_NO, ACCT_DESC 
                    FROM ORG_MAPPED_DESCRIPTION";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            mappings.Add(new Mapping
                            {
                                MAPPED_DESC_ID = reader.GetInt32(0),
                                DETAIL_ID = reader.GetInt32(1),
                                STMNT_ID = reader.IsDBNull(2) ? null : reader.GetString(2),
                                SHEET_ID = reader.IsDBNull(3) ? null : reader.GetString(3),
                                HEADER_ID = reader.GetInt32(4),
                                REF_CD = reader.IsDBNull(5) ? null : reader.GetString(5),
                                DESCRIPTION = reader.IsDBNull(6) ? null : reader.GetString(6),
                                SYS_CREATE_TS = reader.GetDateTime(7),
                                CREATED_BY = reader.IsDBNull(8) ? null : reader.GetString(8),
                                LEDGER_NO = reader.IsDBNull(9) ? null : reader.GetString(9),
                                ACCT_DESC = reader.IsDBNull(10) ? null : reader.GetString(10)
                            });
                        }
                    }
                }
            }
            return mappings;
        }

        private List<string> GetExcelWorkbookStatementTypes()
        {
            var statementTypes = new List<string>(); // Changed to a list of strings since we're fetching only STMNT_ID
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT DISTINCT SHEET_ID FROM EXCEL_WORKBOOK_STMNT_DETAIL"; // Updated SQL query
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            statementTypes.Add(reader.IsDBNull(0) ? null : reader.GetString(0)); // Add STMNT_ID to the list
                        }
                    }
                }
            }
            return statementTypes; // Return a list of STMNT_IDs
        }

        // Model Classes
        public class FinancialStatementDetail
        {
            public int DETAIL_ID { get; set; }
            public string STMNT_ID { get; set; } // Changed from int to string
            public string SHEET_ID { get; set; } // Changed from int to string
            public int HEADER_ID { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }

        public class AccountDetail
        {
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
            public string GL_ACCT_NO { get; set; }
        }

        public class CombinedRow
        {
            public int DETAIL_ID { get; set; }
            public string STMNT_ID { get; set; } // Changed from int to string
            public string SHEET_ID { get; set; } // Changed from int to string
            public int HEADER_ID { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
        }

        public class Mapping
        {
            public int MAPPED_DESC_ID { get; set; }
            public int DETAIL_ID { get; set; }
            public string STMNT_ID { get; set; } // Changed from int to string
            public string SHEET_ID { get; set; } // Changed from int to string
            public int HEADER_ID { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
        }

        public class ExcelWorkbookStatementType
        {
            public string STMNT_ID { get; set; } // Changed from int to string
            public string EXCEL_SHEET { get; set; }
        }
    }
}