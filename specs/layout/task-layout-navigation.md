# Tasks: Layout & Contextual Navigation

## Task 1 – Create Main Layout
- Create `/Views/Shared/_MainLayout.cshtml`
- Add two stacked Bootstrap navbars
- Navbar 1: White background
- Navbar 2: Dark blue background with module links

---

## Task 2 – Create Sidebar Partial
- Create `/Views/Shared/_Sidebar.cshtml`
- Sidebar should render links based on active module
- Use a centralized dictionary mapping module → links

---

## Task 3 – Implement Layout Grid
- Use Bootstrap grid system
- Sidebar column width: 20% (`col-2`)
- Main content width: 80% (`col-10`)

---

## Task 4 – Module Detection
- Set `ViewData["Module"]` in each controller
- Module name must match sidebar configuration key

---

## Task 5 – Apply Layout Globally
- Update `_ViewStart.cshtml` to use `_MainLayout.cshtml`
- Ensure all module views inherit the layout

---

## Task 6 – Manual Testing
- Click Employees → employee links appear
- Click Payroll → payroll links appear
- Click Attendance → attendance links appear
- Click LMS → LMS links appear
- Click Tax → tax links appear

---

## Task 7 – Regression Check
- Ensure existing pages still load
- Ensure no full page reload occurs
