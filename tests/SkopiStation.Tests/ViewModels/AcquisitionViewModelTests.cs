using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkopiStation.App.Threading;
using SkopiStation.App.ViewModels;
using SkopiStation.Devices;
using SkopiStation.Domain;
using SkopiStation.Tests.Fakes;

namespace SkopiStation.Tests.ViewModels;

public class AcquisitionViewModelTests
{
    private const string ValidPressure = "MEAS|IntraocularPressure|16.4|mmHg|2026-09-15T10:22:31Z";
    private const string OutOfRangePressure = "MEAS|IntraocularPressure|24.9|mmHg|2026-09-15T10:25:00Z";

    /// <summary>Runs posted work inline: a unit test host has no WPF Dispatcher to marshal to.</summary>
    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private static async Task<(AcquisitionViewModel Acquisition,
        FakeMeasurementDevice Device,
        FakePatientRepository Repository,
        PatientListViewModel Patients)> CreateAsync()
    {
        var repository = new FakePatientRepository();
        repository.Add("SK-000001", "Moreau", "Camille", new DateOnly(1980, 3, 14));

        var patients = new PatientListViewModel(repository, new PatientEditorViewModel(repository));
        await patients.LoadAsync(CancellationToken.None);

        var device = new FakeMeasurementDevice(NullLogger<FakeMeasurementDevice>.Instance);
        var acquisition = new AcquisitionViewModel(
            device,
            repository,
            patients,
            new ImmediateDispatcher(),
            NullLogger<AcquisitionViewModel>.Instance);

        return (acquisition, device, repository, patients);
    }

    private static PatientListItemViewModel FirstPatient(PatientListViewModel patients) =>
        patients.PatientsView.Cast<PatientListItemViewModel>().First();

    [Fact]
    public async Task A_received_measurement_reaches_the_collection()
    {
        var (acquisition, device, _, _) = await CreateAsync();

        device.Emit(ValidPressure);

        acquisition.ReceivedMeasurements.Should().ContainSingle()
            .Which.Value.Should().Be(16.4m);
    }

    [Fact]
    public async Task A_malformed_frame_never_reaches_the_collection()
    {
        var (acquisition, device, _, _) = await CreateAsync();

        device.Emit("MEAS|IntraocularPressure|16.4|kPa|2026-09-15T10:22:31Z");
        device.Emit("garbage");

        acquisition.ReceivedMeasurements.Should().BeEmpty();
    }

    [Fact]
    public async Task The_most_recent_reading_is_listed_first()
    {
        var (acquisition, device, _, _) = await CreateAsync();

        device.Emit(ValidPressure);
        device.Emit(OutOfRangePressure);

        acquisition.ReceivedMeasurements[0].Value.Should().Be(24.9m);
    }

    [Fact]
    public async Task A_reading_outside_the_reference_range_is_kept_and_flagged()
    {
        var (acquisition, device, _, _) = await CreateAsync();

        device.Emit(OutOfRangePressure);

        acquisition.ReceivedMeasurements.Should().ContainSingle()
            .Which.IsOutOfRange.Should().BeTrue();
    }

    [Fact]
    public async Task Readings_survive_a_disconnection()
    {
        var (acquisition, device, _, _) = await CreateAsync();
        await device.ConnectAsync("FAKE", CancellationToken.None);
        device.Emit(ValidPressure);
        device.Emit(OutOfRangePressure);

        await device.DisconnectAsync();

        acquisition.ReceivedMeasurements.Should().HaveCount(2);
        acquisition.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Filing_a_reading_records_it_against_the_selected_patient()
    {
        var (acquisition, device, repository, patients) = await CreateAsync();
        var patient = FirstPatient(patients);
        patients.SelectedPatient = patient;
        device.Emit(ValidPressure);
        acquisition.SelectedMeasurement = acquisition.ReceivedMeasurements[0];

        await acquisition.AttachCommand.ExecuteAsync(null);

        repository.SavedMeasurements.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                PatientId = patient.Id,
                Kind = MeasurementKind.IntraocularPressure,
                Value = 16.4m,
                Unit = "mmHg",
                Source = MeasurementSource.Device,
            });
    }

    [Fact]
    public async Task Filing_updates_the_measurement_count_shown_in_the_list()
    {
        var (acquisition, device, _, patients) = await CreateAsync();
        var patient = FirstPatient(patients);
        patients.SelectedPatient = patient;
        var before = patient.MeasurementCount;
        device.Emit(ValidPressure);
        acquisition.SelectedMeasurement = acquisition.ReceivedMeasurements[0];

        await acquisition.AttachCommand.ExecuteAsync(null);

        patient.MeasurementCount.Should().Be(before + 1);
    }

    [Fact]
    public async Task A_reading_cannot_be_filed_twice()
    {
        var (acquisition, device, _, patients) = await CreateAsync();
        patients.SelectedPatient = FirstPatient(patients);
        device.Emit(ValidPressure);
        acquisition.SelectedMeasurement = acquisition.ReceivedMeasurements[0];

        await acquisition.AttachCommand.ExecuteAsync(null);

        acquisition.SelectedMeasurement.IsAttached.Should().BeTrue();
        acquisition.AttachCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task A_reading_cannot_be_filed_without_a_patient()
    {
        var (acquisition, device, repository, patients) = await CreateAsync();
        patients.SelectedPatient = null;
        device.Emit(ValidPressure);
        acquisition.SelectedMeasurement = acquisition.ReceivedMeasurements[0];

        acquisition.AttachCommand.CanExecute(null).Should().BeFalse();
        repository.SavedMeasurements.Should().BeEmpty();
    }

    [Fact]
    public async Task A_manual_reading_is_added_with_the_manual_source()
    {
        var (acquisition, _, _, _) = await CreateAsync();
        acquisition.ManualEntry.Kind = MeasurementKind.CornealThickness;
        acquisition.ManualEntry.Value = "548";

        acquisition.AddManualEntryCommand.Execute(null);

        acquisition.ReceivedMeasurements.Should().ContainSingle()
            .Which.Source.Should().Be(MeasurementSource.Manual);
    }

    [Fact]
    public async Task An_implausible_manual_reading_is_refused()
    {
        var (acquisition, _, _, _) = await CreateAsync();
        acquisition.ManualEntry.Kind = MeasurementKind.IntraocularPressure;
        acquisition.ManualEntry.Value = "500";

        acquisition.ManualEntry.CanSubmit.Should().BeFalse();
        acquisition.AddManualEntryCommand.CanExecute(null).Should().BeFalse();
        acquisition.AddManualEntryCommand.Execute(null);

        acquisition.ReceivedMeasurements.Should().BeEmpty();
    }

    [Fact]
    public async Task The_add_command_follows_the_validity_of_the_manual_form()
    {
        var (acquisition, _, _, _) = await CreateAsync();

        acquisition.AddManualEntryCommand.CanExecute(null).Should().BeFalse();

        acquisition.ManualEntry.Value = "17.2";
        acquisition.AddManualEntryCommand.CanExecute(null).Should().BeTrue();

        acquisition.ManualEntry.Value = "500";
        acquisition.AddManualEntryCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task A_manual_reading_with_a_comma_is_refused()
    {
        var (acquisition, _, _, _) = await CreateAsync();
        acquisition.ManualEntry.Value = "2,5";

        acquisition.ManualEntry.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public async Task Changing_the_kind_revalidates_the_value_against_the_new_range()
    {
        var (acquisition, _, _, _) = await CreateAsync();
        acquisition.ManualEntry.Kind = MeasurementKind.CornealThickness;
        acquisition.ManualEntry.Value = "548";
        acquisition.ManualEntry.CanSubmit.Should().BeTrue();

        acquisition.ManualEntry.Kind = MeasurementKind.IntraocularPressure;

        acquisition.ManualEntry.CanSubmit.Should().BeFalse();
        acquisition.ManualEntry.Unit.Should().Be("mmHg");
    }

    [Fact]
    public async Task Connecting_and_disconnecting_drives_the_state_indicator()
    {
        var (acquisition, device, _, _) = await CreateAsync();

        await device.ConnectAsync("FAKE", CancellationToken.None);
        acquisition.IsConnected.Should().BeTrue();
        acquisition.StatusMessage.Should().Contain("FAKE");

        await device.DisconnectAsync();
        acquisition.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Disposing_stops_listening_to_the_device()
    {
        var (acquisition, device, _, _) = await CreateAsync();

        acquisition.Dispose();
        device.Emit(ValidPressure);

        acquisition.ReceivedMeasurements.Should().BeEmpty();
    }
}
