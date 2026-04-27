using Logging;
using LetterTemplatePractice.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LetterTemplatePractice.Controllers
{
    [Authorize]
    public sealed class LogsController : Controller
    {
        private readonly IAppLogger _logger;

        public LogsController(IAppLogger logger) => _logger = logger;

        [HttpGet]
        public IActionResult Index()
        {
            var filter = new LogFilterViewModel
            {
                AvailableDates = _logger.GetAvailableDates()
            };

            ViewData["Title"] = "Application Logs";
            return View(filter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(LogFilterViewModel filter)
        {
            var logs = _logger
                .GetFilteredLogs(filter.Date, filter.Level, filter.Search)
                .ToList();

            filter.HasSubmitted = true;
            filter.Logs = logs;
            filter.AvailableDates = _logger.GetAvailableDates();

            ViewData["Title"] = "Application Logs";
            return View(filter);
        }
    }
}
