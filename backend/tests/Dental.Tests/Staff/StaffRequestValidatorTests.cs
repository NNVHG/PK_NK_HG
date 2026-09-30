using Dental.Application.Features.Staff.DTOs;
using Dental.Application.Features.Staff.Validators;
using Dental.Domain.Constants;
using Xunit;

namespace Dental.Tests.Staff;

public sealed class StaffRequestValidatorTests
{
    [Fact]
    public async Task CreateStaffValidator_RejectsPatientRoleAndInvalidPassword()
    {
        var validator = new CreateStaffRequestValidator();
        var result = await validator.ValidateAsync(
            new CreateStaffRequest("Nhân viên", "0912345678", "weak", RoleCodes.Patient, null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateStaffRequest.RoleCode));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateStaffRequest.Password));
    }

    [Fact]
    public async Task StaffQueryValidator_RejectsInvalidPaginationAndPatientRoleFilter()
    {
        var validator = new StaffQueryRequestValidator();
        var result = await validator.ValidateAsync(new StaffQueryRequest
        {
            Page = 0,
            PageSize = 101,
            RoleCode = RoleCodes.Patient,
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(StaffQueryRequest.Page));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(StaffQueryRequest.PageSize));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(StaffQueryRequest.RoleCode));
    }
}
