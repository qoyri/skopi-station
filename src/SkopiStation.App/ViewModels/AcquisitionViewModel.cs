using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SkopiStation.App.Threading;
using SkopiStation.Data;
using SkopiStation.Devices;
using SkopiStation.Domain;

namespace SkopiStation.App.ViewModels;

/// <summary>Acquisition screen: connection to the device, live feed, and filing to a patient.</summary>
/// <remarks>
/// The device raises its events on a background thread. Every mutation of an observable member is
/// therefore routed through <see cref="IUiDispatcher"/> rather than applied where the event
/// arrives. See the README for why the dispatcher was preferred to
/// <c>BindingOperations.EnableCollectionSynchronization</c>.
/// </remarks>
public sealed partial class AcquisitionViewModel : ObservableObject, IDisposable
{
    private readonly IMeasurementDevice device;
    private readonly IPatientRepository repository;
    private readonly PatientListViewModel patients;
    private readonly IUiDispatcher dispatcher;
    private readonly ILogger<AcquisitionViewModel> logger;

    [ObservableProperty]
    private string? selectedPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private DeviceState state = DeviceState.Disconnected;

    [ObservableProperty]
    private string statusMessage = "Not connected.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AttachCommand))]
    private ReceivedMeasurementViewModel? selectedMeasurement;

    public AcquisitionViewModel(
        IMeasurementDevice device,
        IPatientRepository repository,
        PatientListViewModel patients,
        IUiDispatcher dispatcher,
        ILogger<AcquisitionViewModel> logger)
    {
        this.device = device;
        this.repository = repository;
        this.patients = patients;
        this.dispatcher = dispatcher;
        this.logger = logger;

        device.MeasurementReceived += OnMeasurementReceived;
        device.StateChanged += OnStateChanged;

        patients.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PatientListViewModel.SelectedPatient))
            {
                OnPropertyChanged(nameof(SelectedPatientName));
                AttachCommand.NotifyCanExecuteChanged();
            }
        };

        ManualEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ManualEntryViewModel.CanSubmit))
            {
                AddManualEntryCommand.NotifyCanExecuteChanged();
            }
        };

        RefreshPorts();
    }

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public ObservableCollection<ReceivedMeasurementViewModel> ReceivedMeasurements { get; } = [];

    public ManualEntryViewModel ManualEntry { get; } = new();

    public bool IsConnected => State is DeviceState.Connected;

    public string SelectedPatientName =>
        patients.SelectedPatient is { } patient ? patient.ToString() : "no patient selected";

    public void Dispose()
    {
        device.MeasurementReceived -= OnMeasurementReceived;
        device.StateChanged -= OnStateChanged;
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();

        foreach (var port in SerialMeasurementDevice.AvailablePortNames())
        {
            AvailablePorts.Add(port);
        }

        // The fake device ignores the name, but the drop-down must still offer something to pick
        // when the machine has no serial port at all.
        if (AvailablePorts.Count == 0)
        {
            AvailablePorts.Add("SIMULATED");
        }

        SelectedPort ??= AvailablePorts.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken ct)
    {
        if (SelectedPort is null)
        {
            return;
        }

        try
        {
            await device.ConnectAsync(SelectedPort, ct);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not connect to {Port}", SelectedPort);
            StatusMessage = $"Could not connect to {SelectedPort}: {exception.Message}";
        }
    }

    private bool CanConnect() => State is DeviceState.Disconnected or DeviceState.Faulted;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        try
        {
            // Readings already received stay in the list: they are the operator's work, and losing
            // them because the cable was pulled would be the worst possible moment to lose them.
            await device.DisconnectAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not disconnect cleanly");
            StatusMessage = $"Disconnected with an error: {exception.Message}";
        }
    }

    private bool CanDisconnect() => State is DeviceState.Connected or DeviceState.Connecting;

    [RelayCommand(CanExecute = nameof(CanAttach))]
    private async Task AttachAsync(CancellationToken ct)
    {
        if (SelectedMeasurement is not { } received || patients.SelectedPatient is not { } patient)
        {
            return;
        }

        try
        {
            await repository.AddMeasurementAsync(received.ToMeasurement(patient.Id), ct);
            received.AttachedTo = patient.ToString();
            await patients.MeasurementAddedAsync(patient.Id, ct);
            StatusMessage = $"Filed under {patient}.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not file the measurement");
            StatusMessage = $"Could not file the measurement: {exception.Message}";
        }
    }

    private bool CanAttach() =>
        SelectedMeasurement is { IsAttached: false } && patients.SelectedPatient is not null;

    [RelayCommand(CanExecute = nameof(CanAddManualEntry))]
    private void AddManualEntry()
    {
        // The command is disabled when the form is invalid, but RelayCommand.Execute does not
        // consult CanExecute, so the guard stays for callers that invoke it directly.
        if (!ManualEntry.CanSubmit)
        {
            return;
        }

        var received = new ReceivedMeasurementViewModel(ManualEntry.ToFrame(), MeasurementSource.Manual);
        ReceivedMeasurements.Insert(0, received);
        SelectedMeasurement = received;
        ManualEntry.Reset();
        StatusMessage = "Manual reading added to the list; file it to a patient to record it.";
    }

    private bool CanAddManualEntry() => ManualEntry.CanSubmit;

    private void OnMeasurementReceived(object? sender, MeasurementReceivedEventArgs e) =>
        dispatcher.Post(() =>
            ReceivedMeasurements.Insert(0, new ReceivedMeasurementViewModel(e.Frame, MeasurementSource.Device)));

    private void OnStateChanged(object? sender, DeviceStateChangedEventArgs e) =>
        dispatcher.Post(() =>
        {
            State = e.State;
            StatusMessage = e.State switch
            {
                DeviceState.Connecting => $"Connecting to {e.Message}…",
                DeviceState.Connected => $"Connected to {e.Message}.",
                DeviceState.Faulted => $"Device error: {e.Message}",
                _ => "Not connected.",
            };
        });
}
