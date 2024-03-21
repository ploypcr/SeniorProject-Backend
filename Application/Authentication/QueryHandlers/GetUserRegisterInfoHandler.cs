using System.Security.Authentication;
using Application.Abstractions;
using Application.Abstractions.Authentication;
using Application.Abstractions.Services;
using Application.Authentication.Queries;
using Domain.Entities;
using Google.Apis.Auth;
using MediatR;


namespace Application.Authentication.QueryHandlers;

public class GetUserRegisterInfoHandler : IRequestHandler<GetUserRegisterInfo, TokenResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;


    public GetUserRegisterInfoHandler(IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<TokenResult> Handle(GetUserRegisterInfo request, CancellationToken cancellationToken)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(request.Token);
        if(payload.HostedDomain != "ku.th"){
            throw new InvalidCredentialException("Email is not in ku.th domain");
        }
        var user = await _userRepository.GetUserByEmailAsync(payload.Email);
        if(user == null){
            throw new InvalidCredentialException("Please register first.");
        }

        foreach(var r in user.RefreshTokens){
            if(r.CreatedTime.AddDays(1) < DateTime.UtcNow){
                user.RemoveRefreshToken(r);
            }
        }

        var token = _jwtTokenGenerator.GenerateToken(user, "User");
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.AddRefreshToken(
            RefreshToken.Create(refreshToken)
        );
        await _userRepository.UpdateUserAsync(user);


        return new TokenResult(token, refreshToken, DateTime.UtcNow.AddMinutes(480));
    }
}
