using Microsoft.AspNetCore.Mvc;
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
            return Ok();
        }

        [HttpGet("/balance")]
        public IActionResult GetBalnce([FromQuery(Name = "account_id")] string accountId)
        {
            var result = _accountService.GetBalance(accountId);

            if (!result.IsSuccess)
                return NotFound(0);

            return Ok(result);
        }
    }
}
