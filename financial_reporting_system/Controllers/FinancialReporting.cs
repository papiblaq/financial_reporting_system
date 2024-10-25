using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace financial_reporting_system.Controllers
{
    public class FinancialReporting : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<FinancialReporting> _logger;
        private readonly IWebHostEnvironment _env;

        public FinancialReporting(IConfiguration configuration, ILogger<FinancialReporting> logger, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _logger = logger;
            _env = env;
        }

        // GET: /FinancialReporting
        public IActionResult Index()
        {
            var model = new StatementHeaderInputModel
            {
                StatementTypes = GetStatementTypes(),
                SheetIds = new List<SelectListItem>(), // Initialize empty list
                AvailableExcelSheets = GetAvailableExcelSheets()
            };

            return View(model);
        }

        // Fetch Sheet IDs based on selected Statement ID
        [HttpPost]
        public JsonResult GetSheetIdsByStatementId(int stmntId)
        {
            var sheetIds = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT SHEET_ID, REF_CD, DESCRIPTION FROM ORG_FINANCIAL_STMNT_SHEET WHERE STMNT_ID = :STMNT_ID";
                    using (var command = new OracleCommand(query, connection))
                    {
                        command.Parameters.Add(new OracleParameter("STMNT_ID", OracleDbType.Int32) { Value = stmntId });

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int sheetId = Convert.ToInt32(reader["SHEET_ID"]);
                                string ref_cd = reader["REF_CD"].ToString();
                                string refCd = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{ref_cd} ({refCd})";

                                sheetIds.Add(new SelectListItem
                                {
                                    Value = sheetId.ToString(),
                                    Text = formattedText
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching sheet IDs.");
            }

            return Json(sheetIds);
        }

        private List<SelectListItem> GetStatementTypes()
        {
            var statementTypes = new List<SelectListItem>();

            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT STMNT_ID, REF_CD, DESCRIPTION FROM ORG_FIN_STATEMENT_TYPE";
                    using (var command = new OracleCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int stmntId = Convert.ToInt32(reader["STMNT_ID"]); // Use STMNT_ID
                                string ref_cd = reader["REF_CD"].ToString();
                                string refCd = reader["DESCRIPTION"].ToString();
                                string formattedText = $"{ref_cd} ({refCd})";

                                statementTypes.Add(new SelectListItem
                                {
                                    Value = stmntId.ToString(), // Ensure Value is set to STMNT_ID
                                    Text = formattedText
                                });
                            }
                        }
                    }
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error occurred while fetching statement types.");
            }

            return statementTypes;
        }

        private List<SelectListItem> GetAvailableExcelSheets()
        {
            var excelSheets = new List<SelectListItem>();
            var templatesPath = Path.Combine(_env.WebRootPath, "Templates");

            if (Directory.Exists(templatesPath))
            {
                var files = Directory.GetFiles(templatesPath, "*.xls");
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    excelSheets.Add(new SelectListItem
                    {
                        Value = fileName,
                        Text = fileName
                    });
                }
            }

            return excelSheets;
        }
    }
}