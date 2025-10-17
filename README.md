# NeoMapper 🪄

A small yet powerful generic *mapper* for .NET 8 written in C# with no external dependencies. Automatically maps entities ⇄ DTOs using extension methods, supporting attributes, type conversion, and collections.

---

## 📦 Installation
NeoMapper is available on **NuGet**:

```bash
dotnet add package NeoMapper
```

NuGet Badge:

[![NuGet](https://img.shields.io/nuget/v/NeoMapper.svg)](https://www.nuget.org/packages/NeoMapper/) [![Downloads](https://img.shields.io/nuget/dt/NeoMapper.svg)](https://www.nuget.org/packages/NeoMapper/)

---

## 🚀 Features
- **Simple extension methods**:
  - `MapTo<TDest>()` → creates a destination object from the source.
  - `MapFrom(source)` → fills an existing instance.
- **Customizable attributes**:
  - `[MapIgnore]` → ignores properties.
  - `[MapName("OtherProperty")]` → alias for different property names.
- **Built-in type conversion**:
  - Nullables (`int?`, `DateTime?`, etc.).
  - Enums ↔ string/numbers.
  - `Guid`, `DateTime`, `decimal`, `TimeSpan`, etc.
- **Collections**: converts `IEnumerable<T>` to `List<TDestination>`.
- **Recursive mapping**: complex objects are mapped property by property.
- **Custom converters**: register your own conversion functions between types.

---

## 🧑‍💻 Quick usage
```csharp
using GenericMapper;

var user = new User
{
    Id = 7,
    FullName = "Ada Lovelace",
    CreatedAt = DateTime.UtcNow,
    Address = new Address { Street = "St. James's", City = "London" },
    Roles = new List<Role> { Role.Admin, Role.User }
};

// Entity → DTO
var dto = user.MapTo<UserDto>();

// DTO → Entity
var user2 = dto.MapTo<User>();

// Map onto existing instance
user2.MapFrom(new UserDto { Id = 7, Name = "Ada Byron" });
```

---

## 🛠️ Attribute example
```csharp
public sealed class UserDto
{
    public int Id { get; set; }
    [MapName("FullName")] // Map FullName from entity to Name in DTO
    public string Name { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public AddressDto? Address { get; set; }
    public List<string> Roles { get; set; } = new();
}
```

---

## 🔧 Custom converters
```csharp
MappingExtensions.RegisterConverter<Role, string>(r => r.ToString());
MappingExtensions.RegisterConverter<string, Role>(s => Enum.Parse<Role>(s, true));
```

---

## 📂 Recommended structure
- `src/NeoMapper/ObjectMapper.cs` → main NeoMapper code.
- DTOs and Entities in their respective layers.

---

## 📜 License
MIT – You can use and adapt it freely.

---

## ✨ Contributions
Ideas and improvements are welcome! You can add:
- Support for fluent configuration expressions.
- Advanced date/time format handling.
- Direct serialization from JSON.

---

NeoMapper: *Transform your objects as if by magic.* 🪄
