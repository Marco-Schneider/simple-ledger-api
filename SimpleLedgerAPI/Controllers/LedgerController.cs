using Microsoft.AspNetCore.Mvc;
using SimpleLedgerAPI.Domain;
using SimpleLedgerAPI.Services;

namespace SimpleLedgerAPI.Controllers
{
    public class LedgerController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public LedgerController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpPost("/reset")]
        public IActionResult Reset()
        {
            _accountService.Reset();
            return Ok("OK");
        }

        [HttpGet("/balance")]
        public IActionResult GetBalnce([FromQuery(Name = "account_id")] string accountId)
        {
            var result = _accountService.GetBalance(accountId);

            if (!result.IsSuccess)
                return NotFound(0);

            return Ok(result.Data!.Balance);
        }

        [HttpPost("/event")]
        public IActionResult ProcessEvent([FromBody] EventRequest request)
        {
            switch (request.Type.ToLower())
            {
                case "deposit":
                    var depositResult = _accountService.Deposit(request.Destination, request.Amount);

                    return Created(string.Empty, new EventResponse(null, depositResult.Data));
                case "withdraw":
                    var withdrawResult = _accountService.Withdraw(request.Origin, request.Amount);

                    if (!withdrawResult.IsSuccess)
                        return NotFound(0);

                    return Created(string.Empty, new EventResponse(withdrawResult.Data, null));
                case "transfer":
                    var transferResult = _accountService.Transfer(request.Origin, request.Destination, request.Amount);

                    if(!transferResult.IsSuccess)
                        return NotFound(0);

                    return Created(string.Empty, new EventResponse(
                    transferResult.Data!.Origin,
                    transferResult.Data!.Destination));
                default:
                    return BadRequest("Invalid event type");
            }
        }
    }
}
