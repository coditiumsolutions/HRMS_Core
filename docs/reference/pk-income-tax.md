# Pakistan salaried income tax: source and method used by payroll
Version: 1.0 | Date: 2026-11-25 | Used by: `Services/Payroll/IncomeTaxCalculator.cs`, table `TaxRules`, script `scripts/database/redesign/12_tax_slabs_annual.sql`

## Source
- **https://www.mytaxcalculator.pk** (homepage; "Updated for FY 2026-2027 FBR slabs", last updated 14 Nov 2026), read on 2026-11-25. Pages used:
  - https://www.mytaxcalculator.pk/ : FY 2026-27 slab table, monthly examples, FY 2025-26 rates in the FAQ.
  - https://www.mytaxcalculator.pk/tax-guides/understanding-tax-system : the same table in PKR, the explanation of bands and the worked example (checked by the site against Finance Act 2026).
  - https://www.mytaxcalculator.pk/budget-2025-26-vs-2026-27 : FY 2025-26 against FY 2026-27 rates, and the surcharge note.
- The site links the official text: Finance Act 2026 (https://download1.fbr.gov.pk/Docs/20266291261044366FinanceAct2026.pdf) and the Income Tax Ordinance 2001, First Schedule. **The accountant should confirm the slabs against the Finance Act**; this site is the working source, not the law.
- The payroll code is written independently. It copies the calculation *method* only and does not call or depend on the website.

## Method (as the site states it)
1. Tax is worked on **annual** taxable income, with progressive bands. A higher rate applies only to the part of the income **above the start of the band**, not to the whole salary.
2. For income inside a band: **annual tax = fixed amount + rate % x (annual income - band start)**. The first bands have a fixed amount of 0; from the third band the fixed amount is the tax already due on the lower bands.
3. **Monthly tax = annual tax / 12**, rounded to the rupee.
4. The tax year runs 1 July to 30 June (FY 2026-27 = 1 Jul 2026 to 30 Jun 2027).
5. Worked example from the site: annual income 2,400,000 -> 116,000 + 20% x 200,000 = 156,000 a year (13,000 a month). 3,600,000 -> 316,000 + 25% x 400,000 = 416,000.

## Slabs: FY 2026-27 (tax year start 2026), as published
| Annual income from | up to | Fixed amount | Rate on the part above "from" |
|---:|---:|---:|---:|
| 0 | 600,000 | 0 | 0% |
| 600,000 | 1,200,000 | 0 | 1% |
| 1,200,000 | 2,200,000 | 6,000 | 11% |
| 2,200,000 | 3,200,000 | 116,000 | 20% |
| 3,200,000 | 4,100,000 | 316,000 | 25% |
| 4,100,000 | 5,600,000 | 541,000 | 29% |
| 5,600,000 | 7,000,000 | 976,000 | 32% |
| 7,000,000 | no limit | 1,424,000 | 35% |

## Slabs: FY 2025-26 (tax year start 2025), derived
The site gives only the **rates** for 2025-26 (FAQ and comparison page): 0% to 600,000; 1% to 1,200,000; 11% to 2,200,000; 23% to 3,200,000; 30% to 4,100,000; 35% above 4,100,000. The fixed amounts are not published there, so they were **derived by continuity** (each fixed amount equals the tax due at the start of the band, using the band below it):

| Annual income from | up to | Fixed amount | Rate |
|---:|---:|---:|---:|
| 0 | 600,000 | 0 | 0% |
| 600,000 | 1,200,000 | 0 | 1% |
| 1,200,000 | 2,200,000 | 6,000 | 11% |
| 2,200,000 | 3,200,000 | 116,000 | 23% |
| 3,200,000 | 4,100,000 | 346,000 | 30% |
| 4,100,000 | no limit | 616,000 | 35% |

Derivation: 6,000 + 11% x 1,000,000 = 116,000; 116,000 + 23% x 1,000,000 = 346,000; 346,000 + 30% x 900,000 = 616,000. The site's comparison page lists 35% for 4.1M-5.6M and says nothing for 5.6M-7M, while its FAQ says 35% above 4.1M; the single 35% band above 4.1M is used. **Verify these figures against the Finance Act 2025 before relying on 2025-26 payslips.** Years before 2025-26 are not set up; HR adds them in Tax rules when needed (the site's calculator covers FY 2014-15 onward but its per-year tables could not be read).

## What the site does not cover (not implemented)
- **Exempt or deducted amounts**: no exemption for house rent, medical or other allowances is described. The site says its slab table is applied to the salary "with nothing deducted". Deductions (zakat, tuition fees) and credits (donations, pension contributions) exist in the law but are not part of the slab method and are not applied.
- **Rebates**: none mentioned.
- **Surcharge**: the site says the 9% salaried surcharge above PKR 10 million, which applied in FY 2025-26, was **abolished** for FY 2026-27. Payroll does not apply a surcharge for 2025-26; for a basic salary above about 833,000 a month in a 2025-26 period this under-charges, so check with the accountant if any 2025-26 payslips for such salaries are generated.
- **Taxable income** is today the employee's monthly basic x 12 (allowances are not taxed). Which pay counts as taxable is decision B17.

## How payroll applies it
1. Tax year of a payslip: month 7-12 -> payslip year, month 1-6 -> payslip year - 1 (decision T6).
2. Only when `Employee.ApplyTax` is on. No slabs for that tax year = no tax, and the payslip working says so.
3. Annual income = basic x 12 -> slab with the highest "from" not above it -> annual tax -> monthly tax (annual / 12, rounded to the rupee).
4. Payslip fields: `TaxAmount` = monthly tax; `TaxPercentage` = the **slab rate** (marginal rate, not the effective rate); the tax line shows e.g. "Slab 20% + 116,000 over 2,200,000"; `CalculationDetails` holds the full working.
5. Limitation of "monthly = annual / 12" (also how the site does it): pay that changes during the year, or a partial year (a joiner), is taxed as if the current monthly pay lasted all year.

## Table `TaxRules` (script 12)
`MinSalary` and `MaxSalary` keep their names but hold **annual** income (the slab start and end). New column `FixedAmount decimal(18,2) NOT NULL DEFAULT 0`. A slab is chosen by its start (`MinSalary`); the end is informational and checked on entry (must be above the start; no two slabs of a year may start at the same income).
