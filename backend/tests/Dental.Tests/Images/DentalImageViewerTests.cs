using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Images;

public sealed class DentalImageViewerTests
{
    private readonly IDentalImageRepository repo = Substitute.For<IDentalImageRepository>();
    private readonly IDentalImageStorage storage = Substitute.For<IDentalImageStorage>();
    private DentalImageService Service => new(repo, storage);
    private void Permit(string type = "image/png", long size = 20) => repo.FileAsync(1, 5, RoleCodes.Patient, Arg.Any<CancellationToken>()).Returns(Result<StoredDentalImage>.Success(new("5/11111111111111111111111111111111.png", type, size)));
    [Theory] [InlineData(RoleCodes.Assistant)] [InlineData("UNKNOWN")]
    public async Task RoleCannotBypassReadPolicy(string role)
    {
        var r = await Service.FileAsync(1, 5, role); Assert.Equal(Error.Forbidden, r.Error); Assert.Empty(repo.ReceivedCalls()); Assert.Empty(storage.ReceivedCalls());
    }
    [Fact] public async Task DeniedOwnershipNeverOpensFile()
    {
        repo.FileAsync(1,5,RoleCodes.Patient,Arg.Any<CancellationToken>()).Returns(Result<StoredDentalImage>.Failure(Error.NotFound));
        Assert.Equal(Error.NotFound,(await Service.FileAsync(1,5,RoleCodes.Patient)).Error); Assert.Empty(storage.ReceivedCalls());
    }
    [Fact] public async Task ValidReadReturnsStreamWithoutExposingPath()
    {
        Permit(); var stream = new MemoryStream(new byte[20]); storage.OpenReadAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(stream);
        var r=await Service.FileAsync(1,5,RoleCodes.Patient); Assert.True(r.IsSuccess); Assert.Same(stream,r.Value!.Content); Assert.Equal("image/png",r.Value.ContentType); await r.Value.Content.DisposeAsync();
    }
    [Theory] [InlineData("text/html",20)] [InlineData("image/png",11)] [InlineData("image/png",10485761)]
    public async Task InvalidStoredMetadataCannotBecomeHtmlOrUnboundedFile(string type,long size)
    { Permit(type,size); Assert.Equal(Error.ServerError,(await Service.FileAsync(1,5,RoleCodes.Patient)).Error); Assert.Empty(storage.ReceivedCalls()); }
    [Fact] public async Task MissingPhysicalFileIsGenericNotFound()
    {
        Permit(); storage.OpenReadAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns<Stream>(_=>throw new FileNotFoundException("Sensitive path must not be returned"));
        Assert.Equal(Error.NotFound,(await Service.FileAsync(1,5,RoleCodes.Patient)).Error);
    }
    [Fact] public async Task TruncatedStoredFileIsDisposedAndRejected()
    {
        Permit(); var stream=new MemoryStream(new byte[19]);storage.OpenReadAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(stream);
        Assert.Equal(Error.ServerError,(await Service.FileAsync(1,5,RoleCodes.Patient)).Error); Assert.False(stream.CanRead);
    }
    [Theory] [InlineData(0,5)] [InlineData(-1,5)] [InlineData(1,0)]
    public async Task InvalidIdentityDoesNotReachStorage(int image,int actor)
    { Assert.True((await Service.FileAsync(image,actor,RoleCodes.Patient)).IsFailure);Assert.Empty(storage.ReceivedCalls()); }
}
