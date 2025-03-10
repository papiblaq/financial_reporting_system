using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Syncfusion.XlsIO;
using System;
using System.Collections.Generic;
using System.Data;
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

        public IActionResult Index(int stmntId = 0, int detailId = 0)
        {
            // Fetch filtered financial statement details based on the selected statement type
            var financialStatementDetails = GetFinancialStatementDetails(stmntId);

            // Pass the detailId to GetAccountDetails if needed
            var accountDetails = GetAccountDetails(detailId);  // Fetch account details based on selected DETAIL_ID

            // Get available statement types
            var statementTypes = GetOrgFinStatementTypes();

            // Find the selected statement type description
            var selectedDescription = statementTypes
                .FirstOrDefault(st => st.STMNT_ID == stmntId)?.DESCRIPTION ?? "All statement types";

            // Pass data to the view using ViewBag
            ViewBag.AccountDetails = accountDetails;
            ViewBag.StatementTypes = statementTypes;
            ViewBag.SelectedDescription = selectedDescription;
            ViewBag.SelectedStmntId = stmntId;             // Pass the selected statement type ID
            ViewBag.SelectedDetailId = detailId;           // Pass the selected detail ID

            return View(financialStatementDetails);
        }




        // New action to fetch filtered financial statements based on STMNT_ID
        [HttpGet]
        public IActionResult GetFilteredFinancialStatements(int stmntId)
        {
            var financialStatementDetails = GetFinancialStatementDetails(stmntId); // Filter by STMNT_ID
            return Json(financialStatementDetails); // Return filtered data as JSON
        }



        // action for unmaped gl 



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




        // Fetch financial statement types (used for dropdown)
        private List<OrgFinStatementType> GetOrgFinStatementTypes()
        {
            var statementTypes = new List<OrgFinStatementType>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT STMNT_ID, DESCRIPTION FROM ORG_FIN_STATEMENT_TYPE WHERE EXCEL_SHEET IS NOT NULL";
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




        // Method to fetch all financial statement details or filter by STMNT_ID
        private List<FinancialStatementDetail> GetFinancialStatementDetails(int stmntId)
        {
            var financialStatementDetails = new List<FinancialStatementDetail>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    // Build the query based on the value of stmntId

                        command.CommandText = @"
                        SELECT D.DETAIL_ID, D.STMNT_ID, D.SHEET_ID, D.HEADER_ID, D.GL_ACCT_CAT_CD, 
                        D.REF_CD, D.DESCRIPTION, D.SYS_CREATE_TS, D.CREATED_BY
                        FROM ORG_FINANCIAL_STMNT_DETAIL D WHERE REC_ST = 'A'";


                    // Log the parameter value for debugging
                    Console.WriteLine($"Executing query with STMNT_ID: {stmntId}");

                    // If stmntId is not 0, add the parameter to the query
                    if (stmntId != 0)
                    {
                        var stmntIdParam = new OracleParameter("stmntId", OracleDbType.Int32)
                        {
                            Value = stmntId
                        };
                        command.Parameters.Add(stmntIdParam);
                    }

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
                Console.WriteLine("Financial statement details fetched successfully.");
            }

            return financialStatementDetails;
        }









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
                FROM SINGLE_SHEET_MAPPED_DESCRIPTION_WITH_LEDGRRS l
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
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;

                        // Insert into SINGLE_SHEET_MAPPED_DESCRIPTION
                        command.CommandText = @"
                    INSERT INTO SINGLE_SHEET_MAPPED_DESCRIPTION 
                    (DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, LEDGER_NO, ACCT_DESC) 
                    VALUES (:DETAIL_ID, :STMNT_ID, :SHEET_ID, :HEADER_ID, :GL_ACCT_CAT_CD, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY, :LEDGER_NO, :ACCT_DESC)";

                        foreach (var row in combinedRows)
                        {
                            command.Parameters.Clear();

                            // Add parameters for the INSERT statement
                            command.Parameters.Add(new OracleParameter("DETAIL_ID", OracleDbType.Int32, row.DETAIL_ID, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Int32, row.STMNT_ID, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("SHEET_ID", OracleDbType.Int32, row.SHEET_ID, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("HEADER_ID", OracleDbType.Int32, row.HEADER_ID, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", OracleDbType.Varchar2, row.GL_ACCT_CAT_CD ?? (object)DBNull.Value, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("REF_CD", OracleDbType.Varchar2, row.REF_CD ?? (object)DBNull.Value, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("DESCRIPTION", OracleDbType.Varchar2, row.DESCRIPTION ?? (object)DBNull.Value, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("SYS_CREATE_TS", OracleDbType.TimeStamp, row.SYS_CREATE_TS, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("CREATED_BY", OracleDbType.Varchar2, row.CREATED_BY ?? (object)DBNull.Value, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("LEDGER_NO", OracleDbType.Varchar2, row.LEDGER_NO ?? (object)DBNull.Value, ParameterDirection.Input));
                            command.Parameters.Add(new OracleParameter("ACCT_DESC", OracleDbType.Varchar2, row.ACCT_DESC ?? (object)DBNull.Value, ParameterDirection.Input));

                            command.ExecuteNonQuery();

                            // Call the stored procedure if LEDGER_NO is not null or empty
                            if (!string.IsNullOrEmpty(row.LEDGER_NO))
                            {
                                command.CommandText = "CALL CALL_SINGLE_SHEET_TRIGGER_LOGIC(:LEDGER_NO)";
                                command.Parameters.Clear();
                                command.Parameters.Add(new OracleParameter("LEDGER_NO", OracleDbType.Varchar2, row.LEDGER_NO, ParameterDirection.Input));
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (OracleException ex)
            {
                // Log Oracle-specific errors to the console
                Console.WriteLine($"Oracle Exception: {ex.Message}");
                Console.WriteLine($"Oracle Error Code: {ex.Number}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw; // Re-throw the exception or handle it as needed
            }
            catch (Exception ex)
            {
                // Log general exceptions to the console
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw; // Re-throw the exception or handle it as needed
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
                    command.CommandText = " SELECT SINGLE_SHEET_MAPPED_DESC_ID, DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, LEDGER_NO, ACCT_DESC FROM SINGLE_SHEET_MAPPED_DESCRIPTION";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            mappings.Add(new Mapping
                            {
                                SINGLE_SHEET_MAPPED_DESC_ID = reader.GetInt32(0),
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
            public string GL_ACCT_NO { get; set; }
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
            public int SINGLE_SHEET_MAPPED_DESC_ID { get; set; }
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

        public class OrgFinStatementType
        {
            public int STMNT_ID { get; set; }
            public string DESCRIPTION { get; set; }
        }
    }
}