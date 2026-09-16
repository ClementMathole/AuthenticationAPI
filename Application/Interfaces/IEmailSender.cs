namespace Application.Interfaces;

public interface IEmailSender
{
    Task SendConfirmationAsync
    (
        string email,
        Guid userId,
        string token,
        CancellationToken ct = default
    );
}
