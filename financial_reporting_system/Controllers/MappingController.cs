using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.IO;

namespace syncfusion_grid.Controllers
{
    public class MappingController : Controller
    {
        private readonly string _connectionString;

        public MappingController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
        }

        public IActionResult Index()
        {
            var financialStatementDetails = GetFinancialStatementDetails(0); // Fetch all financial statement details initially
            var accountDetails = GetAccountDetails();
            var statementTypes = GetOrgFinStatementTypes(); // Fetch financial statement types
            ViewBag.AccountDetails = accountDetails;
            ViewBag.StatementTypes = statementTypes; // Pass financial statement types to the view
            return View(financialStatementDetails);
        }

        // New action to fetch filtered financial statements based on STMNT_ID
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

        // Export mappings to Excel
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
                    worksheet.Range["F1"].Text = "GL Account ID";
                    worksheet.Range["G1"].Text = "GL Account No";
                    worksheet.Range["H1"].Text = "Ledger No";
                    worksheet.Range["I1"].Text = "Account Description";
                    worksheet.Range["J1"].Text = "Balance Code";

                    // Set data
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        worksheet.Range["A" + (i + 2)].Text = mappings[i].GL_ACCT_CAT_CD;
                        worksheet.Range["B" + (i + 2)].Text = mappings[i].REF_CD;
                        worksheet.Range["C" + (i + 2)].Text = mappings[i].DESCRIPTION;
                        worksheet.Range["D" + (i + 2)].Text = mappings[i].SYS_CREATE_TS.ToString("yyyy-MM-dd");
                        worksheet.Range["E" + (i + 2)].Text = mappings[i].CREATED_BY;
                        worksheet.Range["F" + (i + 2)].Text = mappings[i].GL_ACCT_ID.ToString();
                        worksheet.Range["G" + (i + 2)].Text = mappings[i].GL_ACCT_NO;
                        worksheet.Range["H" + (i + 2)].Text = mappings[i].LEDGER_NO;
                        worksheet.Range["I" + (i + 2)].Text = mappings[i].ACCT_DESC;
                        worksheet.Range["J" + (i + 2)].Text = mappings[i].BAL_CD;
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

        // Method to fetch all financial statement details or filter by STMNT_ID
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
                        FROM ORG_FINANCIAL_STMNT_DETAIL 
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

        private List<AccountDetail> GetAccountDetails()
        {
            var accountDetails = new List<AccountDetail>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT GL_ACCT_CAT_CD, GL_ACCT_ID, GL_ACCT_NO, LEDGER_NO, ACCT_DESC, BAL_CD FROM V_ORG_CHART_OF_ACCOUNT_DETAILS";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            accountDetails.Add(new AccountDetail
                            {
                                GL_ACCT_CAT_CD = reader.IsDBNull(0) ? null : reader.GetString(0),
                                GL_ACCT_ID = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                GL_ACCT_NO = reader.IsDBNull(2) ? null : reader.GetString(2),
                                LEDGER_NO = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ACCT_DESC = reader.IsDBNull(4) ? null : reader.GetString(4),
                                BAL_CD = reader.IsDBNull(5) ? null : reader.GetString(5)
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
                    command.CommandText = @"
                        INSERT INTO ORG_FINANCIAL_MAPPING 
                        (DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, GL_ACCT_ID, GL_ACCT_NO, LEDGER_NO, ACCT_DESC, BAL_CD) 
                        VALUES (:DETAIL_ID, :STMNT_ID, :SHEET_ID, :HEADER_ID, :GL_ACCT_CAT_CD, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY, :GL_ACCT_ID, :GL_ACCT_NO, :LEDGER_NO, :ACCT_DESC, :BAL_CD)";

                    foreach (var row in combinedRows)
                    {
                        command.Parameters.Clear();
                        command.Parameters.Add(new OracleParameter("DETAIL_ID", row.DETAIL_ID));
                        command.Parameters.Add(new OracleParameter("STMNT_ID", row.STMNT_ID));
                        command.Parameters.Add(new OracleParameter("SHEET_ID", row.SHEET_ID));
                        command.Parameters.Add(new OracleParameter("HEADER_ID", row.HEADER_ID));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", row.GL_ACCT_CAT_CD ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("REF_CD", row.REF_CD ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("DESCRIPTION", row.DESCRIPTION ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("SYS_CREATE_TS", row.SYS_CREATE_TS));
                        command.Parameters.Add(new OracleParameter("CREATED_BY", row.CREATED_BY ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_ID", row.GL_ACCT_ID));
                        command.Parameters.Add(new OracleParameter("GL_ACCT_NO", row.GL_ACCT_NO ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("LEDGER_NO", row.LEDGER_NO ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("ACCT_DESC", row.ACCT_DESC ?? (object)DBNull.Value));
                        command.Parameters.Add(new OracleParameter("BAL_CD", row.BAL_CD ?? (object)DBNull.Value));
                        command.ExecuteNonQuery();
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
                    command.CommandText = "DELETE FROM ORG_FINANCIAL_MAPPING WHERE MAPPING_ID IN (" + string.Join(",", mappingIds) + ")";
                    command.ExecuteNonQuery();
                }
            }
        }

        // Fetches all mappings for the Grid view
        public IActionResult Grid()
        {
            var mappings = GetMappings();
            return View(mappings);
        }

        private List<Mapping> GetMappings()
        {
            var mappings = new List<Mapping>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT MAPPING_ID, DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, GL_ACCT_ID, GL_ACCT_NO, LEDGER_NO, ACCT_DESC, BAL_CD FROM ORG_FINANCIAL_MAPPING";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            mappings.Add(new Mapping
                            {
                                MAPPING_ID = reader.GetInt32(0),
                                DETAIL_ID = reader.GetInt32(1),
                                STMNT_ID = reader.GetInt32(2),
                                SHEET_ID = reader.GetInt32(3),
                                HEADER_ID = reader.GetInt32(4),
                                GL_ACCT_CAT_CD = reader.IsDBNull(5) ? null : reader.GetString(5),
                                REF_CD = reader.IsDBNull(6) ? null : reader.GetString(6),
                                DESCRIPTION = reader.IsDBNull(7) ? null : reader.GetString(7),
                                SYS_CREATE_TS = reader.GetDateTime(8),
                                CREATED_BY = reader.IsDBNull(9) ? null : reader.GetString(9),
                                GL_ACCT_ID = reader.GetInt32(10),
                                GL_ACCT_NO = reader.IsDBNull(11) ? null : reader.GetString(11),
                                LEDGER_NO = reader.IsDBNull(12) ? null : reader.GetString(12),
                                ACCT_DESC = reader.IsDBNull(13) ? null : reader.GetString(13),
                                BAL_CD = reader.IsDBNull(14) ? null : reader.GetString(14)
                            });
                        }
                    }
                }
            }

            return mappings;
        }

        // Fetch financial statement types (used for dropdown)
        private List<OrgFinStatementType> GetOrgFinStatementTypes()
        {
            var statementTypes = new List<OrgFinStatementType>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT STMNT_ID, DESCRIPTION FROM ORG_FIN_STATEMENT_TYPE";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            statementTypes.Add(new OrgFinStatementType
                            {
                                STMNT_ID = reader.GetInt32(0),
                                DESCRIPTION = reader.GetString(1)
                            });
                        }
                    }
                }
            }

            return statementTypes;
        }

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
            public string GL_ACCT_CAT_CD { get; set; }
            public int GL_ACCT_ID { get; set; }
            public string GL_ACCT_NO { get; set; }
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
            public string BAL_CD { get; set; }
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
            public int GL_ACCT_ID { get; set; }
            public string GL_ACCT_NO { get; set; }
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
            public string BAL_CD { get; set; }
        }

        public class Mapping
        {
            public int MAPPING_ID { get; set; }
            public int DETAIL_ID { get; set; }
            public int STMNT_ID { get; set; }
            public int SHEET_ID { get; set; }
            public int HEADER_ID { get; set; }
            public string GL_ACCT_CAT_CD { get; set; }
            public string REF_CD { get; set; }
            public string DESCRIPTION { get; set; }
            public DateTime SYS_CREATE_TS { get; set; }
            public string CREATED_BY { get; set; }
            public int GL_ACCT_ID { get; set; }
            public string GL_ACCT_NO { get; set; }
            public string LEDGER_NO { get; set; }
            public string ACCT_DESC { get; set; }
            public string BAL_CD { get; set; }
        }

        public class OrgFinStatementType
        {
            public int STMNT_ID { get; set; }
            public string DESCRIPTION { get; set; }
        }
    }
}