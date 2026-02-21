using Microsoft.AspNetCore.Mvc;
using Flux.Api.Models; // This links to the model you just made

namespace Flux.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // This makes the URL: http://localhost:5271/api/bank
public class BankController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAccount()
    {
        // Creating a "fake" account based on the Model
        var myAccount = new BankAccount
        {
            Owner = "Gatsby Lee",
            Balance = 5000.75m, 
            Type = AccountType.Checking
        };

        return Ok(myAccount); 
    }
}