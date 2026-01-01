using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace HRMBT.Web.Controllers
{
    public class DebugController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DebugController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var results = new List<string>();
            var connection = _context.Database.GetDbConnection();
            
            try 
            {
                await connection.OpenAsync();
                results.Add($"Connected to: {connection.Database}");

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        results.Add("Tables found:");
                        while (await reader.ReadAsync())
                        {
                            results.Add("- " + reader.GetString(0));
                        }
                    }
                }

                // Try to get counts for likely tables
                string[] tablesToCheck = { "Employee", "Employees", "Payroll", "Payrolls" };
                foreach (var table in tablesToCheck)
                {
                    try 
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
                            var count = await command.ExecuteScalarAsync();
                            results.Add($"Table [{table}] count: {count}");
                        }
                    }
                    catch { /* ignore missing tables */ }
                }
            }
            catch (System.Exception ex)
            {
                results.Add($"Error: {ex.Message}");
            }
            finally
            {
                await connection.CloseAsync();
            }

            return Ok(results);
        }
    }
}

