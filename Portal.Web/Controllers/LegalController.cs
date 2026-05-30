using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Portal.Web.Controllers;

[AllowAnonymous]
public class LegalController : Controller
{
    [HttpGet]
    [Route("/Terms-and-Conditions")]
    public IActionResult TermsAndConditions() => View();

    [HttpGet]
    [Route("/Terms-of-Use")]
    public IActionResult TermsOfUse() => View();

    [HttpGet]
    [Route("/Privacy-Policy")]
    public IActionResult PrivacyPolicy() => View();

    [HttpGet]
    [Route("/Cookies-Policy")]
    public IActionResult CookiesPolicy() => View();
}
