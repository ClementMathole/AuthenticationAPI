namespace Application.Exceptions;

public sealed class AccountLockedException() : Exception("Account temporarily locked. Try again later.")
{ }
