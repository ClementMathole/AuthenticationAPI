using Application.Interfaces;

namespace Infrastructure.Email
{
    public class ConsoleEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string to, string subject, string body)
        {
            Console.WriteLine($"Sending email to {to}: {subject}\n{body}");
            return Task.CompletedTask;
        }
    }
}
