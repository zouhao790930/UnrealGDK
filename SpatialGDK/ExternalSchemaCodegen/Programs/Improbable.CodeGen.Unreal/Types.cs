using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Improbable.CodeGen.Base;
using ValueType = Improbable.CodeGen.Base.ValueType;

namespace Improbable.CodeGen.Unreal
{
    public class TypeGeneratedCode 
    {
        public TypeGeneratedCode(string headerText, string sourceText)
        {
            this.headerText = headerText;
            this.sourceText = sourceText;
        }
        public string headerText;
        public string sourceText;
    }

    public static class Types
    {
        public enum Collection
        {
            List, Map, Option
        }

        public static Dictionary<PrimitiveType, string> SchemaToCppTypes = new Dictionary<PrimitiveType, string>
        {
            {PrimitiveType.Double, "double"},
            {PrimitiveType.Float, "float"},
            {PrimitiveType.Int32, "int32"},
            {PrimitiveType.Int64, "int64"},
            {PrimitiveType.Uint32, "uint32"},
            {PrimitiveType.Uint64, "uint64"},
            {PrimitiveType.Sint32, "int32"},
            {PrimitiveType.Sint64, "int64"},
            {PrimitiveType.Fixed32, "uint32"},
            {PrimitiveType.Fixed64, "uint64"},
            {PrimitiveType.Sfixed32, "int32"},
            {PrimitiveType.Sfixed64, "int64"},
            {PrimitiveType.Bool, "bool"},
            {PrimitiveType.String, "FString"},
            {PrimitiveType.Bytes, "TArray<uint8>"},
            {PrimitiveType.EntityId, "Worker_EntityId"}
        };

        public static Dictionary<Collection, string> CollectionTypesToQualifiedTypes = new Dictionary<Collection, string>
        {
            {Collection.Map, "TMap"},
            {Collection.List, "TArray"},
            {Collection.Option, "SpatialGDK::TSchemaOption"},
        };

        public static string GetFieldDefinitionHash(string name, FieldDefinition field, string accumulatorName,Bundle bundle)
        {
            switch (field.TypeSelector)
            {
                case FieldType.Option:
                    return $"{accumulatorName} = ({accumulatorName} * 977) + ({name}.IsSet() ? 1327u * {GetFieldDefinitionHash($"*{name}", field.OptionType.InnerType, bundle)} + 977u : 977u);";
                case FieldType.List:
                    return $@"for (const auto& item : {name})
{{
{Text.Indent(1, $"{accumulatorName} = ({accumulatorName} * 977) + {GetFieldDefinitionHash("item", field.ListType.InnerType, bundle)};")}
}}";
                case FieldType.Map:
                    return $@"for (const auto& pair : {name})
{{
{Text.Indent(1, $"{accumulatorName} += 1327 * ({GetFieldDefinitionHash("pair.Key", field.MapType.KeyType, bundle)} + 977 * {GetFieldDefinitionHash("pair.Value", field.MapType.ValueType, bundle)});")}
}}";
                case FieldType.Singular:
                    return $"{accumulatorName} = ({accumulatorName} * 977) + {GetFieldDefinitionHash(name, field.SingularType.Type, bundle)};";
                default:
                    throw new InvalidOperationException("Trying to hash invalid FieldDefinition");
            }
        }

        public static string GetFieldDefinitionEquals(FieldDefinition field, string fieldName, string otherObjName)
        {
            return $"{fieldName} == {otherObjName}.{fieldName}";
        }

        public static string GetFieldDefinitionHash(string name, ValueTypeReference valueTypeReference, Bundle bundle)
        {
            switch (valueTypeReference.ValueTypeSelector)
            {
                case ValueType.Enum:
                    return $"::GetTypeHash({name})";
                case ValueType.Primitive:
                    return $"::GetTypeHash({name})";
                case ValueType.Type:
                    return $"{GetNamespaceFromQualifiedName(valueTypeReference.Type.QualifiedName, bundle)}::GetTypeHash({name})";
                default:
                    throw new InvalidOperationException("Trying to hash invalid ValueTypeReference");
            }
        }

        public static string GetListInitialisation(string targetObjectName, string listInnerType, string length, bool targetIsOptional = false)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{targetObjectName} = {CollectionTypesToQualifiedTypes[Collection.List]}<{listInnerType}>();");
            builder.AppendLine($"{(targetIsOptional ? $"(*{targetObjectName})" : targetObjectName)}.SetNum({length});");
            return builder.ToString().TrimEnd();
        }

        // For a type, get all required includes based on fields, events and nested types
        public static List<string> GetRequiredTypeIncludes(TypeDescription type, Bundle bundle)
        {
            var includeTypeQualifiedNames = new List<string>();

            // Get all possible includes required by fields (and those in nested types)
            foreach (var field in type.Fields.Concat(type.NestedTypes.SelectMany(t => t.Fields)))
            {
                AddRequiredTypeQualifiedNames(field, ref includeTypeQualifiedNames);
            }

            // Get all possible includes required by events (if type is a component)
            if (type.IsComponent)
            {
                foreach (var _event in type.Events)
                {
                    AddRequiredTypeQualifiedNames(_event.Type, ref includeTypeQualifiedNames);
                }
                foreach (var command in bundle.Components[type.QualifiedName].CommandDefinitions)
                {
                    AddRequiredTypeQualifiedNames(command.RequestType, ref includeTypeQualifiedNames);
                    AddRequiredTypeQualifiedNames(command.ResponseType, ref includeTypeQualifiedNames);
                }
            }

            // Filter out #includes for types nested in our type (those are defined in the same file)
            var nestedDefinitionNames = type.NestedTypes.Select(t => t.QualifiedName).Concat(type.NestedEnums.Select(e => e.Identifier.QualifiedName));

            includeTypeQualifiedNames = includeTypeQualifiedNames.Where(name => !nestedDefinitionNames.Contains($"{name}.")).ToList();

            // Map any nested type dependencies to their include file
            includeTypeQualifiedNames = includeTypeQualifiedNames.Select(t => bundle.GetOutermostTypeWrapperForType(t)).ToList();

            return includeTypeQualifiedNames.Select(name => $"{name.Replace(".", "/")}.h").Distinct().ToList();
        }

        // Nested types are created as sibling classes with the parent type embedded into the class name
        // e.g. if type Bar is nested in type Foo, it will be generated as Foo_Bar
        public static string GetNestedTypeQualifiedName(TypeDescription type)
        {
            var splitQualifiedName = type.QualifiedName.Split(".");
            if (splitQualifiedName.Count() == 1)
            {
                throw new InvalidOperationException("Tried to find nested type name for a top-level type");
            }
            return string.Join('.', splitQualifiedName.Take(splitQualifiedName.Count() - 1)) + "_" + splitQualifiedName.Last();
        }

        public static string GetQualifiedTypeFromQualifiedName(string qualifiedName)
        {
            return ReplacesDotsWithDoubleColons(qualifiedName);
        }

        public static string GetFieldTypeAsCpp(FieldDefinition field, Bundle lookups)
        {
            switch (field.TypeSelector)
            {
                case FieldType.Option:
                    return $"{CollectionTypesToQualifiedTypes[Collection.Option]}<{GetQualifiedTypeName(field.OptionType.InnerType, lookups)}>";
                case FieldType.List:
                    return $"{CollectionTypesToQualifiedTypes[Collection.List]}<{GetQualifiedTypeName(field.ListType.InnerType, lookups)}>";
                case FieldType.Map:
                    return $"{CollectionTypesToQualifiedTypes[Collection.Map]}<{GetQualifiedTypeName(field.MapType.KeyType, lookups)}, {GetQualifiedTypeName(field.MapType.ValueType, lookups)}>";
                case FieldType.Singular:
                    return GetQualifiedTypeName(field.SingularType.Type, lookups);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static string GetNameFromQualifiedName(string qualifiedName)
        {
            return qualifiedName.Substring(qualifiedName.LastIndexOf('.') + 1);
        }

        public static string GetQualifiedTypeName(TypeDescription type)
        {
            return "::" + ReplacesDotsWithDoubleColons(type.QualifiedName);
        }

        public static string GetQualifiedTypeName(string qualifiedName)
        {
            return "::" + ReplacesDotsWithDoubleColons(qualifiedName);
        }

        public static string GetQualifiedTypeName(ValueTypeReference typeRef, Bundle bundle)
        {
            switch (typeRef.ValueTypeSelector)
            {
                case ValueType.Enum:
                    return $"::{GetQualifiedTypeFromQualifiedName(typeRef.Enum.QualifiedName)}";
                case ValueType.Primitive:
                    return SchemaToCppTypes[typeRef.Primitive];
                case ValueType.Type:
                    var qualifiedName = typeRef.Type.QualifiedName;
                    return bundle.IsNestedType(qualifiedName) ? GetTypeClassDefinitionQualifiedName(qualifiedName, bundle)
                        : GetQualifiedTypeFromQualifiedName(qualifiedName); ;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static string GetQualifiedTypeName(TypeReference type)
        {
            return ReplacesDotsWithDoubleColons(type.QualifiedName);
        }

        public static string TypeToHeaderFilename(string qualifiedName)
        {
            return TypeToFilename(qualifiedName, ".h");
        }

        public static string TypeToSourceFilename(string qualifiedName)
        {
            return TypeToFilename(qualifiedName, ".cpp");
        }

        // For each const get accessor for each member field in a type, if the type is a primitive we return by value, otherwise by const ref
        // e.g. double get_my_double_memeber();
        //      const ::improbable::List<...>& get_my_list_memeber();
        public static string GetConstAccessorTypeModification(FieldDefinition field, Bundle bundle)
        {
            var qualifiedFieldType = GetFieldTypeAsCpp(field, bundle);
            if (field.TypeSelector == FieldType.Singular && field.SingularType.Type.ValueTypeSelector == ValueType.Primitive)
            {
                return qualifiedFieldType;
            }
            return $"const {qualifiedFieldType}&";
        }

        public static string GetTypeClassDefinitionName(string qualifiedName, Bundle bundle)
        {
            var siblingQualifiedName = GetTypeClassDefinitionQualifiedName(qualifiedName, bundle);
            return siblingQualifiedName.Substring(siblingQualifiedName.LastIndexOf("::") + 2);
        }

        // When generating header files for schema types with nested types, we declare the nested types as sibling types.
        // To avoid naming conflicts we prefix the sibling type names with the top-level type.
        // E.g. improbable::ComponentInterest::Query becomes improbable::ComponentInterest_Query
        public static string GetTypeClassDefinitionQualifiedName(string qualifiedName, Bundle bundle)
        {
            var outermostType = bundle.GetOutermostTypeWrapperForType(qualifiedName);
            return $"{GetQualifiedTypeName(outermostType.Substring(0, outermostType.LastIndexOf(".")))}::{string.Join("_", qualifiedName.Split(".").Skip(outermostType.Count(c => c == '.')))}";
        }

        public static List<TypeDescription> GetRecursivelyNestedTypes(TypeDescription type)
        {
            return type.NestedTypes.Concat(type.NestedTypes.SelectMany(nestedType => GetRecursivelyNestedTypes(nestedType)))
                .GroupBy(t => t.QualifiedName)
                .Select(t => t.First())
                .ToList();
        }

        public static List<EnumDefinition> GetRecursivelyNestedEnums(TypeDescription type)
        {
            return type.NestedEnums.Concat(type.NestedTypes.SelectMany(nestedType => GetRecursivelyNestedEnums(nestedType)))
                .GroupBy(e => e.Identifier.QualifiedName)
                .Select(e => e.First())
                .ToList();
        }

        // Sorts all nested type definitions in the given schema file according to singular dependencies. Returns
        // an empty vector if there is a cycle (although this should be impossible if the file has passed
        // validation).
        public static List<TypeDescription> SortTopLevelTypesTopologically(TypeDescription type, List<TypeDescription> types, Bundle bundle)
        {
            var topLevelTypes = new List<TypeDescription>(GetRecursivelyNestedTypes(type)) { type };
            Dictionary<string, bool> allTopLevelTypesToVisitedMap = topLevelTypes.ToDictionary(t => t.QualifiedName, t => false);

            bool graphIsCyclic = false;

            var sortedTypes = new List<TypeDescription>();
            while (allTopLevelTypesToVisitedMap.Count() > 0)
            {
                Visit(allTopLevelTypesToVisitedMap.First().Key, topLevelTypes, ref allTopLevelTypesToVisitedMap, ref graphIsCyclic, ref sortedTypes, types, bundle);
                if (graphIsCyclic)
                {
                    throw new InvalidDataException($"Found cyclic dependency in nested types of {type.QualifiedName}");
                }
            }
            return sortedTypes;
        }

        private static string GetNamespaceFromQualifiedName(string qualifiedName, Bundle bundle)
        {
            var outermostTypeWrapper = bundle.GetOutermostTypeWrapperForType(qualifiedName);
            return outermostTypeWrapper.Substring(0, outermostTypeWrapper.LastIndexOf(".")).Replace(".", "::");
        }

        private static string TypeToFilename(string qualifiedName, string extension)
        {
            var path = qualifiedName.Split('.');
            return string.Join("/", path.Take(path.Length - 1).Append($"{path.Last()}{extension}").ToArray());
        }

        private static string ReplacesDotsWithDoubleColons(string identifier)
        {
            return $"{identifier.Replace(".", "::")}";
        }

        // For a field definition inside a type, get all the necessary includes for the type (from collection inner types, etc)
        private static void AddRequiredTypeQualifiedNames(FieldDefinition field, ref List<string> requiredTypeQualifiedNames)
        {
            switch (field.TypeSelector)
            {
                case FieldType.Singular:
                    AddRequiredTypeQualifiedNames(field.SingularType.Type, ref requiredTypeQualifiedNames);
                    break;
                case FieldType.Option:
                    AddRequiredTypeQualifiedNames(field.OptionType.InnerType, ref requiredTypeQualifiedNames);
                    break;
                case FieldType.List:
                    AddRequiredTypeQualifiedNames(field.ListType.InnerType, ref requiredTypeQualifiedNames);
                    break;
                case FieldType.Map:
                    AddRequiredTypeQualifiedNames(field.MapType.KeyType, ref requiredTypeQualifiedNames);
                    AddRequiredTypeQualifiedNames(field.MapType.ValueType, ref requiredTypeQualifiedNames);
                    break;
            }
        }

        private static void AddRequiredTypeQualifiedNames(ValueTypeReference valueType, ref List<string> requiredTypeQualifiedNames)
        {
            switch (valueType.ValueTypeSelector)
            {
                case ValueType.Enum:
                    requiredTypeQualifiedNames.Add(valueType.Enum.QualifiedName);
                    break;
                case ValueType.Type:
                    requiredTypeQualifiedNames.Add(valueType.Type.QualifiedName);
                    break;
                default:
                    return;
            }
        }

        private static void Visit(string typeName, List<TypeDescription> topLevelTypes, ref Dictionary<string, bool> nestedTypeNameToVisitedMap, ref bool graphIsCyclic, ref List<TypeDescription> sortedTypes, List<TypeDescription> types, Bundle bundle)
        {
            var type = topLevelTypes.FirstOrDefault(t => t.QualifiedName == typeName);

            // Exit early if trying to visit a nested type of a previously visited component (also is fine, this is sorting in action)
            if (type.Equals(default(TypeDescription))) 
            {
                return;
            }

            // If visited this type already without removing it, this means we have a cyclic dependency
            if (nestedTypeNameToVisitedMap[typeName]) 
            {
                graphIsCyclic = true;
                return;
            }

            nestedTypeNameToVisitedMap[typeName] = true;

            foreach (var field in type.Fields)
            {
                // We're only doing this topological sort to cater for compiler errors from declaring incomplete type members
                // This only occurs for singular type or map keys
                if (field.TypeSelector == FieldType.Singular && field.SingularType.Type.ValueTypeSelector == ValueType.Type)
                {
                    Visit(topLevelTypes.Find(t => t.QualifiedName == field.SingularType.Type.Type.QualifiedName).QualifiedName, topLevelTypes, ref nestedTypeNameToVisitedMap, ref graphIsCyclic, ref sortedTypes, types, bundle);
                }
                else if (field.TypeSelector == FieldType.Map && field.MapType.KeyType.ValueTypeSelector == ValueType.Type)
                {
                    Visit(topLevelTypes.Find(t => t.QualifiedName == field.MapType.KeyType.Type.QualifiedName).QualifiedName, topLevelTypes, ref nestedTypeNameToVisitedMap, ref graphIsCyclic, ref sortedTypes, types, bundle);
                }
            }
            sortedTypes.Add(type);
            nestedTypeNameToVisitedMap.Remove(typeName);
        }
    }
}