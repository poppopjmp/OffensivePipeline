using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// Reading <c>&lt;HintPath&gt;</c> references out of the legacy, non-SDK project files that almost
/// every tool in <c>Tools/</c> uses. Getting this wrong means an obfuscated executable ships without
/// the third-party assemblies it needs.
/// </summary>
public class ProjectHintPathsTests
{
    private const string LegacyProject = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="System" />
            <Reference Include="Newtonsoft.Json">
              <HintPath>..\packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll</HintPath>
            </Reference>
            <Reference Include="Vendor.Native">
              <HintPath>lib\Vendor.Native.dll</HintPath>
            </Reference>
          </ItemGroup>
        </Project>
        """;

    private const string SdkStyleProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    [Fact]
    public void Reads_Every_HintPath_And_Ignores_References_Without_One()
    {
        using var workspace = new TempWorkspace();
        string project = workspace.File("Tool/Tool.csproj", LegacyProject);

        Assert.Equal(
            [
                @"..\packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll",
                @"lib\Vendor.Native.dll",
            ],
            ProjectHintPaths.Read(project));
    }

    /// <summary>
    /// An SDK-style project has no MSBuild XML namespace, so the namespaced lookup finds no root
    /// element. That must be an empty result, not a crash mid-pipeline.
    /// </summary>
    [Fact]
    public void An_Sdk_Style_Project_Yields_No_Hint_Paths()
    {
        using var workspace = new TempWorkspace();
        string project = workspace.File("Tool/Tool.csproj", SdkStyleProject);

        Assert.Empty(ProjectHintPaths.Read(project));
    }
}
