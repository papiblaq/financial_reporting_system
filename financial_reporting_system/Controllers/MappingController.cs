using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

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
            var financialStatementDetails = GetFinancialStatementDetails();
            var accountDetails = GetAccountDetails();
            ViewBag.AccountDetails = accountDetails;
            return View(financialStatementDetails);
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

        private List<FinancialStatementDetail> GetFinancialStatementDetails()
        {
            var financialStatementDetails = new List<FinancialStatementDetail>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY FROM ORG_FINANCIAL_STMNT_DETAIL";
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
                    command.CommandText = "INSERT INTO ORG_FINANCIAL_MAPPING (DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, GL_ACCT_ID, GL_ACCT_NO, LEDGER_NO, ACCT_DESC, BAL_CD) VALUES (:DETAIL_ID, :STMNT_ID, :SHEET_ID, :HEADER_ID, :GL_ACCT_CAT_CD, :REF_CD, :DESCRIPTION, :SYS_CREATE_TS, :CREATED_BY, :GL_ACCT_ID, :GL_ACCT_NO, :LEDGER_NO, :ACCT_DESC, :BAL_CD)";
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

        // New action method to fetch data from ORG_FINANCIAL_MAPPING and display it in a grid
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
                    command.CommandText = "SELECT DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY, GL_ACCT_ID, GL_ACCT_NO, LEDGER_NO, ACCT_DESC, BAL_CD FROM ORG_FINANCIAL_MAPPING";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            mappings.Add(new Mapping
                            {
                                DETAIL_ID = reader.GetInt32(0),
                                STMNT_ID = reader.GetInt32(1),
                                SHEET_ID = reader.GetInt32(2),
                                HEADER_ID = reader.GetInt32(3),
                                GL_ACCT_CAT_CD = reader.IsDBNull(4) ? null : reader.GetString(4),
                                REF_CD = reader.IsDBNull(5) ? null : reader.GetString(5),
                                DESCRIPTION = reader.IsDBNull(6) ? null : reader.GetString(6),
                                SYS_CREATE_TS = reader.GetDateTime(7),
                                CREATED_BY = reader.IsDBNull(8) ? null : reader.GetString(8),
                                GL_ACCT_ID = reader.GetInt32(9),
                                GL_ACCT_NO = reader.IsDBNull(10) ? null : reader.GetString(10),
                                LEDGER_NO = reader.IsDBNull(11) ? null : reader.GetString(11),
                                ACCT_DESC = reader.IsDBNull(12) ? null : reader.GetString(12),
                                BAL_CD = reader.IsDBNull(13) ? null : reader.GetString(13)
                            });
                        }
                    }
                }
            }

            return mappings;
        }

        public class Mapping
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
    }
}