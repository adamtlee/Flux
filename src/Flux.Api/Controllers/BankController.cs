using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Flux.Data;        // Access the DbContext
using Flux.Data.Models; // Access the BankAccount model

namespace Flux.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BankController : ControllerBase
{
    private readonly BankDbContext _context;

    public BankController(BankDbContext context)
    {
        _context = context;
    }

    // READ ALL
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BankAccount>>> GetAccounts()
    {
        return await _context.Accounts.ToListAsync();
    }

    // READ ONE
    [HttpGet("{id}")]
    public async Task<ActionResult<BankAccount>> GetAccount(Guid id)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null) return NotFound();
        return account;
    }

    // CREATE
    [HttpPost]
    public async Task<ActionResult<BankAccount>> PostAccount(BankAccount account)
    {
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
    }

    // UPDATE
    [HttpPut("{id}")]
    public async Task<IActionResult> PutAccount(Guid id, BankAccount account)
    {
        if (id != account.Id) return BadRequest();

        _context.Entry(account).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null) return NotFound();

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}