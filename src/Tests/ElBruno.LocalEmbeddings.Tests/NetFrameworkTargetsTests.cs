using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.Tests;

public class NetFrameworkTargetsTests
{
    private const string VersionOfThePackage = "1.24.4";

    [Theory]
    [InlineData(VersionOfThePackage)]
    [InlineData("1.28.0")]
    public void NativeLibraries_AreOfTheVersionTheApplicationResolved(string resolved)
    {
        var copied = CopiedByTheTarget(resolved, VersionOfThePackage, "1.28.0");

        Assert.Contains($"microsoft.ml.onnxruntime/{resolved}/runtimes/win-x64/native/onnxruntime.dll", copied);
        Assert.Contains($"microsoft.ml.onnxruntime/{resolved}/runtimes/win-x64/native/onnxruntime_providers_shared.dll", copied);
        Assert.DoesNotContain(resolved == VersionOfThePackage ? "1.28.0" : VersionOfThePackage, copied);
    }

    [Fact]
    public void NativeLibraries_OfAnotherVersionAreNotCopied()
    {
        var copied = CopiedByTheTarget("1.28.0", VersionOfThePackage);

        Assert.DoesNotContain("onnxruntime.dll", copied);
    }

    // What the target of the package copies for an application on .NET Framework, given the version NuGet resolved and the versions in
    // the packages folder.
    private static string CopiedByTheTarget(string resolved, params string[] inThePackagesFolder)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"EmbeddingsTargetsTest_{Guid.NewGuid():N}");
        try
        {
            var packages = Path.Combine(directory, "packages");
            foreach (var version in inThePackagesFolder)
            {
                var native = Path.Combine(packages, "microsoft.ml.onnxruntime", version, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(native);
                File.WriteAllText(Path.Combine(native, "onnxruntime.dll"), "");
                File.WriteAllText(Path.Combine(native, "onnxruntime_providers_shared.dll"), "");
            }

            var project = Path.Combine(directory, "app.proj");
            File.WriteAllText(project, $"""
                <Project>
                  <PropertyGroup>
                    <TargetFrameworkIdentifier>.NETFramework</TargetFrameworkIdentifier>
                    <OutputType>Exe</OutputType>
                    <PlatformTarget>AnyCPU</PlatformTarget>
                    <NuGetPackageRoot>{packages}</NuGetPackageRoot>
                  </PropertyGroup>
                  <ItemGroup>
                    <ReferencePath Include="Microsoft.ML.OnnxRuntime.dll" NuGetPackageVersion="{resolved}" />
                  </ItemGroup>
                  <Target Name="ResolveAssemblyReferences" />
                  <Import Project="{Path.Combine(AppContext.BaseDirectory, "ElBruno.LocalEmbeddings.targets")}" />
                  <Target Name="Print" DependsOnTargets="ResolveAssemblyReferences">
                    <Message Importance="high" Text="COPIED:@(ReferenceCopyLocalPaths)" />
                  </Target>
                </Project>
                """);

            var startInfo = new ProcessStartInfo("dotnet", $"msbuild \"{project}\" -nologo -v:m -t:Print")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start MSBuild.");
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, output);

            var line = output.Split('\n').Single(l => l.Contains("COPIED:"));
            return line.Substring(line.IndexOf("COPIED:", StringComparison.Ordinal)).Replace('\\', '/');
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
