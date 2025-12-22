// File: ObjectMapper.cs
// .NET 8 – Extensiones genéricas para mapear Entidad ⇄ DTO
// Soporta:
//  - Mapeo por nombre (case-sensitive) y por alias con [MapName]
//  - Ignorar propiedades con [MapIgnore]
//  - Conversión básica de tipos (incluye nullables y enums)
//  - Mapeo recursivo de objetos complejos
//  - Mapeo de colecciones genéricas (IEnumerable<T> → List<TDestino>)
//  - Registro opcional de convertidores personalizados por tipo
//
// Uso rápido:
//   var dto = entidad.MapTo<UserDto>();
//   var entidad = dto.MapTo<User>();
//   entidad.MapFrom(dto); // sobre una instancia existente

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NeoMapper
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MapIgnoreAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MapNameAttribute : Attribute
    {
        public string Name { get; }
        public MapNameAttribute(string name) => Name = name;
    }

    internal readonly record struct TypePair(Type Source, Type Destination);

    public static class MappingExtensions
    {
        private static IEnumerable AsEnumerable(object collection)
        {
            return collection is IEnumerable e ? e : Enumerable.Empty<object>();
        }

        private static bool TryAddToCollection(object collection, object? item)
        {
            if (collection == null || item == null) return false;

            var colType = collection.GetType();

            // Caso ICollection<T>
            var iCollection = colType
                .GetInterfaces()
                .FirstOrDefault(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(ICollection<>));

            if (iCollection != null)
            {
                colType
                    .GetMethod("Add", new[] { iCollection.GetGenericArguments()[0] })?
                    .Invoke(collection, new[] { item });
                return true;
            }

            // Fallback: método Add por reflection
            var addMethod = colType.GetMethod("Add");
            if (addMethod != null)
            {
                addMethod.Invoke(collection, new[] { item });
                return true;
            }

            return false;
        }


        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propsCache = new();
        private static readonly ConcurrentDictionary<TypePair, Dictionary<string, PropertyInfo>> _destPropMapCache = new();
        private static readonly ConcurrentDictionary<TypePair, bool> _isSimpleCache = new();
        private static readonly ConcurrentDictionary<TypePair, Func<object?, object?>> _customConverters = new();

        /// <summary>Registra un convertidor personalizado entre tipos.</summary>
        public static void RegisterConverter<TSource, TDest>(Func<TSource?, TDest?> converter)
        {
            _customConverters[new TypePair(typeof(TSource), typeof(TDest))] = (obj) => converter((TSource?)obj)!;
        }

        /// <summary>Mapea la instancia actual a un nuevo objeto del tipo TDest.</summary>
        public static TDest MapTo<TDest>(this object? source) where TDest : new()
        {
            if (source is null) return new TDest();
            var dest = new TDest();
            Map(source, dest);
            return dest;
        }

        /// <summary>Rellena la instancia destino con los valores de la fuente.</summary>
        public static void MapFrom<TSource>(this object dest, TSource? source)
        {
            if (dest is null) throw new ArgumentNullException(nameof(dest));
            if (source is null) return;
            Map(source!, dest);
        }

        /// <summary>Alias: mapea source → dest (objetos no genéricos).</summary>
        public static void Map(object source, object dest)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (dest is null) throw new ArgumentNullException(nameof(dest));

            var sType = source.GetType();
            var dType = dest.GetType();
            var typePair = new TypePair(sType, dType);

            // Preparar diccionario de propiedades destino (por nombre y alias)
            var dProps = _destPropMapCache.GetOrAdd(typePair, tp => BuildDestinationMap(tp.Destination));

            foreach (var sProp in GetMappableProps(sType))
            {
                var sValue = sProp.GetValue(source);
                if (sValue is null) continue;

                var targetName = GetTargetName(sProp);
                if (!dProps.TryGetValue(targetName, out var dProp)) continue; // no existe en destino
                if (!dProp.CanWrite) continue;
                if (dProp.GetCustomAttribute<MapIgnoreAttribute>() != null) continue;

                var destValue = dProp.GetValue(dest);

                var converted = ConvertValue(
                    sValue,
                    dProp.PropertyType,
                    destValue
                );

                if (converted.isSet && converted.value != destValue)
                {
                    dProp.SetValue(dest, converted.value);
                }
            }
        }

        // ----- Helpers -----
        private static PropertyInfo[] GetMappableProps(Type t)
        {
            return _propsCache.GetOrAdd(t, (type) =>
            {
                return type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanRead && p.GetMethod is not null && p.GetMethod.GetParameters().Length == 0)
                    .Where(p => p.GetCustomAttribute<MapIgnoreAttribute>() == null)
                    .ToArray();
            });
        }

        private static Dictionary<string, PropertyInfo> BuildDestinationMap(Type destType)
        {
            var dict = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);

            foreach (var dp in destType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!dp.CanWrite) continue;
                if (dp.GetCustomAttribute<MapIgnoreAttribute>() != null) continue;

                // Nombre directo
                if (!dict.ContainsKey(dp.Name))
                    dict[dp.Name] = dp;

                // Si el destino declara [MapName("Otra")] aceptar también ese alias
                var alias = dp.GetCustomAttribute<MapNameAttribute>()?.Name;
                if (!string.IsNullOrWhiteSpace(alias) && !dict.ContainsKey(alias!))
                {
                    dict[alias!] = dp;
                }
            }
            return dict;
        }

        private static string GetTargetName(PropertyInfo sProp)
        {
            // Si la FUENTE declara [MapName("Destino")] usarlo
            var alias = sProp.GetCustomAttribute<MapNameAttribute>()?.Name;
            return string.IsNullOrWhiteSpace(alias) ? sProp.Name : alias!;
        }

        //private static (bool isSet, object? value) ConvertValue(object? sourceValue, Type destType)
        private static (bool isSet, object? value) ConvertValue(object? sourceValue, Type destType, object? existingDestValue)
        {
            if (sourceValue is null) return (true, null);

            var srcType = sourceValue.GetType();
            var pair = new TypePair(srcType, destType);

            // Convertidor personalizado registrado
            if (_customConverters.TryGetValue(pair, out var conv))
            {
                return (true, conv(sourceValue));
            }

            // Si el destino acepta el tipo fuente directamente
            if (destType.IsAssignableFrom(srcType))
                return (true, sourceValue);

            // Nullables
            var (isNullable, underlyingDest) = UnwrapNullable(destType);

            // Enums
            if (underlyingDest.IsEnum)
            {
                try
                {
                    if (srcType == typeof(string))
                        return (true, Enum.Parse(underlyingDest, (string)sourceValue!, ignoreCase: true));

                    var number = System.Convert.ChangeType(sourceValue, Enum.GetUnderlyingType(underlyingDest));
                    return (true, Enum.ToObject(underlyingDest, number!));
                }
                catch { return (false, null); }
            }

            // Colecciones genéricas: IEnumerable<TSrc> → List<TDest>
            // Colecciones genéricas: IEnumerable<TSrc> → ICollection<TDest>
            // Colecciones genéricas: IEnumerable<TSrc> → IEnumerable<TDest>
            if (IsEnumerableOfT(srcType, out var srcElem) &&
                IsEnumerableOfT(destType, out var destElem))
            {
                // Si ya existe colección destino, reutilizarla
                if (existingDestValue is not null)
                {
                    foreach (var srcItem in (IEnumerable)sourceValue)
                    {
                        if (srcItem is null) continue;

                        object? existingItem = null;

                        // Buscar por Id (convención)
                        var srcIdProp = srcItem.GetType().GetProperty("Id");
                        if (srcIdProp != null)
                        {
                            var srcId = srcIdProp.GetValue(srcItem);

                            existingItem = AsEnumerable(existingDestValue)
                                .Cast<object>()
                                .FirstOrDefault(d =>
                                {
                                    var dIdProp = d.GetType().GetProperty("Id");
                                    return dIdProp != null && Equals(dIdProp.GetValue(d), srcId);
                                });
                        }

                        if (existingItem != null)
                        {
                            // 🔥 Mapear sobre la instancia existente
                            Map(srcItem, existingItem);
                        }
                        else
                        {
                            // Crear nuevo elemento
                            var (ok, mappedItem) = ConvertValue(
                                srcItem,
                                destElem,
                                existingDestValue: null
                            );

                            if (ok && mappedItem != null)
                            {
                                TryAddToCollection(existingDestValue, mappedItem);
                            }
                        }
                    }

                    // No reemplazar la colección
                    return (true, existingDestValue);
                }

                // No había colección → crear una nueva List<T>
                var listType = typeof(List<>).MakeGenericType(destElem);
                var newCollection = Activator.CreateInstance(listType)!;

                foreach (var srcItem in (IEnumerable)sourceValue)
                {
                    var (ok, mappedItem) = ConvertValue(srcItem, destElem, null);
                    if (ok && mappedItem != null)
                        TryAddToCollection(newCollection, mappedItem);
                }

                return (true, newCollection);
            }

            // Tipos "simples": primitivos, string, Guid, DateTime, decimal, TimeSpan
            if (IsSimple(srcType, underlyingDest))
            {
                try
                {
                    var converted = System.Convert.ChangeType(sourceValue, underlyingDest);
                    return (true, converted);
                }
                catch
                {
                    // conversiones específicas comunes
                    if (underlyingDest == typeof(Guid) && sourceValue is string sGuid && Guid.TryParse(sGuid, out var g))
                        return (true, g);

                    if (underlyingDest == typeof(DateTime) && sourceValue is string sDt && DateTime.TryParse(sDt, out var dt))
                        return (true, dt);

                    return (false, null);
                }
            }

            // Objetos complejos: mapeo recursivo
            try
            {
                if (existingDestValue != null)
                {
                    // 🔥 CLAVE: NO crear nueva instancia
                    Map(sourceValue, existingDestValue);
                    return (true, existingDestValue);
                }

                // Solo crear nueva si NO hay instancia previa
                var nested = Activator.CreateInstance(underlyingDest);
                if (nested is null) return (false, null);

                Map(sourceValue, nested);
                return (true, nested);
            }
            catch
            {
                return (false, null);
            }
        }

        private static (bool isNullable, Type underlying) UnwrapNullable(Type t)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return (true, Nullable.GetUnderlyingType(t)!);
            }
            return (false, t);
        }

        private static bool IsEnumerableOfT(Type t, out Type elemType)
        {
            if (t == typeof(string)) { elemType = typeof(char); return false; }

            var ienum = t.GetInterfaces().Append(t)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (ienum != null)
            {
                elemType = ienum.GetGenericArguments()[0];
                return true;
            }
            elemType = typeof(object);
            return false;
        }

        private static bool IsSimple(Type src, Type dest)
        {
            var key = new TypePair(src, dest);
            return _isSimpleCache.GetOrAdd(key, _ =>
            {
                static bool simple(Type t) => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(DateOnly) || t == typeof(TimeOnly);
                return simple(src) && simple(dest);
            });
        }
    }
}



