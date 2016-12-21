#tool "nuget:?package=GitVersion.CommandLine&version=3.6.5"
#tool "nuget:?package=NUnit.ConsoleRunner&version=3.5.0"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var nugetApiKey = Argument("nugetapikey", EnvironmentVariable("NUGET_API_KEY"));

//////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
//////////////////////////////////////////////////////////////////////
var version = "0.1.0";

var artifacts = Directory("./artifacts");
var solution = File("./src/Vault.sln");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Clean")
    .Does(() => 
{
    CleanDirectories("./src/**/bin");
    CleanDirectories("./src/**/obj");

    DeleteFiles("./docs/api/*.yml");
    DeleteFiles("./docs/api/.manifest");

    if (DirectoryExists(artifacts))
        DeleteDirectory(artifacts, true);
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() => 
{
    NuGetRestore(solution);
});

Task("Versioning")
    .IsDependentOn("Clean")
    .WithCriteria(() => !BuildSystem.IsLocalBuild)
    .Does(() => 
{
    GitVersion(new GitVersionSettings
    {
        OutputType = GitVersionOutput.BuildServer
    });

    var result = GitVersion(new GitVersionSettings
    {
        UpdateAssemblyInfo = true
    });

    version = result.NuGetVersion;
});

Task("Build")
    .IsDependentOn("Versioning")
    .IsDependentOn("Restore")    
    .Does(() => 
{
    CreateDirectory(artifacts);

    DotNetBuild(solution, x => 
    {
        x.SetConfiguration("Release");
        x.WithProperty("GenerateDocumentation", "true");
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() => 
{
    var testResults = artifacts + File("TestResults.xml");
    
    NUnit3("./src/**/bin/**/Release/*.Tests.dll", new NUnit3Settings
    {
        Results = testResults
    });
    
    if (BuildSystem.IsRunningOnAppVeyor)
        AppVeyor.UploadTestResults(testResults, AppVeyorTestResultsType.NUnit3);
});

Task("Package")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .Does(() => 
{
    NuGetPack("./build/Vault.nuspec", new NuGetPackSettings
    {
        Version = version,
        BasePath = "./src",
        OutputDirectory = artifacts
    });
});

Task("Publish")
    .IsDependentOn("Package")
    .Does(() =>
{
    var package = "./artifacts/Vault." + version + ".nupkg";

    NuGetPush(package, new NuGetPushSettings
    {
        ApiKey = nugetApiKey
    });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("Package");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);
