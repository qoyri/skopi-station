using System.ComponentModel;
using FluentAssertions;
using SkopiStation.App.ViewModels;
using SkopiStation.Domain;
using SkopiStation.Tests.Fakes;

namespace SkopiStation.Tests.ViewModels;

public class PatientListViewModelTests
{
    private static async Task<(PatientListViewModel ViewModel, FakePatientRepository Repository)> CreateAsync()
    {
        var repository = new FakePatientRepository();
        repository.Add("SK-000001", "Moreau", "Camille", new DateOnly(1980, 3, 14));
        repository.Add("SK-000002", "Lefèvre", "Ana", new DateOnly(1975, 7, 2));
        repository.Add("SK-000003", "Bernard", "Camille", new DateOnly(1990, 11, 30));

        var viewModel = new PatientListViewModel(repository, new PatientEditorViewModel(repository));
        await viewModel.LoadAsync(CancellationToken.None);

        return (viewModel, repository);
    }

    private static IReadOnlyList<string> VisibleRecordNumbers(PatientListViewModel viewModel) =>
    [
        .. viewModel.PatientsView.Cast<PatientListItemViewModel>().Select(patient => patient.RecordNumber),
    ];

    [Fact]
    public async Task All_patients_are_visible_when_the_search_box_is_empty()
    {
        var (viewModel, _) = await CreateAsync();

        VisibleRecordNumbers(viewModel).Should().HaveCount(3);
    }

    [Fact]
    public async Task Filtering_matches_the_last_name()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.SearchText = "moreau";

        VisibleRecordNumbers(viewModel).Should().Equal("SK-000001");
    }

    [Fact]
    public async Task Filtering_matches_the_first_name_and_can_return_several_patients()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.SearchText = "camille";

        VisibleRecordNumbers(viewModel).Should().BeEquivalentTo(["SK-000001", "SK-000003"]);
    }

    [Fact]
    public async Task Filtering_matches_the_record_number()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.SearchText = "SK-000002";

        VisibleRecordNumbers(viewModel).Should().Equal("SK-000002");
    }

    [Fact]
    public async Task Filtering_that_matches_nothing_returns_an_empty_view()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.SearchText = "zzz";

        VisibleRecordNumbers(viewModel).Should().BeEmpty();
    }

    [Fact]
    public async Task Clearing_the_search_box_restores_every_patient()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.SearchText = "moreau";
        viewModel.SearchText = string.Empty;

        VisibleRecordNumbers(viewModel).Should().HaveCount(3);
    }

    /// <summary>
    /// The point of going through ICollectionView is that the backing collection is never rebuilt:
    /// filtering only changes what the view exposes. Holding a reference to the view across several
    /// searches is what proves it — a rebuilt collection would mean a new view instance.
    /// </summary>
    [Fact]
    public async Task Filtering_reuses_the_same_view_instance()
    {
        var (viewModel, _) = await CreateAsync();
        var view = viewModel.PatientsView;

        viewModel.SearchText = "moreau";
        viewModel.SearchText = "bernard";

        viewModel.PatientsView.Should().BeSameAs(view);
    }

    [Fact]
    public async Task Patients_are_sorted_by_last_name_by_default()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.PatientsView.Cast<PatientListItemViewModel>()
            .Select(patient => patient.LastName)
            .Should().Equal("Bernard", "Lefèvre", "Moreau");
    }

    [Fact]
    public async Task Sorting_is_expressed_through_the_view_sort_descriptions()
    {
        var (viewModel, _) = await CreateAsync();

        viewModel.PatientsView.SortDescriptions.Clear();
        viewModel.PatientsView.SortDescriptions.Add(
            new SortDescription(nameof(PatientListItemViewModel.RecordNumber), ListSortDirection.Descending));

        VisibleRecordNumbers(viewModel).Should().Equal("SK-000003", "SK-000002", "SK-000001");
    }

    [Fact]
    public async Task Selecting_a_patient_loads_their_measurements()
    {
        var repository = new FakePatientRepository();
        var summary = repository.Add("SK-000001", "Moreau", "Camille", new DateOnly(1980, 3, 14));
        repository.AddMeasurement(summary.Id, MeasurementKind.IntraocularPressure, 16.4m);
        repository.AddMeasurement(summary.Id, MeasurementKind.AxialLength, 23.8m);

        var viewModel = new PatientListViewModel(repository, new PatientEditorViewModel(repository));
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.SelectedPatient = viewModel.PatientsView.Cast<PatientListItemViewModel>().Single();

        viewModel.Measurements.Should().HaveCount(2);
    }

    [Fact]
    public async Task Clearing_the_selection_empties_the_detail_panel()
    {
        var (viewModel, _) = await CreateAsync();
        viewModel.SelectedPatient = viewModel.PatientsView.Cast<PatientListItemViewModel>().First();

        viewModel.SelectedPatient = null;

        viewModel.Measurements.Should().BeEmpty();
        viewModel.Editor.IsLoaded.Should().BeFalse();
    }
}
