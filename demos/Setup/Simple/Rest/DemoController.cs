// ─────────────────────────────────────────────────────────────
//  Controller example — same protobuf formatters, zero extra config.
// ─────────────────────────────────────────────────────────────

using Microsoft.AspNetCore.Mvc;
using ProtobuffEncoder.Demo.Setup.Shared;

namespace ProtobuffEncoder.Demo.Setup.Simple.Rest;

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    [HttpPost("echo")]
    public ActionResult<DemoResponse> Echo([FromBody] DemoRequest request)
    {
        return Ok(new DemoResponse { Message = $"Controller Echo: {request.Name}" });
    }

    [HttpPost("order")]
    public ActionResult<OrderConfirmation> Order([FromBody] OrderRequest order)
    {
        return Ok(new OrderConfirmation
        {
            OrderId = Guid.NewGuid().ToString("N")[..8],
            Total = order.Quantity * order.UnitPrice
        });
    }
}
