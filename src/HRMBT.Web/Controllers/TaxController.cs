using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;

namespace HRMBT.Web.Controllers
{
[Route("tax-rules")]
    public class TaxController : Controller
    {

        private readonly ApplicationDbContext _context;

        public TaxController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static IEnumerable<string> SplitConfigCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) yield break;
            foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(item))
                    yield return item;
            }
        }

        private static bool ConfigKeyMatches(string? key, string match)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return string.Equals(key.Trim(), match, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tax year labels ("2025-2026", July to June). No Configuration list any more (T6): the years that already have slabs,
        /// plus the current tax year and the next one so a new year can be started.
        /// </summary>
        private async Task<List<string>> GetTaxYearsFromConfigurationAsync()
        {
            var labels = await _context.TaxRules.AsNoTracking()
                .Select(r => r.TaxYear)
                .Distinct()
                .ToListAsync();
            var starts = labels
                .Select(l => int.TryParse(l.Length >= 4 ? l.Substring(0, 4) : "", out var y) ? y : 0)
                .ToList();

            var current = CurrentTaxYearStart(DateTime.Today);
            var set = new HashSet<int>(starts) { current, current + 1 };
            return set.Where(y => y > 0).OrderBy(y => y).Select(y => $"{y}-{y + 1}").ToList();
        }

        /// <summary>July to June: 2026-03 belongs to 2025-2026, 2026-07 to 2026-2027.</summary>
        private static int CurrentTaxYearStart(DateTime date) => date.Month >= 7 ? date.Year : date.Year - 1;

        /// <summary>Current tax year label, derived from today's date (default of the create form).</summary>
        private Task<string?> GetConfiguredCurrentTaxYearAsync()
        {
            var y = CurrentTaxYearStart(DateTime.Today);
            return Task.FromResult<string?>($"{y}-{y + 1}");
        }

        /// <summary>Dropdown choices: only the list built by GetTaxYearsFromConfigurationAsync, plus any extra years (e.g. existing slab when editing).</summary>
        private static List<string> BuildTaxYearChoicesForForm(IEnumerable<string> taxSlabYearsFromConfig, IEnumerable<string>? includeAdditional)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in taxSlabYearsFromConfig)
            {
                if (!string.IsNullOrWhiteSpace(x))
                    set.Add(x.Trim());
            }

            if (includeAdditional != null)
            {
                foreach (var a in includeAdditional)
                {
                    if (!string.IsNullOrWhiteSpace(a))
                        set.Add(a.Trim());
                }
            }

            return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void SetTaxYearsSelect(List<string> orderedOptions, string? selectedForSelectList)
        {
            if (orderedOptions.Count == 0)
            {
                ViewBag.TaxYears = new SelectList(Array.Empty<string>());
                return;
            }

            object? selectedMatch = null;
            if (!string.IsNullOrWhiteSpace(selectedForSelectList))
            {
                var want = selectedForSelectList.Trim();
                selectedMatch = orderedOptions.FirstOrDefault(o =>
                    string.Equals(o, want, StringComparison.OrdinalIgnoreCase));
            }

            ViewBag.TaxYears = new SelectList(orderedOptions, selectedMatch);
        }

        /// <returns>Years allowed when saving — does not broaden with arbitrary POSTed values.</returns>
        private static List<string> AllowedTaxYearsForValidation(IEnumerable<string> fromConfig, string? preservedExistingTaxYear)
        {
            var baseChoices = BuildTaxYearChoicesForForm(fromConfig,
                includeAdditional: string.IsNullOrWhiteSpace(preservedExistingTaxYear)
                    ? null
                    : [preservedExistingTaxYear]);

            return baseChoices;
        }

        private static bool AllowedTaxYearsContains(IEnumerable<string> allowedList, string? taxYearPosted)
        {
            if (string.IsNullOrWhiteSpace(taxYearPosted)) return false;
            var v = taxYearPosted.Trim();
            return allowedList.Any(a => string.Equals(a, v, StringComparison.OrdinalIgnoreCase));
        }

        /// <returns>Model <see cref="TaxRule.TaxYear"/> string to bind (empty when no valid default).</returns>
        private async Task<string> LoadTaxYearsForTaxRuleFormAsync(string? preferredSelection, IEnumerable<string>? alwaysIncludeYears)
        {
            var configured = await GetTaxYearsFromConfigurationAsync();
            var options = BuildTaxYearChoicesForForm(configured, alwaysIncludeYears);
            var sel = preferredSelection?.Trim();

            if (!string.IsNullOrEmpty(sel) && !options.Any(o => string.Equals(o, sel!, StringComparison.OrdinalIgnoreCase)))
            {
                if (alwaysIncludeYears != null)
                    options = BuildTaxYearChoicesForForm(configured,
                        alwaysIncludeYears.Append(sel!));
                else
                    sel = "";
            }

            if (string.IsNullOrEmpty(sel))
                sel = "";
            else
            {
                var hit = options.FirstOrDefault(o => string.Equals(o, sel, StringComparison.OrdinalIgnoreCase));
                sel = hit ?? "";
            }

            SetTaxYearsSelect(options, string.IsNullOrEmpty(sel) ? null : sel);
            return sel;
        }

        private void LoadTaxYearFilterSelect(IReadOnlyList<string> yearsFromConfig, string? selectedTaxYear)
        {
            var items = new List<SelectListItem>
            {
                new()
                {
                    Value = "",
                    Text = "— Select tax year —",
                    Selected = string.IsNullOrWhiteSpace(selectedTaxYear)
                }
            };
            items.AddRange(yearsFromConfig.Select(y => new SelectListItem
            {
                Value = y,
                Text = y,
                Selected = !string.IsNullOrWhiteSpace(selectedTaxYear)
                           && string.Equals(y, selectedTaxYear.Trim(), StringComparison.OrdinalIgnoreCase)
            }));
            ViewBag.TaxYearFilter = items;
            ViewBag.SelectedTaxYear = selectedTaxYear ?? "";
        }

        // GET: Tax
        [HttpGet("")]
        public async Task<IActionResult> Index(string? taxYear)
        {
            ViewData["Module"] = "Tax";
            var configuredYears = await GetTaxYearsFromConfigurationAsync();
            LoadTaxYearFilterSelect(configuredYears, taxYear);

            List<TaxRule> taxRules = [];
            if (!string.IsNullOrWhiteSpace(taxYear))
            {
                var year = taxYear.Trim();
                taxRules = (await _context.TaxRules.AsNoTracking().ToListAsync())
                    .Where(r => r.TaxYear != null
                                && string.Equals(r.TaxYear, year, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.MinSalary)
                    .ToList();
            }

            ViewBag.HasTaxYearFilter = !string.IsNullOrWhiteSpace(taxYear);
            return View(taxRules);
        }

        // GET: Tax/Details/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            ViewData["Module"] = "Tax";
            if (id == null) return NotFound();

            var taxRule = await _context.TaxRules.FirstOrDefaultAsync(m => m.Id == id);
            if (taxRule == null) return NotFound();

            return View(taxRule);
        }

        // GET: Tax/Create
        [HttpGet("new")]
        public async Task<IActionResult> Create()
        {
            ViewData["Module"] = "Tax";
            var preference = await GetConfiguredCurrentTaxYearAsync();
            var sel = await LoadTaxYearsForTaxRuleFormAsync(preference, alwaysIncludeYears: null);
            return View(new TaxRule { TaxYear = sel });
        }

        // POST: Tax/Create
        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MinSalary,MaxSalary,TaxPercentage,TaxYear")] TaxRule taxRule)
        {
            ViewData["Module"] = "Tax";
            var configured = await GetTaxYearsFromConfigurationAsync();
            var allowed = AllowedTaxYearsForValidation(configured, preservedExistingTaxYear: null);

            if (!AllowedTaxYearsContains(allowed, taxRule.TaxYear))
                ModelState.AddModelError(nameof(taxRule.TaxYear),
                    $"Pick a tax year from the list. Years that already have slabs, the current tax year and the next one are offered.");

            if (ModelState.IsValid)
            {
                _context.Add(taxRule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tax rule created.";
                return RedirectToAction(nameof(Index));
            }

            taxRule.TaxYear = await LoadTaxYearsForTaxRuleFormAsync(taxRule.TaxYear, alwaysIncludeYears: null);
            return View(taxRule);
        }

        // GET: Tax/Edit/5
        [HttpGet("{id}/edit")]
        public async Task<IActionResult> Edit(int? id)
        {
            ViewData["Module"] = "Tax";
            if (id == null) return NotFound();

            var taxRule = await _context.TaxRules.FindAsync(id);
            if (taxRule == null) return NotFound();

            taxRule.TaxYear = await LoadTaxYearsForTaxRuleFormAsync(taxRule.TaxYear, alwaysIncludeYears: [taxRule.TaxYear]);
            return View(taxRule);
        }

        // POST: Tax/Edit/5
        [HttpPost("{id}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MinSalary,MaxSalary,TaxPercentage,TaxYear")] TaxRule taxRule)
        {
            ViewData["Module"] = "Tax";
            if (id != taxRule.Id) return NotFound();

            var existingTracked = await _context.TaxRules.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existingTracked == null)
                return NotFound();

            var configured = await GetTaxYearsFromConfigurationAsync();
            var allowed = AllowedTaxYearsForValidation(configured, preservedExistingTaxYear: existingTracked.TaxYear);

            if (!AllowedTaxYearsContains(allowed, taxRule.TaxYear))
                ModelState.AddModelError(nameof(taxRule.TaxYear),
                    $"Pick a tax year from the list, or keep this slab's current year.");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(taxRule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tax rule updated.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaxRuleExists(taxRule.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            taxRule.TaxYear = await LoadTaxYearsForTaxRuleFormAsync(taxRule.TaxYear, alwaysIncludeYears: [existingTracked.TaxYear]);
            return View(taxRule);
        }

        // GET: Tax/Delete/5
        [HttpGet("{id}/delete")]
        public async Task<IActionResult> Delete(int? id)
        {
            ViewData["Module"] = "Tax";
            if (id == null) return NotFound();

            var taxRule = await _context.TaxRules.FirstOrDefaultAsync(m => m.Id == id);
            if (taxRule == null) return NotFound();

            return View(taxRule);
        }

        // POST: Tax/Delete/5
        [HttpPost("{id}/delete"), ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["Module"] = "Tax";
            var taxRule = await _context.TaxRules.FindAsync(id);
            if (taxRule != null)
            {
                _context.TaxRules.Remove(taxRule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tax rule deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TaxRuleExists(int id)
        {
            return _context.TaxRules.Any(e => e.Id == id);
        }
    }
}
