using MapExTools.Core.Coc;

namespace MapExTools.Tests.Coc;

/// <summary>固定样本：输入 .coc 经 <see cref="CocSettingsManager.Save"/> 写入后，与期望输出逐字节相同。</summary>
public sealed class CocSampleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_root))
            System.IO.Directory.Delete(_root, recursive: true);
    }

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (CocSampleCase c in CocSamples.Cases)
            data.Add(c.Name);
        return data;
    }

    [Fact]
    public void Samples_AllFilesPresent()
    {
        Assert.NotEmpty(CocSamples.Cases);
        foreach (CocSampleCase c in CocSamples.Cases)
        {
            if (c.Input != null)
                Assert.True(File.Exists(Path.Combine(CocSamples.Directory, c.Input)), c.Input);
            if (c.Expected != null)
                Assert.True(File.Exists(Path.Combine(CocSamples.Directory, c.Expected)), c.Expected);
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Save_MatchesExpectedSampleByteForByte(string name)
    {
        CocSampleCase sample = CocSamples.Get(name);
        string coc = Path.Combine(_root, "ModsSettings", "MapExtPDX", "MapExtPDX.coc");
        byte[]? input = sample.Input == null ? null : CocSamples.ReadBytes(sample.Input);
        if (input != null)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(coc)!);
            File.WriteAllBytes(coc, input);
        }

        var manager = new CocSettingsManager(coc, isGameRunning: () => false);
        StartupSettingsState state = manager.Read();

        // 目标值：恢复默认时先铺默认值，样本里的键覆盖其上
        Dictionary<string, object> target = sample.Reset
            ? CocSettingsManager.GetDefaultValues()
            : new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((string key, object value) in sample.Target)
            target[key] = value;

        CocSaveResult result = manager.Save(state, target);

        if (sample.Expected == null)
        {
            Assert.Equal(CocSaveStatus.NoChanges, result.Status);
            Assert.Empty(result.Changes);
            Assert.Equal(input, File.ReadAllBytes(coc));
            Assert.False(File.Exists(manager.BackupPath));
            return;
        }

        Assert.Equal(CocSaveStatus.Saved, result.Status);
        Assert.Equal(CocSamples.ReadBytes(sample.Expected), File.ReadAllBytes(coc));
        if (input == null)
        {
            Assert.Null(result.BackupPath);
            Assert.False(File.Exists(manager.BackupPath));
        }
        else
        {
            Assert.Equal(manager.BackupPath, result.BackupPath);
            Assert.Equal(input, File.ReadAllBytes(manager.BackupPath));
        }

        Assert.False(File.Exists(coc + ".tmp"));

        // 读回：目标值全部生效，且游戏都读得懂
        StartupSettingsState written = manager.Read();
        foreach ((string key, object value) in target)
        {
            Assert.Equal(value, written[key].Value);
            Assert.False(written[key].Invalid, key);
        }
    }
}
