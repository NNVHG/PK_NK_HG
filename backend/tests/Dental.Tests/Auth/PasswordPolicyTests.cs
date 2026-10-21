using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Features.MOD_AUTH;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Aa1234",false)][InlineData("Aa12345",false)][InlineData("Aa123456",true)]
    [InlineData("12345678",false)][InlineData("abcdefgh",false)][InlineData("ABCDEFG1",false)]
    [InlineData("Abcdefgh",false)][InlineData("Strong1\0x",false)][InlineData("",false)]
    public void ChangeAndRecoveryMatchTheExistingEightCharacterDecision(string password,bool valid)
    {
        Assert.Equal(valid,new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest("legacy",password)).IsValid);
        Assert.Equal(valid,new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(Guid.NewGuid(),"0967000000","123456",password)).IsValid);
    }
    [Fact] public void BcryptLimitCountsUtf8BytesInBothFlows()
    {
        foreach(var (password,valid) in new[]{("Aa1"+new string('x',69),true),("Aa1"+new string('x',70),false),("Aa1"+new string('Đ',34),true),("Aa1"+new string('Đ',35),false)})
        {
            Assert.Equal(valid,new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest("legacy",password)).IsValid);
            Assert.Equal(valid,new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(Guid.NewGuid(),"0967000000","123456",password)).IsValid);
        }
    }
    [Fact] public void ExistingSixCharacterOrPhoneCredentialsStillPassLoginValidation()
    {
        Assert.True(new LoginRequestValidator().Validate(new LoginRequest("0967000000","Aa1234")).IsValid);
        Assert.True(new LoginRequestValidator().Validate(new LoginRequest("0967000000","0967000000")).IsValid);
    }
}
