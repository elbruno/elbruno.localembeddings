using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.Tests;

public class NetFrameworkTargetsTests
{
    private const string VersionOfThePackage = "1.24.4";

    [Theory]
    [InlineData("AnyCPU", "win-x64")]
    [InlineData("x64", "win-x64")]
    [InlineData("ARM64", "win-arm64")]
    public void NativeLibraries_AreTheOnesNuGetResolvedForThePlatform(string platform, string rid)
    {
        var copied = CopiedByTheTarget(platform, "");

        Assert.Contains($"microsoft.ml.onnxruntime/{VersionOfThePackage}/runtimes/{rid}/native/onnxruntime.dll", copied);
        Assert.Contains($"microsoft.ml.onnxruntime/{VersionOfThePackage}/runtimes/{rid}/native/onnxruntime_providers_shared.dll", copied);
        Assert.DoesNotContain(rid == "win-x64" ? "win-arm64" : "win-x64", copied);
    }

    [Fact]
    public void NativeLibraries_AreOfTheVersionOfTheManagedAssemblyWhenNuGetResolvedAnotherRuntime()
    {
        // The SDK gives win-x86 to an application without PlatformTarget, then moves it to AnyCPU.
        var directory = Path.Combine(Path.GetTempPath(), $"EmbeddingsTargetsTest_{Guid.NewGuid():N}");
        try
        {
            var packages = Path.Combine(directory, "packages");
            foreach (var version in new[] { VersionOfThePackage, "1.28.0" })
            {
                var native = Path.Combine(packages, "microsoft.ml.onnxruntime", version, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(native);
                File.WriteAllText(Path.Combine(native, "onnxruntime.dll"), "");
                File.WriteAllText(Path.Combine(native, "onnxruntime_providers_shared.dll"), "");
            }

            var copied = CopiedByTheTarget("AnyCPU", $"""
                <ReferencePath Include="Microsoft.ML.OnnxRuntime.dll" NuGetPackageVersion="1.28.0" />
                """, $"<RuntimeIdentifier>win-x86</RuntimeIdentifier><NuGetPackageRoot>{packages}</NuGetPackageRoot>", resolved: false);

            Assert.Contains("microsoft.ml.onnxruntime/1.28.0/runtimes/win-x64/native/onnxruntime.dll", copied);
            Assert.Contains("microsoft.ml.onnxruntime/1.28.0/runtimes/win-x64/native/onnxruntime_providers_shared.dll", copied);
            Assert.DoesNotContain(VersionOfThePackage, copied);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    // As the props of Microsoft.ML.OnnxRuntime.DirectML add its onnxruntime.dll.
    [InlineData("""<None Include="directml\onnxruntime.dll" Link="onnxruntime.dll" />""")]
    // A file of the project.
    [InlineData("""<None Include="onnxruntime.dll" />""")]
    // Another package, as Microsoft.ML.OnnxRuntime.DirectML through a referenced project.
    [InlineData("""<RuntimeTargetsCopyLocalItems Include="directml\onnxruntime.dll" NuGetPackageId="Microsoft.ML.OnnxRuntime.DirectML" RuntimeIdentifier="win-x64" />""")]
    public void NativeLibraries_AreNotCopiedOverAFileWithTheSameName(string items)
    {
        var copied = CopiedByTheTarget("AnyCPU", items);

        Assert.DoesNotContain("/onnxruntime.dll", copied);
        Assert.Contains("runtimes/win-x64/native/onnxruntime_providers_shared.dll", copied);
    }

    [Theory]
    // Microsoft.ML.OnnxRuntime.Managed alone raised: the native files stay the ones of Microsoft.ML.OnnxRuntime.
    [InlineData("net48", """<PackageReference Include="Microsoft.ML.OnnxRuntime.Managed" Version="1.28.0" />""", VersionOfThePackage)]
    // Another version of Microsoft.ML.OnnxRuntime for another target framework of the application.
    [InlineData("net48;net8.0", """<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.28.0" Condition="'$(TargetFramework)' == 'net8.0'" />""", VersionOfThePackage)]
    // The application excludes the native assets of Microsoft.ML.OnnxRuntime.
    [InlineData("net48", """<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.24.4" IncludeAssets="compile;runtime" />""", null)]
    public void NativeLibraries_AreTheOnesNuGetResolvedForTheApplication(string targetFrameworks, string items, string? version)
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
                    <TargetFrameworks>{targetFrameworks}</TargetFrameworks>
                    <OutputType>Exe</OutputType>
                    <PlatformTarget>AnyCPU</PlatformTarget>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="..\library\library.csproj" />
                    {items}
                  </ItemGroup>
                  <Import Project="{Path.Combine(AppContext.BaseDirectory, "ElBruno.LocalEmbeddings.targets")}" />
                  <Target Name="Print" DependsOnTargets="ResolveReferences">
                    <Message Importance="high" Text="COPIED:@(ReferenceCopyLocalPaths)" />
                  </Target>
                </Project>
                """);

            MSBuild($"\"{project}\" -t:Restore");
            var output = MSBuild($"\"{project}\" -t:Print -p:TargetFramework=net48");

            var line = output.Split('\n').Single(l => l.Contains("COPIED:")).Replace('\\', '/');
            if (version is null)
            {
                Assert.DoesNotContain("runtimes/win-x64/native/onnxruntime.dll", line);
            }
            else
            {
                Assert.Contains($"microsoft.ml.onnxruntime/{version}/runtimes/win-x64/native/onnxruntime.dll", line);
                Assert.DoesNotContain(version == VersionOfThePackage ? "microsoft.ml.onnxruntime/1.28.0/" : $"microsoft.ml.onnxruntime/{VersionOfThePackage}/", line);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // What the target of the package copies for an application on .NET Framework, given its platform, the native files NuGet resolved
    // for Microsoft.ML.OnnxRuntime and the items of the application.
    private static string CopiedByTheTarget(string platform, string items, string properties = "", bool resolved = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"EmbeddingsTargetsTest_{Guid.NewGuid():N}");
        try
        {
            var package = Path.Combine(directory, "packages", "microsoft.ml.onnxruntime", VersionOfThePackage);
            var runtimeTargets = !resolved ? "" : string.Concat(
                from rid in new[] { "win-x64", "win-arm64" }
                from file in new[] { "onnxruntime.dll", "onnxruntime_providers_shared.dll", "onnxruntime.lib" }
                select $"""<RuntimeTargetsCopyLocalItems Include="{Path.Combine(package, "runtimes", rid, "native", file)}" NuGetPackageId="Microsoft.ML.OnnxRuntime" RuntimeIdentifier="{rid}" />""");

            var project = Path.Combine(directory, "app.proj");
            Directory.CreateDirectory(directory);
            File.WriteAllText(project, $"""
                <Project>
                  <PropertyGroup>
                    <TargetFrameworkIdentifier>.NETFramework</TargetFrameworkIdentifier>
                    <OutputType>Exe</OutputType>
                    <PlatformTarget>{platform}</PlatformTarget>
                    {properties}
                  </PropertyGroup>
                  <ItemGroup>
                    {runtimeTargets}
                    {items}
                  </ItemGroup>
                  <Target Name="ResolveAssemblyReferences" />
                  <Target Name="GetCopyToOutputDirectoryItems">
                    <ItemGroup>
                      <AllItemsFullPathWithTargetPath Include="@(None)" TargetPath="%(None.Link)" Condition="'%(None.Link)' != ''" />
                      <AllItemsFullPathWithTargetPath Include="@(None)" TargetPath="%(None.Identity)" Condition="'%(None.Link)' == ''" />
                    </ItemGroup>
                  </Target>
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
