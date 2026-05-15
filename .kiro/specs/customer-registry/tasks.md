# Implementation Plan: Customer Registry

## Overview

Implement tenant-scoped CRUD operations for managing customers within the Portal platform. This adds CustomerRepository, ICustomerService/CustomerService, CustomerController, view models, and Razor views following the exact patterns established in Module 0 (Platform Foundation). The Customer entity and database table (`[customer].[Customer]`) already exist.

## Tasks

- [x] 1. Create CustomerRepository
  - [x] 1.1 Create CustomerRepository extending GenericStoredProcedureRepository
    - Create `Portal.Infrastructure/Repositories/CustomerRepository.cs`
    - Extend `GenericStoredProcedureRepository<Customer>` with constructor accepting `DbContext`
    - Implement `GetAllByBusinessIdAsync(int businessId)` — SELECT all columns FROM `[customer].[Customer]` WHERE BusinessId = @BusinessId
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)` — SELECT single record WHERE Id = @Id AND BusinessId = @BusinessId
    - Implement `InsertAsync(Customer entity)` — INSERT INTO `[customer].[Customer]` with all fields using SqlParameter and null-safe `?? (object)DBNull.Value` for nullable fields
    - Implement `UpdateAsync(Customer entity)` — UPDATE Name, ContactPerson, Email, TelephoneNumber, MobileNumber, AddressLine1, AddressLine2, City, PostalCode, Country, IsActive, UpdatedAtUtc WHERE Id = @Id
    - Implement `DeactivateAsync(int id, int businessId)` — UPDATE IsActive = 0, UpdatedAtUtc WHERE Id = @Id AND BusinessId = @BusinessId
    - Use full table names in SQL queries (no aliases), wrap all methods in try/catch with `throw;`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9_

  - [ ]* 1.2 Write property tests for CustomerRepository
    - **Property 1: Tenant isolation on retrieval**
    - **Property 2: Customer creation invariants and round-trip**
    - **Validates: Requirements 1.2, 1.3, 1.4, 3.1**

- [x] 2. Create ICustomerService and CustomerService
  - [x] 2.1 Create ICustomerService interface
    - Create `Portal.Infrastructure/Services/ICustomerService.cs`
    - Define methods: `GetCustomersAsync(string? searchTerm, bool? isActive)`, `GetCustomerByIdAsync(int id)`, `CreateCustomerAsync(Customer customer)`, `UpdateCustomerAsync(Customer customer)`, `DeactivateCustomerAsync(int id)`
    - _Requirements: 2.1_

  - [x] 2.2 Create CustomerService implementation
    - Create `Portal.Infrastructure/Services/CustomerService.cs` implementing `ICustomerService`
    - Inject `CustomerRepository` and `ICurrentTenantService`
    - `GetCustomersAsync` — retrieve all customers for current tenant's BusinessId, apply search term filter (case-insensitive Name contains) and IsActive filter in-memory
    - `GetCustomerByIdAsync` — retrieve by Id and current tenant's BusinessId, return null if not found
    - `CreateCustomerAsync` — validate Name (non-whitespace), validate Email format (if provided), set BusinessId from `ICurrentTenantService`, set IsActive = true, set CreatedAtUtc and UpdatedAtUtc to DateTime.UtcNow, call repository InsertAsync
    - `UpdateCustomerAsync` — validate Name (non-whitespace), validate Email format (if provided), set UpdatedAtUtc to DateTime.UtcNow, call repository UpdateAsync
    - `DeactivateCustomerAsync` — get customer by Id, throw InvalidOperationException if not found, set IsActive = false and UpdatedAtUtc, call repository UpdateAsync
    - Throw `ArgumentException("Customer name is required")` when Name is null/whitespace
    - Throw `ArgumentException("Email address is not in a valid format")` when Email is invalid format
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 3.2, 3.3_

  - [ ]* 2.3 Write property tests for CustomerService
    - **Property 2: Customer creation invariants and round-trip**
    - **Property 3: Customer update round-trip**
    - **Property 4: Deactivation sets IsActive to false**
    - **Property 5: Whitespace names are rejected**
    - **Property 6: Invalid email format is rejected**
    - **Property 7: Search and filter correctness**
    - **Validates: Requirements 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 5.1, 5.2, 5.3**

- [x] 3. Register services in DI container
  - [x] 3.1 Add CustomerRepository and ICustomerService registrations to Program.cs
    - Add `builder.Services.AddScoped<CustomerRepository>();`
    - Add `builder.Services.AddScoped<ICustomerService, CustomerService>();`
    - Add required `using` statements for Portal.Infrastructure.Repositories and Portal.Infrastructure.Services
    - _Requirements: 2.2_

- [x] 4. Checkpoint - Verify compilation
  - Ensure the solution compiles with `dotnet build`. Ask the user if questions arise.

- [x] 5. Create view models
  - [x] 5.1 Create CustomerFormViewModel
    - Create `Portal.Web/Models/CustomerFormViewModel.cs`
    - Properties: Name (Required, MaxLength 200), ContactPerson (MaxLength 200), Email (MaxLength 200, EmailAddress), TelephoneNumber (MaxLength 30), MobileNumber (MaxLength 30), AddressLine1 (MaxLength 200), AddressLine2 (MaxLength 200), City (MaxLength 100), PostalCode (MaxLength 20), Country (MaxLength 100)
    - Use data annotation attributes for validation
    - _Requirements: 8.1, 8.2_

  - [x] 5.2 Create CustomerListViewModel
    - Create `Portal.Web/Models/CustomerListViewModel.cs`
    - Properties: `List<Customer> Customers`, `string? SearchTerm`, `bool? IsActiveFilter`
    - _Requirements: 7.2, 7.6_

- [x] 6. Create CustomerController
  - [x] 6.1 Create CustomerController with all actions
    - Create `Portal.Web/Controllers/CustomerController.cs`
    - Apply `[Authorize]` attribute on the controller
    - Inject `ICustomerService` via constructor
    - `[HttpGet] Index(string? searchTerm, bool? isActive)` — call `GetCustomersAsync`, populate `CustomerListViewModel`, return View
    - `[HttpGet] Create()` — return View with empty `CustomerFormViewModel`
    - `[HttpPost][ValidateAntiForgeryToken] Create(CustomerFormViewModel model)` — check ModelState, map to Customer entity, call `CreateCustomerAsync`, catch ArgumentException and add to ModelState, redirect to Index on success
    - `[HttpGet] Edit(int id)` — call `GetCustomerByIdAsync`, return NotFound if null, map to `CustomerFormViewModel`, return View
    - `[HttpPost][ValidateAntiForgeryToken] Edit(int id, CustomerFormViewModel model)` — check ModelState, get existing customer, return NotFound if null, map fields, call `UpdateCustomerAsync`, catch ArgumentException, redirect to Index on success
    - `[HttpPost][ValidateAntiForgeryToken] Deactivate(int id)` — call `DeactivateCustomerAsync`, redirect to Index
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 5.1, 5.2, 5.3, 5.4_

  - [ ]* 6.2 Write property tests for CustomerController
    - **Property 8: Validation failure preserves form state**
    - **Property 1: Tenant isolation on retrieval**
    - **Validates: Requirements 4.7, 3.4, 6.3, 6.4**

- [x] 7. Checkpoint - Verify controller compilation
  - Ensure the solution compiles with `dotnet build`. Ask the user if questions arise.

- [x] 8. Create Customer views
  - [x] 8.1 Create Customer Index view
    - Create `Portal.Web/Views/Customer/Index.cshtml`
    - Model: `CustomerListViewModel`
    - Display search input field and IsActive filter dropdown above the customer list
    - Display customers in a card-based or table layout following MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body, cards with 20-30px border radius)
    - Show Name, Email, TelephoneNumber, City, and IsActive status for each customer
    - Visually distinguish active from inactive customers (e.g., muted styling for inactive)
    - Provide Edit and Deactivate action links per customer
    - Provide "Create New Customer" button
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 5.5_

  - [x] 8.2 Create Customer Create view
    - Create `Portal.Web/Views/Customer/Create.cshtml`
    - Model: `CustomerFormViewModel`
    - Display input fields for: Name, ContactPerson, Email, TelephoneNumber, MobileNumber, AddressLine1, AddressLine2, City, PostalCode, Country
    - Mark Name field as required with visual indicator (asterisk)
    - Display validation error messages adjacent to relevant fields using `asp-validation-for`
    - Include anti-forgery token via `asp-antiforgery`
    - Follow MyChair Design System styling (input fields, buttons, spacing, typography)
    - _Requirements: 8.1, 8.2, 8.3, 8.5_

  - [x] 8.3 Create Customer Edit view
    - Create `Portal.Web/Views/Customer/Edit.cshtml`
    - Model: `CustomerFormViewModel`
    - Pre-populate all fields with existing customer values
    - Same layout and validation as Create view
    - Include hidden field for customer Id
    - Follow MyChair Design System styling
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 9. Final checkpoint - Ensure full compilation
  - Ensure the solution compiles with `dotnet build` and all views render without errors. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The design uses C# / ASP.NET Core MVC 8 — all code examples use this stack
- Repository pattern follows the established GenericStoredProcedureRepository convention with try/catch rethrow
- The Customer entity already exists; ContactPerson and MobileNumber fields may need to be added to the entity during implementation
- All SQL uses full table name `[customer].[Customer]` with no aliases
