using MediatR;

namespace EventosVivos.Application.Auth.Commands;

public record ExchangeTokenCommand(string Provider, string IdToken) : IRequest<TokenResult>;

public record TokenResult(string AccessToken, string TokenType, int ExpiresIn, string Email, string Name);
