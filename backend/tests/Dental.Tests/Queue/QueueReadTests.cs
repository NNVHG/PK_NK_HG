using Dental.Api.Controllers;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Validators;
using Microsoft.AspNetCore.Mvc;
using Xunit;
namespace Dental.Tests.Queue;
public sealed class QueueReadTests
{
    [Theory]
    [InlineData(0, 20, null, null)]
    [InlineData(1000001, 20, null, null)]
    [InlineData(1, 0, null, null)]
    [InlineData(1, 101, null, null)]
    [InlineData(1, 20, 0, null)]
    [InlineData(1, 20, 6, null)]
    [InlineData(1, 20, null, 0)]
    public void RejectsInvalidQuery(int page, int size, int? status, int? dentist)
        => Assert.False(new QueueQueryRequestValidator().Validate(new QueueQueryRequest(Status: status, DentistId: dentist, Page: page, PageSize: size)).IsValid);
    [Theory]
    [InlineData(null)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void AcceptsKnownStatuses(int? status)
        => Assert.True(new QueueQueryRequestValidator().Validate(new QueueQueryRequest(Status: status)).IsValid);
    [Fact]
    public void QueueReadEndpointsDisableCaching()
    {
        foreach (var name in new[] { nameof(QueueController.GetQueue), nameof(QueueController.GetQueueDetail) })
        {
            var attr = Assert.Single(typeof(QueueController).GetMethod(name)!.GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>());
            Assert.True(attr.NoStore); Assert.Equal(ResponseCacheLocation.None, attr.Location);
        }
    }
}
