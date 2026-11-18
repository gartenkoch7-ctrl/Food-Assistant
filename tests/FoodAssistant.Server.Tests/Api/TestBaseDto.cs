using System.Text.Json.Serialization;

namespace FoodAssistant.Server.Tests.Api;

/// <summary>Polymorphic return type: the framework writes the <c>$type</c> discriminator of the declared base.</summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(TestDerivedDto), "derived")]
public record TestBaseDto(int A);
