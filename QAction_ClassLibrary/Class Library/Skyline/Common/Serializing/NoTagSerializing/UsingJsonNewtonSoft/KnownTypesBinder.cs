namespace Skyline.DataMiner.Library.Common.Serializing.NoTagSerializing.UsingJsonNewtonSoft
{
    using Newtonsoft.Json.Serialization;

    using Skyline.DataMiner.Library.Common.Attributes;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    [DllImport("Newtonsoft.Json.dll")]
    internal class KnownTypesBinder : ISerializationBinder
    {
        private Lazy<string[]> nonUniqueTypeNames;

        public KnownTypesBinder()
        {
        }

        public KnownTypesBinder(IList<Type> knownTypes)
        {
            AddKnownTypes(knownTypes);
        }

        public IList<Type> KnownTypes { get; private set; }

        public void AddKnownTypes(IList<Type> knownTypes)
        {
            if (knownTypes != null)
            {
                KnownTypes = knownTypes;
                nonUniqueTypeNames = new Lazy<string[]>(() => { return KnownTypes.GroupBy(x => x.Name).Where(g => g.Count() > 1).Select(y => y.Key).ToArray(); });
            }
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = String.Empty;

            if (serializedType == null)
            {
                throw new ArgumentNullException("serializedType");
            }

            if (KnownTypes != null && KnownTypes.Contains(serializedType) && !nonUniqueTypeNames.Value.Contains(serializedType.Name))
            {
                typeName = serializedType.Name;
            }
            else
            {
                typeName = serializedType.ToString();
            }
        }

#pragma warning disable S3776 // Cognitive Complexity of methods should not be too high

        /// <summary>
        /// We want to be able to handle collections of interfaces, so type information is added to the json.
        /// Our messages and classes aren't always in a shared assembly. 
        /// So we remove the assembly information and try to recover the right type from different assemblies on the destination side.
        /// This is done using the KnownTypes, but we also need to handle System types and collections.
        /// The main goal is to avoid users of interapp to have to add json attributes or even care that it's using json.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public Type BindToType(string assemblyName, string typeName)
#pragma warning restore S3776 // Cognitive Complexity of methods should not be too high
        {
            if (typeName == null)
            {
                throw new ArgumentNullException("typeName");
            }

            Type foundType = null;
            bool isArray = false;
            bool isDoubleArray = false;
            bool isGenericType = false;

            // To Deal with Double Arrays:
            if (typeName.EndsWith("[][]", StringComparison.Ordinal))
            {
#pragma warning disable S1226 // Method parameters, caught exceptions and foreach variables' initial values should not be ignored
#pragma warning disable S3257 // Declarations and initializations should be as concise as possible
                typeName = typeName.TrimEnd(new char[] { '[', ']' });
#pragma warning restore S3257 // Declarations and initializations should be as concise as possible
#pragma warning restore S1226 // Method parameters, caught exceptions and foreach variables' initial values should not be ignored
                isDoubleArray = true;
            }

            // To Deal with Arrays:
            if (typeName.EndsWith("[]", StringComparison.Ordinal))
            {
#pragma warning disable S1226 // Method parameters, caught exceptions and foreach variables' initial values should not be ignored
#pragma warning disable S3257 // Declarations and initializations should be as concise as possible
                typeName = typeName.TrimEnd(new char[] { '[', ']' });
#pragma warning restore S3257 // Declarations and initializations should be as concise as possible
#pragma warning restore S1226 // Method parameters, caught exceptions and foreach variables' initial values should not be ignored
                isArray = true;
            }

            // Check if Generic Type
            // For generic types to work by default, an assembly is needed
            // (or the inner type has to be part of the assembly of the collection)
            // Otherwise the FindType call won't work.

            // Workaround to fix this:
            // Detect if Generic Type in collection
            // Find the type of the generic
            // add the type to the original typeName

            int openGeneric = typeName.IndexOf("[");
            List<Type> subTypes = new List<Type>();
            if (openGeneric != -1)
            {
                // Recursive check of inner Type
                // Find the first <
                int endGeneric = typeName.LastIndexOf("]");
                if (endGeneric != -1 && endGeneric != openGeneric + 1)
                {
                    string subTypeString = typeName.Substring(openGeneric + 1, (endGeneric - openGeneric) - 1);

                    // ignore multidimensional arrays double[,]
                    if (subTypeString.Any(p => p != ','))
                    {
                        isGenericType = true;

                        List<string> topLevelSplit = SplitOnTopLevel(subTypeString);

                        foreach (var topLevel in topLevelSplit)
                        {
                            subTypes.Add(BindToType(assemblyName, topLevel));
                        }

                        typeName = typeName.Substring(0, openGeneric);
                    }
                }
            }

            if (typeName.StartsWith("System", StringComparison.Ordinal) && foundType == null)
            {
                // MSCORLIB
                var mscorlibAssembly = typeof(Object).Assembly;
                try
                {
                    foundType = mscorlibAssembly.GetType(typeName);
                }
                catch
                {
                    // Ignore exception in order to see if we find the type. Need to use this for logic unfortunately.
                }

                if (foundType == null)
                {
                    // SYSTEM
                    var systemAssembly = typeof(System.Uri).Assembly;
                    try
                    {
                        foundType = systemAssembly.GetType(typeName);
                    }
                    catch
                    {
                        // Ignore exception in order to see if we find the type. Need to use this for logic unfortunately.
                    }
                }


                if (foundType == null)
                {
                    // SYSTEM.CORE
                    var sysCoreAssembly = typeof(HashSet<>).Assembly;
                    try
                    {
                        foundType = sysCoreAssembly.GetType(typeName);
                    }
                    catch
                    {
                        // Ignore exception in order to see if we find the type. Need to use this for logic unfortunately.
                    }
                }
            }

            if (KnownTypes != null && foundType == null)
            {
                try
                {
                    foundType = KnownTypes.SingleOrDefault(t => t.Name == typeName);
                }
                catch (InvalidOperationException ex)
                {
                    throw (new IncorrectDataException("Type Name: " + typeName + " was unique on serialization side but not on deserialization side. Please verify the same KnownTypes List is used on both ends of the communication.", ex));
                }
            }

            if (KnownTypes != null && foundType == null)
            {
                foundType = KnownTypes.SingleOrDefault(t => t.ToString() == typeName);
            }

            if (foundType == null)
            {
                // Checks the current assembly.
                foreach (Type t in typeof(KnownTypesBinder).Assembly.GetTypes())
                {
                    if (typeName == t.FullName)
                    {
                        foundType = t;
                        break;
                    }
                }
            }

            if (foundType == null)
            {
                // Check all known assemblies from knowntypes.

                foreach (var knownType in KnownTypes)
                {
                    foundType = knownType.Assembly.GetType(typeName);
                    if (foundType != null) break;
                }
            }

            if (foundType == null)
            {
                DefaultSerializationBinder def = new DefaultSerializationBinder();

                if (foundType == null && String.IsNullOrWhiteSpace(assemblyName))
                {
#pragma warning disable S1226 // Method parameters, caught exceptions and foreach variables' initial values should not be ignored
                    assemblyName = typeof(KnownTypesBinder).Assembly.GetName().Name;
#pragma warning restore S1226 // Method parameters, caught exceptions and foreach variables' initial values should not be ignored
                }

                try
                {
                    foundType = def.BindToType(assemblyName, typeName);
                }
                catch
                {
                    // Ignore exception in order to see if we find the type. Need to use this for logic unfortunately.
                }

            }

            if (isGenericType)
            {
                foundType = foundType.MakeGenericType(subTypes.ToArray());
            }

            if (isDoubleArray)
            {
                foundType = foundType.MakeArrayType().MakeArrayType();
            }
            if (isArray)
            {
                foundType = foundType.MakeArrayType();
            }

            if (foundType == null)
            {
                throw new InvalidOperationException("Deserialization Failed for type: " + typeName);
            }

            return foundType;
        }

        private static List<string> SplitOnTopLevel(string subTypeString)
        {
            // Need to handle dictionary inside dictionary inside ...
            //  string,dictionary[string,dictionary[string,string]],int  for example (without using fully qualified names in example)
            // string
            // dictionary[string
            // dictionary[string
            // string]]
            // int

            // want to get:
            // string
            // Dictionary[string, Dictionary[string,int]]
            // int

            // string, string[],dictionary [string,string[]]
            // string
            // string[]
            // Dictionary [string
            // string[]]

            // string
            // string[]
            // Dictionary [string,string[]]


            string[] subTypeStringSplit = subTypeString.Split(','); // to handle dictionary/tuple

            List<string> topLevelSplit = new List<string>();
            List<string> subLevelMerge = new List<string>();
            foreach (var subTypeArrayItem in subTypeStringSplit)
            {
                subLevelMerge.Add(subTypeArrayItem);

                if (!subTypeArrayItem.Contains("]") && !subTypeArrayItem.Contains("["))
                {
                    topLevelSplit.Add(String.Join(",", subLevelMerge));
                    subLevelMerge.Clear();
                }
            }

            if (subLevelMerge.Count > 0)
            {
                topLevelSplit.Add(String.Join(",", subLevelMerge));
                subLevelMerge.Clear();
            }

            return topLevelSplit;
        }
    }
}