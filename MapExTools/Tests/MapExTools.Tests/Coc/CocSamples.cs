using System.Text.Json;

namespace MapExTools.Tests.Coc;

/// <summary>
/// <c>Coc/Samples/cases.json</c> 里的一个固定样本。期望输出由开发版 <c>Set-MapExtStartup.ps1</c> 生成，
/// 并用游戏的 <c>COCParser</c> 与 JSON 解析核对过（计划 2.1）；<see cref="Expected"/> 为 null 表示不应写入。
/// </summary>
internal sealed record CocSampleCase(
    string Name,
    string? Input,
    bool Reset,
    IReadOnlyDictionary<string, object> Target,
    string? Expected);

internal static class CocSamples
{
    private static readonly Lazy<IReadOnlyList<CocSampleCase>> s_cases = new(LoadCases);

    /// <summary>样本目录：测试工程把 <c>Coc/Samples</c> 复制到输出目录。</summary>
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Coc", "Samples");

    public static IReadOnlyList<CocSampleCase> Cases => s_cases.Value;

    public static CocSampleCase Get(string name) => Cases.Single(c => c.Name == name);

    public static byte[] ReadBytes(string fileName) => File.ReadAllBytes(Path.Combine(Directory, fileName));

    private static IReadOnlyList<CocSampleCase> LoadCases()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, "cases.json")));
        var cases = new List<CocSampleCase>();
        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            var target = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (JsonProperty p in item.GetProperty("target").EnumerateObject())
            {
                target[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString()!,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw new InvalidDataException($"cases.json 的 {p.Name} 取值类型不对"),
                };
            }

            cases.Add(new CocSampleCase(
                item.GetProperty("name").GetString()!,
                GetOptionalString(item, "input"),
                item.TryGetProperty("reset", out JsonElement reset) && reset.GetBoolean(),
                target,
                GetOptionalString(item, "expected")));
        }

        return cases;
    }

    private static string? GetOptionalString(JsonElement item, string name) =>
        item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
