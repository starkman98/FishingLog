using FishingLog.Api.Endpoints;
using FishingLog.Application.Exceptions;
using FishingLog.Application.Interfaces;
using FishingLog.Application.Services;
using FishingLog.Application.Validators;
using FishingLog.Domain.Interfaces;
using FishingLog.Infrastructure.Persistence;
using FishingLog.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<FishingLogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing connection string.")));

// --- Repositories ---
builder.Services.AddScoped<IFishingTripRepository, FishingTripRepository>();

// --- Services ---
builder.Services.AddScoped<IFishingTripService, FishingTripService>();

// --- Validators (registers all validators in the Application assembly) ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateFishingTripRequestValidator>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy("AllowedConfiguredOrigins", policy =>
        policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FishingLogDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var exceptionFeature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        if (exceptionFeature?.Error is BusinessRuleException businessEx)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = businessEx.Message });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "An unexpected error occurred."
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowedConfiguredOrigins");

app.MapHealthChecks("/health");
app.MapFishingTripEndpoints();

app.Run();

