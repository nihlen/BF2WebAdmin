namespace BF2WebAdmin.Shared.Communication.DTOs;

/// <summary>One check of "Test connection": what failed, in Message, when Success is false.</summary>
public record TestServerResult(string Type, bool Success, string? Message = null);