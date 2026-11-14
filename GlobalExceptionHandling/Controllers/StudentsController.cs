using LoggerService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GlobalExceptionHandling.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly ILoggerManager _logger;

        public StudentsController(ILoggerManager logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            _logger.LogInfo("Fetching all the Students from the storage");

            var students = DataManager.GetAllStudents();

            throw new Exception("Exception while fetching all the students from the database");
        }
    }
}
