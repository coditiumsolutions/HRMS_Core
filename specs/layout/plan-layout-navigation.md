# Implementation Plan: Layout & Contextual Navigation

---

## Architecture Decisions

- Use a shared Razor layout for all modules
- Use partial views for sidebar and top navigation
- Determine active module from route/controller name
- Store sidebar links in a centralized configuration

---

## Technical Approach

1. Create a new main layout file:
   - `/Views/Shared/_MainLayout.cshtml`

2. Implement two top navbars:
   - Navbar 1: White background
   - Navbar 2: Dark blue background with module links

3. Create a sidebar partial view:
   - `/Views/Shared/_Sidebar.cshtml`

4. Sidebar rendering logic:
   - Read active module from `ViewData["Module"]`
   - Load sidebar links based on module name

5. Layout grid:
   - Use Bootstrap grid system
   - Sidebar: `col-2`
   - Main content: `col-10`

6. Apply layout to all controllers:
   - Set `_MainLayout.cshtml` as default layout

---

## Non-Goals

- No authentication changes
- No role-based permissions
- No JavaScript SPA behavior

---

## Risks & Mitigation

- Risk: Incorrect module detection  
  Mitigation: Standardize module names per controller

- Risk: Layout duplication  
  Mitigation: Use shared layout and partial views
