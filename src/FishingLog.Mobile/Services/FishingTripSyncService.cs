using FishingLog.Contracts;
using FishingLog.Mobile.Data;
using FishingLog.Mobile.Data.Entities;
using FishingLog.Mobile.Data.Repositories;

namespace FishingLog.Mobile.Services;

/// <summary>
/// Two-way sync between the local SQLite database and the FishingLog REST API.
/// <para>
/// Conflict resolution: last-write-wins based on <c>LastModified</c> timestamp.
/// If a local record is dirty and newer than the server version, local wins.
/// If the server version is newer, the server wins.
/// </para>
/// </summary>
public class FishingTripSyncService : IFishingTripSyncService
{
    private readonly IFishingTripLocalRepository _localRepository;
    private readonly ISyncMetadataRepository _syncMetadata;
    private readonly IFishingTripApiClient _apiClient;

    /// <summary>
    /// Initializes a new instance of <see cref="FishingTripSyncService"/>.
    /// </summary>
    public FishingTripSyncService(
        IFishingTripLocalRepository localRepository,
        ISyncMetadataRepository syncMetadata,
        IFishingTripApiClient apiClient)
    {
        _localRepository = localRepository;
        _syncMetadata = syncMetadata;
        _apiClient = apiClient;
    }

    /// <inheritdoc/>
    public async Task SyncAsync(CancellationToken ct = default)
    {
        await UploadDirtyTripsAsync(ct);
        await DownloadRemoteChangesAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Step 1 — Upload
    // -------------------------------------------------------------------------

    private async Task UploadDirtyTripsAsync(CancellationToken ct)
    {
        var dirtyTrips = await _localRepository.GetDirtyAsync(ct);

        foreach (var trip in dirtyTrips)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                if (trip.ServerId is null)
                    await UploadNewTripAsync(trip, ct);
                else if (trip.IsDeleted)
                    await DeleteTripOnServerAsync(trip, ct);
                else
                    await UpdateTripOnServerAsync(trip, ct);
            }
            catch (HttpRequestException)
            {
                // Network error — leave dirty, will retry on the next sync cycle
            }
        }
    }

    private async Task UploadNewTripAsync(FishingTripLocalEntity trip, CancellationToken ct)
    {
        var response = await _apiClient.CreateAsync(MapToCreateRequest(trip), ct);

        if (response is not null)
            await _localRepository.MarkAsSyncedAsync(trip.Id, response.Id, response.LastModified, ct);
    }

    private async Task UpdateTripOnServerAsync(FishingTripLocalEntity trip, CancellationToken ct)
    {
        if (!Guid.TryParse(trip.ServerId, out var serverId))
            return;

        var response = await _apiClient.UpdateAsync(serverId, MapToUpdateRequest(trip), ct);

        if (response is not null)
            await _localRepository.MarkAsSyncedAsync(trip.Id, serverId, response.LastModified, ct);
    }

    private async Task DeleteTripOnServerAsync(FishingTripLocalEntity trip, CancellationToken ct)
    {
        if (!Guid.TryParse(trip.ServerId, out var serverId))
            return;

        var deleted = await _apiClient.DeleteAsync(serverId, ct);

        if (deleted)
            await _localRepository.MarkAsSyncedAsync(trip.Id, serverId, trip.LastModifiedUtc, ct);
    }

    // -------------------------------------------------------------------------
    // Step 2 — Download
    // -------------------------------------------------------------------------

    private async Task DownloadRemoteChangesAsync(CancellationToken ct)
    {
        var lastSync = await _syncMetadata.GetLastSyncAsync(SyncEntityType.FishingTrip, ct);

        // If never synced before, use DateTime.MinValue to get everything
        var syncFrom = lastSync ?? DateTime.MinValue;

        var remoteTrips = await _apiClient.GetModifiedSinceAsync(syncFrom, ct);

        foreach (var remoteTrip in remoteTrips)
        {
            if (ct.IsCancellationRequested)
                break;

            await UpsertRemoteTripAsync(remoteTrip, ct);
        }

        // Advance the sync cursor so the next sync only downloads new changes
        if (remoteTrips.Count > 0)
            await _syncMetadata.SetLastSyncAsync(SyncEntityType.FishingTrip, DateTime.UtcNow, ct);
    }

    private async Task UpsertRemoteTripAsync(FishingTripResponse remoteTrip, CancellationToken ct)
    {
        var existing = await _localRepository.GetByServerIdAsync(remoteTrip.Id, ct);

        if (existing is null)
        {
            // Not in local DB at all — insert as a clean record
            await _localRepository.SaveFromServerAsync(MapToLocalEntity(remoteTrip), ct);
        }
        else if (existing.IsDirty && existing.LastModifiedUtc > remoteTrip.LastModified)
        {
            // Local is dirty and newer — local wins, skip
            // The upload step above will push local changes to the server
        }
        else
        {
            // Server is newer, or local is clean — apply server changes
            ApplyRemoteToLocal(existing, remoteTrip);
            await _localRepository.SaveFromServerAsync(existing, ct);
        }
    }

    // -------------------------------------------------------------------------
    // Mapping helpers
    // -------------------------------------------------------------------------

    private static CreateFishingTripRequest MapToCreateRequest(FishingTripLocalEntity t) => new(
        t.Name,
        t.LocationName,
        t.WaterTemp,
        t.WeatherDescription,
        t.Latitude,
        t.Longitude,
        t.StartTime,
        t.EndTime,
        t.Note);

    private static UpdateFishingTripRequest MapToUpdateRequest(FishingTripLocalEntity t) => new(
        t.Name,
        t.LocationName,
        t.WaterTemp,
        t.WeatherDescription,
        t.Latitude,
        t.Longitude,
        t.StartTime,
        t.EndTime,
        t.Note);

    private static FishingTripLocalEntity MapToLocalEntity(FishingTripResponse r) => new()
    {
        ServerId = r.Id.ToString(),
        LastModifiedUtc = r.LastModified,
        IsDirty = false,
        IsDeleted = false,
        Name = r.Name,
        LocationName = r.LocationName,
        WaterTemp = r.WaterTemp,
        WeatherDescription = r.WeatherDescription,
        Latitude = r.Latitude,
        Longitude = r.Longitude,
        StartTime = r.StartTime,
        EndTime = r.EndTime,
        Note = r.Note,
        CreatedAt = r.CreatedAt
    };

    private static void ApplyRemoteToLocal(FishingTripLocalEntity local, FishingTripResponse remote)
    {
        local.Name = remote.Name;
        local.LocationName = remote.LocationName;
        local.WaterTemp = remote.WaterTemp;
        local.WeatherDescription = remote.WeatherDescription;
        local.Latitude = remote.Latitude;
        local.Longitude = remote.Longitude;
        local.StartTime = remote.StartTime;
        local.EndTime = remote.EndTime;
        local.Note = remote.Note;
        local.LastModifiedUtc = remote.LastModified;
        local.IsDirty = false;
    }
}
