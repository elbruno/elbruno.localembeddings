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

    [Fact]
    public void NativeLibraries_AreNotCopiedOverAFileWithTheSameName()
    {
        // As the props of Microsoft.ML.OnnxRuntime.DirectML add its onnxruntime.dll.
        var copied = CopiedByTheTarget(VersionOfThePackage, [VersionOfThePackage],
            """<None Include="directml\onnxruntime.dll" Link="onnxruntime.dll" />""");

        Assert.DoesNotContain("/onnxruntime.dll", copied);
        Assert.Contains("runtimes/win-x64/native/onnxruntime_providers_shared.dll", copied);
    }

    [Theory]
    [InlineData("native")]
    [InlineData("Runtime;Native")]
    [InlineData("All")]
    public void NativeLibraries_AreNotCopiedWhenTheApplicationExcludesThem(string excludeAssets)
    {
        var copied = CopiedByTheTarget(VersionOfThePackage, [VersionOfThePackage],
            $"""<PackageReference Include="Microsoft.ML.OnnxRuntime" ExcludeAssets="{excludeAssets}" />""");

        Assert.DoesNotContain("onnxruntime", copied);
    }

    [Fact]
    public void NativeLibraries_AreOfMicrosoftMLOnnxRuntimeWhenTheApplicationRaisesOnlyTheManagedPackage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"EmbeddingsTargetsTest_{Guid.NewGuid():N}");
        try
        {
            // The application reaches Microsoft.ML.OnnxRuntime through a library, as through this package.
            Directory.CreateDirectory(Path.Combine(directory, "library"));
            File.WriteAllText(Path.Combine(directory, "library", "library.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.ML.OnnxRuntime" Version="{VersionOfThePackage}" />
                  </ItemGroup>
                </Project>
                """);
            Directory.CreateDirectory(Path.Combine(directory, "app"));
            File.WriteAllText(Path.Combine(directory, "app", "Program.cs"), "class Program { static void Main() { } }");
            var project = Path.Combine(directory, "app", "app.csproj");
            File.WriteAllText(project, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net48</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <PlatformTarget>AnyCPU</PlatformTarget>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="..\library\library.csproj" />
                    <PackageReference Include="Microsoft.ML.OnnxRuntime.Managed" Version="1.28.0" />
                  </ItemGroup>
                  <Import Project="{Path.Combine(AppContext.BaseDirectory, "ElBruno.LocalEmbeddings.targets")}" />
                  <Target Name="Print" DependsOnTargets="ResolveReferences">
                    <Message Importance="high" Text="COPIED:@(ReferenceCopyLocalPaths)" />
                  </Target>
                </Project>
                """);

            var output = MSBuild($"\"{project}\" -restore -t:Print");

            var line = output.Split('\n').Single(l => l.Contains("COPIED:")).Replace('\\', '/');
            Assert.Contains($"microsoft.ml.onnxruntime/{VersionOfThePackage}/runtimes/win-x64/native/onnxruntime.dll", line);
            Assert.DoesNotContain("microsoft.ml.onnxruntime/1.28.0/", line);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // What the target of the package copies for an application on .NET Framework, given the version NuGet resolved and the versions in
    // the packages folder.
    private static string CopiedByTheTarget(string resolved, params string[] inThePackagesFolder) =>
        CopiedByTheTarget(resolved, inThePackagesFolder, "");

    private static string CopiedByTheTarget(string resolved, string[] inThePackagesFolder, string items)
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
                    {items}
                  </ItemGroup>
                  <Target Name="ResolveAssemblyReferences" />
                  <Import Project="{Path.Combine(AppContext.BaseDirectory, "ElBruno.LocalEmbeddings.targets")}" />
                  <Target Name="Print" DependsOnTargets="ResolveAssemblyReferences">
                    <Message Importance="high" Text="COPIED:@(ReferenceCopyLocalPaths)" />
                  </Target>
                </Project>
                """);

            var output = MSBuild($"\"{project}\" -t:Print");

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

    private static string MSBuild(string arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"msbuild {arguments} -nologo -v:m")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start MSBuild.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output);
        return output;
    }
}
