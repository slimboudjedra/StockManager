using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StockManager.ViewModels;

namespace StockManager.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // GET: Account/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: Account/Login
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var user = await _userManager.FindByEmailAsync(input.Email.Trim());

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    input.Password,
                    input.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    return RedirectToLocal(input.ReturnUrl);
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Das Konto ist vorübergehend gesperrt. Bitte versuchen Sie es später erneut.");

                    return View(input);
                }
            }

            // Absichtlich dieselbe Meldung, ob die E-Mail unbekannt ist oder das Passwort falsch.
            ModelState.AddModelError(string.Empty, "E-Mail-Adresse oder Passwort ist falsch.");

            return View(input);
        }

        //---------------------------------------------------------------------------------------------------------//

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(nameof(Login));
        }

        //---------------------------------------------------------------------------------------------------------//

        // GET: Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        //---------------------------------------------------------------------------------------------------------//

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}