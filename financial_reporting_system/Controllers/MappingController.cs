using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

public class MappingController : Controller
{
    private readonly string _connectionString;

    public MappingController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleConnection");
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.StatementIds = GetStatementIds();
        ViewBag.AccountCategories = GetAccountCategories();
        return View();
    }

    [HttpPost]
    public IActionResult Index(int portfolioCodeInput, string accountCategoryInput)
    {
        if (portfolioCodeInput == 0 || string.IsNullOrEmpty(accountCategoryInput))
        {
            ViewBag.ErrorMessage = "Please select valid values from both dropdowns.";
            return View();
        }

        var portfolios = FetchPortfolios(accountCategoryInput);
        var orgFinancialMappings = FetchOrgFinancialMappings(portfolioCodeInput);

        ViewBag.Portfolios = portfolios;
        ViewBag.OrgFinancialMappings = orgFinancialMappings;

        return View();
    }

    [HttpPost]
    public JsonResult CombineGridData(List<int> selectedRowsGridA, int selectedRowGridB)
    {
        if (selectedRowsGridA == null || selectedRowsGridA.Count == 0 || selectedRowGridB == 0)
        {
            return Json(new { success = false, error = "Invalid selection. Please select rows from both grids." });
        }

        try
        {
            var gridAData = FetchDataForGridA(selectedRowsGridA);
            var gridBData = FetchDataForGridB(selectedRowGridB);

            foreach (var rowA in gridAData)
            {
                var combinedRow = new
                {
                    STMNT_ID = gridBData["STMNT_ID"],
                    SHEET_ID = gridBData["SHEET_ID"],
                    HEADER_ID = gridBData["HEADER_ID"],
                    DETAIL_ID = gridBData["DETAIL_ID"],
                    REF_CD = gridBData["REF_CD"],
                    DESCRIPTION = gridBData["DESCRIPTION"],
                    BAL_CD = rowA["BAL_CD"],
                    GL_ACCT_ID = rowA["GL_ACCT_ID"],
                    GL_ACCT_NO = rowA["GL_ACCT_NO"],
                    GL_ACCT_CAT_CD = rowA["GL_ACCT_CAT_CD"],
                    ACCT_DESC = rowA["ACCT_DESC"],
                    SYS_CREATE_TS = DateTime.UtcNow,
                    CREATED_BY = "User"
                };

                InsertCombinedDataIntoDatabase(combinedRow);
            }

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    private List<Dictionary<string, object>> FetchPortfolios(string accountCategoryInput)
    {
        var portfolios = new List<Dictionary<string, object>>();
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            string query = "SELECT * FROM V_ORG_CHART_OF_ACCOUNT_DETAILS WHERE GL_ACCT_CAT_CD = :accountCategoryInput";
            using (var command = new OracleCommand(query, connection))
            {
                command.Parameters.Add(new OracleParameter("accountCategoryInput", OracleDbType.Varchar2) { Value = accountCategoryInput });
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var portfolio = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            portfolio[reader.GetName(i)] = reader[i];
                        }
                        portfolios.Add(portfolio);
                    }
                }
            }
        }
        return portfolios;
    }

    private List<Dictionary<string, object>> FetchOrgFinancialMappings(int portfolioCodeInput)
    {
        var orgFinancialMappings = new List<Dictionary<string, object>>();
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            string query = "SELECT * FROM ORG_FINANCIAL_STMNT_DETAIL WHERE STMNT_ID = :portfolioCodeInput";
            using (var command = new OracleCommand(query, connection))
            {
                command.Parameters.Add(new OracleParameter("portfolioCodeInput", OracleDbType.Decimal) { Value = portfolioCodeInput });
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var mapping = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            mapping[reader.GetName(i)] = reader[i];
                        }
                        orgFinancialMappings.Add(mapping);
                    }
                }
            }
        }
        return orgFinancialMappings;
    }

    private List<Dictionary<string, object>> FetchDataForGridA(List<int> selectedRowsGridA)
    {
        // Implementation to fetch data from V_ORG_CHART_OF_ACCOUNT_DETAILS based on selected rows
        return new List<Dictionary<string, object>>();
    }

    private Dictionary<string, object> FetchDataForGridB(int selectedRowGridB)
    {
        // Implementation to fetch data from ORG_FINANCIAL_STMNT_DETAIL based on selected row
        return new Dictionary<string, object>();
    }

    private void InsertCombinedDataIntoDatabase(dynamic combinedRow)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            string query = @"
                INSERT INTO ORG_FINANCIAL_MAPPING
                (STMNT_ID, SHEET_ID, HEADER_ID, DETAIL_ID, REF_CD, DESCRIPTION, BAL_CD, GL_ACCT_ID, GL_ACCT_NO, GL_ACCT_CAT_CD, ACCT_DESC, SYS_CREATE_TS, CREATED_BY)
                VALUES (:STMNT_ID, :SHEET_ID, :HEADER_ID, :DETAIL_ID, :REF_CD, :DESCRIPTION, :BAL_CD, :GL_ACCT_ID, :GL_ACCT_NO, :GL_ACCT_CAT_CD, :ACCT_DESC, :SYS_CREATE_TS, :CREATED_BY)";
            using (var command = new OracleCommand(query, connection))
            {
                command.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Decimal) { Value = combinedRow.STMNT_ID });
                command.Parameters.Add(new OracleParameter("SHEET_ID", OracleDbType.Varchar2) { Value = combinedRow.SHEET_ID });
                command.Parameters.Add(new OracleParameter("HEADER_ID", OracleDbType.Decimal) { Value = combinedRow.HEADER_ID });
                command.Parameters.Add(new OracleParameter("DETAIL_ID", OracleDbType.Decimal) { Value = combinedRow.DETAIL_ID });
                command.Parameters.Add(new OracleParameter("REF_CD", OracleDbType.Varchar2) { Value = combinedRow.REF_CD });
                command.Parameters.Add(new OracleParameter("DESCRIPTION", OracleDbType.Varchar2) { Value = combinedRow.DESCRIPTION });
                command.Parameters.Add(new OracleParameter("BAL_CD", OracleDbType.Varchar2) { Value = combinedRow.BAL_CD });
                command.Parameters.Add(new OracleParameter("GL_ACCT_ID", OracleDbType.Decimal) { Value = combinedRow.GL_ACCT_ID });
                command.Parameters.Add(new OracleParameter("GL_ACCT_NO", OracleDbType.Varchar2) { Value = combinedRow.GL_ACCT_NO });
                command.Parameters.Add(new OracleParameter("GL_ACCT_CAT_CD", OracleDbType.Varchar2) { Value = combinedRow.GL_ACCT_CAT_CD });
                command.Parameters.Add(new OracleParameter("ACCT_DESC", OracleDbType.Varchar2) { Value = combinedRow.ACCT_DESC });
                command.Parameters.Add(new OracleParameter("SYS_CREATE_TS", OracleDbType.TimeStamp) { Value = combinedRow.SYS_CREATE_TS });
                command.Parameters.Add(new OracleParameter("CREATED_BY", OracleDbType.Varchar2) { Value = combinedRow.CREATED_BY });
                command.ExecuteNonQuery();
            }
        }
    }

    private List<int> GetStatementIds()
    {
        // Fetch list of Statement IDs from database

        List<int> statementIds = new List<int>();

        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            string query = "SELECT STMNT_ID FROM ORG_FIN_STATEMENT_TYPE";

            using (var command = new OracleCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        statementIds.Add(Convert.ToInt32(reader["STMNT_ID"]));
                    }
                }
            }
        }

        return statementIds;
    }

    private List<string> GetAccountCategories()
    {
        // Fetch list of Account Categories from database
       List<string> accountCategories = new List<string>();

        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            string query = "SELECT DISTINCT GL_ACCT_CAT_CD FROM V_ORG_CHART_OF_ACCOUNT_DETAILS";

            using (var command = new OracleCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        accountCategories.Add(reader["GL_ACCT_CAT_CD"].ToString());
                    }
                }
            }
        }

        return accountCategories;
    }
}