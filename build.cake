#module nuget:?package=Cake.DotNetTool.Module&version=0.2.0
#tool "dotnet:https://api.nuget.org/v3/index.json?package=Wyam.Tool&version=2.2.4"
#tool "nuget:https://api.nuget.org/v3/index.json?package=KuduSync.NET&version=1.5.2"
#tool "nuget:https://api.nuget.org/v3/index.json?package=NuGet.CommandLine&version=4.9.4"
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Git&version=0.19.0"
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Kudu&version=0.8.0"
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Wyam&version=2.2.4"
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Yaml&version=3.0.0"
#addin "nuget:https://api.nuget.org/v3/index.json?package=YamlDotNet&version=5.2.1"
#addin "nuget:https://api.nuget.org/v3/index.json?package=Octokit&version=0.32.0"

#load "nuget.cake"

using Octokit;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define variables
var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isPullRequest       = AppVeyor.Environment.PullRequest.IsPullRequest;
var accessToken         = EnvironmentVariable("git_access_token");
var deployRemote        = EnvironmentVariable("git_deploy_remote");
var currentBranch       = isRunningOnAppVeyor ? BuildSystem.AppVeyor.Environment.Repository.Branch : GitBranchCurrent("./").FriendlyName;
var deployBranch        = string.Concat("publish/", currentBranch);
var zipFileName         = "output.zip";

// Define directories.
var releaseDir          = Directory("./release");
var sourceDir           = releaseDir + Directory("repo");
var addinDir            = releaseDir + Directory("addins");
var outputPath          = MakeAbsolute(Directory("./output"));
var rootPublishFolder   = MakeAbsolute(Directory("publish"));

// Definitions
class AddinSpec
{
    public string Name { get; set; }
    public string NuGet { get; set; }
    public bool Prerelease { get; set; }
    public List<string> Assemblies { get; set; }
    public string Repository { get; set; }
    public string Author { get; set; }
    public string Description { get; set; }
    public List<string> Categories { get; set; }
}

// Variables
List<AddinSpec> addinSpecs = new List<AddinSpec>();


//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////


Setup(ctx =>
{
    // Executed BEFORE the first task.
    Information("Building branch {0} ({1})...", currentBranch, deployBranch);
});


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("CleanSource")
    .Does(() =>
{
    if(DirectoryExists(sourceDir))
    {
        CleanDirectory(sourceDir);
        DeleteDirectory(sourceDir, new DeleteDirectorySettings {
            Recursive = true,
            Force = true
        });
    }
    foreach(var cakeDir in GetDirectories(releaseDir.Path.FullPath + "/cake*"))
    {
        DeleteDirectory(cakeDir, new DeleteDirectorySettings {
            Recursive = true,
            Force = true
        });
    }
});

Task("GetSource")
    .IsDependentOn("CleanSource")
    .Does(() =>
    {
        GitHubClient github = new GitHubClient(new ProductHeaderValue("CakeDocs"));
        if (!string.IsNullOrEmpty(accessToken))
        {
            github.Credentials = new Credentials(accessToken);
        }
        // The GitHub releases API returns Not Found if all are pre-release, so need workaround below
        //Release release = github.Repository.Release.GetLatest("cake-build", "cake").Result;
        Release release = github.Repository.Release.GetAll("cake-build", "cake").Result.First( r =>r.PublishedAt.HasValue);
        FilePath releaseZip = DownloadFile(release.ZipballUrl);
        Unzip(releaseZip, releaseDir);

        // Need to rename the container directory in the zip file to something consistent
        var containerDir = GetDirectories(releaseDir.Path.FullPath + "/*").First(x => x.GetDirectoryName().StartsWith("cake"));
        MoveDirectory(containerDir, sourceDir);
    });

Task("CleanAddinPackages")
    .Does(() =>
{
    CleanDirectory(addinDir);
});

Task("GetAddinSpecs")
    .Does(() =>
{
    var addinSpecFiles = GetFiles("./addins/*.yml");
    addinSpecs
        .AddRange(addinSpecFiles
            .Select(x =>
            {
                Verbose("Deserializing addin YAML from " + x);
                return DeserializeYamlFromFile<AddinSpec>(x);
            })
        );
});

Task("GetAddinPackages")
    .IsDependentOn("CleanAddinPackages")
    .IsDependentOn("GetAddinSpecs")
    .Does(context =>
    {
        context.DownloadPackages(addinDir,
            addinSpecs
                .Where(x => !string.IsNullOrEmpty(x.NuGet))
                .Select(x => x.NuGet)
                .ToArray());
    });

Task("Build")
    .IsDependentOn("GetArtifacts")
    .Does(() =>
    {
        Wyam(new WyamSettings
        {
            Recipe = "Docs",
            Theme = "Samson",
            UpdatePackages = true,
            Settings = new Dictionary<string, object>
            {
                { "AssemblyFiles",  addinSpecs.Where(x => x.Assemblies != null).SelectMany(x => x.Assemblies).Select(x => "../release/addins" + x) }
            }
        });
    });

// Does not download artifacts (run Build or GetArtifacts target first)
Task("Preview")
    .IsDependentOn("GetAddinSpecs")
    .Does(() =>
    {
        Wyam(new WyamSettings
        {
            Recipe = "Docs",
            Theme = "Samson",
            UpdatePackages = true,
            Preview = true,
            Watch = true,
            Settings = new Dictionary<string, object>
            {
                { "AssemblyFiles",  addinSpecs.Where(x => x.Assemblies != null).SelectMany(x => x.Assemblies).Select(x => "../release/addins" + x) }
            }
        });
    });

// Assumes Wyam source is local and at ../Wyam
Task("Debug")
    .Does(() =>
    {
        StartProcess("../Wyam/src/clients/Wyam/bin/Debug/net462/wyam.exe",
            "-a \"../Wyam/tests/integration/Wyam.Examples.Tests/bin/Debug/net462/**/*.dll\" -r \"docs -i\" -t \"../Wyam/themes/Docs/Samson\" -p --attach");
    });

// Does not download artifacts (run Build or GetArtifacts target first)
Task("Debug-Addins")
    .IsDependentOn("GetAddinSpecs")
    .Does(() =>
    {
        StartProcess("../Wyam/src/clients/Wyam/bin/Debug/net462/wyam.exe",
            "-a \"../Wyam/tests/integration/Wyam.Examples.Tests/bin/Debug/net462/**/*.dll\" -r \"docs -i\" -t \"../Wyam/themes/Docs/Samson\" -p --attach"
            + " --setting \"AssemblyFiles=["
            + String.Join(",", addinSpecs.Where(x => x.Assemblies != null).SelectMany(x => x.Assemblies).Select(x => "../release/addins" + x))
            + "]\"");
    });

Task("Copy-Bootstrapper-Download")
    .Does(()=>
    {
        CopyDirectory("./download", outputPath.Combine("download"));
        CopyDirectory("./download/bootstrapper", outputPath.Combine("bootstrapper"));
    });

Task("Deploy")
    .WithCriteria(isRunningOnAppVeyor)
    .WithCriteria(!isPullRequest)
    .WithCriteria(!string.IsNullOrEmpty(accessToken))
    .WithCriteria(!string.IsNullOrEmpty(deployRemote))
    .WithCriteria(!string.IsNullOrEmpty(deployBranch))
    .IsDependentOn("Build")
    .IsDependentOn("Copy-Bootstrapper-Download")
    .Does(() =>
    {
        EnsureDirectoryExists(rootPublishFolder);
        var sourceCommit = GitLogTip("./");
        var publishFolder = rootPublishFolder.Combine(DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Information("Getting publish branch {0}...", deployBranch);
        GitClone(deployRemote, publishFolder, new GitCloneSettings{ BranchName = deployBranch });

        Information("Sync output files...");
        Kudu.Sync(outputPath, publishFolder, new KuduSyncSettings {
            PathsToIgnore = new []{ ".git", "appveyor.yml" }
        });

        Information("Stage all changes...");
        GitAddAll(publishFolder);

        Information("Commit all changes...");
        GitCommit(
            publishFolder,
            sourceCommit.Committer.Name,
            sourceCommit.Committer.Email,
            string.Format("AppVeyor Publish: {0}\r\n{1}", sourceCommit.Sha, sourceCommit.Message)
            );

        Information("Pushing all changes...");
        GitPush(publishFolder, accessToken, "x-oauth-basic", deployBranch);
    });

Task("ZipArtifacts")
    .IsDependentOn("Build")
    .IsDependentOn("Copy-Bootstrapper-Download")
    .Does(() =>
{
    Zip(outputPath, zipFileName);
});

Task("UploadArtifacts")
    .IsDependentOn("ZipArtifacts")
    .WithCriteria(BuildSystem.IsRunningOnAzurePipelinesHosted)
    .Does(() =>
{
    TFBuild.Commands.UploadArtifact("website", zipFileName, "website");
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

Task("GetArtifacts")
    .IsDependentOn("GetSource")
    .IsDependentOn("GetAddinPackages");

Task("AppVeyor")
    .IsDependentOn(isPullRequest ? "Build" : "Deploy");

Task("AzureDevOps")
    .IsDependentOn("UploadArtifacts");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

if (!StringComparer.OrdinalIgnoreCase.Equals(target, "Deploy"))
{
    RunTarget(target);
}
