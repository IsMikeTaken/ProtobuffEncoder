using Microsoft.AspNetCore.Mvc;
using ProtobuffEncoder.Demo.Setup.Models;

namespace ProtobuffEncoder.Demo.Setup.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimpleController : ControllerBase
{
    [HttpPost("echo")]
    public ActionResult<DemoResponse> Echo([FromBody] DemoRequest request)
    {
        return Ok(new DemoResponse { Message = $"Controller Echo: {request.Name}" });
    }
}
