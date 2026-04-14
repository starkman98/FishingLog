using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FishingLog.Mobile.Data.Entities;
using FishingLog.Mobile.Data.Repositories;
using FishingLog.Mobile.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace FishingLog.Mobile.ViewModels;

/// <summary>
/// ViewModel for the fishing trips list page.
/// Loads local trips, triggers sync and navigates to add/edit.
/// </summary>
public partial class FishingTripsViewModel : BaseViewModel
{
    private readonly IFishingTripLocalRepository _repository;
    private readonly IFishingTripSyncService _syncService;
    private readonly ILogger<FishingTripsViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<FishingTripLocalEntity> _trips = [];

    private bool _isRefreshing;

    /// <summary>Gets or sets whether the pull-to-refresh spinner is active.</summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FishingTripsViewModel"/>.
    /// </summary>
    public FishingTripsViewModel(
        IFishingTripLocalRepository repository,
        IFishingTripSyncService syncService,
        ILogger<FishingTripsViewModel> logger)
    {
        _repository = repository;
        _syncService = syncService;
        _logger = logger;
        Title = "Fishing Trips";
    }

    /// <summary>Loads all non-deleted trips from the local database.</summary>
    [RelayCommand]
    private async Task LoadTripsAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var trips = await _repository.GetAllAsync();
            Trips = new ObservableCollection<FishingTripLocalEntity>(trips);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Runs the sync service then reloads the list.</summary>
    [RelayCommand]
    private async Task SyncAsync()
    {
        _logger.LogInformation("[VM] SyncCommand started.");
        try
        {
            IsRefreshing = true;
            await _syncService.SyncAsync();
            var trips = await _repository.GetAllAsync();
            Trips = new ObservableCollection<FishingTripLocalEntity>(trips);
            _logger.LogInformation("[VM] SyncCommand finished. Trips loaded: {Count}", Trips.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "[VM] SyncCommand: offline/timeout — {Type}: {Message}", ex.GetType().Name, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VM] SyncCommand: unexpected error — {Type}: {Message}", ex.GetType().Name, ex.Message);
        }
        finally
        {
            IsRefreshing = false;
            _logger.LogInformation("[VM] SyncCommand finally. IsRefreshing reset to false.");
        }
    }

    /// <summary>Navigates to the add trip page.</summary>
    [RelayCommand]
    private async Task AddTripAsync()
        => await Shell.Current.GoToAsync("AddEditFishingTripPage");

    /// <summary>Navigates to the edit page for the selected trip.</summary>
    [RelayCommand]
    private async Task SelectTripAsync(FishingTripLocalEntity trip)
        => await Shell.Current.GoToAsync($"AddEditFishingTripPage?localId={trip.Id}");

    /// <summary>Soft-deletes a trip after confirmation.</summary>
    [RelayCommand]
    private async Task DeleteTripAsync(FishingTripLocalEntity trip)
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete trip",
            $"Delete \"{trip.Name}\"?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        await _repository.DeleteAsync(trip.Id);
        Trips.Remove(trip);
    }
}
