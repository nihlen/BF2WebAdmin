namespace BF2WebAdmin.Server.Communication.DTOs;

/// <summary>One check of "Test connection": what failed, in Message, when Success is false.</summary>
public record TestServerResult(string Type, bool Success, string? Message = null);