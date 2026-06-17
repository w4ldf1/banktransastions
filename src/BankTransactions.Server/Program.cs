using BankTransactions.Server.Data;
using BankTransactions.Server.Security;
using BankTransactions.Server.Services;
using BankTransactions.Shared;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BankDb")
    ?? "Data Source=bank-transactions.db";

builder.Services.AddDbContext<BankingDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<BankingService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            DbUpdateException => StatusCodes.Status409Conflict,
            Microsoft.Data.Sqlite.SqliteException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        await context.Response.WriteAsJsonAsync(new ApiError(exception?.Message ?? "Unexpected server error."));
    });
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.Equals("/", StringComparison.Ordinal)
        || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var tokenService = context.RequestServices.GetRequiredService<TokenService>();
    var user = tokenService.Validate(context.Request.Headers.Authorization);
    if (user is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new ApiError("Authorization token is missing or invalid."));
        return;
    }

    context.Items["User"] = user;
    await next();
});

app.MapGet("/", () => Results.Ok(new { Service = "Bank transactions API", Variant = 9 }));
app.MapGet("/health", () => Results.Ok(new { Status = "ok", Utc = DateTime.UtcNow }));

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    BankingDbContext db,
    PasswordHasher passwordHasher,
    TokenService tokenService) =>
{
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Username == request.Username.Trim());
    if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var token = tokenService.Issue(new AuthenticatedUser(user.Id, user.Username, user.Role));
    return Results.Ok(new AuthResponse(token, user.Username, user.Role));
});

app.MapGet("/api/customers", async (string? mode, string? search, BankingService service) =>
    Results.Ok(await service.SearchCustomersAsync(DataAccessModeParser.Parse(mode), search)));

app.MapPost("/api/customers", async (string? mode, CustomerCreateRequest request, BankingService service) =>
    Results.Created("/api/customers", await service.CreateCustomerAsync(DataAccessModeParser.Parse(mode), request)));

app.MapPut("/api/customers/{id:int}", async (int id, string? mode, CustomerUpdateRequest request, BankingService service) =>
    Results.Ok(await service.UpdateCustomerAsync(id, DataAccessModeParser.Parse(mode), request)));

app.MapDelete("/api/customers/{id:int}", async (int id, string? mode, BankingService service) =>
{
    await service.DeleteCustomerAsync(id, DataAccessModeParser.Parse(mode));
    return Results.NoContent();
});

app.MapGet("/api/partner-banks", async (string? mode, string? search, BankingService service) =>
    Results.Ok(await service.SearchPartnerBanksAsync(DataAccessModeParser.Parse(mode), search)));

app.MapPost("/api/partner-banks", async (string? mode, PartnerBankCreateRequest request, BankingService service) =>
    Results.Created("/api/partner-banks", await service.CreatePartnerBankAsync(DataAccessModeParser.Parse(mode), request)));

app.MapPut("/api/partner-banks/{id:int}", async (int id, string? mode, PartnerBankUpdateRequest request, BankingService service) =>
    Results.Ok(await service.UpdatePartnerBankAsync(id, DataAccessModeParser.Parse(mode), request)));

app.MapDelete("/api/partner-banks/{id:int}", async (int id, string? mode, BankingService service) =>
{
    await service.DeletePartnerBankAsync(id, DataAccessModeParser.Parse(mode));
    return Results.NoContent();
});

app.MapGet("/api/accounts", async (string? mode, string? search, BankingService service) =>
    Results.Ok(await service.SearchAccountsAsync(DataAccessModeParser.Parse(mode), search)));

app.MapPost("/api/accounts", async (string? mode, AccountCreateRequest request, BankingService service) =>
    Results.Created("/api/accounts", await service.CreateAccountAsync(DataAccessModeParser.Parse(mode), request)));

app.MapPut("/api/accounts/{id:int}", async (int id, string? mode, AccountUpdateRequest request, BankingService service) =>
    Results.Ok(await service.UpdateAccountAsync(id, DataAccessModeParser.Parse(mode), request)));

app.MapDelete("/api/accounts/{id:int}", async (int id, string? mode, BankingService service) =>
{
    await service.DeleteAccountAsync(id, DataAccessModeParser.Parse(mode));
    return Results.NoContent();
});

app.MapGet("/api/transactions", async (string? mode, string? search, BankingService service) =>
    Results.Ok(await service.SearchTransactionsAsync(DataAccessModeParser.Parse(mode), search)));

app.MapPost("/api/transactions", async (string? mode, TransactionCreateRequest request, BankingService service) =>
    Results.Created("/api/transactions", await service.CreateTransactionAsync(DataAccessModeParser.Parse(mode), request)));

app.MapPut("/api/transactions/{id:int}", async (int id, string? mode, TransactionUpdateRequest request, BankingService service) =>
    Results.Ok(await service.UpdateTransactionAsync(id, DataAccessModeParser.Parse(mode), request)));

app.MapDelete("/api/transactions/{id:int}", async (int id, string? mode, BankingService service) =>
{
    await service.DeleteTransactionAsync(id, DataAccessModeParser.Parse(mode));
    return Results.NoContent();
});

app.Run();

public partial class Program;
