using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FishingLog.Mobile.Data.Entities;
using FishingLog.Mobile.Data.Repositories;

namespace FishingLog.Mobile.ViewModels;

/// <summary>
/// ViewModel for add and edit fishing trip page.
/// Receives an optional <c>localId</c> query parameter via Shell navigation.
/// When localId is 0 or absent the page is in Add mode, otherwise Edit mode.
/// </summary>
[QueryProperty(nameof(LocalIdQuery), "localId")]
public partial class AddEditFishingTripViewModel : BaseViewModel
{
    private readonly IFishingTripLocalRepository _repository;
    private int _localId;

    // -------------------------------------------------------------------------
    // Form fields
    // -------------------------------------------------------------------------

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _locationName;
    [ObservableProperty] private string? _note;

    // DatePicker and TimePicker are separate controls in MAUI
    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private TimeSpan _startTimeOfDay = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEndDateVisible))]
    private bool _hasEndDate;

    [ObservableProperty] private DateTime _endDate = DateTime.Today;

    /// <summary>Controls visibility of the end date picker.</summary>
    public bool IsEndDateVisible => HasEndDate;

    // -------------------------------------------------------------------------
    // Shell query property — called by MAUI when navigating with ?localId=n
    // -------------------------------------------------------------------------

    /// <summary>
    /// Set by Shell navigation when editing an existing trip.
    /// Uses a string because Shell always passes query parameters as strings.
    /// </summary>
    public string? LocalIdQuery
    {
        set
        {
            if (int.TryParse(value, out var id) && id > 0)
                _ = LoadTripAsync(id);
        }
    }

    /// <summary>True when editing an existing trip, false when adding a new one.</summary>
    public bool IsEditMode => _localId > 0;

    /// <summary>
    /// Initializes a new instance of <see cref="AddEditFishingTripViewModel"/>.
    /// </summary>
    public AddEditFishingTripViewModel(IFishingTripLocalRepository repository)
    {
        _repository = repository;
        Title = "New Trip";
    }

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------

    /// <summary>Validates and saves the trip locally, then navigates back.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Name is required.", "OK");
            return;
        }

        var startTime = StartDate.Date + StartTimeOfDay;
        var endTime = HasEndDate ? (DateTime?)EndDate.Date : null;

        if (endTime.HasValue && endTime < startTime)
        {
            await Shell.Current.DisplayAlertAsync("Validation", "End date must be after start date.", "OK");
            return;
        }

        if (IsEditMode)
            await UpdateExistingTripAsync(startTime, endTime);
        else
            await AddNewTripAsync(startTime, endTime);

        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Navigates back without saving.</summary>
    [RelayCommand]
    private async Task CancelAsync()
        => await Shell.Current.GoToAsync("..");

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task LoadTripAsync(int id)
    {
        var trip = await _repository.GetByIdAsync(id);
        if (trip is null) return;

        _localId = id;
        Name = trip.Name;
        LocationName = trip.LocationName;
        Note = trip.Note;
        StartDate = trip.StartTime.Date;
        StartTimeOfDay = trip.StartTime.TimeOfDay;
        HasEndDate = trip.EndTime.HasValue;
        EndDate = trip.EndTime?.Date ?? DateTime.Today;
        Title = "Edit Trip";
    }

    private async Task AddNewTripAsync(DateTime startTime, DateTime? endTime)
    {
        var trip = new FishingTripLocalEntity
        {
            Name = Name,
            LocationName = LocationName,
            Note = Note,
            StartTime = startTime,
            EndTime = endTime,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(trip);
    }

    private async Task UpdateExistingTripAsync(DateTime startTime, DateTime? endTime)
    {
        var trip = await _repository.GetByIdAsync(_localId);
        if (trip is null) return;

        trip.Name = Name;
        trip.LocationName = LocationName;
        trip.Note = Note;
        trip.StartTime = startTime;
        trip.EndTime = endTime;
        await _repository.UpdateAsync(trip);
    }
}