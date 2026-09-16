using Domain.Entities;

namespace Application.DTOs;

public sealed record AuthenticatedSession(User User, Guid SessionId);
