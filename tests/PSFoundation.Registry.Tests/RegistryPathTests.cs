using System;
using Microsoft.Win32;
using Xunit;

namespace PSFoundation.Registry.Tests;

public sealed class RegistryPathTests
{
    [Theory]
    [InlineData(@"HKLM\Software\\Synthetic\", @"HKLM:\Software\Synthetic")]
    [InlineData(@"registry::hkey_current_user\Software", @"HKCU:\Software")]
    [InlineData("HKLM", "HKLM:")]
    [InlineData("HKU:", "Registry::HKEY_USERS")]
    [InlineData(@"HKCR\*\shell", @"Registry::HKEY_CLASSES_ROOT\*\shell")]
    [InlineData(@"HKCC\Software", @"Registry::HKEY_CURRENT_CONFIG\Software")]
    public void PreservesLegacyNormalization(string input, string expected) => Assert.Equal(expected, RegistryPath.Parse(input).ProviderPath);

    [Theory]
    [InlineData("HKLMother\\x")]
    [InlineData(" HKCU\\x")]
    [InlineData("nonsense")]
    public void RejectsUnknownHiveBoundaries(string input) => Assert.Throws<ArgumentException>(() => RegistryPath.Parse(input));

    [Fact]
    public void ReturnedHandleSurvivesDisposalOfTemporaryBaseKey()
    {
        var name = @"Software\PSFoundation.Tests\" + Guid.NewGuid().ToString("N");
        using (var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
        {
            using (var created = root.CreateSubKey(name))
                created.SetValue("Value", "synthetic");
            try
            {
                using (var opened = new RegistryReader().Open(RegistryPath.Parse("HKCU\\" + name), false, RegistryView.Registry32))
                    Assert.Equal("synthetic", opened!.GetValue("Value"));
                Assert.Null(new RegistryReader().Open(RegistryPath.Parse("HKCU\\" + name + "\\Missing")));
            }
            finally { root.DeleteSubKeyTree(name); }
        }
    }
}
