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
            var filter = new LogFilterViewModel();

            try
            {
                filter.AvailableDates = _logger.GetAvailableDates();
            }
            catch (Exception ex)
            {
                filter.ErrorMessage = $"Unable to load log dates: {ex.Message}";
            }

            ViewData["Title"] = "Application Logs";
            return View(filter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(LogFilterViewModel filter)
        {
            filter.HasSubmitted = true;

            try
            {
                filter.Logs = _logger
                    .GetFilteredLogs(filter.Date, filter.Level, filter.Search)
                    .ToList();
                filter.AvailableDates = _logger.GetAvailableDates();
            }
            catch (Exception ex)
            {
                filter.ErrorMessage = $"Unable to load logs: {ex.Message}";
            }

            ViewData["Title"] = "Application Logs";
            return View(filter);
        }
    }
}
