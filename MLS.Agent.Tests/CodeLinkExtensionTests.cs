﻿using FluentAssertions;
using Markdig;
using MLS.Agent.Markdown;
using MLS.Project.Generators;
using System.IO;
using Xunit;
using HtmlAgilityPack;

namespace MLS.Agent.Tests
{
    public class CodeLinkExtensionTests
    {
        [Theory]
        [InlineData("cs")]
        [InlineData("csharp")]
        [InlineData("c#")]
        [InlineData("CS")]
        [InlineData("CSHARP")]
        [InlineData("C#")]
        public void Inserts_code_when_an_existing_file_is_linked(string language)
        {
            var testDir = TestAssets.SampleConsole;
            var fileContent = @"using System;

namespace BasicConsoleApp
    {
        class Program
        {
            static void MyProgram(string[] args)
            {
                Console.WriteLine(""Hello World!"");
            }
        }
    }".EnforceLF();
            var directoryAccessor = new InMemoryDirectoryAccessor(testDir)
            {
                 ("Program.cs", fileContent),
                 ("sample.csproj", "")
            };
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var document =
$@"```{language} Program.cs
```";
            string html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();
            html.Should().Contain(fileContent.HtmlEncode());
        }

        [Fact]
        public void Does_not_insert_code_when_specified_language_is_not_csharp()
        {
            string expectedValue =
@"<pre><code class=""language-js"">console.log(&quot;Hello World&quot;);
</code></pre>
".EnforceLF();

            var testDir = TestAssets.SampleConsole;
            var directoryAccessor = new InMemoryDirectoryAccessor(testDir);
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var document = @"
```js Program.cs
console.log(""Hello World"");
```";
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();
            html.Should().Contain(expectedValue);
        }

        [Fact]
        public void Error_messsage_is_displayed_when_the_linked_file_does_not_exist()
        {
            var testDir = TestAssets.SampleConsole;
            var directoryAccessor = new InMemoryDirectoryAccessor(testDir)
            {
                ("sample.csproj", "")
            };
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var document =
@"```cs DOESNOTEXIST
```";
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();
            html.Should().Contain("File not found: DOESNOTEXIST");
        }

        [Fact]
        public void Error_message_is_displayed_when_no_project_is_passed_and_no_project_file_is_found()
        {
            var testDir = TestAssets.SampleConsole;
            var directoryAccessor = new InMemoryDirectoryAccessor(testDir)
            {
                ("Program.cs", "")
            };
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var document =
@"```cs Program.cs
```";
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            html.Should().Contain($"No project file could be found at path {testDir.FullName}");
        }


        [Fact]
        public void Error_message_is_displayed_when_the_passed_project_file_doesnot_exist()
        {
            var testDir = TestAssets.SampleConsole;
            var directoryAccessor = new InMemoryDirectoryAccessor(testDir)
            {
                ("Program.cs", "")
            };
            var projectPath = "sample.csproj";

            var document =
$@"```cs --project {projectPath} Program.cs
```";
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            html.Should().Contain($"Project not found: {projectPath}");
        }

        [Fact]
        public void Sets_the_trydotnet_package_attribute_using_the_passed_project_path()
        {
            var rootDirectory = TestAssets.SampleConsole;
            var currentDir = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "docs"));
            var directoryAccessor = new InMemoryDirectoryAccessor(currentDir, rootDirectory)
            {
                ("src/sample/Program.cs", ""),
                ("src/sample/sample.csproj", "")
            };

            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();

            var package = "../src/sample/sample.csproj";
            var document =
$@"```cs --project {package} ../src/sample/Program.cs
```";

            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            var output = htmlDocument.DocumentNode
                .SelectSingleNode("//pre/code").Attributes["data-trydotnet-package"];

            var fullProjectPath = directoryAccessor.GetFullyQualifiedPath(new RelativeFilePath(package));
            output.Value.Should().Be(fullProjectPath.FullName);
        }

        [Fact]
        public void Sets_the_code_in_the_pre_tag_using_the_region_specified_in_markdown()
        {
            var regionCode = @"Console.WriteLine(""Hello World!"");";
            var fileContent = $@"using System;

namespace BasicConsoleApp
    {{
        class Program
        {{
            static void MyProgram(string[] args)
            {{
                #region codeRegion
                {regionCode}
                #endregion
            }}
        }}
    }}".EnforceLF();


            var rootDirectory = TestAssets.SampleConsole;
            var directoryAccessor = new InMemoryDirectoryAccessor(rootDirectory)
            {
                ("Program.cs", fileContent),
                ("sample.csproj", "")
            };

            var document =
$@"```cs Program.cs --region codeRegion
```";
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            var output = htmlDocument.DocumentNode
                .SelectSingleNode("//pre/code").InnerText;

            output.Should().Be($"\n{regionCode.HtmlEncode()}\n");
        }

        [Fact]
        public void Sets_the_trydotnet_filename_using_the_filename_specified_in_the_markdown()
        {
            var rootDirectory = TestAssets.SampleConsole;
            var filename = "Program.cs";
            var codeContent = @"
#region codeRegion
Console.WriteLine(""Hello World"");
#endregion";
            var directoryAccessor = new InMemoryDirectoryAccessor(rootDirectory)
            {
                (filename, codeContent),
                ("sample.csproj", "")
            };

            var document =
$@"```cs Program.cs --region codeRegion
```";
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            var output = htmlDocument.DocumentNode
                .SelectSingleNode("//pre/code").Attributes["data-trydotnet-file-name"];

            output.Value.Should().Be(directoryAccessor.GetFullyQualifiedPath(new RelativeFilePath(filename)).FullName);
        }

        [Fact]
        public void Sets_the_trydotnet_region_using_the_region_passed_in_the_markdown()
        {
            var rootDirectory = TestAssets.SampleConsole;
            var region = "codeRegion";
            var codeContent = $@"
#region {region}
Console.WriteLine(""Hello World"");
#endregion";
            var directoryAccessor = new InMemoryDirectoryAccessor(rootDirectory)
            {
                ("Program.cs", codeContent),
                ("sample.csproj", "")
            };

            var document =
$@"```cs Program.cs --region {region}
```";
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            var output = htmlDocument.DocumentNode
                .SelectSingleNode("//pre/code").Attributes["data-trydotnet-region"];

            output.Value.Should().Be(region);
        }

        [Fact]
        public void If_the_specified_region_does_not_exist_error_message_is_shown()
        {
            var rootDirectory = TestAssets.SampleConsole;
            var region = "noRegion";
            var directoryAccessor = new InMemoryDirectoryAccessor(rootDirectory)
            {
                ("Program.cs", ""),
                ("sample.csproj", "")
            };

            var document =
$@"```cs Program.cs --region {region}
```";
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            htmlDocument.DocumentNode.SelectSingleNode("//pre|//code").Should().BeNull();

            html.Should().Contain($"Region not found: {region}");
        }

        [Fact]
        public void If_the_specified_region_exists_more_than_once()
        {
            var rootDirectory = TestAssets.SampleConsole;
            var codeContent = @"
#region codeRegion
#endregion
#region codeRegion
#endregion";
            var region = "codeRegion";
            var directoryAccessor = new InMemoryDirectoryAccessor(rootDirectory)
            {
                ("Program.cs", codeContent),
                ("sample.csproj", "")
            };

            var document =
$@"```cs Program.cs --region {region}
```";
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            htmlDocument.DocumentNode.SelectSingleNode("//pre|//code").Should().BeNull();

            html.Should().Contain($"Multiple regions found: {region}");
        }

        [Fact(Skip ="Not yet implemented")]
        public void If_the_specified_region_cannot_be_found_and_there_are_syntax_errors_then_error_message_is_shown()
        {
            var rootDirectory = TestAssets.SampleConsole;
            var codeContent = @"
#region codeRegion
#end region
";

            var file = "Program.cs";
            var directoryAccessor = new InMemoryDirectoryAccessor(rootDirectory)
            {
                ("Program.cs", codeContent),
                ("sample.csproj", "")
            };

            var document =
$@"```cs {file} --region codeRegion
```";
            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();
            var html = Markdig.Markdown.ToHtml(document, pipeline).EnforceLF();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            htmlDocument.DocumentNode.SelectSingleNode("//pre|//code").Should().BeNull();

            html.Should().Contain($"The specified file could not be parsed: {file}");
        }

        [Fact(Skip = "To be implemented")]
        public void Package_is_based_on_working_directory_when_project_options_is_not_specified()
        {
            /*var rootDirectory = TestAssets.SampleConsole;
            var currentDir = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "docs"));
            var directoryAccessor = new InMemoryDirectoryAccessor(currentDir, rootDirectory)
            {
                ("src/sample/Program.cs", ""),
                ("sample.csproj", "")
            };

            var pipeline = new MarkdownPipelineBuilder().UseCodeLinks(directoryAccessor).Build();

            var projectTemplate = "../src/sample/sample.csproj";
            var document =
$@"```cs ../src/sample/Program.cs
```";
                projectTemplate.Value.Should().Be("BasicConsoleApp");*/
        }


        [Fact(Skip = "To be implemented")]
        public void When_the_specified_package_does_not_exist_then_an_error_message_is_shown()
        {
            /*using (var agent = new AgentService(new StartupOptions(rootDirectory: TestAssets.SampleConsole)))
            {
                var response = await agent.GetAsync(@"Readme.md");

                response.Should().BeSuccessful();

                var html = await response.Content.ReadAsStringAsync();

                var document = new HtmlDocument();
                document.LoadHtml(html);
                var errorMessage = document.DocumentNode
                    .SelectSingleNode("//pre/code").Attributes["data-trydotnet-error"];

                errorMessage.Value.Should().Be("The specified project template does not exist");
            }*/
        }

        [Fact(Skip = "To be implemented")]
        public void When_the_specified_package_does_not_exist_then_original_fenced_code_is_displayed()
        {
            /*using (var agent = new AgentService(new StartupOptions(rootDirectory: TestAssets.SampleConsole)))
            {
                var response = await agent.GetAsync(@"Readme.md");

                response.Should().BeSuccessful();

                var html = await response.Content.ReadAsStringAsync();

                var document = new HtmlDocument();
                document.LoadHtml(html);
                var fencedCode = document.DocumentNode
                    .SelectSingleNode("//pre/code").InnerHtml;

                fencedCode.Should().Be("//specify the code");*/
        }
    }
}