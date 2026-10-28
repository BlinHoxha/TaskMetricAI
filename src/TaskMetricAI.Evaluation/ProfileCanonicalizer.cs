using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskMetricAI.Evaluation;

internal static class ProfileCanonicalizer
{
    internal static string Hash(string canonicalContent) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalContent))).ToLowerInvariant();

    internal static string Serialize(EvaluationProfile profile)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            void Number(string key, decimal value) => json.WriteString(key, value.ToString("G29", CultureInfo.InvariantCulture));
            void Interval(ScoreInterval value)
            {
                Number("lower", value.Lower);
                Number("upper", value.Upper);
                json.WriteBoolean("includeLower", value.IncludeLower);
                json.WriteBoolean("includeUpper", value.IncludeUpper);
            }
            json.WriteStartObject();
            json.WriteString("schema", "profile-content-v2");
            json.WriteString("tenantId", profile.TenantId);
            json.WriteString("profileId", profile.ProfileId);
            json.WriteNumber("version", profile.Version);
            json.WriteString("name", profile.Name);
            json.WriteString("description", profile.Description);
            json.WriteString("scopeType", profile.Scope.Type.ToString());
            json.WriteString("scopeId", profile.Scope.Id);
            json.WriteNumber("percentageDecimalPlaces", profile.Policy.PercentageDecimalPlaces);
            json.WriteString("scoringPolicy", profile.Policy.ScoringVersion);
            json.WriteString("sensitivityPolicy", profile.Policy.SensitivityVersion);
            json.WriteStartArray("criteria");
            foreach (var criterion in profile.Criteria)
            {
                json.WriteStartObject();
                json.WriteString("id", criterion.CriterionId);
                json.WriteString("label", criterion.Label);
                json.WriteString("definition", criterion.Definition);
                Number("weight", criterion.Weight);
                json.WriteBoolean("isEnabled", criterion.IsEnabled);
                if (criterion.SourceMetric is { } source)
                {
                    json.WriteStartObject("sourceMetric");
                    json.WriteString("tenantId", source.TenantId);
                    json.WriteString("metricId", source.MetricId);
                    json.WriteNumber("version", source.Version);
                    json.WriteEndObject();
                }
                else json.WriteNull("sourceMetric");
                json.WriteStartArray("anchors");
                foreach (var anchor in new[] { criterion.Anchors.One, criterion.Anchors.Two, criterion.Anchors.Three, criterion.Anchors.Four, criterion.Anchors.Five })
                    json.WriteStringValue(anchor);
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("complexityBands");
            foreach (var band in profile.ComplexityBands)
            {
                json.WriteStartObject();
                json.WriteString("id", band.BandId);
                json.WriteString("name", band.Name);
                Interval(band.Interval);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            if (profile.StoryPointBands is { } points)
            {
                json.WriteStartArray("storyPointBands");
                foreach (var band in points)
                {
                    json.WriteStartObject();
                    json.WriteString("id", band.BandId);
                    Interval(band.Interval);
                    json.WriteNumber("points", band.Points);
                    json.WriteBoolean("recommendSplit", band.RecommendSplit);
                    json.WriteEndObject();
                }
                json.WriteEndArray();
            }
            else json.WriteNull("storyPointBands");
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
