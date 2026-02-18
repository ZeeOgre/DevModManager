using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DMM.Core.IO
{
    public static class Yaml
    {
        public static readonly ISerializer Serializer =
            new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

        public static readonly IDeserializer Deserializer =
            new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

        public static string Dump<T>(T value) => Serializer.Serialize(value);

        public static T Load<T>(string yaml) => Deserializer.Deserialize<T>(yaml);
    }
}