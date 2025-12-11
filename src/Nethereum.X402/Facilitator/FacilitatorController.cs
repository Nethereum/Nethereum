using Microsoft.AspNetCore.Mvc;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;

namespace Nethereum.X402.Facilitator;

[ApiController]
[Route("facilitator")]
public class FacilitatorController : ControllerBase
{
    private readonly IX402PaymentProcessor _processor;

    public FacilitatorController(IX402PaymentProcessor processor)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    [HttpPost("verify")]
    [ProducesResponseType(typeof(VerificationResponse), 200)]
    public async Task<ActionResult<VerificationResponse>> Verify(
        [FromBody] FacilitatorVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.PaymentPayload == null || request?.PaymentRequirements == null)
        {
            return BadRequest(new { error = "PaymentPayload and PaymentRequirements are required" });
        }

        var result = await _processor.VerifyPaymentAsync(
            request.PaymentPayload,
            request.PaymentRequirements,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("settle")]
    [ProducesResponseType(typeof(SettlementResponse), 200)]
    public async Task<ActionResult<SettlementResponse>> Settle(
        [FromBody] FacilitatorSettleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.PaymentPayload == null || request?.PaymentRequirements == null)
        {
            return BadRequest(new { error = "PaymentPayload and PaymentRequirements are required" });
        }

        var result = await _processor.SettlePaymentAsync(
            request.PaymentPayload,
            request.PaymentRequirements,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("supported")]
    [ProducesResponseType(typeof(SupportedPaymentKindsResponse), 200)]
    public async Task<ActionResult<SupportedPaymentKindsResponse>> GetSupported(
        CancellationToken cancellationToken = default)
    {
        var result = await _processor.GetSupportedAsync(cancellationToken);
        return Ok(result);
    }
}
