using System.Net;
using System.Reflection;
using System.Text.Json;
using Dental.Api.Controllers;
using Dental.Api.RateLimiting;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class PasswordRecoveryTests
{
    private const string Phone = "0999999998";
    private readonly IPasswordRecoveryRepository repository = Substitute.For<IPasswordRecoveryRepository>();
    private readonly IPasswordRecoveryDelivery sms = Substitute.For<IPasswordRecoveryDelivery>();
    private readonly IPasswordHasher passwords = Substitute.For<IPasswordHasher>();
    private readonly Clock clock = new();
    private static RecoveryAccount Account => new(7, "stored-hash", Phone, null);
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private PasswordRecoveryService Service(IPasswordRecoveryCodes codes) => new(repository, codes, sms, passwords, new KeyedIdentityHasher(new byte[32]));

    [Fact]
    public async Task MissingConfigurationNeverQueriesAccountOrReturnsCode()
    {
        using var codes = new PasswordRecoveryCodes(clock);
        Assert.Equal("AUTH_060", (await Service(codes).RequestAsync(new(Phone))).Error.Code);
        Assert.Empty(repository.ReceivedCalls()); Assert.Empty(passwords.ReceivedCalls());
    }

    [Theory]
    [InlineData("")] [InlineData("1")] [InlineData("+84999999999")] [InlineData("0999999998 ")]
    public async Task InvalidPhoneNeverQueriesOrSends(string phone)
    {
        using var codes = new PasswordRecoveryCodes(clock);
        Assert.True((await Service(codes).RequestAsync(new(phone))).IsFailure);
        Assert.Empty(repository.ReceivedCalls()); Assert.Empty(sms.ReceivedCalls());
    }

    [Fact]
    public async Task ActiveAndUnknownAccountsHaveSamePublicShapeWithoutOtpOrPhone()
    {
        sms.IsConfigured.Returns(true); sms.Enqueue(Phone, Arg.Any<RecoveryCode>()).Returns(true);
        repository.FindActiveAsync(Phone, null, Arg.Any<CancellationToken>()).Returns(Account);
        using var codes = new PasswordRecoveryCodes(clock);
        var known = await Service(codes).RequestAsync(new(Phone));
        var unknown = await Service(codes).RequestAsync(new("0999999997"));
        Assert.True(known.IsSuccess && unknown.IsSuccess);
        Assert.Equal(known.Value!.ExpiresInSeconds, unknown.Value!.ExpiresInSeconds);
        Assert.Equal(known.Value.ResendAfterSeconds, unknown.Value.ResendAfterSeconds);
        Assert.NotEqual(known.Value.ChallengeId, unknown.Value.ChallengeId);
        var json = JsonSerializer.Serialize(known.Value);
        Assert.DoesNotContain(Phone, json); Assert.DoesNotContain("Code", json); Assert.DoesNotContain("stored-hash", json);
        sms.Received(1).Enqueue(Phone, Arg.Is<RecoveryCode>(x => x.Code.Length == 6 && x.Code.All(char.IsAsciiDigit)));
    }

    [Fact]
    public async Task DeliveryFailureRevokesCodeAndPreservesGenericResponse()
    {
        sms.IsConfigured.Returns(true);
        repository.FindActiveAsync(Phone, null, Arg.Any<CancellationToken>()).Returns(Account);
        using var codes = new PasswordRecoveryCodes(clock);
        var response = await Service(codes).RequestAsync(new(Phone));
        var code = (RecoveryCode)sms.ReceivedCalls().Single(x => x.GetMethodInfo().Name == "Enqueue").GetArguments()[1]!;
        Assert.True(response.IsSuccess);
        Assert.Null(codes.Consume(response.Value!.ChallengeId, Phone, code.Code, null));
    }

    [Fact]
    public void OtpIsSingleUseBoundToPhoneAndChallenge()
    {
        using var codes = new PasswordRecoveryCodes(clock);
        var issued = codes.Issue(Phone, Account).Value!;
        Assert.Null(codes.Consume(Guid.NewGuid(), Phone, issued.Code, null));
        Assert.Null(codes.Consume(issued.ChallengeId, "0999999997", issued.Code, null));
        Assert.Equal(Account, codes.Consume(issued.ChallengeId, Phone, issued.Code, null));
        Assert.Null(codes.Consume(issued.ChallengeId, Phone, issued.Code, null));
    }

    [Theory] [InlineData(299, true)] [InlineData(300, false)] [InlineData(301, false)]
    public void ExpiryBoundaryUsesServerClock(int seconds, bool valid)
    {
        using var codes = new PasswordRecoveryCodes(clock);
        var issued = codes.Issue(Phone, Account).Value!; clock.Now = clock.Now.AddSeconds(seconds);
        Assert.Equal(valid, codes.Consume(issued.ChallengeId, Phone, issued.Code, null) is not null);
    }

    [Fact]
    public void FifthWrongAttemptLocksEvenCorrectCode()
    {
        using var codes = new PasswordRecoveryCodes(clock);
        var issued = codes.Issue(Phone, Account).Value!;
        var wrong = issued.Code == "000000" ? "000001" : "000000";
        for (var i = 0; i < 5; i++) Assert.Null(codes.Consume(issued.ChallengeId, Phone, wrong, null));
        Assert.Null(codes.Consume(issued.ChallengeId, Phone, issued.Code, null));
        Assert.Equal("AUTH_062", codes.Issue(Phone, Account).Error.Code);
        clock.Now = clock.Now.AddSeconds(60);
        var next = codes.Issue(Phone, Account).Value!;
        Assert.Equal(Account, codes.Consume(next.ChallengeId, Phone, next.Code, null));
    }

    [Fact]
    public void ResendAt60SecondsRevokesOldChallengeAndCannotBeBypassedWithDifferentId()
    {
        using var codes = new PasswordRecoveryCodes(clock);
        var old = codes.Issue(Phone, Account).Value!;
        clock.Now = clock.Now.AddSeconds(59); Assert.True(codes.Issue(Phone, Account).IsFailure);
        clock.Now = clock.Now.AddSeconds(1); var next = codes.Issue(Phone, Account).Value!;
        Assert.Null(codes.Consume(old.ChallengeId, Phone, old.Code, null));
        Assert.Equal(Account, codes.Consume(next.ChallengeId, Phone, next.Code, null));
    }

    [Fact]
    public async Task ConcurrentCorrectAttemptsHaveExactlyOneWinner()
    {
        using var codes = new PasswordRecoveryCodes(clock);
        var issued = codes.Issue(Phone, Account).Value!;
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => codes.Consume(issued.ChallengeId, Phone, issued.Code, null))));
        Assert.Single(results, x => x is not null);
    }

    [Fact]
    public async Task ResetHashesPasswordAndCannotReplayWhenPersistenceFails()
    {
        using var codes = new PasswordRecoveryCodes(clock);
        var issued = codes.Issue(Phone, Account).Value!;
        passwords.Hash("Newpass1").Returns("new-hash");
        repository.ResetAsync(Account, "new-hash", Arg.Any<CancellationToken>()).Returns(false);
        var request = new ResetPasswordRequest(issued.ChallengeId, Phone, issued.Code, "Newpass1");
        Assert.Equal("AUTH_061", (await Service(codes).ResetAsync(request)).Error.Code);
        Assert.Equal("AUTH_061", (await Service(codes).ResetAsync(request)).Error.Code);
        await repository.Received(1).ResetAsync(Account, "new-hash", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetAcceptsKeyedAndLegacyPatientIdentity(bool legacy)
    {
        const string citizenId = "987654321098";
        var hasher = new KeyedIdentityHasher(new byte[32]);
        var storedHash = legacy ? Dental.Application.Features.MOD_PAT.CitizenIdIdentity.Hash(citizenId) : hasher.Hash(citizenId);
        var account = new RecoveryAccount(7, "stored-hash", Phone, storedHash);
        using var codes = new PasswordRecoveryCodes(clock);
        var issued = codes.Issue(Phone, account).Value!;
        passwords.Hash("Newpass1").Returns("new-hash");
        repository.ResetAsync(account, "new-hash", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Service(codes).ResetAsync(new(issued.ChallengeId, Phone, issued.Code, "Newpass1", citizenId));
        Assert.True(result.IsSuccess);
        await repository.Received(1).ResetAsync(account, "new-hash", Arg.Any<CancellationToken>());
    }

    [Theory] [InlineData("weak")] [InlineData("NoDigits")] [InlineData("nodigits1")] [InlineData("NOLOWER1")]
    public void PasswordRulesRejectWeakPasswords(string password)
        => Assert.False(new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(Guid.NewGuid(), Phone, "123456", password)).IsValid);

    [Fact]
    public void PasswordRulesRespectBcryptByteLimit()
    {
        var validator = new ResetPasswordRequestValidator();
        Assert.True(validator.Validate(new ResetPasswordRequest(Guid.NewGuid(), Phone, "123456", "Ab1" + new string('x', 69))).IsValid);
        Assert.False(validator.Validate(new ResetPasswordRequest(Guid.NewGuid(), Phone, "123456", "Ab1" + new string('ệ', 24))).IsValid);
    }

    [Fact]
    public void RecoveryEndpointsAreExplicitAnonymousExceptionsWithRateLimitsAndNoStore()
    {
        var controller = typeof(PasswordRecoveryController);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.True(controller.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        foreach (var method in new[] { "RequestCode", "Reset" })
        {
            var endpoint = controller.GetMethod(method)!;
            Assert.NotNull(endpoint.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(endpoint.GetCustomAttribute<EnableRateLimitingAttribute>());
        }
    }

    private sealed class Handler(HttpStatusCode result) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Uri { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            return new(result);
        }
    }

    [Fact]
    public async Task QueueDoesNotAwaitProviderAndFailureRevokesOnlyIssuedChallenge()
    {
        var provider = Substitute.For<IPasswordRecoverySms>();
        var codes = Substitute.For<IPasswordRecoveryCodes>();
        var issued = new RecoveryCode(Guid.NewGuid(), "123456");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var revoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        codes.IsCurrent(Phone, issued.ChallengeId).Returns(true);
        provider.SendAsync(Phone, issued.Code, Arg.Any<CancellationToken>()).Returns(_ => { entered.TrySetResult(); return delivered.Task; });
        codes.When(x => x.Revoke(Phone, issued.ChallengeId)).Do(_ => revoked.TrySetResult());
        using var queue = new PasswordRecoveryDelivery(provider, codes);
        await queue.StartAsync(CancellationToken.None);
        Assert.True(queue.Enqueue(Phone, issued));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(delivered.Task.IsCompleted);
        delivered.SetResult(false);
        await revoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await queue.StopAsync(CancellationToken.None);
        codes.Received(1).Revoke(Phone, issued.ChallengeId);
    }

    [Fact]
    public async Task QueueSkipsExpiredOrSupersededCodes()
    {
        var provider = Substitute.For<IPasswordRecoverySms>(); var codes = Substitute.For<IPasswordRecoveryCodes>();
        var checkedCode = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        codes.IsCurrent(Phone, Arg.Any<Guid>()).Returns(_ => { checkedCode.TrySetResult(); return false; });
        using var queue = new PasswordRecoveryDelivery(provider, codes);
        queue.Enqueue(Phone, new(Guid.NewGuid(), "123456")); await queue.StartAsync(CancellationToken.None);
        await checkedCode.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await queue.StopAsync(CancellationToken.None);
        Assert.Empty(provider.ReceivedCalls());
    }
    [Theory] [InlineData(HttpStatusCode.Created, true)] [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task SmsAdapterUsesPrivateFormBodyAndRejectsProviderFailure(HttpStatusCode status, bool expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["PasswordRecovery:Sms:Enabled"] = "true", ["PasswordRecovery:Sms:AccountSid"] = "AC" + new string('1',32),
            ["PasswordRecovery:Sms:AuthToken"] = new string('2',32), ["PasswordRecovery:Sms:From"] = "+15005550006"
        }).Build();
        using var handler = new Handler(status); using var client = new HttpClient(handler);
        var sender = new PasswordRecoverySms(client, config);
        Assert.True(sender.IsConfigured); Assert.Equal(expected, await sender.SendAsync(Phone,"123456",CancellationToken.None));
        Assert.Contains("To=%2B84999999998",handler.Body); Assert.Contains("123456",handler.Body);
        Assert.DoesNotContain(Phone,handler.Uri); Assert.DoesNotContain("123456",handler.Uri);
    }
}
