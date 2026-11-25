using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMBT.Web.Data;
using HRMBT.Web.Models;
using HRMBT.Web.Models.ViewModels;

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
        /// Tax year labels ("2025-2026", July to June) offered in the form: every year from 2014 to five years ahead, plus any year that already has slabs.
        /// HR can start slabs for any of them; the list is not limited to years that exist.
        /// </summary>
        private async Task<List<string>> GetTaxYearsFromConfigurationAsync()
        {
            var labels = await _context.TaxRules.AsNoTracking()
                .Select(r => r.TaxYear)
                .Distinct()
                .ToListAsync();
            var starts = labels
                .Select(l => int.TryParse(l.Length >= 4 ? l.Substring(0, 4) : "", out var y) ? y : 0)
                .Where(y => y > 0)
                .ToList();

            var current = CurrentTaxYearStart(DateTime.Today);
            var first = Math.Min(2014, starts.Count > 0 ? starts.Min() : 2014);
            var last = Math.Max(current + 5, starts.Count > 0 ? starts.Max() : 0);
            return Enumerable.Range(first, last - first + 1).OrderByDescending(y => y).Select(y => $"{y}-{y + 1}").ToList();
        }

        /// <summary>A label must be "YYYY-YYYY" with the second year one more than the first, between 2000 and 2100.</summary>
        private static bool IsValidTaxYearLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label) || label.Trim().Length != 9 || label.Trim()[4] != '-') return false;
            var t = label.Trim();
            return int.TryParse(t.AsSpan(0, 4), out var a) && int.TryParse(t.AsSpan(5, 4), out var b) && b == a + 1 && a >= 2000 && a <= 2100;
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

        // GET: Tax - one card per tax year
        [HttpGet("")]
        public async Task<IActionResult> Index(string? taxYear)
        {
            ViewData["Module"] = "Tax";

            // old links such as /tax-rules?taxYear=2025-2026 open that year's page
            if (IsValidTaxYearLabel(taxYear))
                return RedirectToActionPermanent(nameof(Year), new { start = int.Parse(taxYear!.Trim().Substring(0, 4)) });

            var rules = await _context.TaxRules.AsNoTracking().ToListAsync();
            var current = CurrentTaxYearStart(DateTime.Today);

            var cards = rules
                .Where(r => r.StartYear > 0)
                .GroupBy(r => r.StartYear)
                .OrderByDescending(g => g.Key)
                .Select(g => new TaxYearCardVM
                {
                    StartYear = g.Key,
                    SlabCount = g.Count(),
                    LowestRate = g.Min(r => r.TaxPercentage),
                    HighestRate = g.Max(r => r.TaxPercentage),
                    TopSlabFrom = g.Max(r => r.MinSalary),
                    IsCurrent = g.Key == current,
                    HasWarnings = BuildSlabWarnings(g.ToList()).Count > 0
                })
                .ToList();

            ViewBag.CurrentStartYear = current;
            return View(cards);
        }

        // GET: Tax/years/2026 - the slabs of one tax year
        [HttpGet("years/{start:int}")]
        public async Task<IActionResult> Year(int start)
        {
            ViewData["Module"] = "Tax";
            var label = $"{start}-{start + 1}";
            var rules = (await _context.TaxRules.AsNoTracking().Where(r => r.TaxYear == label).ToListAsync())
                .OrderBy(r => r.MinSalary)
                .ToList();
            if (rules.Count == 0)
                return NotFound();

            return View(new TaxYearVM
            {
                StartYear = start,
                IsCurrent = start == CurrentTaxYearStart(DateTime.Today),
                Rules = rules,
                Warnings = BuildSlabWarnings(rules)
            });
        }

        /// <summary>
        /// Plain-language checks on one year's slabs (information only, nothing is blocked): the first slab starts at 0, the last has no limit,
        /// slabs meet without gaps or overlaps, and each fixed amount equals the tax already due at the start of the slab.
        /// </summary>
        private static List<string> BuildSlabWarnings(List<TaxRule> slabs)
        {
            var w = new List<string>();
            var s = slabs.OrderBy(r => r.MinSalary).ToList();
            if (s.Count == 0) return w;
            var inv = CultureInfo.InvariantCulture;

            if (s[0].MinSalary != 0m)
                w.Add($"The first slab starts at {s[0].MinSalary.ToString("N0", inv)}, not 0, so income below it is not taxed.");
            if (s[^1].MaxSalary.HasValue && s[^1].MaxSalary.Value > 0m)
                w.Add($"The last slab stops at {s[^1].MaxSalary!.Value.ToString("N0", inv)}; leave its \"up to\" empty so higher incomes are covered.");

            for (var i = 1; i < s.Count; i++)
            {
                var prev = s[i - 1];
                var cur = s[i];
                if (prev.MaxSalary.HasValue && prev.MaxSalary.Value > 0m && prev.MaxSalary.Value != cur.MinSalary)
                    w.Add($"The slab ending at {prev.MaxSalary.Value.ToString("N0", inv)} does not meet the next slab, which starts at {cur.MinSalary.ToString("N0", inv)}.");

                var expected = Math.Round(prev.FixedAmount + (cur.MinSalary - prev.MinSalary) * prev.TaxPercentage / 100m, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(expected - cur.FixedAmount) > 1m)
                    w.Add($"Slab from {cur.MinSalary.ToString("N0", inv)}: fixed amount is {cur.FixedAmount.ToString("N0", inv)}, but the slab below implies {expected.ToString("N0", inv)}. Check it against the source.");
            }
            return w;
        }

        // GET: Tax/years/new - set up a whole tax year (all slabs) on one page
        [HttpGet("years/new")]
        public async Task<IActionResult> NewYear(int? copyFrom)
        {
            ViewData["Module"] = "Tax";
            var vm = new TaxYearNewVM();

            if (copyFrom is >= 2000 and <= 2100)
            {
                var label = $"{copyFrom}-{copyFrom + 1}";
                var rules = (await _context.TaxRules.AsNoTracking().Where(r => r.TaxYear == label).ToListAsync())
                    .OrderBy(r => r.MinSalary);
                vm.Slabs = rules.Select(r => new TaxSlabRowVM
                {
                    MinSalary = r.MinSalary,
                    MaxSalary = r.MaxSalary > 0m ? r.MaxSalary : null,
                    FixedAmount = r.FixedAmount,
                    TaxPercentage = r.TaxPercentage
                }).ToList();
                ViewBag.CopiedFrom = label;
            }
            if (vm.Slabs.Count == 0)
            {
                vm.Slabs.Add(new TaxSlabRowVM { MinSalary = 0m });
                vm.Slabs.Add(new TaxSlabRowVM());
                vm.Slabs.Add(new TaxSlabRowVM());
            }

            await LoadNewYearLookupsAsync(vm);
            return View(vm);
        }

        // POST: Tax/years/new
        [HttpPost("years/new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewYear(TaxYearNewVM model)
        {
            ViewData["Module"] = "Tax";
            model.Slabs ??= new List<TaxSlabRowVM>();
            ModelState.Remove(nameof(model.TaxYear));

            if (!IsValidTaxYearLabel(model.TaxYear))
                ModelState.AddModelError(nameof(model.TaxYear), "Pick a tax year from the list (for example 2027-2028).");
            else
            {
                model.TaxYear = model.TaxYear.Trim();
                if (await _context.TaxRules.AnyAsync(r => r.TaxYear == model.TaxYear))
                    ModelState.AddModelError(nameof(model.TaxYear), $"Tax year {model.TaxYear} already has slabs. Open it from the tax year cards to add or change slabs.");
            }

            var rows = model.Slabs.Select((r, i) => (Row: r, No: i + 1)).Where(x => !x.Row.IsBlank).ToList();
            if (rows.Count == 0)
                ModelState.AddModelError(string.Empty, "Add at least one slab.");

            foreach (var (r, no) in rows)
            {
                if (!r.MinSalary.HasValue || r.MinSalary.Value < 0m)
                    ModelState.AddModelError(string.Empty, $"Slab {no}: enter the annual income it starts from (0 or more).");
                if (!r.TaxPercentage.HasValue || r.TaxPercentage.Value < 0m || r.TaxPercentage.Value > 100m)
                    ModelState.AddModelError(string.Empty, $"Slab {no}: enter a rate between 0 and 100.");
                if (r.FixedAmount.HasValue && r.FixedAmount.Value < 0m)
                    ModelState.AddModelError(string.Empty, $"Slab {no}: the fixed amount cannot be negative.");
                if (r.MaxSalary.HasValue && r.MaxSalary.Value > 0m && r.MinSalary.HasValue && r.MaxSalary.Value <= r.MinSalary.Value)
                    ModelState.AddModelError(string.Empty, $"Slab {no}: \"up to\" must be above \"from\" (leave it empty for no limit).");
            }

            foreach (var dup in rows.Where(x => x.Row.MinSalary.HasValue).GroupBy(x => x.Row.MinSalary!.Value).Where(g => g.Count() > 1))
                ModelState.AddModelError(string.Empty, $"Two slabs start at {dup.Key.ToString("N0", CultureInfo.InvariantCulture)}; each slab needs its own start.");

            if (!ModelState.IsValid)
            {
                await LoadNewYearLookupsAsync(model);
                return View(model);
            }

            // saved in order of start; a slab with no "up to" ends where the next one starts, the last one has no limit
            var ordered = rows.Select(x => x.Row).OrderBy(r => r.MinSalary!.Value).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var r = ordered[i];
                decimal? max = r.MaxSalary.HasValue && r.MaxSalary.Value > 0m ? r.MaxSalary : null;
                if (max == null && i < ordered.Count - 1)
                    max = ordered[i + 1].MinSalary;
                _context.TaxRules.Add(new TaxRule
                {
                    TaxYear = model.TaxYear,
                    MinSalary = r.MinSalary!.Value,
                    MaxSalary = max,
                    FixedAmount = r.FixedAmount ?? 0m,
                    TaxPercentage = r.TaxPercentage!.Value
                });
            }
            await _context.SaveChangesAsync();

            var start = int.Parse(model.TaxYear.Substring(0, 4));
            TempData["SuccessMessage"] = $"Tax year {model.TaxYear} created with {ordered.Count} slab(s).";
            return RedirectToAction(nameof(Year), new { start });
        }

        /// <summary>Years that can still be created (no slabs yet), and the years whose slabs can be copied as a starting point.</summary>
        private async Task LoadNewYearLookupsAsync(TaxYearNewVM vm)
        {
            var existing = await _context.TaxRules.AsNoTracking().Select(r => r.TaxYear).Distinct().ToListAsync();
            var all = await GetTaxYearsFromConfigurationAsync();
            var free = all.Where(l => !existing.Contains(l, StringComparer.OrdinalIgnoreCase)).OrderBy(l => l).ToList();
            ViewBag.FreeYears = free;
            ViewBag.CopyYears = existing.Select(l => l.Length >= 4 && int.TryParse(l.AsSpan(0, 4), out var y) ? y : 0)
                .Where(y => y > 0).OrderByDescending(y => y).ToList();

            if (string.IsNullOrWhiteSpace(vm.TaxYear) || !free.Contains(vm.TaxYear))
            {
                var current = CurrentTaxYearStart(DateTime.Today);
                vm.TaxYear = free.FirstOrDefault(l => int.Parse(l.AsSpan(0, 4)) >= current) ?? free.FirstOrDefault() ?? string.Empty;
            }
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
        public async Task<IActionResult> Create(int? year)
        {
            ViewData["Module"] = "Tax";
            var preference = year is >= 2000 and <= 2100 ? $"{year}-{year + 1}" : await GetConfiguredCurrentTaxYearAsync();
            var sel = await LoadTaxYearsForTaxRuleFormAsync(preference, alwaysIncludeYears: null);
            return View(new TaxRule { TaxYear = sel });
        }

        // POST: Tax/Create
        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MinSalary,MaxSalary,FixedAmount,TaxPercentage,TaxYear")] TaxRule taxRule)
        {
            ViewData["Module"] = "Tax";
            if (!IsValidTaxYearLabel(taxRule.TaxYear))
                ModelState.AddModelError(nameof(taxRule.TaxYear), "Pick a tax year from the list (for example 2027-2028).");
            else
                taxRule.TaxYear = taxRule.TaxYear.Trim();

            await ValidateSlabAsync(taxRule, null);

            if (ModelState.IsValid)
            {
                _context.Add(taxRule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tax slab saved.";
                return RedirectToAction(nameof(Year), new { start = taxRule.StartYear });
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,MinSalary,MaxSalary,FixedAmount,TaxPercentage,TaxYear")] TaxRule taxRule)
        {
            ViewData["Module"] = "Tax";
            if (id != taxRule.Id) return NotFound();

            var existingTracked = await _context.TaxRules.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existingTracked == null)
                return NotFound();

            if (!IsValidTaxYearLabel(taxRule.TaxYear))
                ModelState.AddModelError(nameof(taxRule.TaxYear), "Pick a tax year from the list (for example 2027-2028).");
            else
                taxRule.TaxYear = taxRule.TaxYear.Trim();

            await ValidateSlabAsync(taxRule, id);

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
                return RedirectToAction(nameof(Year), new { start = taxRule.StartYear });
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
                var start = taxRule.StartYear;
                _context.TaxRules.Remove(taxRule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tax slab deleted.";
                // the year page is gone when its last slab was deleted
                var left = await _context.TaxRules.AnyAsync(r => r.TaxYear == $"{start}-{start + 1}");
                if (left) return RedirectToAction(nameof(Year), new { start });
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>A slab's "up to" must be above its start, and a tax year cannot have two slabs starting at the same income.</summary>
        private async Task ValidateSlabAsync(TaxRule rule, int? ignoreId)
        {
            if (rule.MaxSalary.HasValue && rule.MaxSalary.Value > 0m && rule.MaxSalary.Value <= rule.MinSalary)
                ModelState.AddModelError(nameof(rule.MaxSalary), "Annual income up to must be above Annual income from (leave empty for no limit).");

            if (!string.IsNullOrWhiteSpace(rule.TaxYear)
                && await _context.TaxRules.AnyAsync(r => r.TaxYear == rule.TaxYear && r.MinSalary == rule.MinSalary && r.Id != (ignoreId ?? 0)))
                ModelState.AddModelError(nameof(rule.MinSalary), "This tax year already has a slab starting at this income.");
        }

        private bool TaxRuleExists(int id)
        {
            return _context.TaxRules.Any(e => e.Id == id);
        }
    }
}
