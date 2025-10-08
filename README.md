# MagicMapper 🪄

Un pequeño y poderoso *mapper* genérico para .NET 8 escrito en C# sin dependencias externas. Permite mapear automáticamente entidades ⇄ DTOs usando métodos de extensión, con soporte para atributos, conversión de tipos y colecciones.

---

## 📦 Instalación
MagicMapper está disponible en **NuGet**:

```bash
dotnet add package MagicMapper
```

Badge NuGet:

[![NuGet](https://img.shields.io/nuget/v/MagicMapper.svg)](https://www.nuget.org/packages/MagicMapper/) [![Downloads](https://img.shields.io/nuget/dt/MagicMapper.svg)](https://www.nuget.org/packages/MagicMapper/)

---

## 🚀 Características
- **Métodos de extensión simples**:
  - `MapTo<TDest>()` → crea un objeto destino a partir de la fuente.
  - `MapFrom(source)` → rellena una instancia existente.
- **Atributos personalizables**:
  - `[MapIgnore]` → ignora propiedades.
  - `[MapName("OtraPropiedad")]` → alias entre nombres diferentes.
- **Conversión de tipos incluida**:
  - Nullables (`int?`, `DateTime?`, etc.).
  - Enums ↔ string/números.
  - `Guid`, `DateTime`, `decimal`, `TimeSpan`, etc.
- **Colecciones**: convierte `IEnumerable<T>` a `List<TDestino>`.
- **Mapeo recursivo**: objetos complejos son mapeados propiedad por propiedad.
- **Convertidores personalizados**: registra tus propias funciones de conversión entre tipos.

---

## 🧑‍💻 Uso rápido
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

// Entidad → DTO
var dto = user.MapTo<UserDto>();

// DTO → Entidad
var user2 = dto.MapTo<User>();

// Mapear sobre instancia existente
user2.MapFrom(new UserDto { Id = 7, Name = "Ada Byron" });
```

---

## 🛠️ Ejemplo de atributos
```csharp
public sealed class UserDto
{
    public int Id { get; set; }
    [MapName("FullName")] // Mapear FullName de la entidad hacia Name del DTO
    public string Name { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public AddressDto? Address { get; set; }
    public List<string> Roles { get; set; } = new();
}
```

---

## 🔧 Convertidores personalizados
```csharp
MappingExtensions.RegisterConverter<Role, string>(r => r.ToString());
MappingExtensions.RegisterConverter<string, Role>(s => Enum.Parse<Role>(s, true));
```

---

## 📂 Estructura recomendada
- `src/MagicMapper/ObjectMapper.cs` → código principal de MagicMapper.
- DTOs y Entidades en sus capas respectivas.

---

## 📜 Licencia
MIT – Puedes usarlo y adaptarlo libremente.

---

## ✨ Contribuciones
¡Ideas y mejoras son bienvenidas! Puedes añadir:
- Soporte para expresiones de configuración fluida.
- Manejo avanzado de formatos de fecha/hora.
- Serialización directa desde JSON.

---

MagicMapper: *Convierte tus objetos como por arte de magia.* 🪄
