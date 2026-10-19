using Dental.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Dental.Tests.MOD_ASS;

public sealed class ChairStatusBodyLimitTests
{
    private static (ActionContext Action, ResourceExecutingContext Resource) Context(long? length)
    {
        var http = new DefaultHttpContext(); http.Request.Protocol = "HTTP/1.1"; http.Request.ContentLength = length; http.Response.Body = new MemoryStream();
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return (action, new ResourceExecutingContext(action, [], []));
    }
    [Fact] public async Task KnownOversizeStopsBeforeModelBinding()
    {
        var (action, resource) = Context(4097); var called = false;
        await new ChairStatusBodyLimitAttribute().OnResourceExecutionAsync(resource, () => { called = true; return Task.FromResult(new ResourceExecutedContext(action, [])); });
        Assert.False(called); Assert.Equal(413, Assert.IsType<ObjectResult>(resource.Result).StatusCode);
        Assert.Equal("no-store", action.HttpContext.Response.Headers.CacheControl); Assert.Equal("close", action.HttpContext.Response.Headers.Connection);
    }
    [Theory] [InlineData(null)] [InlineData(4096L)]
    public async Task PermittedOrUnknownLengthContinuesToBoundedReader(long? length)
    {
        var (action, resource) = Context(length); var called = false;
        await new ChairStatusBodyLimitAttribute().OnResourceExecutionAsync(resource, () => { called = true; return Task.FromResult(new ResourceExecutedContext(action, [])); });
        Assert.True(called); Assert.Null(resource.Result);
    }
    [Fact] public async Task Http2DoesNotReceiveConnectionSpecificHeader()
    {
        var (action, resource) = Context(4097); action.HttpContext.Request.Protocol = "HTTP/2";
        await new ChairStatusBodyLimitAttribute().OnResourceExecutionAsync(resource, () => throw new InvalidOperationException("Should not bind"));
        Assert.False(action.HttpContext.Response.Headers.ContainsKey("Connection"));
        Assert.Equal(413, Assert.IsType<ObjectResult>(resource.Result).StatusCode);
    }
    [Fact] public async Task OversizeModelBindingExceptionBecomesLocal413()
    {
        var (action, resource) = Context(null); var executed = new ResourceExecutedContext(action, []) { Exception = new BadHttpRequestException("Synthetic oversize", 413) };
        await new ChairStatusBodyLimitAttribute().OnResourceExecutionAsync(resource, () => Task.FromResult(executed));
        Assert.True(executed.ExceptionHandled); Assert.Equal(413, action.HttpContext.Response.StatusCode);
        action.HttpContext.Response.Body.Position = 0;
        Assert.Contains("ASS_064", await new StreamReader(action.HttpContext.Response.Body).ReadToEndAsync());
    }
    [Fact] public async Task DirectOversizeExceptionBecomes413ButOtherExceptionsPropagate()
    {
        var (action, resource) = Context(null);
        await new ChairStatusBodyLimitAttribute().OnResourceExecutionAsync(resource, () => throw new BadHttpRequestException("Synthetic oversize", 413));
        Assert.Equal(413, action.HttpContext.Response.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ChairStatusBodyLimitAttribute().OnResourceExecutionAsync(resource, () => throw new InvalidOperationException("Synthetic failure")));
    }
}
