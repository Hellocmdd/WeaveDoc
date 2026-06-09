namespace WeaveDoc.App.Services.Documents;

public interface IUnsavedChangesConfirmationService
{
    Task<UnsavedChangesDecision> ConfirmAsync(string displayName, CancellationToken cancellationToken = default);
}
