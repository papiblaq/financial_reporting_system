using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;

namespace update_table_based_on_dropdown.Controllers
{
    public class TestingController : Controller
    {
        private readonly string _connectionString;

        public TestingController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
        }

        public IActionResult Index()
        {
            var statementTypes = GetOrgFinStatementTypes();
            ViewBag.StatementTypes = statementTypes;
            return View();
        }

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

        public IActionResult GetData(int stmntId)
        {
            var data = new List<DataItem>();

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT DETAIL_ID, STMNT_ID, SHEET_ID, HEADER_ID, GL_ACCT_CAT_CD, REF_CD, DESCRIPTION, SYS_CREATE_TS, CREATED_BY 
                        FROM ORG_FINANCIAL_STMNT_DETAIL 
                        WHERE STMNT_ID = :stmntId";
                    command.Parameters.Add(new OracleParameter("stmntId", stmntId));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            data.Add(new DataItem
                            {
                                DetailId = reader.GetInt32(0),
                                StmntId = reader.GetInt32(1),
                                SheetId = reader.GetInt32(2),
                                HeaderId = reader.GetInt32(3),
                                GlAcctCatCd = reader.IsDBNull(4) ? null : reader.GetString(4),
                                RefCd = reader.IsDBNull(5) ? null : reader.GetString(5),
                                Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                                SysCreateTs = reader.GetDateTime(7),
                                CreatedBy = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }
                    }
                }
            }

            return Json(data);
        }
    }

    public class OrgFinStatementType
    {
        public int STMNT_ID { get; set; }
        public string DESCRIPTION { get; set; }
    }

    public class DataItem
    {
        public int DetailId { get; set; }
        public int StmntId { get; set; }
        public int SheetId { get; set; }
        public int HeaderId { get; set; }
        public string GlAcctCatCd { get; set; }
        public string RefCd { get; set; }
        public string Description { get; set; }
        public DateTime SysCreateTs { get; set; }
        public string CreatedBy { get; set; }
    }
}