using Ticketing.Worker.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ticketing.Worker.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class MetricsController : ControllerBase
    {
        private readonly TicketService _ticketService;

        public MetricsController(TicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpGet]
        public ActionResult<string> Get()
        {
            return _ticketService.Metrics();
        }
    }
}