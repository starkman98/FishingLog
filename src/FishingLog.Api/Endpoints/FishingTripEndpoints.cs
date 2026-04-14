using FishingLog.Application.Interfaces;
using FishingLog.Contracts;

namespace FishingLog.Api.Endpoints;

/// <summary>
/// Registers all fishing trip Minimal API endpoints.
/// Call <see cref="MapFishingTripEndpoints"/> from Program.cs.
/// </summary>
public static class FishingTripEndpoints
{
    /// <summary>Maps all fishing trip routes under /api/fishing-trips.</summary>
    public static void MapFishingTripEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fishing-trips")
            .WithTags("FishingTrips");

        group.MapGet("/", GetAllTrips);
        group.MapGet("/{id:guid}", GetTripById);
        group.MapPost("/", CreateTrip);
        group.MapPut("/{id:guid}", UpdateTrip);
        group.MapDelete("/{id:guid}", DeleteTrip);
    }

    /// <summary>
    /// GET /api/fishing-trips
    /// Optional query parameter: ?modifiedSince=2024-01-01T00:00:00Z
    /// When modifiedSince is provided only trips changed after that timestamp are returned.
    /// This is the endpoint the mobile sync service calls on each sync.
    /// </summary>
    private static async Task<IResult> GetAllTrips(
        IFishingTripService service,
        DateTime? modifiedSince,
        CancellationToken ct)
    {
        var trips = modifiedSince.HasValue
            ? await service.GetModifiedSinceAsync(modifiedSince.Value, ct)
            : await service.GetAllAsync(ct);

        return Results.Ok(trips);
    }

    /// <summary>GET /api/fishing-trips/{id}</summary>
    private static async Task<IResult> GetTripById(
        Guid id,
        IFishingTripService service,
        CancellationToken ct)
    {
        var trip = await service.GetByIdAsync(id, ct);
        return trip is null ? Results.NotFound() : Results.Ok(trip);
    }

    /// <summary>POST /api/fishing-trips → 201 Created</summary>
    private static async Task<IResult> CreateTrip(
        CreateFishingTripRequest request,
        IFishingTripService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        if (request.EndTime.HasValue && request.EndTime < request.StartTime)
            return Results.BadRequest(new { error = "EndTime must be after StartTime." });

        request = request with
        {
            StartTime = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc),
            EndTime = request.EndTime.HasValue
                ? DateTime.SpecifyKind(request.EndTime.Value, DateTimeKind.Utc)
                : null
        };

        var created = await service.CreateAsync(request, ct);
        return Results.Created($"/api/fishing-trips/{created.Id}", created);
    }

    /// <summary>PUT /api/fishing-trips/{id} → 200 OK or 404</summary>
    private static async Task<IResult> UpdateTrip(
        Guid id,
        UpdateFishingTripRequest request,
        IFishingTripService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        if (request.EndTime.HasValue && request.EndTime < request.StartTime)
            return Results.BadRequest(new { error = "EndTime must be after StartTime." });

        request = request with
        {
            StartTime = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc),
            EndTime = request.EndTime.HasValue
                ? DateTime.SpecifyKind(request.EndTime.Value, DateTimeKind.Utc)
                : null
        };

        var updated = await service.UpdateAsync(id, request, ct);
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }

    /// <summary>DELETE /api/fishing-trips/{id} → 204 No Content or 404</summary>
    private static async Task<IResult> DeleteTrip(
        Guid id,
        IFishingTripService service,
        CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
