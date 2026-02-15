using AsyncOrders.Application.Orders.Commands.CreateOrder;
using Microsoft.AspNetCore.Mvc;

namespace AsyncOrders.Api.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateOrderResponse>> Create(
        [FromServices] CreateOrderService service,
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var result = await service.ExecuteAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.OrderId }, result);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<object> GetById([FromRoute] Guid id)
    {
        // Placeholder.
        return Ok(new { id });
    }
}
