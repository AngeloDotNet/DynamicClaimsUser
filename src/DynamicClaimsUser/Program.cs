
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace DynamicClaimsUser;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.Services.AddDbContext<ApplicationDbContext>(options
			=> options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

		var jwtSettings = Configure<JwtSettings>(nameof(JwtSettings), builder);

		// Configura Identity
		builder.Services.AddIdentity<IdentityUser, IdentityRole>()
			.AddEntityFrameworkStores<ApplicationDbContext>()
			.AddDefaultTokenProviders();

		// Configura l'autenticazione JWT
		builder.Services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
			options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
		})
		.AddJwtBearer(options =>
		{
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				ValidIssuer = jwtSettings.Issuer,
				ValidAudience = jwtSettings.Audience,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
			};
		});

		// Configura l'autorizzazione
		builder.Services.AddAuthorization();

		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();

		var app = builder.Build();

		if (app.Environment.IsDevelopment())
		{
			app.UseSwagger();
			app.UseSwaggerUI();
		}

		// Configura il middleware per l'autenticazione e l'autorizzazione
		app.UseAuthentication();
		app.UseAuthorization();

		// Endpoint per gestire i claims dinamici
		app.MapPost("/claims", [Authorize] async (ClaimModel claimModel, ApplicationDbContext context) =>
		{
			var dynamicClaim = new DynamicClaim { Type = claimModel.Type, Value = claimModel.Value };
			context.DynamicClaims.Add(dynamicClaim);
			await context.SaveChangesAsync();
			return Results.Ok(dynamicClaim);
		});

		app.MapGet("/claims", [Authorize] async (ApplicationDbContext context) =>
		{
			var claims = await context.DynamicClaims.ToListAsync();
			return Results.Ok(claims);
		});

		app.MapPost("/users/{userId}/claims", [Authorize] async (string userId, [FromBody] ClaimModel claimModel, UserManager<IdentityUser> userManager, ApplicationDbContext context) =>
		{
			var user = await userManager.FindByIdAsync(userId);

			if (user == null)
			{
				return Results.NotFound();
			}

			var dynamicClaim = await context.DynamicClaims.FirstOrDefaultAsync(dc => dc.Type == claimModel.Type && dc.Value == claimModel.Value);

			if (dynamicClaim == null)
			{
				return Results.BadRequest("Claim not found.");
			}

			var claim = new Claim(claimModel.Type, claimModel.Value);
			var result = await userManager.AddClaimAsync(user, claim);

			if (result.Succeeded)
			{
				return Results.Ok();
			}

			return Results.BadRequest(result.Errors);
		});

		app.MapGet("/users/{userId}/claims", [Authorize] async (string userId, UserManager<IdentityUser> userManager) =>
		{
			var user = await userManager.FindByIdAsync(userId);
			if (user == null)
			{
				return Results.NotFound();
			}

			var claims = await userManager.GetClaimsAsync(user);
			return Results.Ok(claims.Select(c => new { c.Type, c.Value }));
		});

		app.MapDelete("/users/{userId}/claims", [Authorize] async (string userId, [FromBody] ClaimModel claimModel, UserManager<IdentityUser> userManager) =>
		{
			var user = await userManager.FindByIdAsync(userId);
			if (user == null)
			{
				return Results.NotFound();
			}

			var claim = new Claim(claimModel.Type, claimModel.Value);
			var result = await userManager.RemoveClaimAsync(user, claim);

			if (result.Succeeded)
			{
				return Results.Ok();
			}

			return Results.BadRequest(result.Errors);
		});

		app.Run();
	}

	private static T Configure<T>(string sectionName, WebApplicationBuilder builder) where T : class
	{
		var section = builder.Configuration.GetSection(sectionName);
		var settings = section.Get<T>();

		builder.Services.Configure<T>(section);

		return settings!;
	}
}
