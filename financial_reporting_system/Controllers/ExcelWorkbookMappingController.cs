using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace financial_reporting_system.Controllers
{
    public class ExcelWorkbookMappingController : Controller
    {
        private readonly string _connectionString;

        public ExcelWorkbookMappingController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
        }

        public IActionResult Index(int stmntId = 0)
        {
            // Fetch workbooks for the dropdown
            var workbooks = GetWorkbooks();
            ViewBag.Workbooks = workbooks;

            // Fetch filtered financial statement details based on the STMNT_ID
            var financialStatementDetails = GetFinancialStatementDetails(stmntId);
            var accountDetails = GetAccountDetails(); // Fetch account details using the new query
            var statementTypes = GetExcelWorkbookStatementTypes();

            // Find the selected statement type excel sheets
            var selectedDescription = statementTypes.FirstOrDefault(st => st.STMNT_ID == stmntId)?.EXCEL_SHEET ?? "All statement types";

            ViewBag.AccountDetails = accountDetails;
            ViewBag.StatementTypes = statementTypes;
            ViewBag.SelectedDescription = selectedDescription; // Pass the selected description to the view

            return View(financialStatementDetails); // Pass the filtered data to the view
        }

        [HttpGet]
        public async Task<IActionResult> GetStatementTypesByWorkbook(string workbook)
        {
            var statementTypes = await GetStatementTypesForWorkbookAsync(workbook);
            return Json(statementTypes);
        }

        private async Task<List<ExcelWorkbookStatementType>> GetStatementTypesForWorkbookAsync(string workbook)
        {
            var statementTypes = new List<ExcelWorkbookStatementType>();

            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(
                    "SELECT STMNT_ID, EXCEL_SHEET FROM EXCEL_WORKBOOK_STATEMENT_TYPE WHERE EXCEL_WORKBOOK = :EXCEL_WORKBOOK", connection))
                {
                    command.Parameters.Add(new OracleParameter("EXCEL_WORKBOOK", workbook));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            statementTypes.Add(new ExcelWorkbookStatementType
                            {
                                STMNT_ID = reader.GetInt32(0),
                                EXCEL_SHEET = reader.GetString(1)
                            });
                        }
                    }
                }
            }

            return statementTypes;
        }

        // Method for fetching workbooks
        private List<SelectListItem> GetWorkbooks()
        {
            var workbooks = new List<SelectListItem>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand("SELECT DISTINCT EXCEL_WORKBOOK FROM EXCEL_WORKBOOK_STATEMENT_TYPE", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            workbooks.Add(new SelectListItem
                            {
                                Text = reader["EXCEL_WORKBOOK"].ToString(),
                                Value = reader["EXCEL_WORKBOOK"].ToString()
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

        [HttpGet]
        public IActionResult GetFilteredFinancialStatements(int stmntId)
        {
            var financialStatementDetails = GetFinancialStatementDetails(stmntId); // Filter by STMNT_ID
            return Json(financialStatementDetails); // Return filtered data as JSON
        }

        [HttpPost]
        public IActionResult SaveCombinedRows([FromBody] List<CombinedRow> combinedRows)
        {
            try
            {
                InsertCombinedRows(combinedRows);
                return Json(new { message = "Combined rows saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
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
                    worksheet.Range["A1"].Text = "GL Account Category Code";
                    worksheet.Range["B1"].Text = "Reference Code";
                    worksheet.Range["C1"].Text = "Description";
                    worksheet.Range["D1"].Text = "System Create Timestamp";
                    worksheet.Range["E1"].Text = "Created By";
                 
                    worksheet.Range["F1"].Text = "Ledger No";
                    worksheet.Range["G1"].Text = "Account Description";
                    

                    // Set data
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        worksheet.Range["A" + (i + 2)].Text = mappings[i].GL_ACCT_CAT_CD;
                        worksheet.Range["B" + (i + 2)].Text = mappings[i].REF_CD;
                        worksheet.Range["C" + (i + 2)].Text = mappings[i].DESCRIPTION;
                        worksheet.Range["D" + (i + 2)].Text = mappings[i].SYS_CREATE_TS.ToString("yyyy-MM-dd");
                        worksheet.Range["E" + (i + 2)].Text = mappings[i].CREATED_BY;
                   
                        worksheet.Range["F" + (i + 2)].Text = mappings[i].LEDGER_NO;
                        worksheet.Range["G" + (i + 2)].Text = mappings[i].ACCT_DESC;
                    
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
                // Log the exception
                Console.WriteLine(ex.Message);
                return Json(new { error = "An error occurred while exporting to Excel." });
            }
        }

        // method to feth all financial statenment details based on selected stmnt_id

        private List<FinancialStatementDetail> GetFinancialStatementDetails(int stmntId)
        {
            var financialStatementDetails = new List<FinancialStatementDetail>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY 
                        FROM EXCEL_WORKBOOK_STMNT_DETAIL 
                        WHERE :stmntId = 0 OR STMNT_ID = :stmntId";
                    command.Parameters.Add(new OracleParameter("stmntId", stmntId));

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            financialStatementDetails.Add(new FinancialStatementDetail
                            {
                                DETAIL_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                SHEET_ID = reader.GetInt32(2),
                                HEADER_ID = reader.GetInt32(3),
                                GL_ACCT_CAT_CD = reader.IsDBNull(4) ? null : reader.GetString(4),
                                REF_CD = reader.IsDBNull(5) ? null : reader.GetString(5),
                                DESCRIPTION = reader.IsDBNull(6) ? null : reader.GetString(6),
                                SYS_CREATE_TS = reader.GetDateTime(7),
                                CREATED_BY = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }
                    }
                }
            }

            return financialStatementDetails;
        }

        // Updated method to fetch account details using the new query
        private List<AccountDetail> GetAccountDetails()
        {
            var accountDetails = new List<AccountDetail>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT DISTINCT LEDGER_NO, ACCT_DESC 
                        FROM V_ORG_CHART_OF_ACCOUNT_DETAILS_WITHVALUE_DATE";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            accountDetails.Add(new AccountDetail
                            {
                                LEDGER_NO = reader.IsDBNull(0) ? null : reader.GetString(0),
                                ACCT_DESC = reader.IsDBNull(1) ? null : reader.GetString(1)
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
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    // Insert into ORG_MAPPED_DESCRIPTION
                    command.CommandText = @"
                        INSERT INTO ORG_MAPPED_DESCRIPTION 
                        (DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, LEDGER_NO, ACCT_DESC) 
                        VALUES (:DETAIL_ID, :STMNT_ID, :SHEET_ID, :HEADER_ID, :GL_ACCT_CAT_CD, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY, :LEDGER_NO, :ACCT_DESC)";

                    foreach (var row in combinedRows)
                    {
                        // Clear parameters for each iteration
                        command.Parameters.Clear();

                        // Add parameters for the INSERT statement
                        command.Parameters.Add(new OracleParameter("DETAIL_ID", row.DETAIL_ID));
                        command.Parameters.Add(new OracleParameter("STMNT_ID", row.STMNT_ID));
                        command.Parameters.Add(new OracleParameter("SHEET_ID", row.SHEET_ID));
                        command.Parameters.Add(new OracleParameter("HEADER_ID", row.HEADER_ID));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", row.GL_ACCT_CAT_CD ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("REF_CD", row.REF_CD ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", row.DESCRIPTION ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("SYS_CREATE_TS", row.SYS_CREATE_TS));
                        command.Parameters.Add(new OracleParameter("CREATED_BY", row.CREATED_BY ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("LEDGER_NO", row.LEDGER_NO ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("ACCT_DESC", row.ACCT_DESC ?? (object)DBNull.Value));

                        // Execute the INSERT statement
                        command.ExecuteNonQuery();

                        // Call the CALL_TRIGGER_LOGIC procedure if LEDGER_NO is not null or empty
                        if (!string.IsNullOrEmpty(row.LEDGER_NO))
                        {
                            // Reuse the same command object for the procedure call
                            command.CommandText = "CALL CALL_TRIGGER_LOGIC(:LEDGER_NO)";
                            command.Parameters.Clear(); // Clear previous parameters
                            command.Parameters.Add(new OracleParameter("LEDGER_NO", row.LEDGER_NO));
                            command.ExecuteNonQuery();
                        }
                    }
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
                    command.CommandText = "SELECT MAPPED_DESC_ID, DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, LEDGER_NO, ACCT_DESC FROM ORG_MAPPED_DESCRIPTION";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            mappings.Add(new Mapping
                            {
                                MAPPED_DESC_ID = reader.GetInt32(0),
                                DETAIL_ID = reader.GetInt32(1),
                                STMNT_ID = reader.GetInt32(2),
                                SHEET_ID = reader.GetInt32(3),
                                HEADER_ID = reader.GetInt32(4),
                                GL_ACCT_CAT_CD = reader.IsDBNull(5) ? null : reader.GetString(5),
                                REF_CD = reader.IsDBNull(6) ? null : reader.GetString(6),
                                DESCRIPTION = reader.IsDBNull(7) ? null : reader.GetString(7),
                                SYS_CREATE_TS = reader.GetDateTime(8),
                                CREATED_BY = reader.IsDBNull(9) ? null : reader.GetString(9),
                               
                                LEDGER_NO = reader.IsDBNull(10) ? null : reader.GetString(10),
                                ACCT_DESC = reader.IsDBNull(11) ? null : reader.GetString(11),
                               
                            });
                        }
                    }
                }
            }

            return mappings;
        }

        private List<ExcelWorkbookStatementType> GetExcelWorkbookStatementTypes()
        {
            var statementTypes = new List<ExcelWorkbookStatementType>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT STMNT_ID, EXCEL_SHEET FROM EXCEL_WORKBOOK_STATEMENT_TYPE";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            statementTypes.Add(new ExcelWorkbookStatementType
                            {
                                STMNT_ID = reader.GetInt32(0),
                                EXCEL_SHEET = reader.GetString(1)
                            });
                        }
                    }
                }
            }

            return statementTypes;
        }

        // Model Classes
        public class FinancialStatementDetail
        {
            public int DETAIL_ID { get; set; }
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public int HEADER_ID { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
        }

        public class AccountDetail
        {
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
        }

        public class CombinedRow
        {
            public int DETAIL_ID { get; set; }
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public int HEADER_ID { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
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
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public int HEADER_ID { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
            
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
           
        }

        public class ExcelWorkbookStatementType
        {
            public int STMNT_ID { get; set; }
            public string EXCEL_SHEET { get; set; }
        }
    }
}