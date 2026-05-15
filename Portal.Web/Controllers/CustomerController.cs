using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Customer)]
public class CustomerController : Controller
{
    private readonly ICustomerService _customerService;

    public CustomerController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, bool? isActive)
    {
        var customers = await _customerService.GetCustomersAsync(searchTerm, isActive);
        var viewModel = new CustomerListViewModel
        {
            Customers = customers,
            SearchTerm = searchTerm,
            IsActiveFilter = isActive
        };
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CustomerFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Customer, AccessLevels.Full)]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var customer = new Customer
            {
                Name = model.Name,
                ContactPerson = model.ContactPerson,
                Email = model.Email,
                TelephoneNumber = model.TelephoneNumber,
                MobileNumber = model.MobileNumber,
                AddressLine1 = model.AddressLine1,
                AddressLine2 = model.AddressLine2,
                City = model.City,
                PostalCode = model.PostalCode,
                Country = model.Country
            };

            await _customerService.CreateCustomerAsync(customer);
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null) return NotFound();

        var viewModel = new CustomerFormViewModel
        {
            Name = customer.Name,
            ContactPerson = customer.ContactPerson,
            Email = customer.Email,
            TelephoneNumber = customer.TelephoneNumber,
            MobileNumber = customer.MobileNumber,
            AddressLine1 = customer.AddressLine1,
            AddressLine2 = customer.AddressLine2,
            City = customer.City,
            PostalCode = customer.PostalCode,
            Country = customer.Country
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Customer, AccessLevels.Full)]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null) return NotFound();

        try
        {
            customer.Name = model.Name;
            customer.ContactPerson = model.ContactPerson;
            customer.Email = model.Email;
            customer.TelephoneNumber = model.TelephoneNumber;
            customer.MobileNumber = model.MobileNumber;
            customer.AddressLine1 = model.AddressLine1;
            customer.AddressLine2 = model.AddressLine2;
            customer.City = model.City;
            customer.PostalCode = model.PostalCode;
            customer.Country = model.Country;

            await _customerService.UpdateCustomerAsync(customer);
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Customer, AccessLevels.Full)]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _customerService.DeactivateCustomerAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
