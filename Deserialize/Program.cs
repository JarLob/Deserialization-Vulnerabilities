using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml;
using System.Xml.Serialization;

namespace Deserialize
    {
    class LimitedBinder : SerializationBinder
        {
        public override Type BindToType (string assemblyName, string typeName) 
            {
            var type = Type.GetType (String.Format("{0}, {1}", typeName, assemblyName), true);
            if (type != typeof(Exception) &&
                type != typeof(List<Exception>))
                throw new Exception("Unexpected serialized type");
            return type;
            }
        }

    class Program
        {
        static void Main (string[] args)
            {
            //DeserializeXml();
            DeserializeBinaryFormatter();
            }

        private static void DeserializeXml()
            {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load("payload3.xml");

            foreach (XmlElement xmlItem in xmlDoc.SelectNodes("root/item"))
                {
                string typeName = xmlItem.GetAttribute("type");
                var s = new XmlSerializer(Type.GetType(typeName));
                var reader = new XmlTextReader(new StringReader(xmlItem.InnerXml));

                try
                    {
                    var data = (List<Exception>)s.Deserialize(reader);
                    foreach (var e in data)
                        {
                        Console.WriteLine($"Saved exception: {e}");
                        }
                    }
                catch
                    {
                    Console.WriteLine("Failed to deserialize");
                    }
                }
            }

        private static void DeserializeBinaryFormatter()
            {
            using (var fs = new FileStream (@"c:\temp\serialized.dat", FileMode.Open))
            {
                var formatter = new BinaryFormatter();// { Binder = new LimitedBinder () };
                try
                    {
                    var data = (List<Exception>)formatter.Deserialize (fs);
                    foreach (var e in data)
                        {
                        Console.WriteLine (e.ToString());
                        }
                    }
                catch
                    {
                    Console.WriteLine ("Failed to deserialize");
                    }
                }
            }
        }
    }
