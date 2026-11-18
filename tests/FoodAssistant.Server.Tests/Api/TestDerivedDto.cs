namespace FoodAssistant.Server.Tests.Api;

/// <summary>Derived value returned by a handler declared as <see cref="TestBaseDto"/>.</summary>
public sealed record TestDerivedDto(int A, int B) : TestBaseDto(A);
