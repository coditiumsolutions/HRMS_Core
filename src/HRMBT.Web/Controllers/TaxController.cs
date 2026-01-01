using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers
{
    public class TaxController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaxController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Tax
        public async Task<IActionResult> Index()
        {
            ViewData["Module"] = "Tax";
            var taxRules = await _context.TaxRules.ToListAsync();
            return View(taxRules);
        }

        // GET: Tax/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "Tax";
            if (id == null) return NotFound();

            var taxRule = await _context.TaxRules.FirstOrDefaultAsync(m => m.Id == id);
            if (taxRule == null) return NotFound();

            return View(taxRule);
        }

        // GET: Tax/Create
        public IActionResult Create()
        {
            ViewData["Module"] = "Tax";
            return View();
        }

        // POST: Tax/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MinSalary,MaxSalary,TaxPercentage")] TaxRule taxRule)
        {
            ViewData["Module"] = "Tax";
            if (ModelState.IsValid)
            {
                _context.Add(taxRule);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(taxRule);
        }

        // GET: Tax/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["Module"] = "Tax";
            if (id == null) return NotFound();

            var taxRule = await _context.TaxRules.FindAsync(id);
            if (taxRule == null) return NotFound();

            return View(taxRule);
        }

        // POST: Tax/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MinSalary,MaxSalary,TaxPercentage")] TaxRule taxRule)
        {
            ViewData["Module"] = "Tax";
            if (id != taxRule.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(taxRule);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaxRuleExists(taxRule.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(taxRule);
        }

        // GET: Tax/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            ViewData["Module"] = "Tax";
            if (id == null) return NotFound();

            var taxRule = await _context.TaxRules.FirstOrDefaultAsync(m => m.Id == id);
            if (taxRule == null) return NotFound();

            return View(taxRule);
        }

        // POST: Tax/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["Module"] = "Tax";
            var taxRule = await _context.TaxRules.FindAsync(id);
            if (taxRule != null)
            {
                _context.TaxRules.Remove(taxRule);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TaxRuleExists(int id)
        {
            return _context.TaxRules.Any(e => e.Id == id);
        }
    }
}
