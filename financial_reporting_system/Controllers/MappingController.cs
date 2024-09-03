using System;
using System.Collections.Generic;
using System.Data;
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

    // GET: Portfolios/Index
    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.StatementIds = GetStatementIds();
        ViewBag.AccountCategories = GetAccountCategories();
        return View();
    }

    private List<string> GetStatementIds()
    {
        List<string> statementIds = new List<string>();

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
                        statementIds.Add(reader["STMNT_ID"].ToString());
                    }
                }
            }
        }

        return statementIds;
    }

    private List<string> GetAccountCategories()
    {
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

    // POST: Portfolios/Index
    [HttpPost]
    public IActionResult Index(string portfolioCodeInput, string accountCategoryInput)
    {
        List<Dictionary<string, object>> portfolios = new List<Dictionary<string, object>>();
        List<Dictionary<string, object>> orgFinancialMappings = new List<Dictionary<string, object>>();

        try
        {
            if (string.IsNullOrEmpty(portfolioCodeInput) || string.IsNullOrEmpty(accountCategoryInput))
            {
                ViewBag.ErrorMessage = "Please select valid values from both dropdowns.";
                return View();
            }

            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();

                // Query for portfolios
                string portfolioQuery = "SELECT * FROM V_ORG_CHART_OF_ACCOUNT_DETAILS WHERE GL_ACCT_CAT_CD = :accountCategoryInput";
                using (var portfolioCommand = new OracleCommand(portfolioQuery, connection))
                {
                    portfolioCommand.Parameters.Add(new OracleParameter("accountCategoryInput", OracleDbType.Varchar2) { Value = accountCategoryInput });

                    using (var reader = portfolioCommand.ExecuteReader())
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

                // Query for orgFinancialMappings
                string mappingQuery = "SELECT * FROM ORG_FINANCIAL_STMNT_DETAIL WHERE STMNT_ID = :portfolioCodeInput";
                using (var mappingCommand = new OracleCommand(mappingQuery, connection))
                {
                    mappingCommand.Parameters.Add(new OracleParameter("portfolioCodeInput", OracleDbType.Varchar2) { Value = portfolioCodeInput });

                    using (var reader = mappingCommand.ExecuteReader())
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

            ViewBag.Portfolios = portfolios;
            ViewBag.OrgFinancialMappings = orgFinancialMappings;
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = ex.Message;
        }

        return View();
    }
}