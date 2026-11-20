using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
var entra = builder.Configuration.GetRequiredSection("Entra");
if (!Guid.TryParse(entra["TenantId"], out var tenant) || tenant == Guid.Empty ||
    !Guid.TryParse(entra["ClientId"], out var client) || client == Guid.Empty ||
    string.IsNullOrWhiteSpace(entra["ClientSecret"]))
{
    throw new InvalidOperationException("Configure the LCS Entra tenant ID, client ID and client credential.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.AccessDeniedPath = "/AccessDenied";
    options.Cookie.Name = "__Host-LcsLifecycle";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.SlidingExpiration = false;
})
.AddOpenIdConnect(options =>
{
    options.Authority = $"https://login.microsoftonline.com/{tenant:D}/v2.0";
    options.ClientId = client.ToString("D");
    options.ClientSecret = entra["ClientSecret"];
    options.ResponseType = "code";
    options.UsePkce = true;
    options.MapInboundClaims = false;
    options.TokenValidationParameters.NameClaimType = "name";
    options.TokenValidationParameters.RoleClaimType = "roles";
    options.SaveTokens = false;
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
        .RequireRole("Lcs.Registrar", "Lcs.DeviceStaff", "Lcs.Administrator").Build());
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHealthChecks("/health/live").AllowAnonymous();
app.Run();
