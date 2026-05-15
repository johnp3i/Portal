# Requirements Document

## Introduction

Customer Registry (Module 1) provides tenant-scoped CRUD operations for managing customers within the Portal platform. Each business (tenant) maintains its own registry of customers, which are subsequently referenced by the Quotation and Invoice modules. The module delivers a repository, service layer, controller, and UI screens following the established patterns from Module 0 (Platform Foundation).

The Customer entity and database table (`[customer].[Customer]`) already exist. This module implements the application layer (repository through UI) to expose customer management functionality to authenticated users.

## Glossary

- **Customer**: A client entity registered under a specific Business tenant, stored in `[customer].[Customer]`.
- **CustomerRepository**: A table repository extending GenericStoredProcedureRepository for Customer CRUD operations against `[customer].[Customer]`.
- **CustomerService**: A scoped service implementing ICustomerService that contains business logic for customer management.
- **CustomerController**: An MVC controller handling HTTP requests for customer list, create, edit, and deactivate operations.
- **Business**: The tenant entity representing a subscribing company. Each Customer belongs to exactly one Business.
- **BusinessId**: The foreign key on Customer that associates the customer with a specific tenant.
- **CurrentTenantService**: A scoped service that resolves the current tenant's BusinessId from the authenticated user's claims.
- **Tenant_Isolation**: The enforcement that users can only access Customer records belonging to their own Business, implemented via EF Core global query filters on BusinessId.
- **Deactivation**: Setting IsActive to false on a Customer record. Customers are never hard-deleted because they may be referenced by existing Quotations and Invoices.

## Requirements

### Requirement 1: Customer Repository

**User Story:** As a developer, I want a CustomerRepository with CRUD operations, so that the service layer can persist and retrieve customer data following established repository patterns.

#### Acceptance Criteria

1. THE CustomerRepository SHALL extend GenericStoredProcedureRepository with Customer as the type parameter.
2. THE CustomerRepository SHALL provide a method to retrieve all customers for a given BusinessId.
3. THE CustomerRepository SHALL provide a method to retrieve a single customer by Id and BusinessId.
4. WHEN a new customer is created, THE CustomerRepository SHALL insert a record into `[customer].[Customer]` with BusinessId, Name, and all provided optional fields.
5. WHEN a customer is updated, THE CustomerRepository SHALL update the Name, contact fields, address fields, and UpdatedAtUtc on the matching record.
6. WHEN a customer is deactivated, THE CustomerRepository SHALL set IsActive to false and update UpdatedAtUtc on the matching record.
7. THE CustomerRepository SHALL use full table names in SQL queries without aliases.
8. THE CustomerRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
9. THE CustomerRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 2: Customer Service

**User Story:** As a developer, I want an ICustomerService interface and implementation, so that business logic for customer management is encapsulated in a testable service layer.

#### Acceptance Criteria

1. THE CustomerService SHALL implement the ICustomerService interface.
2. THE CustomerService SHALL be registered as a scoped service in the DI container.
3. WHEN retrieving customers, THE CustomerService SHALL return only customers belonging to the current tenant's BusinessId.
4. WHEN creating a customer, THE CustomerService SHALL set BusinessId from the current tenant, set IsActive to true, and set CreatedAtUtc and UpdatedAtUtc to the current UTC time.
5. WHEN updating a customer, THE CustomerService SHALL set UpdatedAtUtc to the current UTC time.
6. WHEN deactivating a customer, THE CustomerService SHALL set IsActive to false and UpdatedAtUtc to the current UTC time.
7. THE CustomerService SHALL validate that Name is not null or whitespace before creating or updating a customer.
8. WHEN an Email value is provided, THE CustomerService SHALL validate that the Email conforms to a valid email format.
9. IF validation fails, THEN THE CustomerService SHALL throw an ArgumentException with a descriptive message.

### Requirement 3: Tenant Isolation

**User Story:** As a business user, I want to see only my own business's customers, so that customer data remains private between tenants.

#### Acceptance Criteria

1. THE PortalDbContext SHALL apply a global query filter on Customer ensuring that only records matching the current tenant's BusinessId are returned.
2. WHEN creating a customer, THE CustomerService SHALL assign the BusinessId from ICurrentTenantService, preventing users from creating customers under a different tenant.
3. WHEN retrieving a customer by Id, THE CustomerService SHALL verify the customer belongs to the current tenant's BusinessId before returning the record.
4. IF a user attempts to access a customer belonging to a different Business, THEN THE CustomerController SHALL return a NotFound response.

### Requirement 4: Customer Controller

**User Story:** As a business user, I want to list, create, edit, and deactivate customers through the web interface, so that I can manage my customer registry.

#### Acceptance Criteria

1. THE CustomerController SHALL require authentication via the Authorize attribute.
2. THE CustomerController SHALL delegate all business logic to ICustomerService.
3. WHEN a user navigates to the customer list, THE CustomerController SHALL return a view displaying all customers for the current tenant.
4. WHEN a user submits a valid customer creation form, THE CustomerController SHALL create the customer and redirect to the customer list.
5. WHEN a user submits a valid customer edit form, THE CustomerController SHALL update the customer and redirect to the customer list.
6. WHEN a user requests customer deactivation, THE CustomerController SHALL deactivate the customer and redirect to the customer list.
7. IF model validation fails on create or edit, THEN THE CustomerController SHALL redisplay the form with validation error messages.
8. THE CustomerController SHALL use ValidateAntiForgeryToken on all POST actions.

### Requirement 5: Search and Filtering

**User Story:** As a business user, I want to search customers by name and filter by active status, so that I can quickly find specific customers in a large registry.

#### Acceptance Criteria

1. WHEN a search term is provided, THE CustomerController SHALL return only customers whose Name contains the search term (case-insensitive).
2. WHEN an IsActive filter is provided, THE CustomerController SHALL return only customers matching the specified IsActive value.
3. WHEN both search term and IsActive filter are provided, THE CustomerController SHALL apply both filters simultaneously.
4. WHEN no filters are provided, THE CustomerController SHALL return all customers for the current tenant.
5. THE Customer list view SHALL display a search input field and an IsActive filter control that submit filter parameters to the controller.

### Requirement 6: Validation Rules

**User Story:** As a business user, I want clear validation feedback when entering customer data, so that I can correct errors before saving.

#### Acceptance Criteria

1. THE CustomerService SHALL reject customer creation or update when Name is null, empty, or whitespace.
2. WHEN an Email value is provided and is not empty, THE CustomerService SHALL validate that the Email matches a standard email format pattern.
3. IF Name validation fails, THEN THE CustomerController SHALL display "Customer name is required" to the user.
4. IF Email validation fails, THEN THE CustomerController SHALL display "Email address is not in a valid format" to the user.
5. THE CustomerService SHALL accept null or empty Email values without validation error (Email is optional).

### Requirement 7: Customer List UI

**User Story:** As a business user, I want a customer list screen following the MyChair Design System, so that I can view and manage all my customers in a consistent interface.

#### Acceptance Criteria

1. THE Customer list view SHALL display customers in a card-based or table layout following the MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body, cards with 20-30px border radius).
2. THE Customer list view SHALL display Name, Email, TelephoneNumber, City, and IsActive status for each customer.
3. THE Customer list view SHALL provide action links to edit and deactivate each customer.
4. THE Customer list view SHALL provide a button to create a new customer.
5. THE Customer list view SHALL visually distinguish active customers from inactive customers.
6. THE Customer list view SHALL display the search input and IsActive filter controls above the customer list.

### Requirement 8: Customer Create/Edit Form UI

**User Story:** As a business user, I want a customer create/edit form following the MyChair Design System, so that I can enter and modify customer details with clear field labels and validation feedback.

#### Acceptance Criteria

1. THE Customer form view SHALL display input fields for: Name, ContactPerson, Email, TelephoneNumber, MobileNumber, AddressLine1, AddressLine2, City, PostalCode, and Country.
2. THE Customer form view SHALL mark the Name field as required with a visual indicator.
3. WHEN validation errors exist, THE Customer form view SHALL display error messages adjacent to the relevant fields.
4. THE Customer form view SHALL pre-populate all fields with existing values when editing a customer.
5. THE Customer form view SHALL follow the MyChair Design System styling (input fields, buttons, spacing, typography).

### Requirement 9: Integration Points

**User Story:** As a developer, I want the Customer entity to remain referenceable by Quotation and Invoice modules, so that downstream modules can associate documents with customers.

#### Acceptance Criteria

1. THE Customer entity SHALL maintain navigation properties to Quotation and Invoice collections for use by downstream modules.
2. THE CustomerService SHALL support deactivation (soft-delete) only, preserving referential integrity with existing Quotations and Invoices.
3. WHILE a Customer has associated Quotations or Invoices, THE CustomerService SHALL still allow deactivation but SHALL NOT allow hard deletion.

