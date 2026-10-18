using System;
using System.Threading;
using System.Linq;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace Todo.CLI.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

static class TodoCliAuthenticationProviderFactory
{
    public static IAuthenticationProvider GetAuthenticationProvider(IServiceProvider factory)
    {
        // Lazy: command-tree construction (todo --help etc.) must never touch the keychain.
        return new LazyAuthenticationProvider(() => CreateInitialized(factory));
    }

    private static IAuthenticationProvider CreateInitialized(IServiceProvider factory)
    {
        var config = factory.GetRequiredService<TodoCliConfiguration>();

        IPublicClientApplication app = PublicClientApplicationBuilder
            .Create(config.ClientId)
            .WithRedirectUri("http://localhost") // Only loopback redirect uri is supported, see https://aka.ms/msal-net-os-browser for details
            .Build();

        TokenCacheHelper.EnableSerialization(app.UserTokenCache);

        // Try to get token silently from cache first
        var accounts = app.GetAccountsAsync().GetAwaiter().GetResult();
        AuthenticationResult? result = null;

        if (accounts.Any())
        {
            try
            {
                result = app.AcquireTokenSilent(config.Scopes, accounts.First())
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (MsalUiRequiredException)
            {
                // Token expired or invalid, will fall through to interactive login
            }
        }

        // If silent acquisition failed or no accounts found, do interactive login
        if (result == null)
        {
            result = app.AcquireTokenInteractive(config.Scopes)
                .WithPrompt(Prompt.NoPrompt)
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();
        }

        return new ApiKeyAuthenticationProvider("Bearer " + result.AccessToken, "Authorization",
            ApiKeyAuthenticationProvider.KeyLocation.Header);
    }
}

/// <summary>
/// Defers the real provider (MSAL + keychain) until the first request is authenticated,
/// so help/version and argument errors stay instant and credential-free.
/// Thread-safe single initialization.
/// </summary>
internal class LazyAuthenticationProvider : IAuthenticationProvider
{
    private readonly Func<IAuthenticationProvider> _factory;
    private volatile IAuthenticationProvider? _inner;

    public LazyAuthenticationProvider(Func<IAuthenticationProvider> factory) => _factory = factory;

    public Task AuthenticateRequestAsync(Microsoft.Kiota.Abstractions.RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        _inner ??= _factory();
        return _inner.AuthenticateRequestAsync(request, additionalAuthenticationContext, cancellationToken);
    }
}
