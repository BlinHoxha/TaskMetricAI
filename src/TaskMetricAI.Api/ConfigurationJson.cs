using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TaskMetricAI.Application;

namespace TaskMetricAI.Api;

/// <summary>Reject incomplete configuration commands without changing stored-document readers.</summary>
public static class ConfigurationJson
{
    public static void Configure(JsonSerializerOptions json)
    {
        json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        // A command must have one interpretation: reject repeated fields instead of
        // letting a later role, scope, revision or policy value silently win.
        json.AllowDuplicateProperties = false;
        json.RespectNullableAnnotations = true;
        json.RespectRequiredConstructorParameters = true;
        json.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static info =>
                {
                    // Point mapping remains optional by contract, even though the positional
                    // application record has no CLR default for this nullable constructor argument.
                    if (info.Type == typeof(ProfileInput))
                        info.Properties.Single(p => p.Name == "storyPointBands").IsRequired = false;
                }
            }
        };
    }
}
