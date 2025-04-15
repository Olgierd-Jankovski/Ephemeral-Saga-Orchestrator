using Microsoft.AspNetCore.Mvc;

namespace PaymentService;

[ApiController]
[Route("[controller]")]
public class PaymentController : ControllerBase
{
    private readonly PaymentDbContext _context;

    public PaymentController(PaymentDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetAllPayments()
    {
        var Payments = _context.Payments.ToList();
        return Ok(Payments);
    }

    [HttpPost]
    public IActionResult CreatePayment([FromBody] Payment payment)
    {
        if (payment == null)
        {
            return BadRequest("Payment cannot be null");
        }

        payment.CreatedAt = DateTime.UtcNow;
        if (payment.OrderId <= 0 || payment.Amount <= 0)
        {
            return BadRequest("OrderId and Amount must be greater than zero");
        }

        _context.Payments.Add(payment);
        _context.SaveChanges();

        return Ok(payment);
    }
}