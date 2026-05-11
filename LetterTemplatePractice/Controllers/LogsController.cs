using Logging;
using LetterTemplatePractice.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetterTemplatePractice.Controllers
{
    [Authorize(Roles = "Admin")]
    public sealed class LogsController : Controller
    {
        private readonly IAppLogger _logger;

        public LogsController(IAppLogger logger) => _logger = logger;

        // GET /Logs?date=&level=&search=&page=1&pageSize=50
        [HttpGet]
        public IActionResult Index(
            DateTime?    date     = null,
            AppLogLevel? level    = null,
            string?      search   = null,
            int          page     = 1,
            int          pageSize = 15)
        {
            var vm = new LogFilterViewModel
            {
                Date     = date,
                Level    = level,
                Search   = search,
                Page     = page,
                PageSize = pageSize
            };

            try
            {
                vm.AvailableDates = _logger.GetAvailableDates();

                // Always load — no "submit" gate needed with GET
                vm.PagedLogs = _logger.GetPagedLogs(date, level, search, page, pageSize);
            }
            catch (Exception ex)
            {
                vm.ErrorMessage = $"Unable to load logs: {ex.Message}";
            }

            ViewData["Title"] = "Application Logs";
            return View(vm);
        }
    }
}
