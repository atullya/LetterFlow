using Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LetterTemplateAPI.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/logs")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminLogsController : ControllerBase
    {
        private readonly IAppLogger _logger;
        public AdminLogsController(IAppLogger logger) => _logger = logger;

        [HttpGet]
        public IActionResult Get(DateTime? date, AppLogLevel? level, string? search, int page = 1, int pageSize = 50)
        {
            var paged = _logger.GetPagedLogs(date, level, search, page, pageSize);
            var dates = _logger.GetAvailableDates();
            return Ok(new { dates, logs = paged });
        }
    }
}
