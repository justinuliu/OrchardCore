using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Users.ViewModels;
using YesSql;

namespace OrchardCore.Users.Controllers
{
    public class AdminController : Controller, IUpdateModel
    {
        private readonly UserManager<IUser> _userManager;
        private readonly ISession _session;
        private readonly IAuthorizationService _authorizationService;
        private readonly ISiteService _siteService;
        private readonly IDisplayManager<User> _userDisplayManager;
        private readonly INotifier _notifier;

        private readonly dynamic New;
        private readonly IHtmlLocalizer TH;

        public AdminController(
            IDisplayManager<User> userDisplayManager,
            IAuthorizationService authorizationService,
            ISession session,
            UserManager<IUser> userManager,
            INotifier notifier,
            ISiteService siteService,
            IShapeFactory shapeFactory,
            IHtmlLocalizer<AdminController> htmlLocalizer
            )
        {
            _userDisplayManager = userDisplayManager;
            _authorizationService = authorizationService;
            _session = session;
            _userManager = userManager;
            _notifier = notifier;
            _siteService = siteService;

            New = shapeFactory;
            TH = htmlLocalizer;
        }
        public async Task<ActionResult> Index(UserIndexOptions options, PagerParameters pagerParameters)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageUsers))
            {
                return Unauthorized();
            }

            var siteSettings = await _siteService.GetSiteSettingsAsync();
            var pager = new Pager(pagerParameters, siteSettings.PageSize);

            // default options
            if (options == null)
            {
                options = new UserIndexOptions();
            }

            var users = _session.Query<User, UserIndex>();

            switch (options.Filter)
            {
                case UsersFilter.Approved:
                    //users = users.Where(u => u.RegistrationStatus == UserStatus.Approved);
                    break;
                case UsersFilter.Pending:
                    //users = users.Where(u => u.RegistrationStatus == UserStatus.Pending);
                    break;
                case UsersFilter.EmailPending:
                    //users = users.Where(u => u.EmailStatus == UserStatus.Pending);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(options.Search))
            {
                users = users.Where(u => u.NormalizedUserName.Contains(options.Search) || u.NormalizedEmail.Contains(options.Search));
            }

            switch (options.Order)
            {
                case UsersOrder.Name:
                    users = users.OrderBy(u => u.NormalizedUserName);
                    break;
                case UsersOrder.Email:
                    users = users.OrderBy(u => u.NormalizedEmail);
                    break;
                case UsersOrder.CreatedUtc:
                    //users = users.OrderBy(u => u.CreatedUtc);
                    break;
                case UsersOrder.LastLoginUtc:
                    //users = users.OrderBy(u => u.LastLoginUtc);
                    break;
            }

            var count = await users.CountAsync();

            var results = await users
                .Skip(pager.GetStartIndex())
                .Take(pager.PageSize)
                .ListAsync();

            // Maintain previous route data when generating page links
            var routeData = new RouteData();
            routeData.Values.Add("Options.Filter", options.Filter);
            routeData.Values.Add("Options.Search", options.Search);
            routeData.Values.Add("Options.Order", options.Order);

            var pagerShape = (await New.Pager(pager)).TotalItemCount(count).RouteData(routeData);

            var model = new UsersIndexViewModel
            {
                Users = await Task.WhenAll(
                    results.Select(async x => 
                        new UserEntry { Shape = await _userDisplayManager.BuildDisplayAsync(x, this, "SummaryAdmin") })),
                Options = options,
                Pager = pagerShape
            };

            return View(model);
        }

        public async Task<IActionResult> Create()
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageUsers))
            {
                return Unauthorized();
            }

            var shape = await _userDisplayManager.BuildEditorAsync(new User(), this);

            return View(shape);
        }

        [HttpPost]
        [ActionName(nameof(Create))]
        public async Task<IActionResult> CreatePost()
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageUsers))
            {
                return Unauthorized();
            }
            
            var shape = await _userDisplayManager.UpdateEditorAsync(new User(), this);

            if (!ModelState.IsValid)
            {
                return View(shape);
            }

            _notifier.Success(TH["User created successfully"]);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageUsers))
            {
                return Unauthorized();
            }

            var currentUser = await _userManager.FindByIdAsync(id);
            if (!(currentUser is User))
            {
                return NotFound();
            }

            var shape = await _userDisplayManager.BuildEditorAsync((User) currentUser, this);

            return View(shape);
        }

        [HttpPost]
        [ActionName(nameof(Edit))]
        public async Task<IActionResult> EditPost(string id)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageUsers))
            {
                return Unauthorized();
            }

            var currentUser = await _userManager.FindByIdAsync(id);
            if (currentUser == null)
            {
                return NotFound();
            }

            var shape = await _userDisplayManager.UpdateEditorAsync((User) currentUser, this);

            if (!ModelState.IsValid)
            {
                return View(shape);
            }

            _notifier.Success(TH["User updated successfully"]);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageUsers))
            {
                return Unauthorized();
            }

            var currentUser = await _userManager.FindByIdAsync(id);

            if (!(currentUser is User))
            {
                return NotFound();
            }

            var result = await _userManager.DeleteAsync(currentUser);

            if (result.Succeeded)
            {
                _notifier.Success(TH["User deleted successfully"]);
            }
            else
            {
                _session.Cancel();

                _notifier.Error(TH["Could not delete the user"]);

                foreach (var error in result.Errors)
                {
                    _notifier.Error(TH[error.Description]);
                }
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
