using System.Security.Authentication;
using Application.Abstractions;
using Application.Abstractions.Authentication;
using Application.Authentication.Queries;
using Domain.Entities;
using MediatR;
using BC = BCrypt.Net.BCrypt;
using Microsoft.Extensions.Configuration;

namespace Application.Authentication.QueryHandlers;

public class LoginQueryHandler : IRequestHandler<LoginQuery, TokenResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public LoginQueryHandler(IConfiguration configuration,IJwtTokenGenerator jwtTokenGenerator, IUserRepository userRepository){
        _jwtTokenGenerator = jwtTokenGenerator;
        _userRepository = userRepository;
        _configuration = configuration;

    }
    public async Task<TokenResult> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetUserByEmailAsync(request.UserName);

        if(user == null){
            throw new ArgumentException ("Don't have this user");
        }
        if(user.Role != "Admin"){
            throw new ArgumentException("Invalid User.");
        }
        if(!BC.Verify(request.Password, user.Password)){
            throw new InvalidCredentialException("Invalid user or password.");
        }
        foreach(var r in user.RefreshTokens){
            if(r.CreatedTime.AddDays(1) < DateTime.UtcNow){
                user.RemoveRefreshToken(r);
            }
        }

        var token = _jwtTokenGenerator.GenerateToken(user, "Admin");
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        refreshToken = refreshToken.Replace("=","");
        refreshToken = refreshToken.Replace("+","");
        
        var userRefreshToken = RefreshToken.Create(refreshToken, user.Id);
        user.AddRefreshToken(userRefreshToken);
        await _userRepository.UpdateUserAsync(user);

        return new TokenResult(
            token, 
            refreshToken, 
            DateTime.UtcNow.AddMinutes(_configuration.GetSection("Jwt:ExpiryMinutes").Get<double>()));
    }
}