using System.ComponentModel;
using FluentAssertions;
using SkopiStation.App.ViewModels;
using SkopiStation.Data;
using SkopiStation.Tests.Fakes;

namespace SkopiStation.Tests.ViewModels;

public class PatientEditorViewModelTests
{
    private static readonly DateTime ValidBirthDate = DateTime.Today.AddYears(-40);

    private static (PatientEditorViewModel Editor, FakePatientRepository Repository) Create()
    {
        var repository = new FakePatientRepository();
        var summary = repository.Add("SK-000001", "Moreau", "Camille", DateOnly.FromDateTime(ValidBirthDate));

        var editor = new PatientEditorViewModel(repository);
        editor.Load(new PatientListItemViewModel(summary));

        return (editor, repository);
    }

    private static IReadOnlyList<string> ErrorsFor(PatientEditorViewModel editor, string propertyName) =>
        [.. editor.GetErrors(propertyName).Select(result => result.ErrorMessage ?? string.Empty)];

    [Fact]
    public void A_loaded_patient_starts_valid_and_can_be_saved()
    {
        var (editor, _) = Create();

        editor.HasErrors.Should().BeFalse();
        editor.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void The_editor_is_not_loaded_and_cannot_save_without_a_selection()
    {
        var (editor, _) = Create();

        editor.Load(null);

        editor.IsLoaded.Should().BeFalse();
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_last_name_is_rejected(string value)
    {
        var (editor, _) = Create();

        editor.LastName = value;

        ErrorsFor(editor, nameof(editor.LastName)).Should().NotBeEmpty();
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void A_blank_first_name_is_rejected()
    {
        var (editor, _) = Create();

        editor.FirstName = string.Empty;

        ErrorsFor(editor, nameof(editor.FirstName)).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("004217")]
    [InlineData("SK-4217")]
    [InlineData("SK-00421X")]
    [InlineData("sk-004217")]
    [InlineData("SK-0042170")]
    public void A_malformed_record_number_is_rejected(string value)
    {
        var (editor, _) = Create();

        editor.RecordNumber = value;

        ErrorsFor(editor, nameof(editor.RecordNumber))
            .Should().ContainMatch("*SK-004217*");
    }

    [Fact]
    public void A_well_formed_record_number_is_accepted()
    {
        var (editor, _) = Create();

        editor.RecordNumber = "SK-004217";

        ErrorsFor(editor, nameof(editor.RecordNumber)).Should().BeEmpty();
    }

    [Fact]
    public void A_record_number_already_used_by_another_patient_is_rejected()
    {
        var (editor, _) = Create();
        editor.IsRecordNumberTaken = (recordNumber, _) => recordNumber == "SK-009999";

        editor.RecordNumber = "SK-009999";

        ErrorsFor(editor, nameof(editor.RecordNumber))
            .Should().ContainMatch("*another patient*");
    }

    [Fact]
    public void A_birth_date_in_the_future_is_rejected()
    {
        var (editor, _) = Create();

        editor.BirthDate = DateTime.Today.AddDays(1);

        ErrorsFor(editor, nameof(editor.BirthDate)).Should().ContainMatch("*future*");
    }

    [Fact]
    public void Today_is_accepted_as_a_birth_date()
    {
        var (editor, _) = Create();

        editor.BirthDate = DateTime.Today;

        ErrorsFor(editor, nameof(editor.BirthDate)).Should().BeEmpty();
    }

    [Fact]
    public void An_implausibly_old_birth_date_is_rejected()
    {
        var (editor, _) = Create();

        editor.BirthDate = DateTime.Today.AddYears(-121);

        ErrorsFor(editor, nameof(editor.BirthDate)).Should().ContainMatch("*120 years*");
    }

    [Fact]
    public void Errors_are_published_through_INotifyDataErrorInfo()
    {
        var (editor, _) = Create();
        var raised = new List<string?>();
        ((INotifyDataErrorInfo)editor).ErrorsChanged += (_, e) => raised.Add(e.PropertyName);

        editor.LastName = string.Empty;

        raised.Should().Contain(nameof(editor.LastName));
    }

    [Fact]
    public async Task Saving_sends_the_trimmed_identity_to_the_repository()
    {
        var (editor, repository) = Create();
        editor.LastName = "  Durand  ";
        editor.FirstName = "  Léa  ";
        editor.RecordNumber = " SK-004217 ";

        await editor.SaveCommand.ExecuteAsync(null);

        repository.SavedIdentities.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                RecordNumber = "SK-004217",
                LastName = "Durand",
                FirstName = "Léa",
            });
    }

    [Fact]
    public async Task Saving_updates_the_row_shown_in_the_list()
    {
        var repository = new FakePatientRepository();
        var summary = repository.Add("SK-000001", "Moreau", "Camille", DateOnly.FromDateTime(ValidBirthDate));
        var row = new PatientListItemViewModel(summary);

        var editor = new PatientEditorViewModel(repository);
        editor.Load(row);
        editor.LastName = "Durand";

        await editor.SaveCommand.ExecuteAsync(null);

        row.LastName.Should().Be("Durand");
        row.FullName.Should().Be("DURAND Camille");
    }

    [Fact]
    public void A_patient_keeping_its_own_record_number_is_not_reported_as_a_duplicate()
    {
        var repository = new FakePatientRepository();
        var summary = repository.Add("SK-000001", "Moreau", "Camille", DateOnly.FromDateTime(ValidBirthDate));
        var row = new PatientListItemViewModel(summary);

        var editor = new PatientEditorViewModel(repository)
        {
            IsRecordNumberTaken = (recordNumber, excludedId) =>
                recordNumber == "SK-000001" && excludedId != summary.Id,
        };
        editor.Load(row);

        editor.HasErrors.Should().BeFalse();
    }
}
