using Dental.Application.Common;
using Dental.Application.Features.Queue.Services;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Xunit;

namespace Dental.Tests.Queue;

public sealed class QueueStateMachineTests
{
    [Theory]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Admin)]
    public void Waiting_To_InConsultation_ByDentistOrAdmin_Succeeds(string role)
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.Waiting, QueueStatus.InConsultation, role);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Assistant)]
    public void Waiting_To_InConsultation_ByReceptionistOrAssistant_FailsForbidden(string role)
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.Waiting, QueueStatus.InConsultation, role);
        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueTransitionForbidden.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Admin)]
    public void Waiting_To_Cancelled_ByReceptionistOrAdmin_Succeeds(string role)
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.Waiting, QueueStatus.Cancelled, role);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Admin)]
    public void InConsultation_To_Completed_ByDentistOrAdmin_Succeeds(string role)
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.InConsultation, QueueStatus.Completed, role);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Assistant)]
    public void InConsultation_To_Completed_ByReceptionistOrAssistant_FailsForbidden(string role)
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.InConsultation, QueueStatus.Completed, role);
        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueTransitionForbidden.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Admin)]
    public void InConsultation_To_InImaging_ByDentistOrAdmin_Succeeds(string role)
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.InConsultation, QueueStatus.InImaging, role);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Admin)]
    public void InImaging_To_Waiting_ByAssistantOrAdmin_Succeeds(string role)
    {
        // DL-053: Phụ tá bấm "Hoàn tất chụp" chuyển về Waiting
        var result = QueueStateMachine.ValidateTransition(QueueStatus.InImaging, QueueStatus.Waiting, role);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Dentist)]
    public void InImaging_To_Waiting_ByOtherRoles_FailsForbidden(string role)
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.InImaging, QueueStatus.Waiting, role);
        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueTransitionForbidden.Code, result.Error.Code);
    }

    [Fact]
    public void From_Completed_CannotTransitionToAnyState()
    {
        // DL-047: Không cho quay lùi từ Khám xong
        var result = QueueStateMachine.ValidateTransition(QueueStatus.Completed, QueueStatus.Waiting, RoleCodes.Admin);
        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueCannotRevertCompleted.Code, result.Error.Code);
    }

    [Fact]
    public void From_Cancelled_CannotTransitionToAnyState()
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.Cancelled, QueueStatus.Waiting, RoleCodes.Admin);
        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueCannotRevertCancelled.Code, result.Error.Code);
    }

    [Fact]
    public void InvalidTransition_WaitingToCompleted_FailsInvalidState()
    {
        var result = QueueStateMachine.ValidateTransition(QueueStatus.Waiting, QueueStatus.Completed, RoleCodes.Admin);
        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueInvalidStateTransition.Code, result.Error.Code);
    }
}
